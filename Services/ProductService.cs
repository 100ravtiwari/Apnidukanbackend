using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Products;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(ProductQueryParams query)
    {
        var dbQuery = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantDetail)
            .AsNoTracking()
            .AsQueryable();

        // Filter by Category
        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        // Filter by Search text (in English or Hindi name or description)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p => p.Name.ToLower().Contains(term) ||
                                         (p.HindiName != null && p.HindiName.ToLower().Contains(term)) ||
                                         (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        // Filter In-Stock only
        if (query.InStockOnly.HasValue && query.InStockOnly.Value)
        {
            dbQuery = dbQuery.Where(p => p.IsAvailable && p.StockQuantity > 0);
        }

        // Price range filters
        if (query.MinPrice.HasValue)
        {
            dbQuery = dbQuery.Where(p => (p.DiscountPrice ?? p.Price) >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            dbQuery = dbQuery.Where(p => (p.DiscountPrice ?? p.Price) <= query.MaxPrice.Value);
        }

        // Sorting
        dbQuery = query.SortBy?.ToLower() switch
        {
            "price_asc" => dbQuery.OrderBy(p => p.DiscountPrice ?? p.Price),
            "price_desc" => dbQuery.OrderByDescending(p => p.DiscountPrice ?? p.Price),
            "name_asc" => dbQuery.OrderBy(p => p.Name),
            "newest" or _ => dbQuery.OrderByDescending(p => p.CreatedAt)
        };

        var totalCount = await dbQuery.CountAsync();

        var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        var products = await dbQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = products.Select(MapToProductDto).ToList();

        var result = new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return ApiResponse<PagedResult<ProductDto>>.Ok(result);
    }

    public async Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantDetail)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProductDto>.Fail("Product not found.");
        }

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product));
    }

    public async Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto)
    {
        var category = await _context.Categories.FindAsync(dto.CategoryId);
        if (category == null)
        {
            return ApiResponse<ProductDto>.Fail("Category not found.");
        }

        var product = new Product
        {
            Name = dto.Name.Trim(),
            HindiName = dto.HindiName?.Trim(),
            HsnCode = string.IsNullOrWhiteSpace(dto.HsnCode) ? "1904" : dto.HsnCode.Trim(),
            GstRate = dto.GstRate,
            Description = dto.Description?.Trim(),
            Price = dto.Price,
            DiscountPrice = dto.DiscountPrice,
            StockQuantity = dto.StockQuantity,
            Unit = dto.Unit.Trim(),
            ImageUrl = dto.ImageUrl?.Trim(),
            IsAvailable = dto.IsAvailable,
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.Variants != null && dto.Variants.Any())
        {
            var allVariantDetails = await _context.VariantDetails.AsNoTracking().ToListAsync();
            foreach (var v in dto.Variants)
            {
                var matchedDetail = allVariantDetails.FirstOrDefault(vd =>
                    vd.VariantName.Equals(v.VariantName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    vd.VariantName.Equals(v.Unit.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    $"{vd.UnitValue} {vd.Unit}".Trim().Equals(v.Unit.Trim(), StringComparison.OrdinalIgnoreCase));

                product.Variants.Add(new ProductVariant
                {
                    VariantName = v.VariantName.Trim(),
                    Unit = v.Unit.Trim(),
                    Price = v.Price,
                    DiscountPrice = v.DiscountPrice,
                    StockQuantity = v.StockQuantity,
                    IsAvailable = v.IsAvailable,
                    VariantDetailId = matchedDetail?.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        product.Category = category;

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), "Product created successfully with variants & GST!");
    }

    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProductDto>.Fail("Product not found.");
        }

        if (product.CategoryId != dto.CategoryId)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
            {
                return ApiResponse<ProductDto>.Fail("Selected category does not exist.");
            }
        }

        product.Name = dto.Name.Trim();
        product.HindiName = dto.HindiName?.Trim();
        product.HsnCode = dto.HsnCode?.Trim();
        product.GstRate = dto.GstRate;
        product.Description = dto.Description?.Trim();
        product.Price = dto.Price;
        product.DiscountPrice = dto.DiscountPrice;
        product.StockQuantity = dto.StockQuantity;
        product.Unit = dto.Unit.Trim();
        product.ImageUrl = dto.ImageUrl?.Trim();
        product.IsAvailable = dto.IsAvailable;
        product.CategoryId = dto.CategoryId;

        // If new variants passed, update them
        if (dto.Variants != null)
        {
            _context.ProductVariants.RemoveRange(product.Variants);
            var allVariantDetails = await _context.VariantDetails.AsNoTracking().ToListAsync();
            foreach (var v in dto.Variants)
            {
                var matchedDetail = allVariantDetails.FirstOrDefault(vd =>
                    vd.VariantName.Equals(v.VariantName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    vd.VariantName.Equals(v.Unit.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    $"{vd.UnitValue} {vd.Unit}".Trim().Equals(v.Unit.Trim(), StringComparison.OrdinalIgnoreCase));

                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    VariantName = v.VariantName.Trim(),
                    Unit = v.Unit.Trim(),
                    Price = v.Price,
                    DiscountPrice = v.DiscountPrice,
                    StockQuantity = v.StockQuantity,
                    IsAvailable = v.IsAvailable,
                    VariantDetailId = matchedDetail?.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), "Product updated successfully!");
    }

    public async Task<ApiResponse<bool>> DeleteProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return ApiResponse<bool>.Fail("Product not found.");
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Product deleted successfully.");
    }

    public async Task<ApiResponse<ProductDto>> UpdateStockAsync(int id, int newStock)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProductDto>.Fail("Product not found.");
        }

        product.StockQuantity = newStock;
        product.IsAvailable = newStock > 0;
        await _context.SaveChangesAsync();

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), $"Stock updated to {newStock} successfully!");
    }

    public async Task<ApiResponse<ProductDto>> UpdatePriceAsync(int id, decimal price, decimal? discountPrice)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProductDto>.Fail("Product not found.");
        }

        if (price <= 0)
        {
            return ApiResponse<ProductDto>.Fail("Price must be greater than zero.");
        }

        if (discountPrice.HasValue && discountPrice.Value >= price)
        {
            return ApiResponse<ProductDto>.Fail("Discount price must be less than regular price.");
        }

        product.Price = price;
        product.DiscountPrice = discountPrice;
        await _context.SaveChangesAsync();

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), $"Price updated to ₹{price} successfully!");
    }

    private static ProductDto MapToProductDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            HindiName = product.HindiName,
            HsnCode = product.HsnCode,
            GstRate = product.GstRate,
            Description = product.Description,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            StockQuantity = product.StockQuantity,
            Unit = product.Unit,
            ImageUrl = product.ImageUrl,
            IsAvailable = product.IsAvailable,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            CreatedAt = product.CreatedAt,
            Variants = product.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                ProductId = v.ProductId,
                VariantDetailId = v.VariantDetailId,
                VariantName = v.VariantName,
                Unit = v.Unit,
                UnitValue = v.VariantDetail?.UnitValue,
                MeasurementUnit = v.VariantDetail?.Unit,
                Price = v.Price,
                DiscountPrice = v.DiscountPrice,
                StockQuantity = v.StockQuantity,
                IsAvailable = v.IsAvailable
            }).ToList() ?? new()
        };
    }
}
