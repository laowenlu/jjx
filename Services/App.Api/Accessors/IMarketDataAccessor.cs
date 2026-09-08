using Contracts.Domain.Logic;

namespace App.Api.Accessors;

public interface IMarketDataAccessor
{
    Task<List<SearchedSecurityCandidate>> SearchSecurities(string query);

    Task<ResolvedSecurityData?> GetSecurityData(string thsCode, string assetType);
}
