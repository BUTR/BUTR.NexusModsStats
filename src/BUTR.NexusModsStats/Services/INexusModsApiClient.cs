using BUTR.NexusModsStats.Models;

using Microsoft.Extensions.Caching.Distributed;

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
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _expiration = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public NexusModsApiClient(HttpClient httpClient, IDistributedCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
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
        var apiKey = _httpClient.DefaultRequestHeaders.TryGetValues("apikey", out var apiKeys) ? apiKeys.First() : throw new Exception("Missing apikey");
        return GetCachedWithTimeLimitAsync<NexusModsModInfoResponse?>(
            $"/v1/games/{gameDomain}/mods/{modId}.json", apiKey,
            NexusModsApiClientJsonSerializerContext.Default.NexusModsModInfoResponse, ct);
    }

    private async Task<TResponse?> GetCachedWithTimeLimitAsync<TResponse>(string url, string apiKey, JsonTypeInfo<TResponse?> typeInfo, CancellationToken ct) where TResponse : class?
    {
        var apiKeyKey = HashString(apiKey);
        var key = $"{url}{apiKeyKey}";

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        try
        {
            if (await _cache.GetStringAsync(key, token: ct) is { } json)
            {
                if (typeof(TResponse) == typeof(string))
                    return Unsafe.As<TResponse>(json);

                return JsonSerializer.Deserialize(json, typeInfo);
            }

            try
            {
                await semaphore.WaitAsync(ct);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return null;

                json = await response.Content.ReadAsStringAsync(ct);
                await _cache.SetStringAsync(key, json, _expiration, token: ct);

                if (typeof(TResponse) == typeof(string))
                    return Unsafe.As<TResponse>(json);

                return JsonSerializer.Deserialize(json, typeInfo);
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (Exception)
        {
            await _cache.RemoveAsync(key, ct);
            return null;
        }
    }

    [JsonSerializable(typeof(NexusModsModInfoResponse))]
    public partial class NexusModsApiClientJsonSerializerContext : JsonSerializerContext;
}