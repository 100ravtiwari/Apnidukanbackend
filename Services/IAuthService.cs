using Dukan.API.DTOs.Auth;
using Dukan.API.DTOs.Common;

namespace Dukan.API.Services;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
    Task<ApiResponse<UserDto>> RegisterDeliveryPartnerAsync(RegisterDeliveryPartnerDto dto);
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
    Task<ApiResponse<SendMobileOtpResponseDto>> SendMobileOtpAsync(SendMobileOtpDto dto);
    Task<ApiResponse<AuthResponseDto>> VerifyMobileOtpAsync(VerifyMobileOtpDto dto);
    Task<ApiResponse<UserDto>> GetProfileAsync(int userId);
    Task<ApiResponse<UserDto>> UpdateProfileAsync(int userId, UpdateProfileDto dto);
}
