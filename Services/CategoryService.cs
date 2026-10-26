using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Categories;
using Dukan.API.DTOs.Common;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetAllAsync()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count(p => p.IsAvailable)
            })
            .ToListAsync();

        return ApiResponse<List<CategoryDto>>.Ok(categories);
    }

    public async Task<ApiResponse<CategoryDto>> GetByIdAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail("Category not found.");
        }

        var dto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IsActive = category.IsActive,
            ProductCount = category.Products.Count(p => p.IsAvailable)
        };

        return ApiResponse<CategoryDto>.Ok(dto);
    }

    public async Task<ApiResponse<CategoryDto>> CreateAsync(CreateCategoryDto dto)
    {
        var exists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());

        if (exists)
        {
            return ApiResponse<CategoryDto>.Fail("A category with this name already exists.");
        }

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ImageUrl = dto.ImageUrl?.Trim(),
            IsActive = dto.IsActive
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var resultDto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IsActive = category.IsActive,
            ProductCount = 0
        };

        return ApiResponse<CategoryDto>.Ok(resultDto, "Category created successfully!");
    }

    public async Task<ApiResponse<CategoryDto>> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail("Category not found.");
        }

        var duplicate = await _context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == dto.Name.Trim().ToLower());

        if (duplicate)
        {
            return ApiResponse<CategoryDto>.Fail("Another category with this name already exists.");
        }

        category.Name = dto.Name.Trim();
        category.Description = dto.Description?.Trim();
        category.ImageUrl = dto.ImageUrl?.Trim();
        category.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        var productCount = await _context.Products.CountAsync(p => p.CategoryId == id && p.IsAvailable);

        var resultDto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IsActive = category.IsActive,
            ProductCount = productCount
        };

        return ApiResponse<CategoryDto>.Ok(resultDto, "Category updated successfully!");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return ApiResponse<bool>.Fail("Category not found.");
        }

        if (category.Products.Any())
        {
            return ApiResponse<bool>.Fail("Cannot delete category with associated products. Reassign or delete the products first.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Category deleted successfully.");
    }
}
