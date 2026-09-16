import { useEffect, useMemo, useRef, useState } from 'react';
import { zhCN } from 'date-fns/locale/zh-CN';
import type { DateRange } from 'react-day-picker';
import WatchlistManager from '@/api/WatchlistManager';
import type {
  AddWatchlistItemResponseDto,
  DrawdownPointDto,
  DrawdownRangeOptionDto,
  WatchlistItemSummaryDto,
  WatchlistSearchCandidateDto,
} from '@/api/AppDtos';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { CalendarDays } from 'lucide-react';

type FeedbackTone = 'default' | 'error';

type ChartPoint = {
  x: number;
  y: number;
  label: string;
  drawdown: number;
  price: number;
};

const CHART_LEFT = 1;
const CHART_WIDTH = 98;
const CHART_TOP = 5;
const CHART_HEIGHT = 90;

const formatDate = (date: Date) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const parseDate = (value: string) => {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
};

const getSeriesDateRange = (series: DrawdownPointDto[]): DateRange | undefined => {
  if (series.length === 0) {
    return undefined;
  }

  return {
    from: parseDate(series[0].Date),
    to: parseDate(series.at(-1)!.Date),
  };
};

type DateRangePickerProps = {
  range: DateRange | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (range: DateRange) => void;
};

const DateRangePicker = ({ range, open, onOpenChange, onConfirm }: DateRangePickerProps) => {
  const [draftRange, setDraftRange] = useState<DateRange | undefined>(range);
  const today = useMemo(() => new Date(), []);
  const startMonth = useMemo(() => new Date(today.getFullYear() - 10, 0, 1), [today]);

  useEffect(() => {
    if (open) {
      setDraftRange(range);
    }
  }, [open, range]);

  const isComplete = Boolean(draftRange?.from && draftRange.to);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-auto max-w-[calc(100%-2rem)] gap-3 p-4 sm:max-w-none">
        <DialogHeader>
          <DialogTitle>选择起止日期</DialogTitle>
          <DialogDescription className="sr-only">选择回撤曲线的开始日期和结束日期。</DialogDescription>
        </DialogHeader>
        <Calendar
          mode="range"
          min={1}
          selected={draftRange}
          onSelect={setDraftRange}
          defaultMonth={draftRange?.from ?? range?.from}
          captionLayout="dropdown"
          navLayout="around"
          startMonth={startMonth}
          endMonth={today}
          disabled={{ after: today }}
          locale={zhCN}
          formatters={{
            formatMonthDropdown: (date) => `${date.getMonth() + 1}月`,
            formatYearDropdown: (date) => `${date.getFullYear()}年`,
          }}
        />
        <div className="px-2 text-xs text-muted-foreground">
          {draftRange?.from ? formatDate(draftRange.from) : '请选择开始日期'}
          {' — '}
          {draftRange?.to ? formatDate(draftRange.to) : '请选择结束日期'}
        </div>
        <div className="flex justify-end gap-2 border-t border-border pt-3">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button
            type="button"
            disabled={!isComplete}
            onClick={() => {
              if (!draftRange?.from || !draftRange.to) {
                return;
              }

              onConfirm({ from: draftRange.from, to: draftRange.to });
              onOpenChange(false);
            }}
          >
            确定
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
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

const formatAxisPercent = (value: number) => {
  if (Math.abs(value) < 0.000001) {
    return '0%';
  }

  const percent = value * 100;
  return `${percent.toFixed(Math.abs(percent) < 1 ? 1 : 0)}%`;
};

const getNiceTickStep = (roughStep: number) => {
  const exponent = Math.floor(Math.log10(roughStep));
  const magnitude = 10 ** exponent;
  const normalized = roughStep / magnitude;
  const niceNormalized = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
  return niceNormalized * magnitude;
};

const getChartY = (drawdown: number, domainBottom: number) => CHART_TOP + (drawdown / domainBottom) * CHART_HEIGHT;

const findClosestPointIndex = (points: ChartPoint[], targetX: number) => {
  let low = 0;
  let high = points.length - 1;

  while (low < high) {
    const middle = Math.floor((low + high) / 2);
    if (points[middle].x < targetX) {
      low = middle + 1;
    } else {
      high = middle;
    }
  }

  if (low === 0) {
    return 0;
  }

  return Math.abs(points[low].x - targetX) < Math.abs(points[low - 1].x - targetX) ? low : low - 1;
};

const WatchlistChart = ({
  series,
  rangeLabel,
  onDateRangeOpen,
}: {
  series: DrawdownPointDto[];
  rangeLabel: string;
  onDateRangeOpen: () => void;
}) => {
  const [activeIndex, setActiveIndex] = useState<number | null>(null);

  useEffect(() => {
    setActiveIndex(null);
  }, [series]);

  const latest = series.at(-1);
  const worstIndex = useMemo(() => {
    if (series.length === 0) {
      return -1;
    }

    return series.reduce(
      (currentWorstIndex, item, index) =>
        item.Drawdown < series[currentWorstIndex].Drawdown ? index : currentWorstIndex,
      0
    );
  }, [series]);
  const worst = worstIndex >= 0 ? series[worstIndex] : null;

  const chartScale = useMemo(() => {
    const minimumDepth = 0.01;
    const deepestDrawdown = Math.max(Math.abs(worst?.Drawdown ?? 0), minimumDepth);
    const tickStep = getNiceTickStep(deepestDrawdown / 4);
    const tickCount = Math.max(1, Math.ceil(deepestDrawdown / tickStep));
    const domainBottom = -(tickCount * tickStep);

    return {
      domainBottom,
      yTicks: Array.from({ length: tickCount + 1 }, (_, index) => -(index * tickStep)),
    };
  }, [worst]);

  const points = useMemo<ChartPoint[]>(() => {
    if (series.length === 0) {
      return [];
    }

    const timestamps = series.map((item) => Date.parse(`${item.Date}T00:00:00Z`));
    const hasValidTimeline = timestamps.every(Number.isFinite) && timestamps.at(-1)! > timestamps[0];
    const timelineStart = timestamps[0];
    const timelineSpan = timestamps.at(-1)! - timelineStart;

    return series.map((item, index) => ({
      x:
        CHART_LEFT +
        (hasValidTimeline
          ? ((timestamps[index] - timelineStart) / timelineSpan) * CHART_WIDTH
          : series.length === 1
            ? CHART_WIDTH / 2
            : (index / (series.length - 1)) * CHART_WIDTH),
      y: getChartY(item.Drawdown, chartScale.domainBottom),
      label: item.Date,
      drawdown: item.Drawdown,
      price: item.Price,
    }));
  }, [chartScale.domainBottom, series]);

  const path = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ');
  const areaPath =
    points.length > 0
      ? `M ${points[0].x} ${CHART_TOP} L ${points.map((point) => `${point.x} ${point.y}`).join(' L ')} L ${points.at(-1)!.x} ${CHART_TOP} Z`
      : '';

  const xTicks = useMemo(() => {
    if (series.length === 0) {
      return [] as { x: number; label: string }[];
    }

    const indexes = Array.from(
      new Set([0, Math.floor((series.length - 1) / 3), Math.floor(((series.length - 1) * 2) / 3), series.length - 1])
    ).sort((a, b) => a - b);
    const showYear = series[0].Date.slice(0, 4) !== series.at(-1)!.Date.slice(0, 4);

    return indexes.map((index) => ({
      x: points[index]?.x ?? CHART_LEFT,
      label: showYear
        ? series[index].Date.slice(0, 7).replace('-', '/')
        : series[index].Date.slice(5).replace('-', '/'),
    }));
  }, [points, series]);

  const activePoint = activeIndex !== null ? (points[activeIndex] ?? null) : null;
  const worstPoint = worstIndex >= 0 ? points[worstIndex] : null;

  const updateActivePoint = (clientX: number, element: HTMLDivElement) => {
    if (points.length === 0) {
      return;
    }

    const bounds = element.getBoundingClientRect();
    const targetX = ((clientX - bounds.left) / bounds.width) * 100;
    setActiveIndex(findClosestPointIndex(points, targetX));
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs text-muted-foreground">{rangeLabel}回撤曲线</p>
          <button
            type="button"
            onClick={onDateRangeOpen}
            className="mt-1 text-left text-sm text-muted-foreground underline-offset-4 hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            aria-label="选择起止日期"
          >
            {series[0]?.Date} — {series.at(-1)?.Date}
          </button>
        </div>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="inline-block size-2 rounded-full bg-sky-500" />
          回撤率
        </div>
      </div>

      <div className="grid grid-cols-3 divide-x divide-border/70 rounded-xl border border-border/80 bg-background/80">
        <div className="min-w-0 px-3 py-3 sm:px-4">
          <div className="text-[11px] text-muted-foreground">当前回撤</div>
          <div className="mt-1 truncate text-lg font-semibold tabular-nums">
            {formatDrawdown(latest?.Drawdown ?? null)}
          </div>
        </div>
        <div className="min-w-0 px-3 py-3 sm:px-4">
          <div className="text-[11px] text-muted-foreground">最大回撤</div>
          <div className="mt-1 truncate text-lg font-semibold text-emerald-600 tabular-nums dark:text-emerald-400">
            {formatDrawdown(worst?.Drawdown ?? null)}
          </div>
        </div>
        <div className="min-w-0 px-3 py-3 sm:px-4">
          <div className="text-[11px] text-muted-foreground">最新价格</div>
          <div className="mt-1 truncate text-lg font-semibold tabular-nums">{formatPrice(latest?.Price ?? null)}</div>
        </div>
      </div>

      <div className="rounded-xl border border-border/80 bg-background p-3 sm:p-4">
        <div className="grid grid-cols-[42px_minmax(0,1fr)] gap-2 sm:grid-cols-[50px_minmax(0,1fr)] sm:gap-3">
          <div className="relative h-72 text-right text-[10px] tabular-nums text-muted-foreground sm:text-[11px]">
            {chartScale.yTicks.map((tick) => (
              <div
                key={tick}
                className="absolute right-0 -translate-y-1/2"
                style={{ top: `${getChartY(tick, chartScale.domainBottom)}%` }}
              >
                {formatAxisPercent(tick)}
              </div>
            ))}
          </div>

          <div className="min-w-0">
            <div
              className="relative h-72 cursor-crosshair touch-pan-y select-none rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
              role="slider"
              aria-label="回撤曲线，使用左右方向键查看每日数据"
              aria-valuemin={0}
              aria-valuemax={Math.max(points.length - 1, 0)}
              aria-valuenow={activeIndex ?? 0}
              aria-valuetext={
                activePoint
                  ? `${activePoint.label}，价格 ${formatPrice(activePoint.price)}，回撤 ${formatDrawdown(activePoint.drawdown)}`
                  : undefined
              }
              tabIndex={0}
              onPointerDown={(event) => updateActivePoint(event.clientX, event.currentTarget)}
              onPointerMove={(event) => updateActivePoint(event.clientX, event.currentTarget)}
              onPointerLeave={() => setActiveIndex(null)}
              onFocus={() => setActiveIndex((current) => current ?? Math.max(points.length - 1, 0))}
              onBlur={() => setActiveIndex(null)}
              onKeyDown={(event) => {
                if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
                  return;
                }

                event.preventDefault();
                const direction = event.key === 'ArrowLeft' ? -1 : 1;
                setActiveIndex((current) =>
                  Math.min(points.length - 1, Math.max(0, (current ?? points.length - 1) + direction))
                );
              }}
            >
              <svg
                viewBox="0 0 100 100"
                className="absolute inset-0 size-full overflow-visible"
                preserveAspectRatio="none"
                role="img"
                aria-label="回撤曲线图"
              >
                <defs>
                  <linearGradient id="drawdown-area-gradient" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="rgb(14 165 233)" stopOpacity="0.06" />
                    <stop offset="100%" stopColor="rgb(14 165 233)" stopOpacity="0.3" />
                  </linearGradient>
                </defs>

                {chartScale.yTicks.map((tick, index) => (
                  <line
                    key={tick}
                    x1={CHART_LEFT}
                    y1={getChartY(tick, chartScale.domainBottom)}
                    x2={CHART_LEFT + CHART_WIDTH}
                    y2={getChartY(tick, chartScale.domainBottom)}
                    className={index === 0 ? 'stroke-border' : 'stroke-border/60'}
                    strokeDasharray={index === 0 ? undefined : '3 3'}
                    strokeWidth={index === 0 ? '1' : '0.7'}
                    vectorEffect="non-scaling-stroke"
                  />
                ))}

                {xTicks.map((tick, index) => (
                  <line
                    key={`${tick.label}-${index}`}
                    x1={tick.x}
                    y1={CHART_TOP}
                    x2={tick.x}
                    y2={CHART_TOP + CHART_HEIGHT}
                    className="stroke-border/40"
                    strokeDasharray="3 3"
                    strokeWidth="0.7"
                    vectorEffect="non-scaling-stroke"
                  />
                ))}

                {areaPath ? <path d={areaPath} fill="url(#drawdown-area-gradient)" /> : null}

                {path ? (
                  <path
                    d={path}
                    fill="none"
                    className="stroke-sky-500"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.8"
                    vectorEffect="non-scaling-stroke"
                  />
                ) : null}

                {activePoint ? (
                  <>
                    <line
                      x1={CHART_LEFT}
                      y1={activePoint.y}
                      x2={CHART_LEFT + CHART_WIDTH}
                      y2={activePoint.y}
                      className="stroke-foreground/25"
                      strokeDasharray="3 3"
                      strokeWidth="0.8"
                      vectorEffect="non-scaling-stroke"
                    />
                    <line
                      x1={activePoint.x}
                      y1={CHART_TOP}
                      x2={activePoint.x}
                      y2={CHART_TOP + CHART_HEIGHT}
                      className="stroke-foreground/35"
                      strokeDasharray="3 3"
                      strokeWidth="0.8"
                      vectorEffect="non-scaling-stroke"
                    />
                  </>
                ) : null}
              </svg>

              {worstPoint ? (
                <div
                  className="pointer-events-none absolute size-2 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-background bg-destructive shadow-sm"
                  style={{ left: `${worstPoint.x}%`, top: `${worstPoint.y}%` }}
                  title={`最大回撤 ${formatDrawdown(worstPoint.drawdown)}`}
                />
              ) : null}

              {activePoint ? (
                <>
                  <div
                    className="pointer-events-none absolute size-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-[3px] border-background bg-sky-500 shadow"
                    style={{ left: `${activePoint.x}%`, top: `${activePoint.y}%` }}
                  />
                  <div
                    className={cn(
                      'pointer-events-none absolute z-10 min-w-36 rounded-lg border border-border bg-popover/95 px-3 py-2 text-xs shadow-lg backdrop-blur-sm',
                      activePoint.x < 20
                        ? 'translate-x-2'
                        : activePoint.x > 80
                          ? '-translate-x-[calc(100%+0.5rem)]'
                          : '-translate-x-1/2',
                      activePoint.y < 30 ? 'translate-y-3' : '-translate-y-[calc(100%+0.75rem)]'
                    )}
                    style={{ left: `${activePoint.x}%`, top: `${activePoint.y}%` }}
                  >
                    <div className="font-medium text-popover-foreground">{activePoint.label}</div>
                    <div className="mt-1.5 grid grid-cols-2 gap-x-4 gap-y-1 tabular-nums text-muted-foreground">
                      <span>价格</span>
                      <span className="text-right text-popover-foreground">{formatPrice(activePoint.price)}</span>
                      <span>回撤</span>
                      <span className="text-right font-medium text-destructive">
                        {formatDrawdown(activePoint.drawdown)}
                      </span>
                    </div>
                  </div>
                </>
              ) : null}
            </div>

            <div className="relative mt-2 h-4 text-[10px] tabular-nums text-muted-foreground sm:text-[11px]">
              {xTicks.map((tick, index) => (
                <span
                  key={`${tick.label}-${index}`}
                  className={cn(
                    'absolute whitespace-nowrap',
                    index === 0
                      ? 'translate-x-0'
                      : index === xTicks.length - 1
                        ? '-translate-x-full'
                        : '-translate-x-1/2'
                  )}
                  style={{ left: `${tick.x}%` }}
                >
                  {tick.label}
                </span>
              ))}
            </div>
          </div>
        </div>

        {worst ? (
          <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-border/70 pt-3 text-xs text-muted-foreground">
            <span>红点为区间最大回撤</span>
            <span className="tabular-nums">
              {worst.Date} · {formatDrawdown(worst.Drawdown)}
            </span>
          </div>
        ) : null}
      </div>
    </div>
  );
};

