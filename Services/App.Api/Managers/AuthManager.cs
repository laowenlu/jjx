using System.Text.Json;
using App.Api.Accessors;
using App.Api.Engines;
using App.Api.Utilities;
using App.ServiceInvoker.Attributes;
using App.ServiceInvoker.Interfaces;
using Contracts.Dto;
using Microsoft.AspNetCore.Http;

namespace App.Api.Managers
{
    public class AuthManager : IManagerService, IAuthTokenManager
    {
        private const int ExpiryBufferSeconds = 60;
        private readonly IAuthAccessor _authAccessor;
        private readonly AuthenticatorEngine _authenticatorEngine;
        private readonly UserContextService _userContextService;

        public AuthManager(IAuthAccessor authAccessor, AuthenticatorEngine authenticatorEngine, UserContextService userContextService)
        {
            _authAccessor = authAccessor;
            _authenticatorEngine = authenticatorEngine;
            _userContextService = userContextService;
        }

        public async Task<LoginResponseDto> Login(LoginRequestDto request)
        {
            var (user, access, refresh, error) = await _authAccessor.Login(request.Email, request.Password);
            return new LoginResponseDto
            {
                IsSuccess = user != null && !string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(refresh),
                ErrorMessage = error,
                AccessToken = access,
                RefreshToken = refresh,
                Session = user == null ? null : ToSession(user)
            };
        }

        public async Task<SignUpResponseDto> SignUp(SignUpRequestDto request)
        {
            var (user, access, refresh, error) = await _authAccessor.SignUp(request.Email, request.Password, request.Name);
            return new SignUpResponseDto
            {
                IsSuccess = user != null,
                ErrorMessage = user != null && (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh)) ? "Account created. Complete the provider's follow-up step, then sign in." : error,
                RequiresFollowUp = user != null && (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh)),
                AccessToken = access,
                RefreshToken = refresh,
                Session = user == null ? null : ToSession(user)
            };
        }

        public async Task<OperationResultDto> SendPasswordResetEmail(SendPasswordResetRequestDto request)
        {
            var result = await _authAccessor.SendPasswordResetEmail(request.Email, request.RedirectUrl ?? string.Empty);
            return new OperationResultDto
            {
                Success = result,
                Message = result ? "Password reset email sent." : "Failed to send password reset email."
            };
        }

        [RequireAuthenticated]
        public Task<SessionDto> GetSession(GetSessionRequestDto request)
        {
            return Task.FromResult(new SessionDto
            {
                UserId = _userContextService.UserId,
                Email = _userContextService.UserEmail,
                Roles = _userContextService.UserRoles ?? new List<string>()
            });
        }

        [RequireAuthenticated]
        public async Task<OperationResultDto> ChangePassword(ChangePasswordRequestDto request)
        {
            var userId = _userContextService.UserId;
            var result = !string.IsNullOrWhiteSpace(userId) && await _authAccessor.UpdatePassword(userId, request.NewPassword);
            return new OperationResultDto
            {
                Success = result,
                Message = result ? "Password change successful." : "Failed to change password."
            };
        }

        [RequireAuthenticated]
        public async Task<UpdateUserEmailResponseDto> UpdateUserEmail(UpdateUserEmailDto request)
        {
            var userId = _userContextService.UserId;
            var success = !string.IsNullOrWhiteSpace(userId) && await _authAccessor.UpdateEmail(userId, request.NewEmail);
            return new UpdateUserEmailResponseDto { Success = success, Message = success ? "Email updated successfully." : "Failed to update email." };
        }

        [RequireAuthenticated]
        public async Task<UpdateUserPasswordResponseDto> UpdateUserPassword(UpdateUserPasswordDto request)
        {
            var userId = _userContextService.UserId;
            var success = !string.IsNullOrWhiteSpace(userId) && await _authAccessor.UpdatePassword(userId, request.NewPassword);
            return new UpdateUserPasswordResponseDto { Success = success, Message = success ? "Password updated successfully." : "Failed to update password." };
        }

        [RequireAuthenticated]
        public async Task<UpdateUserNameResponseDto> UpdateUserName(UpdateUserNameDto request)
        {
            var userId = _userContextService.UserId;
            var success = !string.IsNullOrWhiteSpace(userId) && await _authAccessor.UpdateUserName(userId, request.NewName);
            return new UpdateUserNameResponseDto { Success = success, Message = success ? "Name updated successfully." : "Failed to update name." };
        }

        async Task<AuthTokenSet?> IAuthTokenManager.EnsureAuthenticatedRequest(HttpRequest request, string? accessToken, string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            long? exp;
            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length < 2) return null;

                var payload = parts[1];
                var base64 = payload.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');

                var bytes = Convert.FromBase64String(base64);
                using var doc = JsonDocument.Parse(bytes);

                if (!doc.RootElement.TryGetProperty("exp", out var expEl))
                {
                    return null;
                }

                if (expEl.ValueKind == JsonValueKind.Number && expEl.TryGetInt64(out var numericExp))
                {
                    exp = numericExp;
                }
                else if (expEl.ValueKind == JsonValueKind.String && long.TryParse(expEl.GetString(), out var stringExp))
                {
                    exp = stringExp;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            AuthTokenSet tokens;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (exp.Value > now + ExpiryBufferSeconds)
            {
                tokens = new AuthTokenSet { AccessToken = accessToken, RefreshToken = refreshToken };
            }
            else
            {
                var (user, refreshedAccessToken, refreshedRefreshToken, _) = await _authAccessor.RefreshToken(accessToken, refreshToken);
                if (user == null || string.IsNullOrWhiteSpace(refreshedAccessToken) || string.IsNullOrWhiteSpace(refreshedRefreshToken)) return null;

                tokens = new AuthTokenSet { AccessToken = refreshedAccessToken, RefreshToken = refreshedRefreshToken };
            }

            request.Headers.Authorization = $"Bearer {tokens.AccessToken}";

            var validationParameters = await _authAccessor.GetAccessTokenValidationParameters();
            var principal = _authenticatorEngine.ValidateBearerToken(request, validationParameters);
            return principal == null ? null : tokens;
        }

        private static SessionDto ToSession(Contracts.Domain.Database.User user)
        {
            return new SessionDto
            {
                UserId = user.AuthId,
                Email = user.Email,
                Roles = user.Roles?.ToList() ?? new List<string>()
            };
        }
    }
}
