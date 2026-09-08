using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Api.Utilities;
using App.ServiceInvoker.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace App.Api.Engines
{
    public class AuthenticatorEngine
    {
        private readonly UserContextService _userContextService;
        private readonly ILogger<AuthenticatorEngine> _logger;

        public AuthenticatorEngine(UserContextService userContextService, ILogger<AuthenticatorEngine> logger)
        {
            _userContextService = userContextService;
            _logger = logger;
        }

        public ClaimsPrincipal? ValidateBearerToken(HttpRequest request, TokenValidationParameters validationParameters)
        {
            var token = ExtractBearerToken(request);
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParameters, out _);
                PopulateUserContext(principal);
                request.HttpContext.User = principal;
                return principal;
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogInformation(ex, "Access token validation failed.");
                return null;
            }
        }

        private static string? ExtractBearerToken(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                return null;
            }

            var authHeader = authHeaderValues.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = authHeader["Bearer ".Length..].Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        private void PopulateUserContext(ClaimsPrincipal principal)
        {
            var userId = principal.Claims.FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Sub
                    or ClaimTypes.NameIdentifier
                    or "sub"
                    or "uid")
                ?.Value;

            var email = principal.Claims.FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Email
                    or ClaimTypes.Email
                    or "email")
                ?.Value;

            _userContextService.UserId = userId;
            _userContextService.UserEmail = email;
            _userContextService.UserRoles = RoleClaimsReader.GetRoles(principal).ToList();
        }
    }
}
