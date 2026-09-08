namespace Contracts.Dto;

public class GetWatchlistItemDetailResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public WatchlistItemSummaryDto? Item { get; set; }

    public string SelectedRange { get; set; } = "1y";

    public List<DrawdownRangeOptionDto> AvailableRanges { get; set; } = [];

    public List<DrawdownPointDto> DrawdownSeries { get; set; } = [];
}
