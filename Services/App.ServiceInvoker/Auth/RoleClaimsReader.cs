using System.Security.Claims;
using System.Text.Json;

namespace App.ServiceInvoker.Auth
{
    public static class RoleClaimsReader
    {
        public static HashSet<string> GetRoles(ClaimsPrincipal principal)
        {
            var appMeta = principal.Claims.FirstOrDefault(c => c.Type == "app_metadata")?.Value;
            if (!string.IsNullOrWhiteSpace(appMeta))
            {
                try
                {
                    using var doc = JsonDocument.Parse(appMeta);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesEl) && rolesEl.ValueKind == JsonValueKind.Array)
                    {
                        return rolesEl.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (JsonException)
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }

            return principal.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
