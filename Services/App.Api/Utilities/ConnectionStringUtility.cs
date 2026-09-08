namespace App.Api.Utilities;

/// <summary>
/// Utility class for constructing and retrieving formatted database connection strings for the application.
/// </summary>
public static class ConnectionStringUtility
{
    /// <summary>
    /// Constructs a PostgreSQL connection string from the application's configuration settings.
    /// </summary>
    /// <param name="config">Application configuration containing DB credentials and endpoint information.</param>
    /// <returns>Formatted connection string for database use.</returns>
    public static string GetDatabaseConnectionString(AppConfiguration config)
    {
        string connectionString =
            $"Host={config.SupabaseHost};Port={config.SupabasePort};Database=postgres;Username=postgres.{config.SupabaseId};Password={config.SupabaseDatabasePassword}";
        return connectionString;
    }
}
