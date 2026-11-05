using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Theme;
using Dukan.API.Hubs;

namespace Dukan.API.Services;

public class ThemeService : IThemeService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<ThemeService> _logger;

    private const string RedisThemeKey = "app:active_theme";
    private static FestivalThemeDto _inMemoryActiveTheme = GetDefaultPresets().First(t => t.ThemeId == "diwali"); // Default to Diwali active right now!

    public ThemeService(
        IHubContext<OrderHub> hubContext,
        ILogger<ThemeService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _hubContext = hubContext;
        _logger = logger;
        _redis = redis;
    }

    private IDatabase? GetDatabase()
    {
        try
        {
            if (_redis != null)
            {
                return _redis.GetDatabase();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis GetDatabase error in ThemeService: {Message}", ex.Message);
        }
        return null;
    }

    public async Task<ApiResponse<FestivalThemeDto>> GetActiveThemeAsync()
    {
        try
        {
            var db = GetDatabase();
            if (db != null)
            {
                var json = await db.StringGetAsync(RedisThemeKey);
                if (!json.IsNullOrEmpty)
                {
                    var parsed = JsonSerializer.Deserialize<FestivalThemeDto>(json.ToString()!, JsonOptions);
                    if (parsed != null)
                    {
                        parsed.IsActive = true;
                        _inMemoryActiveTheme = parsed;
                        return ApiResponse<FestivalThemeDto>.Ok(parsed);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to retrieve theme from Redis, falling back to memory: {Message}", ex.Message);
        }

        _inMemoryActiveTheme.IsActive = true;
        return ApiResponse<FestivalThemeDto>.Ok(_inMemoryActiveTheme);
    }

    public async Task<ApiResponse<List<FestivalThemeDto>>> GetAllThemesAsync()
    {
        var activeThemeRes = await GetActiveThemeAsync();
        var activeId = activeThemeRes.Data?.ThemeId ?? "diwali";

        var presets = GetDefaultPresets();
        foreach (var p in presets)
        {
            p.IsActive = p.ThemeId.Equals(activeId, StringComparison.OrdinalIgnoreCase);
            if (p.IsActive && activeThemeRes.Data != null)
            {
                if (!string.IsNullOrWhiteSpace(activeThemeRes.Data.Greeting)) p.Greeting = activeThemeRes.Data.Greeting;
                if (!string.IsNullOrWhiteSpace(activeThemeRes.Data.FestivalDate)) p.FestivalDate = activeThemeRes.Data.FestivalDate;
            }
        }

        return ApiResponse<List<FestivalThemeDto>>.Ok(presets);
    }

    public async Task<ApiResponse<FestivalThemeDto>> ActivateThemeAsync(ActivateThemeRequestDto dto)
    {
        var targetId = (dto.ThemeId ?? "default").Trim().ToLowerInvariant();
        var presets = GetDefaultPresets();
        var matched = presets.FirstOrDefault(p => p.ThemeId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                      ?? presets.First(p => p.ThemeId == "default");

        if (!string.IsNullOrWhiteSpace(dto.CustomGreeting))
        {
            matched.Greeting = dto.CustomGreeting.Trim();
        }
        if (!string.IsNullOrWhiteSpace(dto.CustomDate))
        {
            matched.FestivalDate = dto.CustomDate.Trim();
        }
        matched.IsActive = true;

        // 1. Update in-memory fallback
        _inMemoryActiveTheme = matched;

        // 2. Persist in Redis
        try
        {
            var db = GetDatabase();
            if (db != null)
            {
                var jsonStr = JsonSerializer.Serialize(matched, JsonOptions);
                await db.StringSetAsync(RedisThemeKey, jsonStr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save active theme in Redis: {Message}", ex.Message);
        }

        // 3. Real-time broadcast to all connected clients & mobile apps via SignalR!
        try
        {
            await _hubContext.Clients.All.SendAsync("ThemeChanged", matched);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SignalR broadcast ThemeChanged failed: {Message}", ex.Message);
        }

        return ApiResponse<FestivalThemeDto>.Ok(matched, $"Festival theme '{matched.Name}' is now LIVE on store!");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static List<FestivalThemeDto> GetDefaultPresets()
    {
        return new List<FestivalThemeDto>
        {
            // 1. 🪔 DIWALI GRAND FESTIVAL THEME (Matches User Screenshot)
            new()
            {
                ThemeId = "diwali",
                Name = "Diwali Grand Celebration",
                FestivalName = "Diwali",
                FestivalDate = "8TH NOVEMBER",
                Greeting = "Shubh Deepavali ✨",
                Tagline = "Diyon, phoolon, pooja aur meethai ka tyohaar — superfast delivery!",
                PrimaryColor = "#f59e0b",
                AccentColor = "#d97706",
                BgColor = "#042f2e", // Deep emerald festive night
                ShowHangingLights = true,
                FestiveCategories = new()
                {
                    new() { Id = "diwali_decor", Title = "Lights, Diya & Diwali Decor", Label = "Diya Set", Icon = "🪔", SearchTag = "Diya", Price = 269, OriginalPrice = 400, ImageUrl = "https://images.unsplash.com/photo-1605647540924-852290f6b0d5?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "diwali_pooja", Title = "Flowers & Pooja Needs", Label = "Lakshmi Ganesha", Icon = "🌸", SearchTag = "Pooja", ImageUrl = "https://images.unsplash.com/photo-1609840114035-3c981b782dfe?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "diwali_gifting", Title = "Diwali Gifting", Label = "Gift Hampers", Icon = "🎁", SearchTag = "Gift", ImageUrl = "https://images.unsplash.com/photo-1549465220-1a8b9238cd48?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "diwali_dining", Title = "Bedsheets, Decor & Dining", Label = "Home Makeover", Icon = "✨", SearchTag = "Home", ImageUrl = "https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "diwali_gadgets", Title = "Appliances & Kitchen Gadgets", Label = "Diwali Electronics", Icon = "⚡", SearchTag = "Kitchen", ImageUrl = "https://images.unsplash.com/photo-1584269600519-112d071b35e6?w=500&auto=format&fit=crop&q=80" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "gift_sweets", Name = "Indian Sweets & Mithai", Category = "Kaju Katli & Ladoo", SearchTag = "Sweets", ImageUrl = "https://images.unsplash.com/photo-1599785209707-a456fc1337bb?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "gift_dryfruits", Name = "Royal Dry Fruit Packs", Category = "Cashews, Almonds & Dates", SearchTag = "Dry Fruits", ImageUrl = "https://images.unsplash.com/photo-1508736793122-f516e3ba5569?w=500&auto=format&fit=crop&q=80" },
                    new() { Id = "gift_chocolates", Name = "Chocolates & Gift Hampers", Category = "Cadbury & Ferrero", SearchTag = "Chocolate", ImageUrl = "https://images.unsplash.com/photo-1549007994-cb92caebd54b?w=500&auto=format&fit=crop&q=80" }
                }
            },

            // 2. 🎨 HOLI RANGOTSAV THEME
            new()
            {
                ThemeId = "holi",
                Name = "Holi Rangotsav Carnival",
                FestivalName = "Holi",
                FestivalDate = "RANGOTSAV SPECIAL",
                Greeting = "Bura Na Mano, Holi Hai! 🎨",
                Tagline = "Skin-safe herbal gulal, water guns, thandai & fresh gujiya delivered in minutes",
                PrimaryColor = "#ec4899",
                AccentColor = "#06b6d4",
                BgColor = "#3b0764",
                ShowColorSplash = true,
                FestiveCategories = new()
                {
                    new() { Id = "holi_gulal", Title = "Organic Gulal & Colors", Label = "Herbal & Safe", Icon = "🎨", SearchTag = "Gulal", Price = 99, OriginalPrice = 150 },
                    new() { Id = "holi_pichkari", Title = "Pichkaris & Water Guns", Label = "Kids Special", Icon = "🔫", SearchTag = "Pichkari", Price = 149, OriginalPrice = 220 },
                    new() { Id = "holi_gujiya", Title = "Fresh Mawa Gujiya", Label = "Haldiram Special", Icon = "🥟", SearchTag = "Gujiya", Price = 240, OriginalPrice = 280 },
                    new() { Id = "holi_thandai", Title = "Thandai & Cold Drinks", Label = "Chilled Refreshers", Icon = "🥛", SearchTag = "Thandai" },
                    new() { Id = "holi_snacks", Title = "Crispy Namkeen & Mathri", Label = "Party Treats", Icon = "🍟", SearchTag = "Namkeen" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "holi_sweet_box", Name = "Holi Gujiya & Sweet Box", Category = "Mawa Sweets", SearchTag = "Gujiya" },
                    new() { Id = "holi_thandai_mix", Name = "Premium Kesar Thandai", Category = "Beverages", SearchTag = "Thandai" },
                    new() { Id = "holi_namkeen_platter", Name = "Spicy Mathri & Bhujia Platter", Category = "Snacks", SearchTag = "Namkeen" }
                }
            },

            // 3. 🇮🇳 INDEPENDENCE & REPUBLIC DAY THEME (TIRANGA)
            new()
            {
                ThemeId = "independence",
                Name = "Desh Ka Tyohar (Tiranga Special)",
                FestivalName = "Independence Day",
                FestivalDate = "15TH AUGUST / 26TH JAN",
                Greeting = "Jai Hind! Happy Independence Day 🇮🇳",
                Tagline = "Swadeshi kirana, tricolor mithai & patriotic celebration at wholesale rates",
                PrimaryColor = "#f97316",
                AccentColor = "#16a34a",
                BgColor = "#0f172a",
                ShowTirangaRibbon = true,
                FestiveCategories = new()
                {
                    new() { Id = "ind_sweets", Title = "Tricolor Mithai & Ladoos", Label = "Desh Ki Mithai", Icon = "🇮🇳", SearchTag = "Sweets", Price = 180, OriginalPrice = 220 },
                    new() { Id = "ind_staples", Title = "100% Indian Chakki Atta", Label = "Aashirvaad & Fortune", Icon = "🌾", SearchTag = "Atta" },
                    new() { Id = "ind_ghee", Title = "Pure Desi Cow Ghee", Label = "Swadeshi Health", Icon = "🧈", SearchTag = "Ghee" },
                    new() { Id = "ind_dryfruits", Title = "Premium Indian Nuts", Label = "Healthy India", Icon = "🥜", SearchTag = "Dry Fruits" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "ind_tiranga_pack", Name = "Tricolor Celebration Hamper", Category = "Sweets & Dry Fruits", SearchTag = "Sweets" },
                    new() { Id = "ind_chai_pack", Name = "Assam & Darjeeling Special Tea", Category = "Tea & Biscuits", SearchTag = "Tea" }
                }
            },

            // 4. 🎄 NEW YEAR & CHRISTMAS THEME
            new()
            {
                ThemeId = "newyear",
                Name = "New Year & Christmas Carnival",
                FestivalName = "New Year & Christmas",
                FestivalDate = "CELEBRATION 2027",
                Greeting = "Happy New Year & Merry Christmas! 🎆",
                Tagline = "Party munchies, chocolates, cold drinks & celebration essentials in superfast delivery",
                PrimaryColor = "#dc2626",
                AccentColor = "#059669",
                BgColor = "#0f172a",
                ShowSnowfall = true,
                FestiveCategories = new()
                {
                    new() { Id = "ny_party", Title = "Party Munchies & Chips", Label = "Crunchy Snacks", Icon = "🎉", SearchTag = "Chips" },
                    new() { Id = "ny_drinks", Title = "Chilled Cold Drinks & Mocktails", Label = "Party Drinks", Icon = "🥤", SearchTag = "Drinks" },
                    new() { Id = "ny_cakes", Title = "Cakes & Cookies", Label = "Sweet Moments", Icon = "🎂", SearchTag = "Biscuit" },
                    new() { Id = "ny_chocolates", Title = "Chocolates & Gift Boxes", Label = "Celebration", Icon = "🍫", SearchTag = "Chocolate" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "ny_party_hamper", Name = "Midnight Party Snack Box", Category = "Chips & Drinks", SearchTag = "Snacks" },
                    new() { Id = "ny_choco_box", Name = "Premium Chocolate Hamper", Category = "Cadbury & Imported", SearchTag = "Chocolate" }
                }
            },

            // 5. 🌙 EID MUBARAK THEME
            new()
            {
                ThemeId = "eid",
                Name = "Eid Mubarak Special",
                FestivalName = "Eid Mubarak",
                FestivalDate = "EID SPECIAL",
                Greeting = "Eid Mubarak! 🌙✨",
                Tagline = "Sheer khurma seviyan, royal dates, saffron & sweet treats delivered to your door",
                PrimaryColor = "#10b981",
                AccentColor = "#f59e0b",
                BgColor = "#064e3b",
                ShowCrescentStars = true,
                FestiveCategories = new()
                {
                    new() { Id = "eid_seviyan", Title = "Roasted Seviyan & Vermicelli", Label = "Sheer Khurma", Icon = "🍲", SearchTag = "Seviyan" },
                    new() { Id = "eid_dates", Title = "Kimia & Medjool Dates", Label = "Fresh Dates", Icon = "🌴", SearchTag = "Dates" },
                    new() { Id = "eid_ghee", Title = "Pure Desi Cow Ghee", Label = "Aromatic Taste", Icon = "🧈", SearchTag = "Ghee" },
                    new() { Id = "eid_dryfruits", Title = "Almonds, Pistachio & Kesar", Label = "Royal Dry Fruits", Icon = "🥜", SearchTag = "Dry Fruits" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "eid_hamper", Name = "Eid Special Dry Fruit & Dates Box", Category = "Royal Gifting", SearchTag = "Gift" }
                }
            },

            // 6. 🎁 RAKSHA BANDHAN THEME
            new()
            {
                ThemeId = "rakhi",
                Name = "Raksha Bandhan Celebration",
                FestivalName = "Rakhi",
                FestivalDate = "SHUBH RAKHI",
                Greeting = "Happy Raksha Bandhan! 🧵✨",
                Tagline = "Designer rakhis, Cadbury celebrations, fresh sweets & brother-sister gift sets",
                PrimaryColor = "#8b5cf6",
                AccentColor = "#f59e0b",
                BgColor = "#311042",
                FestiveCategories = new()
                {
                    new() { Id = "rakhi_chocolates", Title = "Cadbury Celebrations Box", Label = "Sweet Bond", Icon = "🍫", SearchTag = "Celebrations", Price = 175, OriginalPrice = 200 },
                    new() { Id = "rakhi_sweets", Title = "Fresh Kaju Katli & Ladoo", Label = "Desi Ghee Mithai", Icon = "🍬", SearchTag = "Sweets" },
                    new() { Id = "rakhi_hampers", Title = "Sister Special Gift Hampers", Label = "Festive Gifts", Icon = "🎁", SearchTag = "Gift" },
                    new() { Id = "rakhi_pooja", Title = "Roli, Chawal & Aarti Thali", Label = "Pooja Set", Icon = "🪔", SearchTag = "Pooja" },
                },
                GiftShopItems = new()
                {
                    new() { Id = "rakhi_combo", Name = "Rakhi + Celebrations + Kaju Katli Combo", Category = "Complete Gift Set", SearchTag = "Gift" }
                }
            },

            // 7. 🏪 DEFAULT STANDARD STORE THEME (Clean Kirana Green)
            new()
            {
                ThemeId = "default",
                Name = "Standard Store (Daily Kirana)",
                FestivalName = "",
                FestivalDate = "DAILY EXPRESS",
                Greeting = "Ghar Ka Kirana, Seedha Apke Darwaze Par!",
                Tagline = "Aapki apni Champawat local dukan — 100% authentic fresh essentials at wholesale rates",
                PrimaryColor = "#0c831f",
                AccentColor = "#f59e0b",
                BgColor = "#ffffff",
                ShowHangingLights = false,
                FestiveCategories = new(),
                GiftShopItems = new()
            }
        };
    }
}

