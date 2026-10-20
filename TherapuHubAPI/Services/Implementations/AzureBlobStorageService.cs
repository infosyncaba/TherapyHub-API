using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

/// <summary>
/// Azure Blob Storage implementation. Enabled with Storage:Provider = "Azure".
/// Authenticates with the App Service managed identity (Storage:AccountUrl),
/// or with Storage:ConnectionString for local development.
/// Blob keys keep the same format as R2, so migrated paths stay valid.
/// </summary>
public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var containerName = configuration["Storage:Container"]
            ?? throw new InvalidOperationException("Storage:Container is not configured.");
        var connectionString = configuration["Storage:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new BlobContainerClient(connectionString, containerName);
        }
        else
        {
            var accountUrl = configuration["Storage:AccountUrl"]
                ?? throw new InvalidOperationException("Storage:AccountUrl is not configured.");
            _container = new BlobContainerClient(
                new Uri($"{accountUrl.TrimEnd('/')}/{containerName}"),
                new DefaultAzureCredential());
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, string containerPath)
    {
        var safeName = Path.GetFileNameWithoutExtension(file.FileName);
        var ext = Path.GetExtension(file.FileName);
        var uniqueName = $"{safeName}_{Guid.NewGuid():N}{ext}";
        var key = $"{containerPath}/{uniqueName}".Replace("\\", "/");

        await using var stream = file.OpenReadStream();

        await _container.GetBlobClient(key).UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = ResolveContentType(file.FileName) }
        });
        return key;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string blobPath)
    {
        try
        {
            var response = await _container.GetBlobClient(blobPath).DownloadStreamingAsync();
            var contentType = response.Value.Details.ContentType ?? ResolveContentType(blobPath);
            var fileName = Path.GetFileName(blobPath);

            return (response.Value.Content, contentType, fileName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"File not found: {blobPath}", ex);
        }
    }

    public async Task<long> GetFileSizeAsync(string blobPath)
    {
        try
        {
            var properties = await _container.GetBlobClient(blobPath).GetPropertiesAsync();
            return properties.Value.ContentLength;
        }
        catch
        {
            return 0L;
        }
    }

    public async Task DeleteFileAsync(string blobPath)
    {
        await _container.GetBlobClient(blobPath).DeleteIfExistsAsync();
    }

    public string ResolveContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            ".doc"  => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls"  => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt"  => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt"  => "text/plain",
            ".csv"  => "text/csv",
            ".zip"  => "application/zip",
            _       => "application/octet-stream"
        };
    }
}
