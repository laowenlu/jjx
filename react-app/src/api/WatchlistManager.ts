import type {
  AddWatchlistItemRequestDto,
  AddWatchlistItemResponseDto,
  DeleteWatchlistItemRequestDto,
  DrawdownRangeOptionDto,
  GetWatchlistItemDetailRequestDto,
  GetWatchlistItemDetailResponseDto,
  GetWatchlistRequestDto,
  GetWatchlistResponseDto,
  OperationResultDto,
  SearchWatchlistCandidatesRequestDto,
  SearchWatchlistCandidatesResponseDto,
  WatchlistItemSummaryDto,
  WatchlistSearchCandidateDto,
} from './AppDtos';

const BASE_URL = import.meta.env.DEV ? '/hithink-api' : 'https://fuyao.aicubes.cn';
const WATCHLIST_STORAGE_KEY = 'jjx.watchlist';

const RANGE_OPTIONS: DrawdownRangeOptionDto[] = [
  { Value: '6m', Label: '半年' },
  { Value: '1y', Label: '1年' },
  { Value: '2y', Label: '2年' },
  { Value: '3y', Label: '3年' },
  { Value: '5y', Label: '5年' },
  { Value: '8y', Label: '8年' },
  { Value: 'custom', Label: '自定义' },
];

type ApiEnvelope<T> = { code: number; message?: string; data: T | null };
type CollectionData = { item?: Record<string, unknown>[] };
type HistoricalBar = { dateMs: number; price: number; drawdownPrice?: number };
type SecurityData = {
  currentPrice: number | null;
  history: HistoricalBar[];
  dataWarning: string | null;
};
type RangeTimestamps = { start: number; end: number };

const SHANGHAI_DATE_FORMATTER = new Intl.DateTimeFormat('zh-CN', {
  timeZone: 'Asia/Shanghai',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

const getApiKey = () => {
  const apiKey = import.meta.env.VITE_HITHINK_API_KEY;
  if (!apiKey) {
    throw new Error('缺少 VITE_HITHINK_API_KEY 配置。');
  }

  return apiKey;
};

const request = async <T>(path: string): Promise<T> => {
  const apiKey = getApiKey();
  const response = await fetch(`${BASE_URL}${path}`, {
    cache: 'no-store',
    headers: import.meta.env.DEV ? { Accept: 'application/json' } : { Accept: 'application/json', 'X-api-key': apiKey },
  });

  if (!response.ok) {
    throw new Error(`同花顺接口请求失败：${response.status}`);
  }

  const payload = (await response.json()) as ApiEnvelope<T>;
  if (payload.code !== 0 || payload.data === null) {
    throw new Error(payload.message || '同花顺接口返回错误。');
  }

  return payload.data;
};

const readNumber = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
};

const readDateMs = (value: unknown): number | null => {
  const numeric = readNumber(value);
  if (numeric !== null) return numeric;
  if (typeof value === 'string' && value.trim()) {
    const parsed = Date.parse(value);
    return Number.isNaN(parsed) ? null : parsed;
  }
  return null;
};

const readString = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value : null);

const mapSecurityType = (assetType: string) => {
  switch (assetType) {
    case 'a-share':
      return '股票';
    case 'a-share-index':
      return '指数';
    case 'fund-etf':
      return 'ETF';
    case 'fund-lof':
      return 'LOF';
    case 'fund-reits':
      return 'REITs';
    case 'fund-otc':
      return '基金';
    default:
      return assetType || '标的';
  }
};

const readStoredItems = (): WatchlistItemSummaryDto[] => {
  const stored = localStorage.getItem(WATCHLIST_STORAGE_KEY);
  if (!stored) return [];

  try {
    const parsed = JSON.parse(stored) as unknown;
    return Array.isArray(parsed) ? (parsed as WatchlistItemSummaryDto[]) : [];
  } catch {
    localStorage.removeItem(WATCHLIST_STORAGE_KEY);
    return [];
  }
};

const writeStoredItems = (items: WatchlistItemSummaryDto[]) => {
  localStorage.setItem(WATCHLIST_STORAGE_KEY, JSON.stringify(items));
};

