using Contracts.Domain.Database;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace App.Api.Accessors
{
    public interface IAuthAccessor
    {
        Task<(string, string)> GetInviteActionLink(User user, string redirectUrl);
        Task<bool> DeleteUser(string userId);
        Task<List<User>> GetUsersAsync();
        Task<User?> GetUserById(string userId);
        Task<bool> UpdateEmail(string userId, string newEmail);
        Task<bool> UpdatePassword(string userId, string newPassword);
        Task<bool> UpdateUserName(string userId, string newName);
        Task<bool> AddRolesToUserAsync(string userId, string[] rolesToAdd);
        Task<bool> RemoveRolesFromUserAsync(string userId, string[] rolesToRemove);
        Task<(User? user, string? jwt, string? refreshToken, string error)> Login(string email, string password);
        Task<(User? user, string? jwt, string? refreshToken, string error)> SignUp(string email, string password, string? name);
        Task<bool> SendPasswordResetEmail(string email, string redirectUrl);
        Task<(User? user, string? jwt, string? refreshToken, string error)> RefreshToken(string accessToken, string refreshToken);
        Task<TokenValidationParameters> GetAccessTokenValidationParameters();
    }
}
