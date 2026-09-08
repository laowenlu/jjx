using System.Threading.Tasks;

namespace App.Api.Accessors
{
    public interface IBlobStorageAccessor
    {
        /// <summary>
        /// Uploads provided binary data to a specified blob container and returns its public URL if successful.
        /// </summary>
        /// <param name="containerName">The storage container name.</param>
        /// <param name="blobName">The name of the blob/file to store.</param>
        /// <param name="fileData">The file's binary data.</param>
        /// <returns>URL of the uploaded file or null on error.</returns>
        Task<string?> UploadFileAsync(string containerName, string blobName, byte[] fileData);
    }
}