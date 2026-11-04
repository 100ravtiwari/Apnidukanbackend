using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Homepage;
using Dukan.API.DTOs.Products;
using Dukan.API.Models;

namespace Dukan.API.Services;

/// <summary>
/// Generates personalized homepage payloads driven by Redis Sorted Sets (ZSET) category scoring
/// with seamless SQL Database fallback to default trending products.
/// </summary>
public class HomepageService : IHomepageService
{
    private readonly AppDbContext _context;
    private readonly IRedisPersonaService _redisPersona;
    private readonly ILogger<HomepageService> _logger;

    public HomepageService(
        AppDbContext context,
        IRedisPersonaService redisPersona,
        ILogger<HomepageService> logger)
    {
        _context = context;
        _redisPersona = redisPersona;
        _logger = logger;
    }

    public async Task<ApiResponse<HomepageResponseDto>> GetPersonalizedHomepageAsync(string? userId)
    {
        var response = new HomepageResponseDto();

        string? topCategory = null;
        double topScore = 0;

        // 1. Query Redis Sorted Set (ZSET) for highest scoring category if user is identified
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (cat, score) = await _redisPersona.GetTopCategoryForUserAsync(userId);
            topCategory = cat;
            topScore = score;
        }

        // 2. Map persona metadata and dynamic hero banner based on top category
        ConfigurePersonaAndBanner(response, topCategory, topScore);

        // 3. Fetch recommended products from SQL database matching the persona
        var recommended = new List<Product>();

        if (!string.IsNullOrWhiteSpace(topCategory) && topScore > 0)
        {
            recommended = await GetProductsForCategoryAsync(topCategory, 5);
        }

        // If user has no persona or fewer than 5 matching products, supplement with top trending products
        if (recommended.Count < 5)
        {
            var existingIds = new HashSet<int>(recommended.Select(p => p.Id));
            var needed = 5 - recommended.Count;

            var fallbackProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.VariantDetail)
                .Where(p => p.IsAvailable && p.StockQuantity > 0 && !existingIds.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .Take(needed)
                .AsNoTracking()
                .ToListAsync();

            recommended.AddRange(fallbackProducts);
        }

        // 4. Fetch general trending products for the homepage carousel / grid
        var trending = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantDetail)
            .Where(p => p.IsAvailable && p.StockQuantity > 0)
            .OrderByDescending(p => p.StockQuantity)
            .ThenByDescending(p => p.CreatedAt)
            .Take(8)
            .AsNoTracking()
            .ToListAsync();

        response.SuggestedForYou = recommended.Select(ProductService.MapToProductDto).ToList();
        response.TrendingProducts = trending.Select(ProductService.MapToProductDto).ToList();

        return ApiResponse<HomepageResponseDto>.Ok(response, "Personalized homepage loaded successfully.");
    }

    private async Task<List<Product>> GetProductsForCategoryAsync(string categoryKey, int limit)
    {
        var key = categoryKey.ToLowerInvariant();

        // Search SQL database matching category name, product name, or description
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantDetail)
            .Where(p => p.IsAvailable && p.StockQuantity > 0 &&
                        ((p.Category != null && p.Category.Name.ToLower().Contains(key)) ||
                         p.Name.ToLower().Contains(key) ||
                         (p.Description != null && p.Description.ToLower().Contains(key))))
            .OrderByDescending(p => p.StockQuantity)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    private static void ConfigurePersonaAndBanner(HomepageResponseDto response, string? topCategory, double topScore)
    {
        response.TopCategory = topCategory;
        response.TopScore = topScore;

        if (string.IsNullOrWhiteSpace(topCategory) || topScore <= 0)
        {
            response.Persona = "general";
            response.HeroBanner = "general_banner.jpg";
            response.BannerTitle = "10-Minute Kirana & Grocery Delivery";
            response.BannerSubtitle = "Over 1,000+ daily essentials delivered fast to your doorstep in Champawat";
            response.BannerTag = "⚡ Instant 10-15 Mins";
            return;
        }

        var lower = topCategory.ToLowerInvariant();

        if (lower.Contains("pooja") || lower.Contains("agarbatti") || lower.Contains("dhoop"))
        {
            response.Persona = "pooja";
            response.HeroBanner = "pooja_banner.jpg";
            response.BannerTitle = "Pooja Samagri & Daily Spiritual Essentials";
            response.BannerSubtitle = "Pure cow ghee, authentic dhoop, agarbatti, camphor & prayer items in 10 mins";
            response.BannerTag = "🪔 Spiritual & Pure";
        }
        else if (lower.Contains("snack") || lower.Contains("biscuit") || lower.Contains("namkeen") || lower.Contains("beverage"))
        {
            response.Persona = "snacks";
            response.HeroBanner = "snacks_banner.jpg";
            response.BannerTitle = "Munchies, Biscuits & Evening Snacks";
            response.BannerSubtitle = "Crispy namkeens, biscuits & hot tea accompaniments in 10-15 mins";
            response.BannerTag = "🍟 10-Min Munchies";
        }
        else if (lower.Contains("dairy") || lower.Contains("milk") || lower.Contains("butter") || lower.Contains("egg"))
        {
            response.Persona = "dairy";
            response.HeroBanner = "dairy_banner.jpg";
            response.BannerTitle = "Fresh Dairy, Milk & Breakfast Delights";
            response.BannerSubtitle = "Farm fresh milk, creamy curd, fresh bread & butter delivered to your doorstep";
            response.BannerTag = "🥛 Farm Fresh Daily";
        }
        else if (lower.Contains("atta") || lower.Contains("dal") || lower.Contains("rice") || lower.Contains("staple"))
        {
            response.Persona = "staples";
            response.HeroBanner = "staples_banner.jpg";
            response.BannerTitle = "Pahadi & Kitchen Daily Staples";
            response.BannerSubtitle = "Unpolished dals, premium basmati rice & 100% fresh chakki atta";
            response.BannerTag = "🌾 Kitchen Essentials";
        }
        else if (lower.Contains("oil") || lower.Contains("ghee") || lower.Contains("masala"))
        {
            response.Persona = "oil_ghee";
            response.HeroBanner = "oil_banner.jpg";
            response.BannerTitle = "Pure Cooking Oils & Desi Cow Ghee";
            response.BannerSubtitle = "Cold pressed oils, authentic aromatic spices & pure cow ghee";
            response.BannerTag = "🛢️ 100% Pure & Healthy";
        }
        else
        {
            response.Persona = lower;
            response.HeroBanner = $"{lower}_banner.jpg";
            response.BannerTitle = $"Special Picks in {char.ToUpper(lower[0]) + lower[1..]}";
            response.BannerSubtitle = "Curated high-quality groceries delivered in 10-15 minutes";
            response.BannerTag = "⭐ Curated For You";
        }
    }
}

