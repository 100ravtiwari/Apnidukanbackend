using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Homepage;

namespace Dukan.API.Services;

public interface IHomepageService
{
    Task<ApiResponse<HomepageResponseDto>> GetPersonalizedHomepageAsync(string? userId);
}

