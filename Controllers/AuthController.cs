using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Auth;
using Dukan.API.DTOs.Common;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto dto)
    {
        var response = await _authService.RegisterAsync(dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    // Public Onboarding for Delivery Partners (Requires Admin Verification)
    [HttpPost("register-delivery-partner")]
    public async Task<ActionResult<ApiResponse<UserDto>>> RegisterDeliveryPartner([FromBody] RegisterDeliveryPartnerDto dto)
    {
        var response = await _authService.RegisterDeliveryPartnerAsync(dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto)
    {
        var response = await _authService.LoginAsync(dto);
        if (!response.Success)
        {
            return Unauthorized(response);
        }
        return Ok(response);
    }

    // Quick-Commerce Blinkit/Zepto Style: Send Mobile OTP
    [HttpPost("send-mobile-otp")]
    public async Task<ActionResult<ApiResponse<SendMobileOtpResponseDto>>> SendMobileOtp([FromBody] SendMobileOtpDto dto)
    {
        var response = await _authService.SendMobileOtpAsync(dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    // Quick-Commerce Blinkit/Zepto Style: Verify Mobile OTP & Direct Login / Register
    [HttpPost("verify-mobile-otp")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> VerifyMobileOtp([FromBody] VerifyMobileOtpDto dto)
    {
        var response = await _authService.VerifyMobileOtpAsync(dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetProfile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Invalid authentication token."));
        }

        var response = await _authService.GetProfileAsync(userId);
        if (!response.Success)
        {
            return NotFound(response);
        }
        return Ok(response);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("Invalid authentication token."));
        }

        var response = await _authService.UpdateProfileAsync(userId, dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
