using Dukan.API.DTOs.Products;

namespace Dukan.API.DTOs.Homepage;

/// <summary>
/// Response payload for GET /api/homepage
/// Supplies dynamic hero banner and personalized product recommendations based on Redis persona scoring.
/// </summary>
public class HomepageResponseDto
{
    public string Persona { get; set; } = "general"; // e.g. "pooja", "snacks", "dairy", "staples", "oil_ghee", "general"
    public string? TopCategory { get; set; }
    public double TopScore { get; set; }
    public string HeroBanner { get; set; } = "general_banner.jpg";
    public string BannerTitle { get; set; } = "10-Minute Kirana & Grocery Delivery";
    public string BannerSubtitle { get; set; } = "Fresh kitchen essentials, dairy & staples delivered fast to your doorstep";
    public string BannerTag { get; set; } = "Instant 10-15 Mins";
    public List<ProductDto> SuggestedForYou { get; set; } = new();
    public List<ProductDto> TrendingProducts { get; set; } = new();
}

/// <summary>
/// Request payload to track user interaction for persona scoring in Redis.
/// </summary>
public class TrackUserActionDto
{
    public string? UserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = "view"; // "view" (+1), "cart" (+3), "purchase" (+5)
    public double? CustomIncrement { get; set; }
}

/// <summary>
/// Request payload to save a search query into Redis recent searches.
/// </summary>
public class SaveSearchQueryDto
{
    public string? UserId { get; set; }
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// Response payload containing user's recent searches.
/// </summary>
public class RecentSearchesResponseDto
{
    public string UserId { get; set; } = string.Empty;
    public List<string> Searches { get; set; } = new();
}
