namespace Dukan.API.Models;

public class UnavailableProductDetail
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int UnavailableQuantity { get; set; } = 1;

    // Action: "Refund" or "Replace"
    public string Action { get; set; } = "Refund";
    public bool IsRefundSettled { get; set; } = false;
    public decimal RefundAmount { get; set; } = 0.0m;

    public bool IsReplaced { get; set; } = false;
    public string? ReplacementItemName { get; set; }

    public string? SettledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
