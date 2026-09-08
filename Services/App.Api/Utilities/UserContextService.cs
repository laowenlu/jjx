namespace App.Api.Utilities;

/// <summary>
/// Tracks and provides the current user's context information during an application request/session.
/// Used for accessing user metadata in managers, engines, and accessors.
/// </summary>
public class UserContextService
{
    /// <summary>
    /// Unique identifier of the authenticated user.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Display name of the authenticated user.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Email address of the authenticated user.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Security roles granted to the authenticated user.
    /// </summary>
    public List<string>? UserRoles { get; set; }

    /// <summary>
    /// Application identifier if applicable for multi-tenancy or scoping needs.
    /// </summary>
    public Guid? AppId { get; set; }
}
