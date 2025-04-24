using BUTR.NexusModsStats.Models;
using BUTR.NexusModsStats.Utils;

using nietras.SeparatedValues;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace BUTR.NexusModsStats.Services;

public interface INexusModsStatisticsClient
{
    IAsyncEnumerable<LiveStatisticsEntry> GetLiveDownloadCountsAsync(string gameId, CancellationToken ct);
}

public sealed class NexusModsStatisticsClient : INexusModsStatisticsClient
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public NexusModsStatisticsClient(ILogger<NexusModsStatisticsClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<LiveStatisticsEntry> GetLiveDownloadCountsAsync(string gameId, [EnumeratorCancellation] CancellationToken ct)
    {
        using var composableDispose = new ComposableDisposable();

        var semaphore = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        Stream responseStream;
        try
        {
            await semaphore.WaitAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"live_download_counts/mods/{gameId}.csv");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = composableDispose.Add(await _httpClient.SendAsync(request, ct));
            if (!response.IsSuccessStatusCode) yield break;

            responseStream = composableDispose.Add(await response.Content.ReadAsStreamAsync(ct));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get live download counts");
            yield break;
        }
        finally
        {
            semaphore.Release();
        }

        using var reader = await Sep.Reader().FromAsync(responseStream, ct);
        foreach (var readRow in reader)
        {
            var modId = readRow[0].Span.ToString();
            var totalDownloads = readRow[1].Parse<int>();
            var uniqueDownloads = readRow[2].Parse<int>();
            var totalViews = readRow[3].Parse<int>();
            yield return new LiveStatisticsEntry(modId, totalDownloads, uniqueDownloads, totalViews);
        }
    }
}