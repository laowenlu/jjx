using System.Globalization;
using System.Text.Json;
using Contracts.Domain.Logic;

namespace App.Api.Accessors;

public class EastMoneyMarketDataAccessor : IMarketDataAccessor
{
    private static readonly string[] SecIdPrefixes = ["1", "0"];
    private readonly HttpClient _httpClient;

    public EastMoneyMarketDataAccessor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResolvedSecurityData?> GetSecurityData(string code)
    {
        try
        {
            foreach (var prefix in SecIdPrefixes)
            {
                var secId = $"{prefix}.{code}";
                var quote = await TryGetQuote(secId);
                if (!quote.HasValue)
                {
                    continue;
                }

                var history = await TryGetHistory(secId);
                if (history.Count == 0)
                {
                    continue;
                }

                var quoteValue = quote.Value;
                var currentPrice = quoteValue.CurrentPrice ?? history[^1].ClosePrice;
                return new ResolvedSecurityData
                {
                    Code = code,
                    Name = quoteValue.Name,
                    SecurityType = quoteValue.SecurityType,
                    CurrentPrice = currentPrice,
                    History = history,
                    DataWarning = quoteValue.CurrentPrice.HasValue ? null : "当前价格暂不可用，已显示最近一个交易日收盘价。"
                };
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<(string Name, string SecurityType, decimal? CurrentPrice)?> TryGetQuote(string secId)
    {
        var fields = "f57,f58,f43,f116";
        var url = $"https://push2.eastmoney.com/api/qt/stock/get?invt=2&fltt=2&fields={fields}&secid={secId}";
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var name = dataElement.TryGetProperty("f58", out var nameElement) ? nameElement.GetString() : null;
        var resolvedCode = dataElement.TryGetProperty("f57", out var codeElement) ? codeElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(resolvedCode))
        {
            return null;
        }

        decimal? currentPrice = null;
        if (dataElement.TryGetProperty("f43", out var priceElement) && TryReadDecimal(priceElement, out var rawPrice))
        {
            currentPrice = rawPrice / 100m;
        }

        var securityType = secId.StartsWith("1.", StringComparison.Ordinal) ? "沪市" : "深市";
        return (name!, securityType, currentPrice);
    }

    private async Task<List<HistoricalPricePoint>> TryGetHistory(string secId)
    {
        var endDate = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var startDate = DateTime.UtcNow.AddYears(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var url = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56&klt=101&fqt=1&secid={secId}&beg={startDate}&end={endDate}";
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || !dataElement.TryGetProperty("klines", out var klineElement) || klineElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var points = new List<HistoricalPricePoint>();
        foreach (var item in klineElement.EnumerateArray())
        {
            var line = item.GetString();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 3)
            {
                continue;
            }

            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ||
                !decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var closePrice))
            {
                continue;
            }

            points.Add(new HistoricalPricePoint
            {
                Date = date,
                ClosePrice = closePrice
            });
        }

        return points;
    }

    private static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDecimal(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }
}
