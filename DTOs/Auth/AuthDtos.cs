using System.ComponentModel.DataAnnotations;

namespace Dukan.API.DTOs.Auth;

public class RegisterDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string? Role { get; set; }
}

public class LoginDto
{
    [Required(ErrorMessage = "Email or phone number is required")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class UpdateProfileDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
}

public class CreateStaffUserDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = "DeliveryBoy"; // "OrderManager", "DeliveryBoy", "Admin"

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
}

public class DeliveryPartnerStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int ActiveDeliveriesCount { get; set; }
    public bool IsBusy => ActiveDeliveriesCount > 0;
    public string Status => ActiveDeliveriesCount > 0 ? "Busy" : "Available";
}
