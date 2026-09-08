namespace Contracts.Dto
{
    /// <summary>
    /// Request envelope for ServiceInvoker streaming calls.
    /// The server will invoke the specified Manager + Method and stream newline-delimited JSON chunks.
    /// </summary>
    public class ServiceStreamingRequestDto
    {
        public string ManagerName { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;
        public object?[]? Parameters { get; init; }
        public string? AccessToken { get; init; }
        public string? RefreshToken { get; init; }
    }
}
