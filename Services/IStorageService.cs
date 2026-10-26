using Dukan.API.DTOs.Common;

namespace Dukan.API.Services;

public interface IStorageService
{
    Task<ApiResponse<string>> UploadImageAsync(Stream fileStream, string fileName, string contentType);
    Task<(Stream Stream, string ContentType)?> GetImageAsync(string objectName);
}
