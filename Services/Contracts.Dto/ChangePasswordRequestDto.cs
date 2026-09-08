namespace Contracts.Dto
{
    public class ChangePasswordRequestDto
    {
        public string NewPassword { get; set; } = string.Empty;
        // Optionally: current/old password for extra verification, but with RequireAuthenticated usually need only new
    }
}