using System;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace HoYoShadeHub.Helpers;

/// <summary>
/// Helper for detecting GitHub API rate limit (HTTP 403 / 429) errors.
/// </summary>
public static class GitHubRateLimitHelper
{
    /// <summary>
    /// Checks if the given exception is caused by GitHub rate limit (HTTP 403 Forbidden or HTTP 429 Too Many Requests).
    /// </summary>
    /// <param name="ex">The exception to check.</param>
    /// <returns>True if the exception indicates rate limit was exceeded; otherwise, false.</returns>
    public static bool IsRateLimitExceeded(Exception? ex)
    {
        if (ex == null)
        {
            return false;
        }

        if (ex is AggregateException aggEx && aggEx.InnerExceptions.Count > 0)
        {
            return aggEx.InnerExceptions.Any(IsRateLimitExceeded);
        }

        if (ex.InnerException != null && IsRateLimitExceeded(ex.InnerException))
        {
            return true;
        }

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode == HttpStatusCode.Forbidden || httpEx.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return true;
            }
        }

        return ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase);
    }
}
