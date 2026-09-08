namespace Contracts.Dto
{
    public class LoginResponseDto
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public SessionDto? Session { get; set; }
    }
}
