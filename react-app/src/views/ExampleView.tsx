import { useEffect, useMemo, useRef, useState } from 'react';
import WatchlistManager from '@/api/WatchlistManager';
import type {
  AddWatchlistItemResponseDto,
  DrawdownPointDto,
  WatchlistItemSummaryDto,
} from '@/api/AppDtos';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

type FeedbackTone = 'default' | 'error';

type ChartPoint = {
  x: number;
  y: number;
  label: string;
  drawdown: number;
};

const formatPrice = (value: number | null) => {
  if (value === null) {
    return '—';
  }

  return value.toFixed(2);
};

const formatDrawdown = (value: number | null) => {
  if (value === null) {
    return '—';
  }

  return `${(value * 100).toFixed(2)}%`;
};

const WatchlistChart = ({ series }: { series: DrawdownPointDto[] }) => {
  const points = useMemo<ChartPoint[]>(() => {
    if (series.length === 0) {
      return [];
    }

    const minDrawdown = Math.min(...series.map((item) => item.Drawdown));
    const range = Math.max(Math.abs(minDrawdown), 0.01);

    return series.map((item, index) => ({
      x: series.length === 1 ? 0 : (index / (series.length - 1)) * 100,
      y: ((0 - item.Drawdown) / range) * 100,
      label: item.Date,
      drawdown: item.Drawdown,
    }));
  }, [series]);

  const path = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`)
    .join(' ');

  const latest = series.at(-1);
  const worst = series.reduce<DrawdownPointDto | null>((currentWorst, item) => {
    if (!currentWorst || item.Drawdown < currentWorst.Drawdown) {
      return item;
    }

    return currentWorst;
  }, null);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs text-muted-foreground">近一年回撤曲线</p>
          <p className="mt-1 text-2xl font-semibold tracking-tight">{formatDrawdown(latest?.Drawdown ?? null)}</p>
        </div>
        <div className="text-right text-xs text-muted-foreground">
          <p>最深回撤 {formatDrawdown(worst?.Drawdown ?? null)}</p>
          <p className="mt-1">{series[0]?.Date} — {series.at(-1)?.Date}</p>
        </div>
      </div>

      <div className="rounded-xl border border-border/80 bg-muted/30 p-4">
        <svg viewBox="0 0 100 100" className="h-64 w-full overflow-visible" preserveAspectRatio="none" role="img" aria-label="回撤曲线图">
          <line x1="0" y1="0" x2="100" y2="0" className="stroke-border" strokeWidth="0.7" vectorEffect="non-scaling-stroke" />
          <line x1="0" y1="50" x2="100" y2="50" className="stroke-border/70" strokeWidth="0.7" vectorEffect="non-scaling-stroke" />
          <line x1="0" y1="100" x2="100" y2="100" className="stroke-border" strokeWidth="0.7" vectorEffect="non-scaling-stroke" />
          {path ? (
            <path d={path} fill="none" className="stroke-primary" strokeWidth="1.8" vectorEffect="non-scaling-stroke" />
          ) : null}
        </svg>
      </div>

      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>{series[0]?.Date}</span>
        <span>{series.at(-1)?.Date}</span>
      </div>
    </div>
  );
};

const HomeView = () => {
  const [items, setItems] = useState<WatchlistItemSummaryDto[]>([]);
  const [selectedCode, setSelectedCode] = useState<string | null>(null);
  const [series, setSeries] = useState<DrawdownPointDto[]>([]);
  const [code, setCode] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [feedbackTone, setFeedbackTone] = useState<FeedbackTone>('default');
  const detailRequestCodeRef = useRef<string | null>(null);

  const selectedItem = items.find((item) => item.Code === selectedCode) ?? null;

  const loadWatchlist = async () => {
    setIsLoading(true);

    try {
      const response = await WatchlistManager.GetWatchlist({});
      setItems(response.Items);
      setSelectedCode((current) => current && response.Items.some((item) => item.Code === current)
        ? current
        : response.Items[0]?.Code ?? null);
    } catch {
      setFeedbackTone('error');
      setFeedback('加载监控列表失败。');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadWatchlist();
  }, []);

  useEffect(() => {
    if (!selectedCode) {
      setSeries([]);
      return;
    }

    const loadDetail = async () => {
      setIsDetailLoading(true);
      setSeries([]);
      detailRequestCodeRef.current = selectedCode;

      try {
        const response = await WatchlistManager.GetWatchlistItemDetail({ Code: selectedCode });
        if (!response.Success) {
          setFeedbackTone('error');
          setFeedback(response.Message);
          setSeries([]);
          return;
        }

        if (detailRequestCodeRef.current !== selectedCode) {
          return;
        }

        setSeries(response.DrawdownSeries);
      } catch {
        if (detailRequestCodeRef.current !== selectedCode) {
          return;
        }

        setFeedbackTone('error');
        setFeedback('加载回撤曲线失败。');
        setSeries([]);
      } finally {
        if (detailRequestCodeRef.current === selectedCode) {
          setIsDetailLoading(false);
        }
      }
    };

    void loadDetail();
  }, [selectedCode]);

  const handleAdd = async () => {
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setFeedback('');

    try {
      const response: AddWatchlistItemResponseDto = await WatchlistManager.AddWatchlistItem({ Code: code });
      setFeedbackTone(response.Success || response.AlreadyExists ? 'default' : 'error');
      setFeedback(response.Message);

      if (!response.Success || !response.Item) {
        return;
      }

      setItems((current) => [response.Item!, ...current.filter((item) => item.Code !== response.Item!.Code)]);
      setSelectedCode(response.Item.Code);
      setCode('');
    } catch {
      setFeedbackTone('error');
      setFeedback('添加失败，请稍后重试。');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (targetCode: string) => {
    try {
      const response = await WatchlistManager.DeleteWatchlistItem({ Code: targetCode });
      if (!response.Success) {
        setFeedbackTone('error');
        setFeedback(response.Message);
        return;
      }

      const nextItems = items.filter((item) => item.Code !== targetCode);
      setItems(nextItems);
      setFeedbackTone('default');
      setFeedback('删除成功');

      if (selectedCode === targetCode) {
        setSelectedCode(nextItems[0]?.Code ?? null);
      }
    } catch {
      setFeedbackTone('error');
      setFeedback('删除失败，请稍后重试。');
    }
  };

  return (
    <main className="min-h-screen bg-background px-4 py-8 text-foreground sm:px-6 lg:px-8">
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header className="space-y-2">
          <h1 className="text-3xl font-semibold tracking-tight">监控列表</h1>
          <p className="text-sm text-muted-foreground">查看基金、ETF、股票近一年的最大回撤。</p>
        </header>

        <Card className="border border-border/80 bg-card/90 shadow-none">
          <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
            <Input
              value={code}
              onChange={(event) => setCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  void handleAdd();
                }
              }}
              placeholder="输入 6 位代码"
              className="h-10 bg-background"
            />
            <Button onClick={() => void handleAdd()} disabled={isSubmitting} className="h-10 px-4">
              {isSubmitting ? '添加中…' : '添加'}
            </Button>
          </CardContent>
        </Card>

        {feedback ? (
          <div className={cn('text-sm', feedbackTone === 'error' ? 'text-destructive' : 'text-muted-foreground')}>
            {feedback}
          </div>
        ) : null}

        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:items-start">
          <Card className="border border-border/80 bg-card/90 shadow-none">
            <CardHeader>
              <CardTitle>标的列表</CardTitle>
            </CardHeader>
            <CardContent>
              {isLoading ? (
                <div className="py-10 text-sm text-muted-foreground">加载中…</div>
              ) : items.length === 0 ? (
                <div className="py-10 text-sm text-muted-foreground">还没有监控标的。</div>
              ) : (
                <div className="divide-y divide-border/70 rounded-xl border border-border/70 bg-background/70">
                  {items.map((item) => (
                    <button
                      key={item.Code}
                      type="button"
                      onClick={() => setSelectedCode(item.Code)}
                      className={cn(
                        'flex w-full flex-col gap-3 px-4 py-4 text-left transition-colors hover:bg-muted/40',
                        selectedCode === item.Code && 'bg-muted/60'
                      )}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <div className="text-sm font-medium text-foreground">{item.Name}</div>
                          <div className="mt-1 text-xs text-muted-foreground">{item.Code} · {item.SecurityType}</div>
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-muted-foreground hover:text-foreground"
                          onClick={(event) => {
                            event.stopPropagation();
                            void handleDelete(item.Code);
                          }}
                        >
                          删除
                        </Button>
                      </div>

                      <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
                        <div>
                          <div className="text-xs text-muted-foreground">现价</div>
                          <div className="mt-1 font-medium">{formatPrice(item.CurrentPrice)}</div>
                        </div>
                        <div>
                          <div className="text-xs text-muted-foreground">最大回撤</div>
                          <div className="mt-1 font-medium">{formatDrawdown(item.MaxDrawdown)}</div>
                        </div>
                        <div>
                          <div className="text-xs text-muted-foreground">更新时间</div>
                          <div className="mt-1 font-medium">{item.LastUpdatedUtc.slice(0, 10)}</div>
                        </div>
                      </div>

                      {item.DataWarning ? (
                        <div className="text-xs text-muted-foreground">{item.DataWarning}</div>
                      ) : null}
                    </button>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="border border-border/80 bg-card/90 shadow-none">
            <CardHeader>
              <CardTitle>{selectedItem ? `${selectedItem.Name} · ${selectedItem.Code}` : '回撤详情'}</CardTitle>
            </CardHeader>
            <CardContent>
              {!selectedItem ? (
                <div className="py-16 text-sm text-muted-foreground">选择一个标的后查看回撤曲线。</div>
              ) : isDetailLoading ? (
                <div className="py-16 text-sm text-muted-foreground">曲线加载中…</div>
              ) : series.length === 0 ? (
                <div className="py-16 text-sm text-muted-foreground">当前没有可展示的回撤数据。</div>
              ) : (
                <WatchlistChart series={series} />
              )}
            </CardContent>
          </Card>
        </div>
      </section>
    </main>
  );
};

export default HomeView;
