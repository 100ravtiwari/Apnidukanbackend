using StackExchange.Redis;

namespace Dukan.API.Services;

/// <summary>
/// Redis-backed implementation of user profiling, category scoring via Sorted Sets (ZSET),
/// and recent search history via Lists (LIST).
/// Includes graceful failure recovery if Redis connection is unavailable.
/// </summary>
public class RedisPersonaService : IRedisPersonaService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisPersonaService> _logger;

    public RedisPersonaService(ILogger<RedisPersonaService> logger, IConnectionMultiplexer? redis = null)
    {
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
            _logger.LogWarning("Redis GetDatabase failed: {Message}", ex.Message);
        }
        return null;
    }

    /// <summary>
    /// User Profiling & Persona Tracking:
    /// Increments category score using Redis Sorted Sets (ZSET).
    /// Redis Structure:
    ///   Key:    user:{userId}:categories
    ///   Member: {category} (e.g. "pooja", "snacks", "dairy")
    ///   Score:  cumulative weight (+1 view, +3 cart, +5 purchase)
    /// Command: ZINCRBY
    /// </summary>
    public async Task TrackUserActionAsync(string userId, string category, double scoreIncrement = 1.0)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(category))
            return;

        try
        {
            var db = GetDatabase();
            if (db == null) return;

            var key = $"user:{userId.Trim().ToLowerInvariant()}:categories";
            var member = NormalizeCategoryName(category);

            // ZINCRBY user:{userId}:categories {scoreIncrement} {member}
            await db.SortedSetIncrementAsync(key, member, scoreIncrement);

            // Keep key active for 60 days
            await db.KeyExpireAsync(key, TimeSpan.FromDays(60));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis TrackUserActionAsync failed for user {UserId}, category {Category}: {Message}", 
                userId, category, ex.Message);
        }
    }

    /// <summary>
    /// Queries the Redis Sorted Set (ZSET) in descending order to find the user's top-scoring category.
    /// Command: ZREVRANGEBYSCORE or ZREVRANGE user:{userId}:categories 0 0 WITHSCORES
    /// </summary>
    public async Task<(string? Category, double Score)> GetTopCategoryForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (null, 0);

        try
        {
            var db = GetDatabase();
            if (db == null) return (null, 0);

            var key = $"user:{userId.Trim().ToLowerInvariant()}:categories";

            // SortedSetRangeByRankWithScoresAsync: index 0 to 0 with Order.Descending gets the #1 element
            var topEntries = await db.SortedSetRangeByRankWithScoresAsync(key, start: 0, stop: 0, order: Order.Descending);

            if (topEntries != null && topEntries.Length > 0)
            {
                var entry = topEntries[0];
                if (entry.Score > 0 && !entry.Element.IsNullOrEmpty)
                {
                    return (entry.Element.ToString(), entry.Score);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis GetTopCategoryForUserAsync failed for user {UserId}: {Message}", userId, ex.Message);
        }

        return (null, 0);
    }

    /// <summary>
    /// Returns all category scores for a user ranked from highest to lowest.
    /// </summary>
    public async Task<List<(string Category, double Score)>> GetAllCategoryScoresAsync(string userId)
    {
        var result = new List<(string Category, double Score)>();
        if (string.IsNullOrWhiteSpace(userId)) return result;

        try
        {
            var db = GetDatabase();
            if (db == null) return result;

            var key = $"user:{userId.Trim().ToLowerInvariant()}:categories";
            var entries = await db.SortedSetRangeByRankWithScoresAsync(key, start: 0, stop: -1, order: Order.Descending);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (!entry.Element.IsNullOrEmpty)
                    {
                        result.Add((entry.Element.ToString()!, entry.Score));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis GetAllCategoryScoresAsync failed for user {UserId}: {Message}", userId, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Last 5 Searches Logging:
    /// Uses Redis List (LIST) to maintain an ordered, deduplicated history capped at 5 items.
    /// Redis Structure:
    ///   Key:   user:{userId}:recent_searches
    /// Steps:
    ///   1. LREM key 0 {query}    (Remove duplicate if it already exists)
    ///   2. LPUSH key {query}     (Prepend to head of list)
    ///   3. LTRIM key 0 4         (Cap maximum length at 5 items)
    /// </summary>
    public async Task SaveSearchQueryAsync(string userId, string query)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(query))
            return;

        var cleanQuery = query.Trim();
        try
        {
            var db = GetDatabase();
            if (db == null) return;

            var key = $"user:{userId.Trim().ToLowerInvariant()}:recent_searches";

            // 1. Remove duplicate (LREM)
            await db.ListRemoveAsync(key, cleanQuery, 0);

            // 2. Prepend to front (LPUSH)
            await db.ListLeftPushAsync(key, cleanQuery);

            // 3. Trim to last 5 entries (LTRIM 0 4)
            await db.ListTrimAsync(key, 0, 4);

            // 4. Set 30-day retention
            await db.KeyExpireAsync(key, TimeSpan.FromDays(30));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis SaveSearchQueryAsync failed for user {UserId}: {Message}", userId, ex.Message);
        }
    }

    /// <summary>
    /// Retrieves user's recent searches from Redis List (LRANGE 0 4).
    /// </summary>
    public async Task<List<string>> GetRecentSearchesAsync(string userId, int count = 5)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(userId)) return result;

        try
        {
            var db = GetDatabase();
            if (db == null) return result;

            var key = $"user:{userId.Trim().ToLowerInvariant()}:recent_searches";
            var values = await db.ListRangeAsync(key, 0, Math.Max(0, count - 1));

            if (values != null)
            {
                foreach (var val in values)
                {
                    if (!val.IsNullOrEmpty)
                    {
                        result.Add(val.ToString()!);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis GetRecentSearchesAsync failed for user {UserId}: {Message}", userId, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Clears user's recent search list in Redis.
    /// </summary>
    public async Task ClearRecentSearchesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            var db = GetDatabase();
            if (db == null) return;

            var key = $"user:{userId.Trim().ToLowerInvariant()}:recent_searches";
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redis ClearRecentSearchesAsync failed for user {UserId}: {Message}", userId, ex.Message);
        }
    }

    private static string NormalizeCategoryName(string raw)
    {
        var lower = raw.Trim().ToLowerInvariant();
        if (lower.Contains("pooja") || lower.Contains("agarbatti") || lower.Contains("dhoop")) return "pooja";
        if (lower.Contains("snack") || lower.Contains("biscuit") || lower.Contains("namkeen") || lower.Contains("beverage")) return "snacks";
        if (lower.Contains("dairy") || lower.Contains("milk") || lower.Contains("butter") || lower.Contains("egg")) return "dairy";
        if (lower.Contains("atta") || lower.Contains("dal") || lower.Contains("rice") || lower.Contains("flour")) return "staples";
        if (lower.Contains("oil") || lower.Contains("ghee") || lower.Contains("masala")) return "oil_ghee";
        return lower;
    }
}
