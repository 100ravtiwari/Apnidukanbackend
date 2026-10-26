using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Dukan.API.Data;
using Dukan.API.DTOs.Auth;
using Dukan.API.DTOs.Common;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());

        if (existingUser)
        {
            return ApiResponse<AuthResponseDto>.Fail("A user with this email address already exists.");
        }

        var allowedRoles = new[] { "Customer", "DeliveryBoy", "OrderManager" };
        var role = !string.IsNullOrWhiteSpace(dto.Role) && allowedRoles.Contains(dto.Role.Trim(), StringComparer.OrdinalIgnoreCase)
            ? allowedRoles.First(r => r.Equals(dto.Role.Trim(), StringComparison.OrdinalIgnoreCase))
            : "Customer";

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
            City = dto.City?.Trim(),
            Pincode = dto.Pincode?.Trim(),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var (token, expiresAt) = GenerateJwtToken(user);

        var response = new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToUserDto(user)
        };

        return ApiResponse<AuthResponseDto>.Ok(response, "Registration successful!");
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
    {
        var input = dto.Email.Trim();
        var cleanInput = input.ToLower();
        var phoneDigits = new string(input.Where(char.IsDigit).ToArray());

        var users = await _context.Users.ToListAsync();
        var user = users.FirstOrDefault(u =>
            u.Email.Equals(cleanInput, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(u.Phone) && (
                u.Phone.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                (phoneDigits.Length >= 10 && new string(u.Phone.Where(char.IsDigit).ToArray()).EndsWith(phoneDigits.Length >= 10 ? phoneDigits[^10..] : phoneDigits))
            ))
        );

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Invalid email/phone or password.");
        }

        var (token, expiresAt) = GenerateJwtToken(user);

        var response = new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToUserDto(user)
        };

        return ApiResponse<AuthResponseDto>.Ok(response, "Login successful!");
    }

    public async Task<ApiResponse<UserDto>> GetProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("User not found.");
        }

        return ApiResponse<UserDto>.Ok(MapToUserDto(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateProfileAsync(int userId, UpdateProfileDto dto)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("User not found.");
        }

        user.Name = dto.Name.Trim();
        user.Phone = dto.Phone?.Trim();
        user.Address = dto.Address?.Trim();
        user.City = dto.City?.Trim();
        user.Pincode = dto.Pincode?.Trim();

        await _context.SaveChangesAsync();

        return ApiResponse<UserDto>.Ok(MapToUserDto(user), "Profile updated successfully!");
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(User user)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKey = jwtSettings["Key"] ?? "Dukan_Super_Secret_Fallback_Key_1234567890!";
        var issuer = jwtSettings["Issuer"] ?? "DukanAPI";
        var audience = jwtSettings["Audience"] ?? "DukanClient";
        var expiryDays = int.TryParse(jwtSettings["ExpiryDays"], out var days) ? days : 7;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Address = user.Address,
            City = user.City,
            Pincode = user.Pincode,
            Role = user.Role
        };
    }
}
