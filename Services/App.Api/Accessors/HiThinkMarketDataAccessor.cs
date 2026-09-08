using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Contracts.Domain.Logic;

namespace App.Api.Accessors;

public class HiThinkMarketDataAccessor : IMarketDataAccessor
{
    private const string BaseUrl = "https://fuyao.aicubes.cn";
    private readonly HttpClient _httpClient;
    private readonly AppConfiguration _appConfiguration;

    public HiThinkMarketDataAccessor(HttpClient httpClient, AppConfiguration appConfiguration)
    {
        _httpClient = httpClient;
        _appConfiguration = appConfiguration;
    }

    public async Task<List<SearchedSecurityCandidate>> SearchSecurities(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var assetTypes = Uri.EscapeDataString("a-share,fund-otc,fund-etf,fund-lof,fund-reits");
        using var request = CreateRequest($"/api/meta/tickers/search?q={encodedQuery}&asset_type={assetTypes}&limit=20");
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = await ReadDocument(response);
        if (!TryGetDataItems(document, out var itemsElement))
        {
            return [];
        }

        var candidates = new List<SearchedSecurityCandidate>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var thsCode = ReadString(item, "thscode");
            var ticker = ReadString(item, "ticker");
            var name = ReadString(item, "name");
            var assetType = ReadString(item, "asset_type");
            if (string.IsNullOrWhiteSpace(thsCode) || string.IsNullOrWhiteSpace(ticker) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(assetType))
            {
                continue;
            }

            candidates.Add(new SearchedSecurityCandidate
            {
                ThsCode = thsCode,
                Ticker = ticker,
                Name = name,
                AssetType = assetType,
                Exchange = ReadString(item, "exchange") ?? string.Empty
            });
        }

