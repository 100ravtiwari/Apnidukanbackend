using System.ComponentModel.DataAnnotations;

namespace Dukan.API.DTOs.Orders;

public class CreateOrderDto
{
    [Required, StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, Phone]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string DeliveryCity { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string DeliveryPincode { get; set; } = string.Empty;

    // "COD", "UPI", "Online"
    [Required]
    public string PaymentMethod { get; set; } = "COD";

    public string? Notes { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryPincode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CustomerEmail { get; set; }
    public string? DeliveryOtp { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // Rider & Packing info
    public int? AssignedRiderId { get; set; }
    public string? AssignedRiderName { get; set; }
    public string? AssignedRiderPhone { get; set; }
    public string? PackedBy { get; set; }
    public DateTime? PackedAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}

public class UpdateOrderStatusDto
{
    // Valid values: "Pending", "Confirmed", "Packed", "OutForDelivery", "Delivered", "Cancelled"
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class UpdatePaymentStatusDto
{
    // Valid values: "Pending", "Completed", "Failed"
    [Required]
    public string PaymentStatus { get; set; } = string.Empty;
}

public class VerifyDeliveryOtpDto
{
    [Required(ErrorMessage = "Delivery OTP is required")]
    [StringLength(10, MinimumLength = 4, ErrorMessage = "OTP must be 4 to 6 characters")]
    public string Otp { get; set; } = string.Empty;
}

public class DispatchOrderDto
{
    public string? Notes { get; set; }
}

public class PackOrderDto
{
    public string? PackedBy { get; set; }
}

public class AssignAndDispatchDto
{
    [Required(ErrorMessage = "Rider ID is required")]
    public int RiderId { get; set; }
    public string? Notes { get; set; }
}
