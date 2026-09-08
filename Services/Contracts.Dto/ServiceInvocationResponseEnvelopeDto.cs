namespace Contracts.Dto
{
    public class ServiceInvocationResponseEnvelopeDto
    {
        public object? Result { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public SessionDto? Session { get; set; }
    }
}
