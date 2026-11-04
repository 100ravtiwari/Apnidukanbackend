namespace Dukan.API.Services;

/// <summary>
/// Service contract for real-time Redis-backed user profiling, category scoring (ZSET),
/// and recent search history (LIST).
/// </summary>
public interface IRedisPersonaService
{
    /// <summary>
    /// Tracks an action performed by a user (view, cart addition, checkout) and increments
    /// the user's category score in Redis using a Sorted Set (ZSET).
    /// Redis command: ZINCRBY user:{userId}:categories {scoreIncrement} {category}
    /// </summary>
    Task TrackUserActionAsync(string userId, string category, double scoreIncrement = 1.0);

    /// <summary>
    /// Retrieves user's highest scoring category from Redis Sorted Set.
    /// Redis command: ZREVRANGE user:{userId}:categories 0 0 WITHSCORES
    /// </summary>
    Task<(string? Category, double Score)> GetTopCategoryForUserAsync(string userId);

    /// <summary>
    /// Retrieves all category scores for a user ordered by descending score.
    /// </summary>
    Task<List<(string Category, double Score)>> GetAllCategoryScoresAsync(string userId);

    /// <summary>
    /// Logs recent search query using Redis List (LREM + LPUSH + LTRIM to 5 items).
    /// </summary>
    Task SaveSearchQueryAsync(string userId, string query);

    /// <summary>
    /// Retrieves the last 5 recent searches for user from Redis List (LRANGE 0 4).
    /// </summary>
    Task<List<string>> GetRecentSearchesAsync(string userId, int count = 5);

    /// <summary>
    /// Clears user's recent search history in Redis.
    /// </summary>
    Task ClearRecentSearchesAsync(string userId);
}