const getRangeStartTimestamp = (range: string) => {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  switch (range) {
    case '6m':
      date.setMonth(date.getMonth() - 6);
      break;
    case '2y':
      date.setFullYear(date.getFullYear() - 2);
      break;
    case '3y':
      date.setFullYear(date.getFullYear() - 3);
      break;
    case '5y':
      date.setFullYear(date.getFullYear() - 5);
      break;
    case '8y':
      date.setFullYear(date.getFullYear() - 8);
      break;
    case '1y':
    default:
      date.setFullYear(date.getFullYear() - 1);
      break;
  }
  return date.getTime();
};

const getDateTimestamp = (value: string, endOfDay = false) => {
  const [year, month, day] = value.split('-').map(Number);
  if (!year || !month || !day) return null;
  const date = new Date(year, month - 1, day);
  date.setHours(endOfDay ? 23 : 0, endOfDay ? 59 : 0, endOfDay ? 59 : 0, endOfDay ? 999 : 0);
  return Number.isNaN(date.getTime()) ? null : date.getTime();
};

const getRangeTimestamps = (range: string, startDate?: string, endDate?: string): RangeTimestamps => {
  if (range === 'custom' && startDate && endDate) {
    const start = getDateTimestamp(startDate);
    const end = getDateTimestamp(endDate, true);
    if (start !== null && end !== null && end >= start) {
      return { start, end };
    }
  }

  const end = new Date();
  end.setHours(23, 59, 59, 999);
  return {
    start: getRangeStartTimestamp(range),
    end: end.getTime(),
  };
};

const formatShanghaiDate = (timestamp: number) => {
  const parts = SHANGHAI_DATE_FORMATTER.formatToParts(new Date(timestamp));
  const year = parts.find((part) => part.type === 'year')?.value;
  const month = parts.find((part) => part.type === 'month')?.value;
  const day = parts.find((part) => part.type === 'day')?.value;
  return year && month && day ? `${year}-${month}-${day}` : new Date(timestamp).toISOString().slice(0, 10);
};

const getFundRange = (range: string) => {
  switch (range) {
    case '6m':
      return 'hyear';
    case '2y':
      return 'twoyear';
    case '3y':
      return 'tyear';
    case '5y':
    case '8y':
      return 'fyear';
    case '1y':
    default:
      return 'year';
  }
};

const getFundType = (assetType: string) => {
  if (assetType === 'fund-otc') return 'otc';
  if (assetType === 'fund-reits') return 'reits';
  return 'exchange';
};

const getCollectionItems = async (path: string) => {
  const data = await request<CollectionData>(path);
  return data.item ?? [];
};

const searchStandardIndexesByTicker = async (ticker: string): Promise<WatchlistSearchCandidateDto[]> => {
  const exchanges = [
    { code: `${ticker}.SH`, name: '上证指数' },
    { code: `${ticker}.SZ`, name: '深证成指' },
  ];

  const results = await Promise.all(
    exchanges.map(async ({ code, name }) => {
      try {
        const items = await getCollectionItems(
          `/api/a-share-index/prices/snapshot?thscodes=${encodeURIComponent(code)}`
        );
        if (items.length === 0) return null;

        return {
          ThsCode: code,
          Code: ticker,
          Name: name,
          AssetType: 'a-share-index',
          SecurityType: '指数',
        };
      } catch {
        return null;
      }
    })
  );

  return results.filter((item): item is WatchlistSearchCandidateDto => item !== null);
};

const getHistoricalBars = async (path: string): Promise<HistoricalBar[]> => {
  const items = await getCollectionItems(path);
  return items
    .map((item) => ({
      dateMs: readDateMs(item.date_ms),
      price: readNumber(item.close_price),
    }))
    .filter((item): item is HistoricalBar => item.dateMs !== null && item.price !== null)
    .sort((left, right) => left.dateMs - right.dateMs);
};

