namespace Dukan.API.Models;

public class LossInOrderLog
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int? OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantityLoss { get; set; } = 1;

    // Loss types from script2.sql: "Damage In Transit", "Punctured/Leaked Pouch", "Expired Stock", "Customer Refused"
    public string LossType { get; set; } = "Damage In Transit";
    public string? Description { get; set; }
    public string LossBy { get; set; } = "Rider"; // "Rider", "StoreStaff", "Vendor"

    public decimal EstimatedLossAmount { get; set; }
    public string LoggedBy { get; set; } = "Admin";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
