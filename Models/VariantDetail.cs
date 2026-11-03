namespace Dukan.API.Models;

public class VariantDetail
{
    public int Id { get; set; }

    public string VariantName { get; set; } = string.Empty; // e.g. "100 g", "250 g", "500 g", "1 kg", "2 kg", "5 kg", "10 kg", "500 ml", "1 Litre"
    public decimal UnitValue { get; set; } = 1.0m;
    public string Unit { get; set; } = "kg"; // "g", "kg", "ml", "L", "pack"
    public int VariantGroupId { get; set; } = 1; // 1 = Weight, 2 = Volume, 3 = Multipack/Count
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation collection
    public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
