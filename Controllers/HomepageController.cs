using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Homepage;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HomepageController : ControllerBase
{
    private readonly IHomepageService _homepageService;
    private readonly IRedisPersonaService _redisPersonaService;

    public HomepageController(IHomepageService homepageService, IRedisPersonaService redisPersonaService)
    {
        _homepageService = homepageService;
        _redisPersonaService = redisPersonaService;
    }

    /// <summary>
    /// GET /api/homepage?userId=123
    /// Returns dynamic hero banner and personalized product recommendations based on Redis persona scoring.
    /// If user is not provided, extracts user ID from JWT token or falls back to default trending products.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHomepage([FromQuery] string? userId)
    {
        var effectiveUserId = !string.IsNullOrWhiteSpace(userId)
            ? userId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var result = await _homepageService.GetPersonalizedHomepageAsync(effectiveUserId);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/homepage/track-action
    /// Tracks user category interactions (view, cart, purchase) and updates Redis Sorted Set scores.
    /// </summary>
    [HttpPost("track-action")]
    public async Task<IActionResult> TrackUserAction([FromBody] TrackUserActionDto dto)
    {
        var effectiveUserId = !string.IsNullOrWhiteSpace(dto.UserId)
            ? dto.UserId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            return BadRequest(ApiResponse<bool>.Fail("UserId is required to track persona"));

        if (string.IsNullOrWhiteSpace(dto.Category))
            return BadRequest(ApiResponse<bool>.Fail("Category cannot be empty"));

        double increment = dto.CustomIncrement ?? (dto.Action?.ToLowerInvariant() switch
        {
            "purchase" => 5.0,
            "cart" => 3.0,
            "view" or _ => 1.0
        });

        await _redisPersonaService.TrackUserActionAsync(effectiveUserId, dto.Category, increment);

        return Ok(ApiResponse<bool>.Ok(true, $"Tracked {dto.Action} for category '{dto.Category}' (+{increment} pts)."));
    }

    /// <summary>
    /// GET /api/homepage/persona-scores?userId=123
    /// Inspects the Redis category scoring leaderboard for a user.
    /// </summary>
    [HttpGet("persona-scores")]
    public async Task<IActionResult> GetPersonaScores([FromQuery] string? userId)
    {
        var effectiveUserId = !string.IsNullOrWhiteSpace(userId)
            ? userId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(effectiveUserId))
            return BadRequest(ApiResponse<bool>.Fail("UserId is required"));

        var scores = await _redisPersonaService.GetAllCategoryScoresAsync(effectiveUserId);
        return Ok(ApiResponse<List<(string Category, double Score)>>.Ok(scores));
    }
}
