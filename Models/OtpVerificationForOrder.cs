namespace Dukan.API.Models;

public class OtpVerificationForOrder
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public string? MobileNumber { get; set; }
    public string Otp { get; set; } = string.Empty;
    public int OtpTypeId { get; set; } = 1; // 1: Delivery Handover, 2: Cancellation, 3: Return

    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByRiderId { get; set; }
}
