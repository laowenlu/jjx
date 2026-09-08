namespace Contracts.Domain.Logic;

public class ResolvedSecurityData
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SecurityType { get; set; } = string.Empty;

    public decimal? CurrentPrice { get; set; }

    public List<HistoricalPricePoint> History { get; set; } = [];

    public string? DataWarning { get; set; }
}
