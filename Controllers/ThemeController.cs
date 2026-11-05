using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Theme;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThemeController : ControllerBase
{
    private readonly IThemeService _themeService;

    public ThemeController(IThemeService themeService)
    {
        _themeService = themeService;
    }

    /// <summary>
    /// GET /api/theme/active
    /// Returns the currently active festival theme for Storefront and Mobile Apps.
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveTheme()
    {
        var result = await _themeService.GetActiveThemeAsync();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/theme/all
    /// Returns all preset festival themes for the Store Owner / Admin Suite.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllThemes()
    {
        var result = await _themeService.GetAllThemesAsync();
        return Ok(result);
    }

    /// <summary>
    /// POST /api/theme/activate
    /// Admin switches the active festival theme (e.g. Diwali, Holi, Independence Day, New Year, etc.).
    /// Real-time broadcast pushes the change to all connected clients!
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> ActivateTheme([FromBody] ActivateThemeRequestDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.ThemeId))
        {
            return BadRequest(ApiResponse<bool>.Fail("ThemeId is required."));
        }

        var result = await _themeService.ActivateThemeAsync(dto);
        return Ok(result);
    }
}
