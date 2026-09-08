namespace Contracts.Dto
{
    public class ServiceInvocationRequestDto
    {
        public string ManagerName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public object?[]? Parameters { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}
