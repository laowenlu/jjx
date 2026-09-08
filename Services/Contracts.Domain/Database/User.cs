namespace Contracts.Domain.Database;

/// <summary>
/// Represents a system user entity storing identity and role information in the database.
/// Used for mapping user identity, authorization, and account management data.
/// </summary>
public class User
{
    /// <summary>
    /// Email address associated with the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// User's full display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The user's authentication identifier (Supabase AuthId or equivalent external ID).
    /// </summary>
    public string AuthId { get; set; } = string.Empty;

    /// <summary>
    /// The list of security roles assigned to the user for permission management.
    /// </summary>
    public string[] Roles { get; set; } = Array.Empty<string>();
}
