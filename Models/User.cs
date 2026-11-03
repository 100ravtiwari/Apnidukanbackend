namespace Dukan.API.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    // Roles: "Admin", "Customer", "OrderManager", "DeliveryBoy"
    public string Role { get; set; } = "Customer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Delivery Partner Onboarding & Approval Fields
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

    // Navigation properties
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
