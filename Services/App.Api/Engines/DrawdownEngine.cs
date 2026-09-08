using Contracts.Domain.Logic;

namespace App.Api.Engines;

public class DrawdownEngine
{
    public (decimal MaxDrawdown, List<DrawdownPointSnapshot> Series) Calculate(IReadOnlyList<HistoricalPricePoint> history)
    {
        if (history.Count == 0)
        {
            return (0m, []);
        }

        var orderedHistory = history.OrderBy(x => x.Date).ToList();
        var peak = orderedHistory[0].ClosePrice;
        var maxDrawdown = 0m;
        var points = new List<DrawdownPointSnapshot>(orderedHistory.Count);

        foreach (var point in orderedHistory)
        {
            if (point.ClosePrice > peak)
            {
                peak = point.ClosePrice;
            }

            var drawdown = peak <= 0m ? 0m : Math.Round((point.ClosePrice - peak) / peak, 4, MidpointRounding.AwayFromZero);
            if (drawdown < maxDrawdown)
            {
                maxDrawdown = drawdown;
            }

            points.Add(new DrawdownPointSnapshot
            {
                Date = point.Date,
                Price = point.ClosePrice,
                Drawdown = drawdown
            });
        }

        return (maxDrawdown, points);
    }
}
