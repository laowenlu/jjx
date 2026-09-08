namespace App.Api.Utilities.Roles
{
    /// <summary>
    /// Centralizes role definitions and provides an authoritative list of valid system roles for role-based access control.
    /// Use AllRoles for validation/selection within access control logic.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Represents administrative users with the highest level of system permissions.
        /// </summary>
        public const string Admin = "admin";
        /// <summary>
        /// Represents a regular user with standard permissions.
        /// </summary>
        public const string User = "user";
        /// <summary>
        /// Represents any authenticated user account for feature gating.
        /// </summary>
        public const string Authenticated = "authenticated";
        // Add other roles as necessary

        /// <summary>
        /// Aggregate array containing all defined roles, suitable for role validation/enumeration.
        /// </summary>
        public static readonly string[] AllRoles = { Admin, User, Authenticated }; // Use this array for validation
    }
}
