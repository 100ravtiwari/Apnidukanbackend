namespace Dukan.API.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }

    // script2.sql GST & Itemized Settlement
    public decimal GstRate { get; set; } = 5.0m;
    public decimal GstAmount { get; set; } = 0.0m;

    // Kirana out-of-stock exception handling
    public bool IsUnavailable { get; set; } = false;
    public string? UnavailableAction { get; set; } // 'Refund' or 'Replace'
    public decimal? RefundAmount { get; set; }
    public string? ReplacementItemName { get; set; }
}
