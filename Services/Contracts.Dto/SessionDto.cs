namespace Contracts.Dto
{
    public class SessionDto
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
