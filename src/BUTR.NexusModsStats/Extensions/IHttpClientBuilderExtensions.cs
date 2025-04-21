using Microsoft.Extensions.Http.Resilience;

using Polly;

using System.Net;
using System.Net.Http.Headers;

namespace BUTR.NexusModsStats.Extensions;

public static class IHttpClientBuilderExtensions
{
    public static IHttpStandardResiliencePipelineBuilder AddNexusModsResilienceHandler(this IHttpClientBuilder builder) => builder.AddStandardResilienceHandler(options =>
    {
        options.Retry = new HttpRetryStrategyOptions
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

            DelayGenerator = static args =>
            {
                if (args.Outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests } response)
                    return ValueTask.FromResult<TimeSpan?>(null);

                var delay = GetRateLimitDelay(response.Headers, "X-RL-Daily-Remaining", "X-RL-Daily-Reset", TimeSpan.FromDays(1)) ??
                            GetRateLimitDelay(response.Headers, "X-RL-Hourly-Remaining", "X-RL-Hourly-Reset", TimeSpan.FromHours(1)) ??
                            TimeSpan.FromSeconds(1);

                return ValueTask.FromResult<TimeSpan?>(delay);
            },
        };
    });

    private static TimeSpan? GetRateLimitDelay(HttpResponseHeaders headers, string remainingKey, string resetKey, TimeSpan maxDelay)
    {
        if (headers.TryGetValues(remainingKey, out var rem) && int.TryParse(rem.FirstOrDefault(), out var remVal) && remVal == 0 &&
            headers.TryGetValues(resetKey, out var res) && DateTime.TryParse(res.FirstOrDefault(), out var resTime))
        {
            var delay = resTime - DateTime.UtcNow;
            return delay > TimeSpan.Zero && delay < maxDelay ? delay : null;
        }

        return null;
    }

    public static IHttpStandardResiliencePipelineBuilder AddCustomResilienceHandler(this IHttpClientBuilder builder) => builder.AddStandardResilienceHandler(options =>
    {
        options.Retry = new HttpRetryStrategyOptions
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
    });
}