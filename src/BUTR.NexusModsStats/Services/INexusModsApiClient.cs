using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Options;
using BUTR.NexusModsStats.Utils;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BUTR.NexusModsStats.Services;

public interface INexusModsApiClient
{
    Task<NexusModsModInfoResponse?> GetModAsync(string gameDomain, string modId, CancellationToken ct);
}

public sealed partial class NexusModsApiClient : INexusModsApiClient
{
    // The API key never changes at runtime, so hash it once instead of on every request
    private static readonly ConcurrentDictionary<string, string> HashedApiKeys = new();

    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly StripedAsyncLock _locks;
    private readonly NexusModsOptions _options;

    public NexusModsApiClient(ILogger<NexusModsApiClient> logger, HttpClient httpClient, IDistributedCache cache, StripedAsyncLock locks, IOptions<NexusModsOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _cache = cache;
        _locks = locks;
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

    public async Task<NexusModsModInfoResponse?> GetModAsync(string gameDomain, string modId, CancellationToken ct)
    {
        var url = $"/v1/games/{gameDomain}/mods/{modId}.json";
        var apiKeyKey = HashedApiKeys.GetOrAdd(_options.ApiKey, HashString);
        var key = $"{url}{apiKeyKey}";

        var json = await _httpClient.GetStringWithCacheAsync(_cache, _locks, _logger, url, key, ct);
        if (json is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, NexusModsApiClientJsonSerializerContext.Default.NexusModsModInfoResponse);
        }
        catch (JsonException e)
        {
            _logger.LogError(e, "Failed to deserialize Nexus Mods API response for '{Url}'", url);
            return null;
        }
    }

    [JsonSerializable(typeof(NexusModsModInfoResponse))]
    public partial class NexusModsApiClientJsonSerializerContext : JsonSerializerContext;
}