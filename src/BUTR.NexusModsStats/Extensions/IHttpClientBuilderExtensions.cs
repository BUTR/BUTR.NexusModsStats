using Microsoft.Extensions.Http.Resilience;

using Polly;

using System.Net;
using System.Net.Http.Headers;

namespace BUTR.NexusModsStats.Extensions;

public static class IHttpClientBuilderExtensions
{
    /// <summary>
    /// The standard retry strategy plus a delay generator that honors the NexusMods rate-limit reset headers.
    /// </summary>
    public static IHttpStandardResiliencePipelineBuilder AddNexusModsResilienceHandler(this IHttpClientBuilder builder) => builder.AddStandardResilienceHandler(options =>
    {
        options.Retry = CreateRetryOptions();
        options.Retry.DelayGenerator = static args =>
        {
            if (args.Outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests } response)
                return ValueTask.FromResult<TimeSpan?>(null);

            var delay = IsQuotaExhausted(response.Headers, "X-RL-Daily-Remaining")
                ? GetRateLimitDelay(response.Headers, "X-RL-Hourly-Remaining", "X-RL-Hourly-Reset", TimeSpan.FromHours(1)) ?? TimeSpan.FromSeconds(1)
                : TimeSpan.FromSeconds(1);

            return ValueTask.FromResult<TimeSpan?>(delay);
        };
    });

    public static IHttpStandardResiliencePipelineBuilder AddCustomResilienceHandler(this IHttpClientBuilder builder) => builder.AddStandardResilienceHandler(options =>
    {
        options.Retry = CreateRetryOptions();
    });

    private static HttpRetryStrategyOptions CreateRetryOptions() => new()
    {
        MaxRetryAttempts = 5,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(1),

        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(response => response.StatusCode
                is >= HttpStatusCode.InternalServerError
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
            ),
    };

    private static bool IsQuotaExhausted(HttpResponseHeaders headers, string remainingKey) =>
        headers.TryGetValues(remainingKey, out var rem) && int.TryParse(rem.FirstOrDefault(), out var remVal) && remVal == 0;

    private static TimeSpan? GetRateLimitDelay(HttpResponseHeaders headers, string remainingKey, string resetKey, TimeSpan maxDelay)
    {
        if (IsQuotaExhausted(headers, remainingKey) &&
            headers.TryGetValues(resetKey, out var res) && DateTime.TryParse(res.FirstOrDefault(), out var resTime))
        {
            var delay = resTime - DateTime.UtcNow;
            return delay > TimeSpan.Zero && delay < maxDelay ? delay : null;
        }

        return null;
    }
}