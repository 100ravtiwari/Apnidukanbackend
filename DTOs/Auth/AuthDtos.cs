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

// Quick-Commerce Mobile OTP Login / Signup DTOs (Blinkit / Zepto style)
public class SendMobileOtpDto
{
    [Required(ErrorMessage = "Mobile phone number is required")]
    public string Phone { get; set; } = string.Empty;
}

public class SendMobileOtpResponseDto
{
    public string Phone { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public bool IsNewUser { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class VerifyMobileOtpDto
{
    [Required(ErrorMessage = "Mobile phone number is required")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Verification OTP is required")]
    public string Otp { get; set; } = string.Empty;

    public string? Name { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }

    // Delivery Partner Approval Details
    public string? DrivingLicense { get; set; }
    public string? DrivingLicenseUrl { get; set; }
    public string? AadharCardNumber { get; set; }
    public string? AadharCardUrl { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleNumber { get; set; }
    public string ApprovalStatus { get; set; } = "Approved"; // "Approved", "Pending", "Rejected"
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
}

public class RegisterDeliveryPartnerDto
{
    [Required(ErrorMessage = "Full Name is required"), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile phone number is required"), Phone]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP is required for mobile verification")]
    public string Otp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Driving License number is required"), StringLength(100)]
    public string DrivingLicense { get; set; } = string.Empty;

    public string? DrivingLicenseUrl { get; set; }
    public string? AadharCardNumber { get; set; }
    public string? AadharCardUrl { get; set; }

    [Required(ErrorMessage = "Password is required"), MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? VehicleType { get; set; } = "Motorcycle"; // "Motorcycle", "Scooter", "Electric Vehicle", "Bicycle"
    public string? VehicleNumber { get; set; }
    public string? Address { get; set; } = "Champawat Area";
    public string? City { get; set; } = "Champawat";
    public string? Pincode { get; set; } = "262523";
}

public class ApproveRiderDto
{
    public bool IsApproved { get; set; } = true;
    public string? RejectionReason { get; set; }
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

    public string? DrivingLicense { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleNumber { get; set; }
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

    // Rider Verification & Vehicle info
    public string? DrivingLicense { get; set; }
    public string? DrivingLicenseUrl { get; set; }
    public string? AadharCardNumber { get; set; }
    public string? AadharCardUrl { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleNumber { get; set; }
    public string ApprovalStatus { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; }
}
