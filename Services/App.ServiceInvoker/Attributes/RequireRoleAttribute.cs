namespace App.ServiceInvoker.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequireRoleAttribute : Attribute
    {
        public string[] Roles { get; }
        public RequireRoleAttribute(params string[] roles)
        {
            Roles = roles;
        }
    }
}