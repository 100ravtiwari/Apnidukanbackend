namespace Dukan.API.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    // Status: Pending, Confirmed, Packed, OutForDelivery, Delivered, Cancelled
    public string Status { get; set; } = "Pending";

    // Payment: COD, UPI, Card, NetBanking
    public string PaymentMethod { get; set; } = "COD";

    // PaymentStatus: Pending, Completed, Failed
    public string PaymentStatus { get; set; } = "Pending";

    // Delivery Snapshot info
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryPincode { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Delivery Verification OTP and timestamps
    public string? DeliveryOtp { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // Staff Handling & Rider Assignment
    public int? AssignedRiderId { get; set; }
    public User? AssignedRider { get; set; }
    public string? PackedBy { get; set; }
    public DateTime? PackedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
