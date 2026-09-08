namespace App.Api.Utilities
{
    public static class LocalPathHelper
    {
        public static string GetAppResourcePath(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("App name must not be null or empty.", nameof(appName));

            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string resourcePath = Path.Combine(userFolder, "CodeBuddy", "Resources", appName);

            return resourcePath;
        }
    }
}
