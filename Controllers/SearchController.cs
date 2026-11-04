using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Homepage;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IRedisPersonaService _redisPersonaService;

    public SearchController(IRedisPersonaService redisPersonaService)
    {
        _redisPersonaService = redisPersonaService;
    }

    /// <summary>
    /// POST /api/search/save
    /// Saves a search query to Redis List (keeps last 5 deduplicated searches).
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveSearch([FromBody] SaveSearchQueryDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Query))
        {
            return BadRequest(ApiResponse<bool>.Fail("Query cannot be empty."));
        }

        var effectiveUserId = !string.IsNullOrWhiteSpace(dto.UserId)
            ? dto.UserId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "guest";

        await _redisPersonaService.SaveSearchQueryAsync(effectiveUserId, dto.Query);
        return Ok(ApiResponse<bool>.Ok(true, "Search query saved to history."));
    }

    /// <summary>
    /// GET /api/search/recent?userId=123
    /// Retrieves user's last 5 searches from Redis List.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentSearches([FromQuery] string? userId)
    {
        var effectiveUserId = !string.IsNullOrWhiteSpace(userId)
            ? userId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "guest";

        var list = await _redisPersonaService.GetRecentSearchesAsync(effectiveUserId, 5);

        return Ok(ApiResponse<RecentSearchesResponseDto>.Ok(new RecentSearchesResponseDto
        {
            UserId = effectiveUserId,
            Searches = list
        }));
    }

    /// <summary>
    /// DELETE /api/search/clear?userId=123
    /// Clears user's search history from Redis.
    /// </summary>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearRecentSearches([FromQuery] string? userId)
    {
        var effectiveUserId = !string.IsNullOrWhiteSpace(userId)
            ? userId
            : User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "guest";

        await _redisPersonaService.ClearRecentSearchesAsync(effectiveUserId);
        return Ok(ApiResponse<bool>.Ok(true, "Search history cleared."));
    }
}
