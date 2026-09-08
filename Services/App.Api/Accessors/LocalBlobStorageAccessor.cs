using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using App.Api;
using App.Api.Utilities;

namespace App.Api.Accessors
{
    public class LocalBlobStorageAccessor : IBlobStorageAccessor
    {
        private readonly string _rootDirectory;
        private readonly ILogger<LocalBlobStorageAccessor> _logger;

        public LocalBlobStorageAccessor(AppConfiguration config, ILogger<LocalBlobStorageAccessor> logger)
        {
            _rootDirectory = Path.Combine(LocalPathHelper.GetAppResourcePath(config.AppName), "blobs");
            _logger = logger;
            Directory.CreateDirectory(_rootDirectory);
        }

        public Task<string?> UploadFileAsync(string containerName, string blobName, byte[] fileData)
        {
            try
            {
                string containerPath = Path.Combine(_rootDirectory, containerName);
                Directory.CreateDirectory(containerPath);

                string safeBlobName = Path.GetFileName(blobName);
                string filePath = Path.Combine(containerPath, safeBlobName);

                File.WriteAllBytes(filePath, fileData);

                // Emit an AbsoluteUri that works on Windows and *nix (file:///C:/... vs file:///Users/...).
                var fileUri = new Uri(Path.GetFullPath(filePath));
                return Task.FromResult<string?>(fileUri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to local file system blob storage.");
                return Task.FromResult<string?>(null);
            }
        }
    }
}
