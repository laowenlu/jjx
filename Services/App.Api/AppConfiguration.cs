namespace App.Api;

public class AppConfiguration
{
    public string SupabaseId { get; set; } = string.Empty;
    public string SupabaseHost { get; set; } = string.Empty;
    public string SupabasePort { get; set; } = string.Empty;    
    public string SupabaseDatabasePassword { get; set; } = string.Empty;
    public string SupabasePublishableKey { get; set; } = string.Empty;
    public string SupabaseSecretKey { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
}