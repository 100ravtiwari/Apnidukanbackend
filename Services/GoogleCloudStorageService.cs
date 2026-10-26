using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Dukan.API.DTOs.Common;

namespace Dukan.API.Services;

public class GoogleCloudStorageService : IStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCloudStorageService> _logger;
    private readonly StorageClient _storageClient;
    private readonly string _bucketName;

    public GoogleCloudStorageService(
        IConfiguration configuration,
        ILogger<GoogleCloudStorageService> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _bucketName = _configuration["GoogleCloudStorage:BucketName"] ?? "dukan-s3";

        // Find credential file: check configured path, backend/gcpkey.json, or ../gcpkey.json
        string keyPath = _configuration["GoogleCloudStorage:CredentialFilePath"] ?? "gcpkey.json";
        if (!Path.IsPathRooted(keyPath))
        {
            string candidate1 = Path.Combine(environment.ContentRootPath, keyPath);
            string candidate2 = Path.Combine(Directory.GetParent(environment.ContentRootPath)?.FullName ?? "", keyPath);
            if (File.Exists(candidate1))
            {
                keyPath = candidate1;
            }
            else if (File.Exists(candidate2))
            {
                keyPath = candidate2;
            }
        }

        if (File.Exists(keyPath))
        {
            using var keyStream = File.OpenRead(keyPath);
            var credential = GoogleCredential.FromStream(keyStream);
            _storageClient = StorageClient.Create(credential);
            _logger.LogInformation("Google Cloud Storage client initialized with key from: {KeyPath}", keyPath);
        }
        else
        {
            _logger.LogWarning("GCP Key file not found at {KeyPath}. Falling back to default application credentials.", keyPath);
            _storageClient = StorageClient.Create();
        }
    }

    public async Task<ApiResponse<string>> UploadImageAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            // Generate clean unique file name
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            string safeBaseName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "-").ToLowerInvariant();
            string uniqueObjectName = $"products/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}_{safeBaseName}{ext}";

            // Upload object to GCP bucket
            var uploadedObject = await _storageClient.UploadObjectAsync(
                bucket: _bucketName,
                objectName: uniqueObjectName,
                contentType: contentType,
                source: fileStream
            );

            // Internal bucket path served via backend streaming endpoint
            // This ensures reliable display regardless of GCP bucket public permissions,
            // while keeping GCP credentials, project ID, and bucket private from end-users.
            string servedUrl = $"/api/upload/file/{uniqueObjectName}";

            _logger.LogInformation("Image successfully uploaded to GCP Bucket {Bucket} as {ObjectName}. Served at {Url}",
                _bucketName, uniqueObjectName, servedUrl);
            return ApiResponse<string>.Ok(servedUrl, "Image uploaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image to Google Cloud Storage");
            return ApiResponse<string>.Fail($"Storage Upload Failed: {ex.Message}");
        }
    }

    public async Task<(Stream Stream, string ContentType)?> GetImageAsync(string objectName)
    {
        try
        {
            // Clean leading slashes
            string cleanName = objectName.TrimStart('/');

            var memoryStream = new MemoryStream();
            var obj = await _storageClient.DownloadObjectAsync(_bucketName, cleanName, memoryStream);
            memoryStream.Position = 0;

            string contentType = !string.IsNullOrEmpty(obj?.ContentType) 
                ? obj.ContentType 
                : GetContentType(cleanName);

            return (memoryStream, contentType);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Object not found in GCP bucket {Bucket}: {ObjectName}", _bucketName, objectName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching object {ObjectName} from GCP bucket {Bucket}", objectName, _bucketName);
            return null;
        }
    }

    private static string GetContentType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}
