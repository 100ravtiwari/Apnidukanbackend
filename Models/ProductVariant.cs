namespace Dukan.API.Models;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? VariantDetailId { get; set; }
    public VariantDetail? VariantDetail { get; set; }

    public string VariantName { get; set; } = string.Empty; // e.g. "500 g", "1 kg", "5 kg", "1 Litre"
    public string Unit { get; set; } = "pack"; // "g", "kg", "ml", "L", "piece"
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; } = 50;
    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
