using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Utils;

using Microsoft.Extensions.Caching.Distributed;

using nietras.SeparatedValues;

namespace BUTR.NexusModsStats.Services;

public interface INexusModsStatisticsClient
{
    Task<LiveStatisticsEntry?> GetLiveDownloadCountsAsync(string gameId, string modId, CancellationToken ct);
}

public sealed class NexusModsStatisticsClient : INexusModsStatisticsClient
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly StripedAsyncLock _locks;

    public NexusModsStatisticsClient(ILogger<NexusModsStatisticsClient> logger, HttpClient httpClient, IDistributedCache cache, StripedAsyncLock locks)
    {
        _logger = logger;
        _httpClient = httpClient;
        _cache = cache;
        _locks = locks;
    }

    public async Task<LiveStatisticsEntry?> GetLiveDownloadCountsAsync(string gameId, string modId, CancellationToken ct)
    {
        var url = $"live_download_counts/mods/{gameId}.csv";

        // The whole per-game CSV is cached so distinct mods of the same game share one upstream fetch
        var csv = await _httpClient.GetStringWithCacheAsync(_cache, _locks, _logger, url, $"livestats:{gameId}", ct);
        if (csv is null)
            return null;

        try
        {
            // The CSV is raw data without a header row: "modId,totalDownloads,uniqueDownloads,totalViews"
            using var reader = Sep.Reader(o => o with { HasHeader = false, Sep = Sep.New(',') }).FromText(csv);
            foreach (var readRow in reader)
            {
                if (readRow.ColCount < 4 || !readRow[0].Span.SequenceEqual(modId))
                    continue;

                if (int.TryParse(readRow[1].Span, out var totalDownloads) &&
                    int.TryParse(readRow[2].Span, out var uniqueDownloads) &&
                    int.TryParse(readRow[3].Span, out var totalViews))
                    return new LiveStatisticsEntry(modId, totalDownloads, uniqueDownloads, totalViews);

                _logger.LogWarning("Malformed live download counts row for game '{GameId}', mod '{ModId}'", gameId, modId);
                return null;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse live download counts for game '{GameId}'", gameId);
        }

        return null;
    }
}