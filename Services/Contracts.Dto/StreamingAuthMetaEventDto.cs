namespace Contracts.Dto
{
    public class StreamingAuthMetaEventDto
    {
        public string Type { get; set; } = "auth";
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public SessionDto? Session { get; set; }
    }
}
