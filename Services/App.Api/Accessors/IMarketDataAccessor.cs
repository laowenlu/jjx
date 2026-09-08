using Contracts.Domain.Logic;

namespace App.Api.Accessors;

public interface IMarketDataAccessor
{
    Task<ResolvedSecurityData?> GetSecurityData(string code);
}
