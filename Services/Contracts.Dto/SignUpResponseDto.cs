namespace Contracts.Dto
{
    public class SignUpResponseDto
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RequiresFollowUp { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public SessionDto? Session { get; set; }
    }
}
