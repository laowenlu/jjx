using App.Api.Accessors;
using App.Api.Engines;
using App.ServiceInvoker.Interfaces;
using Contracts.Domain.Database;
using Contracts.Dto;

namespace App.Api.Managers;

public class WatchlistManager : IManagerService
{
    private readonly IDatabaseAccessor _databaseAccessor;
    private readonly IMarketDataAccessor _marketDataAccessor;
    private readonly DrawdownEngine _drawdownEngine;

    public WatchlistManager(IDatabaseAccessor databaseAccessor, IMarketDataAccessor marketDataAccessor, DrawdownEngine drawdownEngine)
    {
        _databaseAccessor = databaseAccessor;
        _marketDataAccessor = marketDataAccessor;
        _drawdownEngine = drawdownEngine;
    }

    public async Task<GetWatchlistResponseDto> GetWatchlist(GetWatchlistRequestDto request)
    {
        var items = await _databaseAccessor.GetAllDocuments<WatchedAsset>();
        return new GetWatchlistResponseDto
        {
            Items = items
                .OrderByDescending(x => x.LastUpdatedUtc)
                .ThenBy(x => x.Code)
                .Select(ToSummaryDto)
                .ToList()
        };
    }

    public async Task<AddWatchlistItemResponseDto> AddWatchlistItem(AddWatchlistItemRequestDto request)
    {
        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode == null)
        {
            return new AddWatchlistItemResponseDto
            {
                Success = false,
                Message = "请输入 6 位代码。"
            };
        }

        var existing = await _databaseAccessor.GetDocumentByProperty<WatchedAsset>(x => x.Code == normalizedCode);
        if (existing != null)
        {
            return new AddWatchlistItemResponseDto
            {
                Success = false,
                AlreadyExists = true,
                Message = "已存在",
                Item = ToSummaryDto(existing)
            };
        }

        var securityData = await _marketDataAccessor.GetSecurityData(normalizedCode);
        if (securityData == null)
        {
            return new AddWatchlistItemResponseDto
            {
                Success = false,
                Message = "无法识别该标的，或暂时无法取得数据。"
            };
        }

        var (maxDrawdown, series) = _drawdownEngine.Calculate(securityData.History);
        if (series.Count == 0)
        {
            return new AddWatchlistItemResponseDto
            {
                Success = false,
                Message = "近一年历史数据暂不可用。"
            };
        }

        var watchedAsset = new WatchedAsset
        {
            Code = normalizedCode,
            Name = securityData.Name,
            SecurityType = securityData.SecurityType,
            CurrentPrice = securityData.CurrentPrice,
            MaxDrawdown = maxDrawdown,
            DrawdownSeries = series,
            LastUpdatedUtc = DateTime.UtcNow,
            DataWarning = securityData.DataWarning
        };

        await _databaseAccessor.InsertDocument(watchedAsset);

        return new AddWatchlistItemResponseDto
        {
            Success = true,
            Message = "添加成功",
            Item = ToSummaryDto(watchedAsset)
        };
    }

    public async Task<OperationResultDto> DeleteWatchlistItem(DeleteWatchlistItemRequestDto request)
    {
        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode == null)
        {
            return new OperationResultDto
            {
                Success = false,
                Message = "请输入 6 位代码。"
            };
        }

        var existing = await _databaseAccessor.GetDocumentByProperty<WatchedAsset>(x => x.Code == normalizedCode);
        if (existing == null)
        {
            return new OperationResultDto
            {
                Success = false,
                Message = "未找到该标的。"
            };
        }

        await _databaseAccessor.DeleteDocument(existing);
        return new OperationResultDto
        {
            Success = true,
            Message = "删除成功"
        };
    }

    public async Task<GetWatchlistItemDetailResponseDto> GetWatchlistItemDetail(GetWatchlistItemDetailRequestDto request)
    {
        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode == null)
        {
            return new GetWatchlistItemDetailResponseDto
            {
                Success = false,
                Message = "请输入 6 位代码。"
            };
        }

        var existing = await _databaseAccessor.GetDocumentByProperty<WatchedAsset>(x => x.Code == normalizedCode);
        if (existing == null)
        {
            return new GetWatchlistItemDetailResponseDto
            {
                Success = false,
                Message = "未找到该标的。"
            };
        }

        return new GetWatchlistItemDetailResponseDto
        {
            Success = true,
            Message = "ok",
            Item = ToSummaryDto(existing),
            DrawdownSeries = existing.DrawdownSeries
                .OrderBy(x => x.Date)
                .Select(x => new DrawdownPointDto
                {
                    Date = x.Date.ToString("yyyy-MM-dd"),
                    Price = x.Price,
                    Drawdown = x.Drawdown
                })
                .ToList()
        };
    }

    private static string? NormalizeCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim();
        if (normalized.Length != 6 || !normalized.All(char.IsDigit))
        {
            return null;
        }

        return normalized;
    }

    private static WatchlistItemSummaryDto ToSummaryDto(WatchedAsset asset)
    {
        return new WatchlistItemSummaryDto
        {
            Code = asset.Code,
            Name = asset.Name,
            SecurityType = asset.SecurityType,
            CurrentPrice = asset.CurrentPrice,
            MaxDrawdown = asset.MaxDrawdown,
            DataWarning = asset.DataWarning,
            LastUpdatedUtc = asset.LastUpdatedUtc.ToString("O")
        };
    }
}
