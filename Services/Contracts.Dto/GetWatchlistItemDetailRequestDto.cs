namespace Contracts.Dto;

public class GetWatchlistItemDetailRequestDto
{
    public string ThsCode { get; set; } = string.Empty;

    public string Range { get; set; } = "1y";
}
