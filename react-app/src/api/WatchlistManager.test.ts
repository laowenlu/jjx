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

    expect(detail.DrawdownSeries).toEqual([{ Date: '2026-09-29', Price: 5.2, Drawdown: 0 }]);
  });

  it('单位净值缺失时不将复权净值冒充为价格', async () => {
    mockCollection([{ nav_date: navItems[0].nav_date, unit_nav: null, adj_nav: 5.5313 }]);

    const result = await WatchlistManager.AddWatchlistItem(fund);

    expect(result.Success).toBe(false);
    expect(result.Item).toBeNull();
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