const getEtfHistoricalBars = async (
  encodedCode: string,
  rangeTimestamps: RangeTimestamps
): Promise<HistoricalBar[]> => {
  const windows: RangeTimestamps[] = [];
  let windowStart = rangeTimestamps.start;

  while (windowStart <= rangeTimestamps.end) {
    const maxWindowEnd = new Date(windowStart);
    maxWindowEnd.setFullYear(maxWindowEnd.getFullYear() + 5);
    const windowEnd = Math.min(rangeTimestamps.end, maxWindowEnd.getTime() - 1);
    windows.push({ start: windowStart, end: windowEnd });

    if (windowEnd >= rangeTimestamps.end) {
      break;
    }
    windowStart = windowEnd + 1;
  }

  const histories = await Promise.all(
    windows.map(({ start, end }) =>
      getHistoricalBars(`/api/fund/market/historical?thscode=${encodedCode}&interval=1d&start=${start}&end=${end}`)
    )
  );
  const historyByDate = new Map<number, HistoricalBar>();
  histories.flat().forEach((point) => historyByDate.set(point.dateMs, point));
  return [...historyByDate.values()].sort((left, right) => left.dateMs - right.dateMs);
};

const getHistoricalBarsInRange = (history: HistoricalBar[], start: number, end: number) =>
  history.filter((point) => point.dateMs >= start && point.dateMs <= end);

const getSecurityData = async (
  candidate: WatchlistSearchCandidateDto,
  range: string,
  customRange?: { startDate?: string; endDate?: string },
  rangeTimestamps = getRangeTimestamps(range, customRange?.startDate, customRange?.endDate)
): Promise<SecurityData | null> => {
  const encodedCode = encodeURIComponent(candidate.ThsCode);
  const isFund = ['fund-otc', 'fund-etf', 'fund-lof', 'fund-reits'].includes(candidate.AssetType);

  if (candidate.AssetType === 'fund-etf') {
    const [snapshotItems, history] = await Promise.all([
      getCollectionItems(`/api/fund/market/snapshot?thscode=${encodedCode}`),
      getEtfHistoricalBars(encodedCode, rangeTimestamps),
    ]);

    if (history.length === 0) return null;
    return {
      currentPrice: readNumber(snapshotItems[0]?.last_price) ?? history[history.length - 1].price,
      history,
      dataWarning: null,
    };
  }

  if (isFund) {
    const items = await getCollectionItems(
      `/api/fund/performance/nav?fund_type=${getFundType(candidate.AssetType)}&thscode=${encodedCode}&range=${getFundRange(range === 'custom' ? '8y' : range)}&nav_type=unit%2Cadj`
    );
    const history = items
      .map((item): HistoricalBar | null => {
        const unitNav = readNumber(item.unit_nav);
        const adjustedNav = readNumber(item.adj_nav) ?? unitNav;
        const dateMs = readDateMs(item.nav_date) ?? readDateMs(item.date_ms);
        const price = candidate.AssetType === 'fund-otc' ? unitNav : adjustedNav;
        if (dateMs === null || price === null) return null;
        return {
          dateMs,
          price,
          // 单位净值用于展示，复权净值用于消除分红对回撤计算的影响。
          drawdownPrice: adjustedNav ?? undefined,
        };
      })
      .filter((item): item is HistoricalBar => item !== null)
      .sort((left, right) => left.dateMs - right.dateMs);
    const filteredHistory =
      range === 'custom' ? getHistoricalBarsInRange(history, rangeTimestamps.start, rangeTimestamps.end) : history;

    if (filteredHistory.length === 0) return null;
    let currentPrice = history[history.length - 1].price;
    if (candidate.AssetType === 'fund-lof') {
      try {
        const snapshotItems = await getCollectionItems(`/api/fund/market/snapshot?thscode=${encodedCode}`);
        currentPrice = readNumber(snapshotItems[0]?.last_price) ?? currentPrice;
      } catch {
        // LOF 历史成交日线不可用，快照失败时继续使用最新净值。
      }
    }

    return {
      currentPrice,
      history: filteredHistory,
      dataWarning: candidate.AssetType === 'fund-lof' ? '现价为场内最新成交价，回撤曲线基于基金净值口径。' : null,
    };
  }

  const endpoint = candidate.AssetType === 'a-share-index' ? 'a-share-index' : 'a-share';
  const snapshotItems = await getCollectionItems(`/api/${endpoint}/prices/snapshot?thscodes=${encodedCode}`);
  const history = await getHistoricalBars(
    `/api/${endpoint}/prices/historical?thscode=${encodedCode}&interval=1d&start=${rangeTimestamps.start}&end=${rangeTimestamps.end}${endpoint === 'a-share' ? '&adjust=forward' : ''}`
  );

  if (history.length === 0) return null;
  return {
    currentPrice: readNumber(snapshotItems[0]?.last_price) ?? history[history.length - 1].price,
    history,
    dataWarning: null,
  };
};

