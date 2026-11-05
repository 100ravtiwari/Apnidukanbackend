namespace Dukan.API.DTOs.Theme;

public class FestiveCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "🛍️";
    public string SearchTag { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? ImageUrl { get; set; }
}

public class FestiveGiftItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SearchTag { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class FestivalThemeDto
{
    public string ThemeId { get; set; } = "default"; // "diwali", "holi", "independence", "newyear", "eid", "rakhi", "default"
    public string Name { get; set; } = "Standard Store";
    public string FestivalName { get; set; } = string.Empty;
    public string FestivalDate { get; set; } = string.Empty; // e.g. "8TH NOVEMBER"
    public string Greeting { get; set; } = string.Empty; // e.g. "Shubh Deepavali ✨"
    public string Tagline { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#0c831f";
    public string AccentColor { get; set; } = "#f59e0b";
    public string BgColor { get; set; } = "#ffffff";
    public bool ShowHangingLights { get; set; } = false;
    public bool ShowColorSplash { get; set; } = false;
    public bool ShowSnowfall { get; set; } = false;
    public bool ShowTirangaRibbon { get; set; } = false;
    public bool ShowCrescentStars { get; set; } = false;
    public bool IsActive { get; set; } = false;
    public List<FestiveCategoryDto> FestiveCategories { get; set; } = new();
    public List<FestiveGiftItemDto> GiftShopItems { get; set; } = new();
}

public class ActivateThemeRequestDto
{
    public string ThemeId { get; set; } = "default";
    public string? CustomGreeting { get; set; }
    public string? CustomDate { get; set; }
}
