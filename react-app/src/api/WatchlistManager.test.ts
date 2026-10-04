import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import WatchlistManager from './WatchlistManager';

const fund = {
  ThsCode: '290011.OF',
  Code: '290011',
  Name: '测试场外基金',
  AssetType: 'fund-otc',
  SecurityType: '基金',
};
const navItems = [
  { nav_date: Date.parse('2026-09-30T00:00:00+08:00'), unit_nav: '4.728', adj_nav: '5.5313' },
  { nav_date: Date.parse('2026-09-29T00:00:00+08:00'), unit_nav: 5.2, adj_nav: 5.6 },
];

const mockCollection = (items: Record<string, unknown>[]) =>
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify({ code: 0, data: { item: items } })))
  );

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_HITHINK_API_KEY', 'test-key');
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-10-04T12:00:00+08:00'));
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  vi.useRealTimers();
});

describe('场外基金价格口径', () => {
  it('添加时显示最新单位净值，并保留复权净值计算的回撤', async () => {
    mockCollection(navItems);

    const result = await WatchlistManager.AddWatchlistItem(fund);

    expect(result.Success).toBe(true);
    expect(result.Item?.CurrentPrice).toBe(4.728);
    expect(result.Item?.LatestDataDate).toBe('2026-09-30');
    expect(result.Item?.MaxDrawdown).toBeCloseTo(5.5313 / 5.6 - 1);
  });

  it('打开页面时将缓存中的复权价格替换为单位净值并保存', async () => {
    localStorage.setItem('jjx.watchlist', JSON.stringify([{ ...fund, CurrentPrice: 5.5313 }]));
    mockCollection(navItems);

    const result = await WatchlistManager.GetWatchlist({});

    expect(result.Items[0].CurrentPrice).toBe(4.728);
    expect(JSON.parse(localStorage.getItem('jjx.watchlist')!)[0].CurrentPrice).toBe(4.728);
  });

  it('详情显示单位净值，分红造成的净值下降不直接计为回撤', async () => {
    mockCollection(navItems);
    await WatchlistManager.AddWatchlistItem(fund);
    mockCollection(navItems);

    const detail = await WatchlistManager.GetWatchlistItemDetail({ ThsCode: fund.ThsCode, Range: '1y' });

    expect(detail.DrawdownSeries.map((point) => point.Price)).toEqual([5.2, 4.728]);
    expect(detail.DrawdownSeries[1].Drawdown).toBeCloseTo(5.5313 / 5.6 - 1);
  });

  it('自定义日期范围内显示对应日期的单位净值', async () => {
    mockCollection(navItems);
    await WatchlistManager.AddWatchlistItem(fund);
    mockCollection(navItems);

    const detail = await WatchlistManager.GetWatchlistItemDetail({
      ThsCode: fund.ThsCode,
      Range: 'custom',
      StartDate: '2026-09-29',
      EndDate: '2026-09-29',
    });

    expect(detail.DrawdownSeries).toEqual([{ Date: '2026-09-29', Price: 5.2, Drawdown: 0, Gain: 0 }]);
  });

  it('单位净值缺失时不将复权净值冒充为价格', async () => {
    mockCollection([{ nav_date: navItems[0].nav_date, unit_nav: null, adj_nav: 5.5313 }]);

    const result = await WatchlistManager.AddWatchlistItem(fund);

    expect(result.Success).toBe(false);
    expect(result.Item).toBeNull();
  });
});