const calculateDrawdown = (history: HistoricalBar[]) => {
  let peak = 0;
  let maxDrawdown = 0;
  const series = history.map((point) => {
    const drawdownPrice = point.drawdownPrice ?? point.price;
    peak = Math.max(peak, drawdownPrice);
    const drawdown = peak === 0 ? 0 : drawdownPrice / peak - 1;
    maxDrawdown = Math.min(maxDrawdown, drawdown);
    return {
      Date: formatShanghaiDate(point.dateMs),
      Price: point.price,
      Drawdown: drawdown,
    };
  });
  return { maxDrawdown, series };
};

const toSummary = (
  candidate: WatchlistSearchCandidateDto,
  securityData: SecurityData,
  maxDrawdown: number
): WatchlistItemSummaryDto => ({
  ThsCode: candidate.ThsCode,
  Code: candidate.Code,
  Name: candidate.Name,
  SecurityType: candidate.SecurityType,
  AssetType: candidate.AssetType,
  CurrentPrice: securityData.currentPrice,
  MaxDrawdown: maxDrawdown,
  DataWarning: securityData.dataWarning,
  LatestDataDate: formatShanghaiDate(securityData.history.at(-1)!.dateMs),
  LastUpdatedUtc: new Date().toISOString(),
});

const toCandidate = (item: WatchlistItemSummaryDto): WatchlistSearchCandidateDto => ({
  ThsCode: item.ThsCode,
  Code: item.Code,
  Name: item.Name,
  AssetType: item.AssetType,
  SecurityType: item.SecurityType,
});

const refreshWatchlistItem = async (item: WatchlistItemSummaryDto): Promise<WatchlistItemSummaryDto> => {
  try {
    const candidate = toCandidate(item);
    const securityData = await getSecurityData(candidate, '1y');
    if (!securityData || securityData.history.length === 0) {
      return item;
    }

    const { maxDrawdown } = calculateDrawdown(securityData.history);
    return toSummary(candidate, securityData, maxDrawdown);
  } catch {
    return item;
  }
};

const refreshWatchlistItems = async (items: WatchlistItemSummaryDto[]) => {
  const refreshedItems = [...items];
  let nextIndex = 0;
  const workerCount = Math.min(4, items.length);

  await Promise.all(
    Array.from({ length: workerCount }, async () => {
      while (nextIndex < items.length) {
        const index = nextIndex;
        nextIndex += 1;
        refreshedItems[index] = await refreshWatchlistItem(items[index]);
      }
    })
  );

  return refreshedItems;
};

const SearchWatchlistCandidates = async (
  request: SearchWatchlistCandidatesRequestDto
): Promise<SearchWatchlistCandidatesResponseDto> => {
  const query = request.Query.trim();
  if (query.length !== 6 || !/^\d+$/.test(query)) {
    return { Items: [], Message: '请输入 6 位代码。' };
  }

  const items = await getCollectionItems(
    `/api/meta/tickers/search?q=${encodeURIComponent(query)}&asset_type=a-share,a-share-index,fund-otc,fund-etf,fund-lof,fund-reits&limit=20`
  );
  const candidates = items
    .map((item): WatchlistSearchCandidateDto | null => {
      const thsCode = readString(item.thscode);
      const code = readString(item.ticker);
      const name = readString(item.name);
      const assetType = readString(item.asset_type);
      if (!thsCode || !code || !name || !assetType) return null;
      return {
        ThsCode: thsCode,
        Code: code,
        Name: name,
        AssetType: assetType,
        SecurityType: mapSecurityType(assetType),
      };
    })
    .filter((item): item is WatchlistSearchCandidateDto => item !== null)
    .sort(
      (left, right) =>
        (left.Code === query ? 0 : 1) - (right.Code === query ? 0 : 1) || left.Name.localeCompare(right.Name)
    );
  const inferredIndexes = await searchStandardIndexesByTicker(query);
  const existingCodes = new Set(candidates.map((candidate) => candidate.ThsCode));
  const allCandidates = [
    ...inferredIndexes.filter((candidate) => !existingCodes.has(candidate.ThsCode)),
    ...candidates,
  ];

  return {
    Items: allCandidates,
    Message: allCandidates.length > 0 ? 'ok' : '没有找到可选标的。',
  };
};

