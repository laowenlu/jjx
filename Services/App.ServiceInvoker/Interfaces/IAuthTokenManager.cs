using Microsoft.AspNetCore.Http;

namespace App.ServiceInvoker.Interfaces
{
    public interface IAuthTokenManager
    {
        Task<AuthTokenSet?> EnsureAuthenticatedRequest(HttpRequest request, string? accessToken, string? refreshToken);
    }
}
