namespace Dukan.API.Models;

public class UserLoginOtp
{
    public int Id { get; set; }
    public string MobileNumber { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}