const HomeView = () => {
  const [items, setItems] = useState<WatchlistItemSummaryDto[]>([]);
  const [selectedThsCode, setSelectedThsCode] = useState<string | null>(null);
  const [series, setSeries] = useState<DrawdownPointDto[]>([]);
  const [code, setCode] = useState('');
  const [candidates, setCandidates] = useState<WatchlistSearchCandidateDto[]>([]);
  const [selectedCandidate, setSelectedCandidate] = useState<WatchlistSearchCandidateDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [feedbackTone, setFeedbackTone] = useState<FeedbackTone>('default');
  const [selectedRange, setSelectedRange] = useState('1y');
  const [availableRanges, setAvailableRanges] = useState<DrawdownRangeOptionDto[]>([]);
  const [customDateRange, setCustomDateRange] = useState<DateRange | undefined>();
  const [isDatePickerOpen, setIsDatePickerOpen] = useState(false);
  const detailRequestCodeRef = useRef<string | null>(null);
  const searchRequestCodeRef = useRef('');

  const selectedItem = items.find((item) => item.ThsCode === selectedThsCode) ?? null;
  const selectedRangeLabel = availableRanges.find((item) => item.Value === selectedRange)?.Label ?? '1年';

  const handleDateRangeOpen = () => {
    setCustomDateRange((current) => current ?? getSeriesDateRange(series));
    setIsDatePickerOpen(true);
  };

  const handleRangeChange = (range: string) => {
    if (range === 'custom') {
      setCustomDateRange((current) => current ?? getSeriesDateRange(series));
      window.setTimeout(() => setIsDatePickerOpen(true), 0);
      return;
    }

    setCustomDateRange(undefined);
    setIsDatePickerOpen(false);
    setSelectedRange(range);
  };

  const handleCustomDateRangeConfirm = (range: DateRange) => {
    setCustomDateRange(range);
    setSelectedRange('custom');
  };

  const loadWatchlist = async () => {
    setIsLoading(true);

    try {
      const response = await WatchlistManager.GetWatchlist({});
      setItems(response.Items);
      setSelectedThsCode((current) =>
        current && response.Items.some((item) => item.ThsCode === current)
          ? current
          : (response.Items[0]?.ThsCode ?? null)
      );
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
    const normalized = code.trim();
    setSelectedCandidate((current) => (current?.Code === normalized ? current : null));

    if (normalized.length !== 6) {
      setCandidates([]);
      setIsSearching(false);
      return;
    }

    searchRequestCodeRef.current = normalized;
    setIsSearching(true);

    const timeout = window.setTimeout(async () => {
      try {
        const response = await WatchlistManager.SearchWatchlistCandidates({ Query: normalized });
        if (searchRequestCodeRef.current !== normalized) {
          return;
        }

        setCandidates(response.Items);
        if (response.Items.length === 1) {
          setSelectedCandidate(response.Items[0]);
        }
      } catch (error) {
        if (searchRequestCodeRef.current !== normalized) {
          return;
        }

        setCandidates([]);
        setFeedbackTone('error');
        setFeedback(error instanceof Error ? error.message : '搜索同花顺数据失败，请检查 API Key 或网络连接。');
      } finally {
        if (searchRequestCodeRef.current === normalized) {
          setIsSearching(false);
        }
      }
    }, 250);

    return () => {
      window.clearTimeout(timeout);
    };
  }, [code]);

  useEffect(() => {
    if (!selectedThsCode) {
      setSeries([]);
      return;
    }

    const startDate =
      selectedRange === 'custom' && customDateRange?.from ? formatDate(customDateRange.from) : undefined;
    const endDate = selectedRange === 'custom' && customDateRange?.to ? formatDate(customDateRange.to) : undefined;
    const requestKey = `${selectedThsCode}:${selectedRange}:${startDate ?? ''}:${endDate ?? ''}`;
    detailRequestCodeRef.current = requestKey;

    if (selectedRange === 'custom' && (!customDateRange?.from || !customDateRange.to)) {
      setIsDetailLoading(false);
      return;
    }

    const loadDetail = async () => {
      setIsDetailLoading(true);
      setSeries([]);

      try {
        const response = await WatchlistManager.GetWatchlistItemDetail({
          ThsCode: selectedThsCode,
          Range: selectedRange,
          StartDate: startDate,
          EndDate: endDate,
        });
        if (!response.Success) {
          if (detailRequestCodeRef.current !== requestKey) {
            return;
          }

          setFeedbackTone('error');
          setFeedback(response.Message);
          setAvailableRanges(response.AvailableRanges);
          setSeries([]);
          return;
        }

        if (detailRequestCodeRef.current !== requestKey) {
          return;
        }

        setAvailableRanges(response.AvailableRanges);
        setSelectedRange(response.SelectedRange);
        setSeries(response.DrawdownSeries);
        setFeedback((current) => (current === '当前周期的数据暂不可用。' ? '' : current));
      } catch {
        if (detailRequestCodeRef.current !== requestKey) {
          return;
        }

        setFeedbackTone('error');
        setFeedback('加载回撤曲线失败。');
        setSeries([]);
      } finally {
        if (detailRequestCodeRef.current === requestKey) {
          setIsDetailLoading(false);
        }
      }
    };

    void loadDetail();
  }, [customDateRange, selectedRange, selectedThsCode]);

  const handleAdd = async () => {
    if (isSubmitting) {
      return;
    }

    if (!selectedCandidate) {
      setFeedbackTone('error');
      setFeedback(code.trim().length === 6 ? '请先从下拉候选中选择标的。' : '请输入 6 位代码。');
      return;
    }

    setIsSubmitting(true);
    setFeedback('');

    try {
      const response: AddWatchlistItemResponseDto = await WatchlistManager.AddWatchlistItem({
        Code: selectedCandidate.Code,
        ThsCode: selectedCandidate.ThsCode,
        AssetType: selectedCandidate.AssetType,
        Name: selectedCandidate.Name,
        SecurityType: selectedCandidate.SecurityType,
      });
      setFeedbackTone(response.Success || response.AlreadyExists ? 'default' : 'error');
      setFeedback(response.Message);

      if (!response.Item) {
        return;
      }

      if (response.Success) {
        setItems((current) => [response.Item!, ...current.filter((item) => item.ThsCode !== response.Item!.ThsCode)]);
      }

      setSelectedRange('1y');
      setSelectedThsCode(response.Item.ThsCode);
      setCode('');
      setCandidates([]);
      setSelectedCandidate(null);
    } catch {
      setFeedbackTone('error');
      setFeedback('添加失败，请稍后重试。');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (targetThsCode: string) => {
    try {
      const response = await WatchlistManager.DeleteWatchlistItem({ ThsCode: targetThsCode });
      if (!response.Success) {
        setFeedbackTone('error');
        setFeedback(response.Message);
        return;
      }

      const nextItems = items.filter((item) => item.ThsCode !== targetThsCode);
      setItems(nextItems);
      setFeedbackTone('default');
      setFeedback('删除成功');

      if (selectedThsCode === targetThsCode) {
        setSelectedRange('1y');
        setSelectedThsCode(nextItems[0]?.ThsCode ?? null);
      }
    } catch {
      setFeedbackTone('error');
      setFeedback('删除失败，请稍后重试。');
    }
  };

  return (
    <main className="min-h-screen bg-background px-4 py-8 text-foreground sm:px-6 lg:px-8">
      <section className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        {/* 输入框区域 */}
        <Card className="border border-border/80 bg-card/90 shadow-none">
          <CardContent className="space-y-3">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
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
            </div>

            {code.trim().length === 6 ? (
              <div className="rounded-xl border border-border/70 bg-background/70">
                {isSearching ? (
                  <div className="px-3 py-2 text-sm text-muted-foreground">搜索候选中…</div>
                ) : candidates.length === 0 ? (
                  <div className="px-3 py-2 text-sm text-muted-foreground">没有找到可选标的。</div>
                ) : (
                  <div className="max-h-64 overflow-y-auto py-1">
                    {candidates.map((candidate) => {
                      const isSelected = selectedCandidate?.ThsCode === candidate.ThsCode;
                      return (
                        <button
                          key={candidate.ThsCode}
                          type="button"
                          onClick={() => setSelectedCandidate(candidate)}
                          className={cn(
                            'flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm transition-colors hover:bg-muted/40',
                            isSelected && 'bg-primary/15 text-foreground ring-1 ring-primary/30'
                          )}
                        >
                          <div>
                            <div className="font-medium text-foreground">{candidate.Name}</div>
                            <div className="mt-1 text-xs text-muted-foreground">
                              {candidate.Code} · {candidate.SecurityType} · {candidate.ThsCode}
                            </div>
                          </div>
                          {isSelected ? <span className="text-xs text-muted-foreground">已选</span> : null}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            ) : null}
          </CardContent>
        </Card>

        {feedback ? (
          <div className={cn('text-sm', feedbackTone === 'error' ? 'text-destructive' : 'text-muted-foreground')}>
            {feedback}
          </div>
        ) : null}

        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:items-start">
          {/* 标的列表 */}
          <Card className="border border-border/80 bg-card/90 shadow-none">
            <CardContent>
              {isLoading ? (
                <div className="py-10 text-sm text-muted-foreground">加载中…</div>
              ) : items.length === 0 ? (
                <div className="py-10 text-sm text-muted-foreground">还没有监控标的。</div>
              ) : (
                <div className="divide-y divide-border/70 rounded-xl border border-border/70 bg-background/70">
                  {items.map((item) => (
                    <button
                      key={item.ThsCode}
                      type="button"
                      onClick={() => setSelectedThsCode(item.ThsCode)}
                      className={cn(
                        'flex w-full flex-col gap-2 px-4 py-3 text-left transition-colors hover:bg-muted/40',
                        selectedThsCode === item.ThsCode && 'bg-muted/60'
                      )}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <div className="text-sm font-medium text-foreground">
                            {item.Name}{' '}
                            <span className="text-xs font-normal text-muted-foreground">
                              {item.SecurityType} · {item.ThsCode}
                            </span>
                          </div>
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="text-muted-foreground hover:text-foreground"
                          onClick={(event) => {
                            event.stopPropagation();
                            void handleDelete(item.ThsCode);
                          }}
                        >
                          删除
                        </Button>
                      </div>

                      <div className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                        <div>
                          <div className="text-xs text-muted-foreground">现价</div>
                          <div className="mt-1 font-medium">{formatPrice(item.CurrentPrice)}</div>
                        </div>
                        <div>
                          <div className="text-xs text-muted-foreground">近1年最大回撤</div>
                          <div className="mt-1 font-medium text-emerald-600 dark:text-emerald-400">
                            {formatDrawdown(item.MaxDrawdown)}
                          </div>
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
            {/* 图表头 */}
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <CardTitle>{selectedItem ? `${selectedItem.Name} · ${selectedItem.Code}` : '回撤详情'}</CardTitle>
                {selectedItem ? (
                  <div className="flex items-center gap-2">
                    <DateRangePicker
                      range={customDateRange}
                      open={isDatePickerOpen}
                      onOpenChange={setIsDatePickerOpen}
                      onConfirm={handleCustomDateRangeConfirm}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="icon-sm"
                      onClick={handleDateRangeOpen}
                      aria-label="选择起止日期"
                      title="选择起止日期"
                    >
                      <CalendarDays />
                    </Button>
                    <Select value={selectedRange} onValueChange={handleRangeChange}>
                      <SelectTrigger className="w-[112px] bg-background">
                        <SelectValue placeholder="选择周期" />
                      </SelectTrigger>
                      <SelectContent>
                        {availableRanges.map((option) => (
                          <SelectItem key={option.Value} value={option.Value}>
                            {option.Label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                ) : null}
              </div>
            </CardHeader>
            {/* 图表 */}
            <CardContent>
              {!selectedItem ? (
                <div className="py-16 text-sm text-muted-foreground">选择一个标的后查看回撤曲线。</div>
              ) : isDetailLoading ? (
                <div className="py-16 text-sm text-muted-foreground">曲线加载中…</div>
              ) : series.length === 0 ? (
                <div className="py-16 text-sm text-muted-foreground">当前没有可展示的回撤数据。</div>
              ) : (
                <WatchlistChart series={series} rangeLabel={selectedRangeLabel} onDateRangeOpen={handleDateRangeOpen} />
              )}
            </CardContent>
          </Card>
        </div>
      </section>
    </main>
  );
};

export default HomeView;
