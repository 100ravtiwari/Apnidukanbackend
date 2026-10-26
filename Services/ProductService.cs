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
            .AsNoTracking()
            .AsQueryable();

        // Filter by Category
        if (query.CategoryId.HasValue && query.CategoryId.Value > 0)
        {
            dbQuery = dbQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        // Filter by Search text (in name or description)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(p => p.Name.ToLower().Contains(term) ||
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

        var items = await dbQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToProductDto(p))
            .ToListAsync();

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

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        product.Category = category;

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), "Product created successfully!");
    }

    public async Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        var product = await _context.Products
            .Include(p => p.Category)
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
        product.Description = dto.Description?.Trim();
        product.Price = dto.Price;
        product.DiscountPrice = dto.DiscountPrice;
        product.StockQuantity = dto.StockQuantity;
        product.Unit = dto.Unit.Trim();
        product.ImageUrl = dto.ImageUrl?.Trim();
        product.IsAvailable = dto.IsAvailable;
        product.CategoryId = dto.CategoryId;

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
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return ApiResponse<ProductDto>.Fail("Product not found.");
        }

        product.StockQuantity = newStock;
        product.IsAvailable = newStock > 0;
        await _context.SaveChangesAsync();

        return ApiResponse<ProductDto>.Ok(MapToProductDto(product), $"Stock updated to {newStock} units.");
    }

    public async Task<ApiResponse<ProductDto>> UpdatePriceAsync(int id, decimal price, decimal? discountPrice)
    {
        var product = await _context.Products
            .Include(p => p.Category)
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
            Description = product.Description,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            StockQuantity = product.StockQuantity,
            Unit = product.Unit,
            ImageUrl = product.ImageUrl,
            IsAvailable = product.IsAvailable,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            CreatedAt = product.CreatedAt
        };
    }
}
