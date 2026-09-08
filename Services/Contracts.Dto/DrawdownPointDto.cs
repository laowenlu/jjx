namespace Contracts.Dto;

public class DrawdownPointDto
{
    public string Date { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal Drawdown { get; set; }
}