describe('涨幅计算', () => {
  const mockPrices = (prices: number[], adjustedPrices = prices) =>
    mockCollection(
      prices.map((price, index) => ({
        nav_date: Date.parse(`2026-09-${String(index + 1).padStart(2, '0')}T00:00:00+08:00`),
        unit_nav: price,
        adj_nav: adjustedPrices[index],
      }))
    );

  it('按此前最低价计算每日涨幅，保留曾经出现的最大涨幅', async () => {
    mockPrices([100, 80, 120, 90, 60, 90, 75]);
    const added = await WatchlistManager.AddWatchlistItem(fund);
    const detail = await WatchlistManager.GetWatchlistItemDetail({ ThsCode: fund.ThsCode, Range: '1y' });

    expect(added.Item?.MaxGain).toBeCloseTo(0.5);
    expect(detail.DrawdownSeries.map((point) => point.Gain)).toEqual([0, 0, 0.5, 0.125, 0, 0.5, 0.25]);
    expect(detail.DrawdownSeries.at(-1)?.Drawdown).toBeCloseTo(75 / 120 - 1);
    expect(added.Item?.MaxDrawdown).toBeCloseTo(-0.5);
    expect(JSON.parse(localStorage.getItem('jjx.watchlist')!)[0].MaxGain).toBeCloseTo(0.5);
  });

  it.each([[100, 90, 80], [100, 100, 100], [100], [0, 0]])('价格序列 %j 没有上涨时涨幅为零', async (...prices) => {
    mockPrices(prices);
    const added = await WatchlistManager.AddWatchlistItem(fund);
    const detail = await WatchlistManager.GetWatchlistItemDetail({ ThsCode: fund.ThsCode, Range: '1y' });

    expect(added.Item?.MaxGain).toBe(0);
    expect(detail.DrawdownSeries.every((point) => point.Gain === 0)).toBe(true);
  });

  it('先出现的高价不能作为之后低价的涨幅终点', async () => {
    mockPrices([120, 100, 80, 100]);
    const added = await WatchlistManager.AddWatchlistItem(fund);

    expect(added.Item?.MaxGain).toBeCloseTo(0.25);
  });

  it('基金涨幅使用复权净值，价格仍显示单位净值', async () => {
    mockPrices([2, 1, 1.1], [2, 2, 2.2]);
    const added = await WatchlistManager.AddWatchlistItem(fund);
    const detail = await WatchlistManager.GetWatchlistItemDetail({ ThsCode: fund.ThsCode, Range: '1y' });

    expect(added.Item?.MaxGain).toBeCloseTo(0.1);
    expect(detail.DrawdownSeries.map((point) => point.Price)).toEqual([2, 1, 1.1]);
    expect(detail.DrawdownSeries[1].Gain).toBe(0);
    expect(detail.DrawdownSeries[2].Gain).toBeCloseTo(0.1);
  });

  it('自定义日期范围重新计算最低价，不沿用范围外的低点', async () => {
    mockPrices([50, 100, 120]);
    const added = await WatchlistManager.AddWatchlistItem(fund);
    const detail = await WatchlistManager.GetWatchlistItemDetail({
      ThsCode: fund.ThsCode,
      Range: 'custom',
      StartDate: '2026-09-02',
      EndDate: '2026-09-03',
    });

    expect(added.Item?.MaxGain).toBeCloseTo(1.4);
    expect(detail.DrawdownSeries).toHaveLength(2);
    expect(detail.DrawdownSeries[0].Gain).toBe(0);
    expect(detail.DrawdownSeries[1].Gain).toBeCloseTo(0.2);
  });

  it('刷新旧缓存时补充最大涨幅并保存', async () => {
    localStorage.setItem('jjx.watchlist', JSON.stringify([{ ...fund, CurrentPrice: 100 }]));
    mockPrices([100, 120]);

    const result = await WatchlistManager.GetWatchlist({});

    expect(result.Items[0].MaxGain).toBeCloseTo(0.2);
    expect(JSON.parse(localStorage.getItem('jjx.watchlist')!)[0].MaxGain).toBeCloseTo(0.2);
  });

  it('旧缓存刷新失败时显示缺失的涨幅，不伪造零值', async () => {
    localStorage.setItem('jjx.watchlist', JSON.stringify([{ ...fund, CurrentPrice: 100 }]));
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));

    const result = await WatchlistManager.GetWatchlist({});

    expect(result.Items[0].MaxGain).toBeNull();
  });
});

it('ETF 继续使用场内成交价和收盘价', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: string) => {
      const items = input.includes('/market/snapshot')
        ? [{ last_price: 2.352 }]
        : [{ date_ms: navItems[0].nav_date, close_price: 2.352 }];
      return new Response(JSON.stringify({ code: 0, data: { item: items } }));
    })
  );

  const result = await WatchlistManager.AddWatchlistItem({
    ThsCode: '513100.SH',
    Code: '513100',
    Name: '纳指ETF国泰',
    AssetType: 'fund-etf',
  });

  expect(result.Item?.CurrentPrice).toBe(2.352);
  expect(result.Item?.LatestDataDate).toBe('2026-09-30');
  expect(vi.mocked(fetch).mock.calls.every(([url]) => String(url).includes('/fund/market/'))).toBe(true);
});
