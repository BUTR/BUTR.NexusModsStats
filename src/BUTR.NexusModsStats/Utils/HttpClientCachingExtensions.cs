using Microsoft.Extensions.Caching.Distributed;

using System.Globalization;
using System.Net.Http.Headers;

namespace BUTR.NexusModsStats.Utils;

/// <summary>
/// Fetches a string payload over HTTP with a distributed-cache layer:
/// a fresh cache entry is served without hitting the upstream, a stale entry
/// triggers a refetch, and on upstream failure the stale entry keeps being served.
/// Cache failures are swallowed so a Redis outage degrades to uncached upstream fetches.
/// Entries are stored as "{unixMilliseconds}:{payload}" so no extra JSON metadata type is needed.
/// </summary>
public static class HttpClientCachingExtensions
{
    private static readonly TimeSpan FreshDuration = TimeSpan.FromMinutes(5);
    private static readonly DistributedCacheEntryOptions Expiration = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) };

    public static async Task<string?> GetStringWithCacheAsync(
        this HttpClient httpClient, IDistributedCache cache, StripedAsyncLock locks, ILogger logger,
        string requestUri, string cacheKey, CancellationToken ct)
    {
        if (TryParseEntry(await TryCacheGetAsync(cache, cacheKey, logger, ct), out var stalePayload, out var fetchedAt) && IsFresh(fetchedAt))
            return stalePayload;

        using var _ = await locks.AcquireAsync(cacheKey, ct);

        // Another request might have refreshed the entry while we waited
        if (TryParseEntry(await TryCacheGetAsync(cache, cacheKey, logger, ct), out var refreshedPayload, out var refreshedAt))
        {
            if (IsFresh(refreshedAt))
                return refreshedPayload;
            (stalePayload, fetchedAt) = (refreshedPayload, refreshedAt);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Keep the stale value alive so it can be served while the upstream is unavailable;
                // the stale timestamp is preserved so the next request retries the upstream
                if (stalePayload is not null)
                    await TryCacheSetAsync(cache, cacheKey, FormatEntry(stalePayload, fetchedAt), logger, ct);
                return stalePayload;
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            await TryCacheSetAsync(cache, cacheKey, FormatEntry(payload, DateTimeOffset.UtcNow), logger, ct);
            return payload;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get response for '{RequestUri}'", requestUri);
            return stalePayload;
        }
    }

    private static async Task<string?> TryCacheGetAsync(IDistributedCache cache, string cacheKey, ILogger logger, CancellationToken ct)
    {
        try
        {
            return await cache.GetStringAsync(cacheKey, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Failed to read '{CacheKey}' from the distributed cache", cacheKey);
            return null;
        }
    }

    private static async Task TryCacheSetAsync(IDistributedCache cache, string cacheKey, string entry, ILogger logger, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(cacheKey, entry, Expiration, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Failed to write '{CacheKey}' to the distributed cache", cacheKey);
        }
    }

    private static bool IsFresh(DateTimeOffset fetchedAt) => DateTimeOffset.UtcNow - fetchedAt < FreshDuration;

    private static string FormatEntry(string payload, DateTimeOffset fetchedAt) =>
        $"{fetchedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}:{payload}";

    private static bool TryParseEntry(string? entry, out string? payload, out DateTimeOffset fetchedAt)
    {
        payload = null;
        fetchedAt = default;

        if (entry is null)
            return false;

        var separatorIdx = entry.IndexOf(':');
        if (separatorIdx <= 0 || !long.TryParse(entry.AsSpan(0, separatorIdx), NumberStyles.None, CultureInfo.InvariantCulture, out var unixMs))
            return false;

        payload = entry[(separatorIdx + 1)..];
        fetchedAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        return true;
    }
}