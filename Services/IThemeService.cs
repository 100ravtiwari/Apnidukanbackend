using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Theme;

namespace Dukan.API.Services;

public interface IThemeService
{
    Task<ApiResponse<FestivalThemeDto>> GetActiveThemeAsync();
    Task<ApiResponse<List<FestivalThemeDto>>> GetAllThemesAsync();
    Task<ApiResponse<FestivalThemeDto>> ActivateThemeAsync(ActivateThemeRequestDto dto);
}

