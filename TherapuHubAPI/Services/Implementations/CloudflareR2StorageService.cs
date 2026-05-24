using Amazon.S3;
using Amazon.S3.Model;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class CloudflareR2StorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public CloudflareR2StorageService(IConfiguration configuration)
    {
        var accountId = configuration["CloudflareR2:AccountId"]
            ?? throw new InvalidOperationException("CloudflareR2:AccountId is not configured.");
        var accessKey = configuration["CloudflareR2:AccessKeyId"]
            ?? throw new InvalidOperationException("CloudflareR2:AccessKeyId is not configured.");
        var secretKey = configuration["CloudflareR2:SecretAccessKey"]
            ?? throw new InvalidOperationException("CloudflareR2:SecretAccessKey is not configured.");

        _bucketName = configuration["CloudflareR2:BucketName"]
            ?? throw new InvalidOperationException("CloudflareR2:BucketName is not configured.");

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> SaveFileAsync(IFormFile file, string containerPath)
    {
        var safeName = Path.GetFileNameWithoutExtension(file.FileName);
        var ext = Path.GetExtension(file.FileName);
        var uniqueName = $"{safeName}_{Guid.NewGuid():N}{ext}";
        var key = $"{containerPath}/{uniqueName}".Replace("\\", "/");

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = ResolveContentType(file.FileName),
            DisablePayloadSigning = true
        };

        await _s3Client.PutObjectAsync(request);
        return key;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string blobPath)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = blobPath
        };

        var response = await _s3Client.GetObjectAsync(request);
        var contentType = response.Headers.ContentType ?? ResolveContentType(blobPath);
        var fileName = Path.GetFileName(blobPath);

        return (response.ResponseStream, contentType, fileName);
    }

    public async Task<long> GetFileSizeAsync(string blobPath)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = blobPath
            };
            var response = await _s3Client.GetObjectMetadataAsync(request);
            return response.ContentLength;
        }
        catch
        {
            return 0L;
        }
    }

    public async Task DeleteFileAsync(string blobPath)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = blobPath
        };
        await _s3Client.DeleteObjectAsync(request);
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
