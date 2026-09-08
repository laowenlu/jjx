namespace Contracts.Domain.Logic;

public class DrawdownPointSnapshot
{
    public DateTime Date { get; set; }

    public decimal Price { get; set; }

    public decimal Drawdown { get; set; }
}
