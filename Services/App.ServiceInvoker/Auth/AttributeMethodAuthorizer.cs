using App.ServiceInvoker.Reflection;
using Microsoft.AspNetCore.Http;

namespace App.ServiceInvoker.Auth
{
    public class AttributeMethodAuthorizer : IMethodAuthorizer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AttributeMethodAuthorizer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Authorize(MethodMetadata methodMetadata)
        {
            var context = _httpContextAccessor.HttpContext;
            var user = context?.User;

            var requiresAuth = methodMetadata.RequireAuthenticated != null || methodMetadata.RequireRole != null;
            if (!requiresAuth)
            {
                return;
            }

            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("Authentication required.");
            }

            var roleAttr = methodMetadata.RequireRole;
            if (roleAttr == null || roleAttr.Roles.Length == 0)
            {
                return;
            }

            var roles = RoleClaimsReader.GetRoles(user);
            if (!roleAttr.Roles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                throw new ForbiddenAccessException("Forbidden.");
            }
        }
    }
}
