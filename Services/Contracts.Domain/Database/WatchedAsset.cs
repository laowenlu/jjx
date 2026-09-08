using Contracts.Domain.Logic;

namespace Contracts.Domain.Database;

public class WatchedAsset : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SecurityType { get; set; } = string.Empty;

    public decimal? CurrentPrice { get; set; }

    public decimal? MaxDrawdown { get; set; }

    public DateTime LastUpdatedUtc { get; set; }

    public List<DrawdownPointSnapshot> DrawdownSeries { get; set; } = [];

    public string? DataWarning { get; set; }
}
