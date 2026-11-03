namespace Dukan.API.Models;

public class CartItem
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User? User { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public string? VariantName { get; set; }
    public decimal? VariantPrice { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
