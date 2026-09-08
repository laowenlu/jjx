using App.Api.Accessors;
using App.Api.Engines;
using App.ServiceInvoker.Interfaces;
using Contracts.Domain.Database;
using Contracts.Domain.Logic;
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

    public async Task<SearchWatchlistCandidatesResponseDto> SearchWatchlistCandidates(SearchWatchlistCandidatesRequestDto request)
    {
        var query = NormalizeCode(request.Query);
        if (query == null)
        {
            return new SearchWatchlistCandidatesResponseDto
            {
                Message = "请输入 6 位代码。"
            };
        }

        var candidates = await _marketDataAccessor.SearchSecurities(query);
        return new SearchWatchlistCandidatesResponseDto
        {
            Message = candidates.Count == 0 ? "没有找到可选标的。" : "ok",
            Items = candidates.Select(ToCandidateDto).ToList()
        };
    }

    public async Task<GetWatchlistResponseDto> GetWatchlist(GetWatchlistRequestDto request)
    {
        var items = await _databaseAccessor.GetAllDocuments<WatchedAsset>();
        return new GetWatchlistResponseDto
        {
            Items = items
                .OrderByDescending(x => x.LastUpdatedUtc)
                .ThenBy(x => x.ThsCode)
                .Select(ToSummaryDto)
                .ToList()
        };
    }

    public async Task<AddWatchlistItemResponseDto> AddWatchlistItem(AddWatchlistItemRequestDto request)
    {
        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode == null || string.IsNullOrWhiteSpace(request.ThsCode) || string.IsNullOrWhiteSpace(request.AssetType))
        {
            return new AddWatchlistItemResponseDto
            {
                Success = false,
                Message = "请先从候选列表中选择标的。"
            };
        }

        var thsCode = request.ThsCode.Trim();
        var existing = await FindWatchedAsset(thsCode);
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

        var securityData = await _marketDataAccessor.GetSecurityData(thsCode, request.AssetType.Trim());
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
            ThsCode = securityData.ThsCode,
            Code = securityData.Code,
            Name = securityData.Name,
            SecurityType = securityData.SecurityType,
            AssetType = securityData.AssetType,
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
        if (string.IsNullOrWhiteSpace(request.ThsCode))
        {
            return new OperationResultDto
            {
                Success = false,
                Message = "未找到该标的。"
            };
        }

        var existing = await FindWatchedAsset(request.ThsCode.Trim());
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
        if (string.IsNullOrWhiteSpace(request.ThsCode))
        {
            return new GetWatchlistItemDetailResponseDto
            {
                Success = false,
                Message = "未找到该标的。"
            };
        }

        var existing = await FindWatchedAsset(request.ThsCode.Trim());
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

    private async Task<WatchedAsset?> FindWatchedAsset(string thsCode)
    {
        return await _databaseAccessor.GetDocumentByProperty<WatchedAsset>(x => x.ThsCode == thsCode || x.Code == thsCode);
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

    private static WatchlistSearchCandidateDto ToCandidateDto(SearchedSecurityCandidate candidate)
    {
        return new WatchlistSearchCandidateDto
        {
            ThsCode = candidate.ThsCode,
            Code = candidate.Ticker,
            Name = candidate.Name,
            AssetType = candidate.AssetType,
            SecurityType = ToSecurityTypeLabel(candidate.AssetType)
        };
    }

    private static WatchlistItemSummaryDto ToSummaryDto(WatchedAsset asset)
    {
        return new WatchlistItemSummaryDto
        {
            ThsCode = string.IsNullOrWhiteSpace(asset.ThsCode) ? asset.Code : asset.ThsCode,
            Code = asset.Code,
            Name = asset.Name,
            SecurityType = asset.SecurityType,
            AssetType = asset.AssetType,
            CurrentPrice = asset.CurrentPrice,
            MaxDrawdown = asset.MaxDrawdown,
            DataWarning = asset.DataWarning,
            LastUpdatedUtc = asset.LastUpdatedUtc.ToString("O")
        };
    }

    private static string ToSecurityTypeLabel(string assetType)
    {
        return assetType switch
        {
            "a-share" => "股票",
            "a-share-index" => "指数",
            "fund-etf" => "ETF",
            "fund-lof" => "LOF",
            "fund-reits" => "REITs",
            "fund-otc" => "基金",
            _ => string.IsNullOrWhiteSpace(assetType) ? "标的" : assetType
        };
    }
}
