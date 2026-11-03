using System.ComponentModel.DataAnnotations;

namespace Dukan.API.DTOs.Products;

public class ProductVariantDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? VariantDetailId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? UnitValue { get; set; }
    public string? MeasurementUnit { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; }
}

public class VariantDetailDto
{
    public int Id { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public decimal UnitValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int VariantGroupId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductVariantDto
{
    [Required, StringLength(100)]
    public string VariantName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Unit { get; set; } = "pack";

    [Required, Range(0.01, 1000000)]
    public decimal Price { get; set; }

    [Range(0, 1000000)]
    public decimal? DiscountPrice { get; set; }

    [Range(0, 100000)]
    public int StockQuantity { get; set; } = 50;

    public bool IsAvailable { get; set; } = true;
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HindiName { get; set; }
    public string? HsnCode { get; set; }
    public decimal GstRate { get; set; } = 5.0m;
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

    // Multi-pack variants from script2.sql
    public List<ProductVariantDto> Variants { get; set; } = new();
}

public class CreateProductDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? HindiName { get; set; }
    public string? HsnCode { get; set; } = "1904";
    public decimal GstRate { get; set; } = 5.0m;

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

    public List<CreateProductVariantDto>? Variants { get; set; }
}

public class UpdateProductDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? HindiName { get; set; }
    public string? HsnCode { get; set; }
    public decimal GstRate { get; set; } = 5.0m;

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

    public List<CreateProductVariantDto>? Variants { get; set; }
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
    public int? CategoryId { get; set; }
    public string? Search { get; set; }
    public bool? InStockOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; } // "price_asc", "price_desc", "name_asc", "newest"
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
