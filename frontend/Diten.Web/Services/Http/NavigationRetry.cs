using System.Net;

namespace Diten.Web.Services.Http;

/*
 * BL-294/nav — SECOND LINE OF DEFENCE for the navigation endpoint.
 *
 * When GET /api/platform/navigation/menu fails, three consumers go blank AT ONCE — the sidebar, the Ctrl+K
 * palette and (to a lesser degree) the profile card. 075de7dc closed the token-refresh cause, but the endpoint
 * can still fall over for reasons that have nothing to do with tokens: a dropped connection, a gateway restart,
 * a service that is down for two seconds during a deploy. Those are momentary, and a second attempt usually
 * succeeds — so the owner's decision is: retry ONCE silently, and if that also fails, SAY SO.
 *
 * Three properties of this helper are load-bearing, and each has a mutation test guarding it:
 *
 *   1. EXACTLY ONE extra attempt. Not a loop, not a policy with a retry count, not "until it works". Two
 *      attempts total, always. A retry storm against an already-struggling gateway is worse than a blank menu.
 *   2. NO DELAY between them. This runs inside the request that renders the page; a backoff sleep would make
 *      every genuine outage add its wait to the user's page load. The retry is free or it does not happen.
 *   3. TRANSIENT FAILURES ONLY. A 401/403/404 is a decision the server already made and will make again with
 *      the same token a millisecond later — retrying it just doubles the load and proves nothing. Only
 *      transport faults, timeouts, 408, 429 and 5xx are worth a second look.
 */
public static class NavigationRetry
{
    /// <summary>
    /// Sends the request built by <paramref name="requestFactory"/>, retrying at most ONCE on a transient
    /// failure. The factory is called per attempt because an <see cref="HttpRequestMessage"/> cannot be sent
    /// twice. Cancellation is never retried — it is the caller giving up, not the server failing.
    /// </summary>
    public static async Task<HttpResponseMessage> SendOnceMoreOnTransientAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        HttpResponseMessage? firstResponse = null;

        try
        {
            using var first = requestFactory();
            firstResponse = await client.SendAsync(first, ct);
            if (!IsTransient(firstResponse.StatusCode))
            {
                return firstResponse;
            }
        }
        catch (OperationCanceledException)
        {
            // The caller abandoned the request (navigation away, request abort). Not a server failure.
            throw;
        }
        catch (HttpRequestException)
        {
            // Transport-level fault — connection refused, reset, DNS. The single most retry-worthy case.
        }

        ct.ThrowIfCancellationRequested();
        firstResponse?.Dispose();

        // THE one and only retry. Anything that fails here surfaces to the caller, which shows the warning.
        using var second = requestFactory();
        return await client.SendAsync(second, ct);
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout       // 408
        || status == HttpStatusCode.TooManyRequests   // 429
        || (int)status >= 500;                        // 5xx — gateway down, restarting, or throwing
}
