using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BUTR.NexusModsStats.Services;

public interface INexusModsApiClient
{
    Task<NexusModsModInfoResponse?> GetModAsync(string gameDomain, string modId, CancellationToken ct);
}

public sealed partial class NexusModsApiClient : INexusModsApiClient
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly NexusModsOptions _options;
    private readonly DistributedCacheEntryOptions _expiration = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public NexusModsApiClient(ILogger<NexusModsApiClient> logger, HttpClient httpClient, IDistributedCache cache, IOptionsSnapshot<NexusModsOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
    }

    private static string HashString(string value)
    {
        Span<byte> data2 = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
        Encoding.UTF8.GetBytes(value, data2);
        Span<byte> data = stackalloc byte[64];
        SHA512.HashData(data2, data);
        return Convert.ToBase64String(data);
    }

    public Task<NexusModsModInfoResponse?> GetModAsync(string gameDomain, string modId, CancellationToken ct)
    {
        return GetCachedWithTimeLimitAsync<NexusModsModInfoResponse?>(
            $"/v1/games/{gameDomain}/mods/{modId}.json", _options.ApiKey,
            NexusModsApiClientJsonSerializerContext.Default.NexusModsModInfoResponse, ct);
    }


    private async Task<TResponse?> GetCachedWithTimeLimitAsync<TResponse>(string url, string apiKey, JsonTypeInfo<TResponse?> typeInfo, CancellationToken ct) where TResponse : class?
    {
        var apiKeyKey = HashString(apiKey);
        var key = $"{url}{apiKeyKey}";

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        var cachedJson = default(string?);
        var cachedValue = default(TResponse?);

        try
        {
            cachedJson = await _cache.GetStringAsync(key, ct);
            if (cachedJson is not null)
            {
                if (typeof(TResponse) == typeof(string))
                    return Unsafe.As<TResponse>(cachedJson);

                cachedValue = JsonSerializer.Deserialize(cachedJson, typeInfo);
            }

            await semaphore.WaitAsync(ct);

            // Another thread might have updated the cache, so check again
            var freshCachedJson = await _cache.GetStringAsync(key, ct);
            if (freshCachedJson is not null && freshCachedJson != cachedJson)
            {
                if (typeof(TResponse) == typeof(string))
                    return Unsafe.As<TResponse>(freshCachedJson);

                return JsonSerializer.Deserialize(freshCachedJson, typeInfo);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Prolong TTL if we have a cached value
                if (cachedJson is not null)
                    await _cache.SetStringAsync(key, cachedJson, _expiration, ct);

                return cachedValue;
            }

            var newJson = await response.Content.ReadAsStringAsync(ct);

            // Avoid deserialization if value is the same
            if (newJson == cachedJson)
            {
                await _cache.SetStringAsync(key, cachedJson, _expiration, ct);
                return cachedValue;
            }

            await _cache.SetStringAsync(key, newJson, _expiration, ct);

            if (typeof(TResponse) == typeof(string))
                return Unsafe.As<TResponse>(newJson);

            return JsonSerializer.Deserialize(newJson, typeInfo);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get Nexus Mods API response");

            if (cachedJson is not null)
                await _cache.SetStringAsync(key, cachedJson, _expiration, ct);

            return cachedValue;
        }
        finally
        {
            semaphore.Release();
        }
    }

    [JsonSerializable(typeof(NexusModsModInfoResponse))]
    public partial class NexusModsApiClientJsonSerializerContext : JsonSerializerContext;
}