namespace Contracts.Dto;

public class WatchlistItemSummaryDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SecurityType { get; set; } = string.Empty;

    public decimal? CurrentPrice { get; set; }

    public decimal? MaxDrawdown { get; set; }

    public string? DataWarning { get; set; }

    public string LastUpdatedUtc { get; set; } = string.Empty;
}
