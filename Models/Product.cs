namespace Dukan.API.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Unit { get; set; } = "piece"; // e.g. "1 kg", "500 g", "1 litre", "packet", "piece"
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;

    // script2.sql enhancements: Bilingual support & GST compliance
    public string? HindiName { get; set; }
    public string? HsnCode { get; set; } = "1904";
    public decimal GstRate { get; set; } = 5.0m; // 0%, 5%, 12%, 18%

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Navigation properties
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
