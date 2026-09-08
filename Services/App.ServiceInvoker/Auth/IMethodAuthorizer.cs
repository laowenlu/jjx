using App.ServiceInvoker.Reflection;

namespace App.ServiceInvoker.Auth
{
    public interface IMethodAuthorizer
    {
        /// <summary>
        /// Enforces method-level access using ServiceInvoker method metadata.
        /// Must throw UnauthorizedAccessException for missing auth and ForbiddenAccessException for missing roles.
        /// </summary>
        void Authorize(MethodMetadata methodMetadata);
    }
}
