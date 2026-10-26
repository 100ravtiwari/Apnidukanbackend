using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<UploadController> _logger;

    public UploadController(IStorageService storageService, ILogger<UploadController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload product photo from gallery/device directly to Google Cloud Storage
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("image")]
    [RequestSizeLimit(10_485_760)] // 10 MB limit
    public async Task<ActionResult<ApiResponse<UploadResponseDto>>> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadResponseDto>.Fail("No file was selected for upload."));
        }

        // Validate allowed image extensions
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<UploadResponseDto>.Fail(
                $"Invalid image format '{ext}'. Allowed formats: JPG, JPEG, PNG, WEBP, GIF."));
        }

        // Limit size to 10MB
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<UploadResponseDto>.Fail("Image file size exceeds the 10MB limit."));
        }

        using var stream = file.OpenReadStream();
        string contentType = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;

        var result = await _storageService.UploadImageAsync(stream, file.FileName, contentType);

        if (!result.Success || string.IsNullOrEmpty(result.Data))
        {
            return StatusCode(500, ApiResponse<UploadResponseDto>.Fail(
                result.Message ?? "Failed to upload image to Google Cloud Storage."));
        }

        return Ok(ApiResponse<UploadResponseDto>.Ok(new UploadResponseDto
        {
            Url = result.Data,
            FileName = file.FileName,
            Size = file.Length
        }, "Photo uploaded successfully!"));
    }

    /// <summary>
    /// Stream image file securely from cloud storage with HTTP caching
    /// </summary>
    [AllowAnonymous]
    [HttpGet("file/{**objectName}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetFile(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return BadRequest();
        }

        var fileResult = await _storageService.GetImageAsync(objectName);
        if (fileResult == null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        return File(fileResult.Value.Stream, fileResult.Value.ContentType);
    }

    /// <summary>
    /// Fallback proxy for any existing full URLs
    /// </summary>
    [AllowAnonymous]
    [HttpGet("proxy")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ProxyImage([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest();

        if (url.Contains("dukan-s3/"))
        {
            string objectPath = url.Substring(url.IndexOf("dukan-s3/") + "dukan-s3/".Length);
            return await GetFile(objectPath);
        }

        return NotFound();
    }
}

public class UploadResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}
