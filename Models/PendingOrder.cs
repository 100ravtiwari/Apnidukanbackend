namespace Dukan.API.Models;

public class PendingOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public string? PgMid { get; set; }
    public string PgPaymentOrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    
    // Status: Initiated, PendingPayment, Success, Failed
    public string Status { get; set; } = "Initiated";

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public string? CheckoutPayload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

