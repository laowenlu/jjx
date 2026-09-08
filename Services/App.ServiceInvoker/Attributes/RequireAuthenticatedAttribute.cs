namespace App.ServiceInvoker.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequireAuthenticatedAttribute : Attribute
    {
    }
}