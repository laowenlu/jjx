namespace Contracts.Dto
{
    public class SendPasswordResetRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string? RedirectUrl { get; set; } // Optional: where user should be sent after reset
    }
}