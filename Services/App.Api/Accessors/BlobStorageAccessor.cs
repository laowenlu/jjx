namespace App.Api.Accessors
{
    /// <summary>
    /// Provides basic access and helper methods for file blob storage in Supabase Storage.
    /// Responsible for file uploads and file type mapping for the blobs.
    /// </summary>
    public class BlobStorageAccessor : IBlobStorageAccessor
    {
        private readonly ILogger<BlobStorageAccessor> _logger;
        private readonly Supabase.Client _supabaseClient;
        private readonly AppConfiguration _config;
        private readonly string _supabaseUrl;

        /// <summary>
        /// Constructs the BlobStorageAccessor and initializes the Supabase client.
        /// </summary>
        public BlobStorageAccessor(AppConfiguration config, ILogger<BlobStorageAccessor> logger)
        {
            _logger = logger;
            _supabaseUrl = $"https://{config.SupabaseId}.supabase.co";
            // Privileged client: uses secret key via apikey header.
            _supabaseClient = new Supabase.Client(
                _supabaseUrl,
                null,
                new Supabase.SupabaseOptions
                {
                    Headers = new Dictionary<string, string>
                    {
                        { "apikey", config.SupabaseSecretKey }
                    }
                });
            _config = config;
        }

        /// <summary>
        /// Uploads provided binary data to a specified blob container and returns its public URL if successful.
        /// </summary>
        /// <param name="containerName">The storage container name.</param>
        /// <param name="blobName">The name of the blob/file to store.</param>
        /// <param name="fileData">The file's binary data.</param>
        /// <returns>URL of the uploaded file or null on error.</returns>
        public async Task<string?> UploadFileAsync(string containerName, string blobName, byte[] fileData)
        {
            try
            {
                var response = await _supabaseClient.Storage.From(containerName).Upload(fileData, blobName, new Supabase.Storage.FileOptions { CacheControl = "3600", Upsert = true });

                // Construct the full URL
                var fullUrl = $"{_supabaseUrl}/storage/v1/object/public/{response}";

                return fullUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to Supabase Storage.");
                return null;
            }
        }

        /// <summary>
        /// Helper to determine a file extension based on its MIME content type string.
        /// </summary>
        private string GetFileExtension(string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => "jpeg",
                "image/png" => "png",
                "image/gif" => "gif",
                _ => "bin"
            };
        }
    }
}
