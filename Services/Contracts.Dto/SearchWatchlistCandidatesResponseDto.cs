namespace Contracts.Dto;

public class SearchWatchlistCandidatesResponseDto
{
    public List<WatchlistSearchCandidateDto> Items { get; set; } = [];

    public string Message { get; set; } = string.Empty;
}
