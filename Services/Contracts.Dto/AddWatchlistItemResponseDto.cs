namespace Contracts.Dto;

public class AddWatchlistItemResponseDto
{
    public bool Success { get; set; }

    public bool AlreadyExists { get; set; }

    public string Message { get; set; } = string.Empty;

    public WatchlistItemSummaryDto? Item { get; set; }
}