const GetWatchlist = async (_request: GetWatchlistRequestDto): Promise<GetWatchlistResponseDto> => {
  void _request;
  const items = readStoredItems();
  if (items.length === 0) {
    return { Items: [] };
  }

  const refreshedItems = await refreshWatchlistItems(items);
  writeStoredItems(refreshedItems);
  return { Items: refreshedItems };
};

const AddWatchlistItem = async (request: AddWatchlistItemRequestDto): Promise<AddWatchlistItemResponseDto> => {
  const items = readStoredItems();
  const existing = items.find((item) => item.ThsCode === request.ThsCode);
  if (existing) {
    return { Success: false, AlreadyExists: true, Message: '已存在', Item: existing };
  }

  const candidate: WatchlistSearchCandidateDto = {
    ThsCode: request.ThsCode,
    Code: request.Code,
    Name: request.Name || request.Code,
    AssetType: request.AssetType,
    SecurityType: request.SecurityType || mapSecurityType(request.AssetType),
  };
  const securityData = await getSecurityData(candidate, '1y');
  if (!securityData || securityData.history.length === 0) {
    return {
      Success: false,
      AlreadyExists: false,
      Message: '近一年历史数据暂不可用。',
      Item: null,
    };
  }

  const { maxDrawdown } = calculateDrawdown(securityData.history);
  const item = toSummary(candidate, securityData, maxDrawdown);
  writeStoredItems([item, ...items]);
  return { Success: true, AlreadyExists: false, Message: '添加成功', Item: item };
};

const DeleteWatchlistItem = async (request: DeleteWatchlistItemRequestDto): Promise<OperationResultDto> => {
  const items = readStoredItems();
  const nextItems = items.filter((item) => item.ThsCode !== request.ThsCode);
  if (nextItems.length === items.length) return { Success: false, Message: '未找到该标的。' };
  writeStoredItems(nextItems);
  return { Success: true, Message: '删除成功' };
};

const GetWatchlistItemDetail = async (
  request: GetWatchlistItemDetailRequestDto
): Promise<GetWatchlistItemDetailResponseDto> => {
  const selectedRange = RANGE_OPTIONS.some((option) => option.Value === request.Range) ? request.Range : '1y';
  const rangeTimestamps = getRangeTimestamps(selectedRange, request.StartDate, request.EndDate);
  const rangeStartDate = formatShanghaiDate(rangeTimestamps.start);
  const rangeEndDate = formatShanghaiDate(rangeTimestamps.end);
  const item = readStoredItems().find((watchlistItem) => watchlistItem.ThsCode === request.ThsCode);

  if (!item) {
    return {
      Success: false,
      Message: '未找到该标的。',
      Item: null,
      SelectedRange: selectedRange,
      RangeStartDate: rangeStartDate,
      RangeEndDate: rangeEndDate,
      AvailableRanges: RANGE_OPTIONS,
      DrawdownSeries: [],
    };
  }

  const candidate = toCandidate(item);
  const securityData = await getSecurityData(
    candidate,
    selectedRange,
    {
      startDate: request.StartDate,
      endDate: request.EndDate,
    },
    rangeTimestamps
  );
  if (!securityData || securityData.history.length === 0) {
    return {
      Success: false,
      Message: '当前周期的数据暂不可用。',
      Item: item,
      SelectedRange: selectedRange,
      RangeStartDate: rangeStartDate,
      RangeEndDate: rangeEndDate,
      AvailableRanges: RANGE_OPTIONS,
      DrawdownSeries: [],
    };
  }

  return {
    Success: true,
    Message: 'ok',
    Item: item,
    SelectedRange: selectedRange,
    RangeStartDate: rangeStartDate,
    RangeEndDate: rangeEndDate,
    AvailableRanges: RANGE_OPTIONS,
    DrawdownSeries: calculateDrawdown(securityData.history).series,
  };
};

export default {
  SearchWatchlistCandidates,
  GetWatchlist,
  AddWatchlistItem,
  DeleteWatchlistItem,
  GetWatchlistItemDetail,
};
