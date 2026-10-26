using System.ComponentModel.DataAnnotations;

namespace Dukan.API.DTOs.Products;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateProductDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Range(0.01, 1000000, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0, 1000000)]
    public decimal? DiscountPrice { get; set; }

    [Range(0, 100000)]
    public int StockQuantity { get; set; } = 0;

    [Required, StringLength(50)]
    public string Unit { get; set; } = "piece";

    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    [Required]
    public int CategoryId { get; set; }
}

public class UpdateProductDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Range(0.01, 1000000, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0, 1000000)]
    public decimal? DiscountPrice { get; set; }

    [Range(0, 100000)]
    public int StockQuantity { get; set; }

    [Required, StringLength(50)]
    public string Unit { get; set; } = "piece";

    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; }

    [Required]
    public int CategoryId { get; set; }
}

public class UpdateStockDto
{
    [Required, Range(0, 100000)]
    public int StockQuantity { get; set; }
}

public class UpdatePriceDto
{
    [Required, Range(0.01, 1000000, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Range(0, 1000000)]
    public decimal? DiscountPrice { get; set; }
}

public class ProductQueryParams
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool? InStockOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    // Sort options: "price_asc", "price_desc", "name_asc", "newest"
    public string? SortBy { get; set; } = "newest";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
