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

    // Optional PG token if created via [Order].[PendingOrder]
    public string? PgPaymentOrderId { get; set; }

    // Distance from G.I.C Chowk, Champawat store in Km
    public decimal? DistanceKm { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal GstTotal { get; set; }
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
    public string? UserAccountName { get; set; }
    public string? UserAccountPhone { get; set; }
    public string? DeliveryOtp { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // script2.sql Stage 1: Order Inception
    public DateTime? EstimatedDeliveryAt { get; set; }
    public string? EstimatedTime { get; set; } = "10-15 Minutes";
    public int OrderActionTypeId { get; set; } = 1; // 1 = No Action (Stage 1 Inception)
    public string? PgPaymentOrderId { get; set; }
    public decimal? DistanceKm { get; set; }

    // Settlement & Audit
    public bool UnavailableSettled { get; set; }

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
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal GstRate { get; set; } = 5.0m;
    public decimal GstAmount { get; set; } = 0.0m;
    public bool IsUnavailable { get; set; }
    public string? UnavailableAction { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? ReplacementItemName { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class UpdatePaymentStatusDto
{
    [Required]
    public string PaymentStatus { get; set; } = string.Empty;
}

public class ConfirmDoorstepUpiDto
{
    public string? TransactionRef { get; set; }
    public string? PayerVpa { get; set; }
}

public class VerifyDeliveryOtpDto
{
    [Required(ErrorMessage = "Delivery OTP is required")]
    [StringLength(10, MinimumLength = 4, ErrorMessage = "OTP must be 4 to 6 characters")]
    public string Otp { get; set; } = string.Empty;

    public string? PaymentCollectionMode { get; set; } // "Cash", "UPI", etc.
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

// script2.sql Kirana exception handling (Refund vs Replace)
public class HandleUnavailableItemDto
{
    [Required]
    public int OrderItemId { get; set; }

    [Required]
    public string Action { get; set; } = "Refund"; // "Refund" or "Replace"

    public decimal RefundAmount { get; set; }

    public string? ReplacementItemName { get; set; }
}

// script2.sql Loss & Damage audit
public class LogLossDto
{
    [Required]
    public int OrderId { get; set; }

    public int? OrderItemId { get; set; }

    [Required]
    public string ProductName { get; set; } = string.Empty;

    public int QuantityLoss { get; set; } = 1;

    [Required]
    public string LossType { get; set; } = "Damage In Transit"; // "Damage In Transit", "Punctured/Leaked Pouch", "Expired Stock", "Customer Refused"

    public string? Description { get; set; }

    public string LossBy { get; set; } = "Rider";

    public decimal EstimatedLossAmount { get; set; }
}

public class LossLogDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantityLoss { get; set; }
    public string LossType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LossBy { get; set; } = string.Empty;
    public decimal EstimatedLossAmount { get; set; }
    public string LoggedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// script2.sql Stage 1: Online PG & [Order].[PendingOrder] DTOs
public class InitiatePgPaymentDto : CreateOrderDto
{
    public string? GatewayProvider { get; set; } = "UPI_INTENT"; // "UPI_INTENT", "PAYTM", "RAZORPAY"
}

public class InitiatePgPaymentResponseDto
{
    public string PgPaymentOrderId { get; set; } = string.Empty;
    public string PgMid { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Initiated";
    public string QrData { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}

public class ConfirmPgPaymentDto
{
    [Required]
    public string PgPaymentOrderId { get; set; } = string.Empty;

    public string? BankTxnId { get; set; }
    public string? GatewayResponseStatus { get; set; } = "TXN_SUCCESS";
}
