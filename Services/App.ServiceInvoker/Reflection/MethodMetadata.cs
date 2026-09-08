using System.Reflection;
using App.ServiceInvoker.Attributes;

namespace App.ServiceInvoker.Reflection
{
    public class MethodMetadata
    {
        public MethodInfo? MethodInfo { get; set; }
        public RequireAuthenticatedAttribute? RequireAuthenticated { get; set; }
        public RequireRoleAttribute? RequireRole { get; set; }
    }
}