        if (query.Trim().Length == 6 && query.Trim().All(char.IsDigit))
        {
            var inferredIndexes = await SearchStandardIndexesByTicker(query.Trim());
            foreach (var inferredIndex in inferredIndexes)
            {
                if (candidates.Any(existing => existing.ThsCode.Equals(inferredIndex.ThsCode, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                candidates.Add(inferredIndex);
            }
        }

        return candidates
            .OrderBy(x => x.AssetType == "a-share-index" ? 0 : 1)
            .ThenBy(x => x.Ticker == query.Trim() ? 0 : 1)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.ThsCode)
            .ToList();
    }

    private async Task<List<SearchedSecurityCandidate>> SearchStandardIndexesByTicker(string ticker)
    {
        var inferredCandidates = new List<SearchedSecurityCandidate>();

        foreach (var exchange in new[] { "SH", "SZ" })
        {
            var thsCode = $"{ticker}.{exchange}";
            using var request = CreateRequest($"/api/a-share-index/prices/snapshot?thscodes={Uri.EscapeDataString(thsCode)}");
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            using var document = await ReadDocument(response);
            if (!TryGetDataItems(document, out var itemsElement) || itemsElement.GetArrayLength() == 0)
            {
                continue;
            }

            inferredCandidates.Add(new SearchedSecurityCandidate
            {
                ThsCode = thsCode,
                Ticker = ticker,
                Name = exchange == "SH" ? "上证指数" : "深证成指",
                AssetType = "a-share-index",
                Exchange = exchange
            });
        }

        return inferredCandidates;
    }

    public async Task<ResolvedSecurityData?> GetSecurityData(string thsCode, string assetType, string range)
    {
        try
        {
            var candidate = (await SearchSecurities(thsCode)).FirstOrDefault(x => x.ThsCode.Equals(thsCode, StringComparison.OrdinalIgnoreCase));
            if (candidate == null)
            {
                return null;
            }

            return assetType switch
            {
                "a-share" => await GetShareSecurityData(candidate, range),
                "a-share-index" => await GetIndexSecurityData(candidate, range),
                "fund-otc" or "fund-etf" or "fund-lof" or "fund-reits" => await GetFundSecurityData(candidate, range),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<ResolvedSecurityData?> GetShareSecurityData(SearchedSecurityCandidate candidate, string range)
    {
        using var snapshotRequest = CreateRequest($"/api/a-share/prices/snapshot?thscodes={Uri.EscapeDataString(candidate.ThsCode)}");
        using var snapshotResponse = await _httpClient.SendAsync(snapshotRequest);
        if (!snapshotResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var snapshotDocument = await ReadDocument(snapshotResponse);
        if (!TryGetDataItems(snapshotDocument, out var snapshotItems))
        {
            return null;
        }

        var snapshotItem = snapshotItems.EnumerateArray().FirstOrDefault();
        var history = await GetHistoricalBars($"/api/a-share/prices/historical?thscode={Uri.EscapeDataString(candidate.ThsCode)}&interval=1d&start={GetRangeStartTimestamp(range)}&end={GetNowTimestamp()}&adjust=forward");
        if (history.Count == 0)
        {
            return null;
        }

        return new ResolvedSecurityData
        {
            ThsCode = candidate.ThsCode,
            Code = candidate.Ticker,
            Name = candidate.Name,
            AssetType = candidate.AssetType,
            SecurityType = ToSecurityTypeLabel(candidate.AssetType),
            CurrentPrice = TryReadDecimal(snapshotItem, "last_price") ?? history[^1].ClosePrice,
            History = history
        };
    }

    private async Task<ResolvedSecurityData?> GetIndexSecurityData(SearchedSecurityCandidate candidate, string range)
    {
        using var snapshotRequest = CreateRequest($"/api/a-share-index/prices/snapshot?thscodes={Uri.EscapeDataString(candidate.ThsCode)}");
        using var snapshotResponse = await _httpClient.SendAsync(snapshotRequest);
        if (!snapshotResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var snapshotDocument = await ReadDocument(snapshotResponse);
        if (!TryGetDataItems(snapshotDocument, out var snapshotItems))
        {
            return null;
        }

        var snapshotItem = snapshotItems.EnumerateArray().FirstOrDefault();
        var history = await GetHistoricalBars($"/api/a-share-index/prices/historical?thscode={Uri.EscapeDataString(candidate.ThsCode)}&interval=1d&start={GetRangeStartTimestamp(range)}&end={GetNowTimestamp()}");
        if (history.Count == 0)
        {
            return null;
        }

        return new ResolvedSecurityData
        {
            ThsCode = candidate.ThsCode,
            Code = candidate.Ticker,
            Name = candidate.Name,
            AssetType = candidate.AssetType,
            SecurityType = ToSecurityTypeLabel(candidate.AssetType),
            CurrentPrice = TryReadDecimal(snapshotItem, "last_price") ?? history[^1].ClosePrice,
            History = history
        };
    }

    private async Task<ResolvedSecurityData?> GetFundSecurityData(SearchedSecurityCandidate candidate, string range)
    {
        var fundType = ToFundType(candidate.AssetType);
        using var navRequest = CreateRequest($"/api/fund/performance/nav?fund_type={fundType}&thscode={Uri.EscapeDataString(candidate.ThsCode)}&range={ToFundRange(range)}&nav_type=unit%2Cadj");
        using var navResponse = await _httpClient.SendAsync(navRequest);
        if (!navResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var navDocument = await ReadDocument(navResponse);
        if (!TryGetDataItems(navDocument, out var navItems))
        {
            return null;
        }

        var history = new List<HistoricalPricePoint>();
        foreach (var item in navItems.EnumerateArray())
        {
            var navDate = ReadString(item, "nav_date");
            if (string.IsNullOrWhiteSpace(navDate) || !DateTime.TryParse(navDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
            {
                continue;
            }

            var price = TryReadDecimal(item, "adj_nav") ?? TryReadDecimal(item, "unit_nav");
            if (!price.HasValue)
            {
                continue;
            }

            history.Add(new HistoricalPricePoint
            {
                Date = date,
                ClosePrice = price.Value
            });
        }

        history = history.OrderBy(x => x.Date).ToList();
        if (history.Count == 0)
        {
            return null;
        }

        return new ResolvedSecurityData
        {
            ThsCode = candidate.ThsCode,
            Code = candidate.Ticker,
            Name = candidate.Name,
            AssetType = candidate.AssetType,
            SecurityType = ToSecurityTypeLabel(candidate.AssetType),
            CurrentPrice = history[^1].ClosePrice,
            History = history,
            DataWarning = candidate.AssetType is "fund-etf" or "fund-lof"
                ? "当前展示基于基金净值口径，不是场内实时成交价。"
                : null
        };
    }

    private async Task<List<HistoricalPricePoint>> GetHistoricalBars(string path)
    {
        using var request = CreateRequest(path);
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = await ReadDocument(response);
        if (!TryGetDataItems(document, out var itemsElement))
        {
            return [];
        }

        var points = new List<HistoricalPricePoint>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var dateMs = TryReadLong(item, "date_ms");
            var closePrice = TryReadDecimal(item, "close_price");
            if (!dateMs.HasValue || !closePrice.HasValue)
            {
                continue;
            }

            points.Add(new HistoricalPricePoint
            {
                Date = DateTimeOffset.FromUnixTimeMilliseconds(dateMs.Value).UtcDateTime.Date,
                ClosePrice = closePrice.Value
            });
        }

        return points.OrderBy(x => x.Date).ToList();
    }

    private HttpRequestMessage CreateRequest(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{path}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-api-key", _appConfiguration.HiThinkApiKey);
        return request;
    }

    private static async Task<JsonDocument> ReadDocument(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static bool TryGetDataItems(JsonDocument document, out JsonElement itemsElement)
    {
        itemsElement = default;
        if (!document.RootElement.TryGetProperty("code", out var codeElement) || codeElement.GetInt32() != 0)
        {
            return false;
        }

        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        return dataElement.TryGetProperty("item", out itemsElement) && itemsElement.ValueKind == JsonValueKind.Array;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.GetString();
    }

    private static decimal? TryReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var numberValue))
        {
            return numberValue;
        }

        if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    private static long? TryReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numberValue))
        {
            return numberValue;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    private static long GetRangeStartTimestamp(string range)
    {
        var startDate = range switch
        {
            "6m" => DateTime.UtcNow.AddMonths(-6).Date,
            "1y" => DateTime.UtcNow.AddYears(-1).Date,
            "2y" => DateTime.UtcNow.AddYears(-2).Date,
            "3y" => DateTime.UtcNow.AddYears(-3).Date,
            "5y" => DateTime.UtcNow.AddYears(-5).Date,
            "8y" => DateTime.UtcNow.AddYears(-8).Date,
            _ => DateTime.UtcNow.AddYears(-1).Date
        };

        return new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
    }

    private static long GetNowTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string ToFundRange(string range)
    {
        return range switch
        {
            "6m" => "half_year",
            "1y" => "year",
            "2y" => "2y",
            "3y" => "3y",
            "5y" => "5y",
            "8y" => "8y",
            _ => "year"
        };
    }

    private static string ToFundType(string assetType)
    {
        return assetType switch
        {
            "fund-otc" => "otc",
            "fund-reits" => "reits",
            _ => "exchange"
        };
    }

    private static string ToSecurityTypeLabel(string assetType)
    {
        return assetType switch
        {
            "a-share" => "股票",
            "a-share-index" => "指数",
            "fund-etf" => "ETF",
            "fund-lof" => "LOF",
            "fund-reits" => "REITs",
            "fund-otc" => "基金",
            _ => assetType
        };
    }
}
