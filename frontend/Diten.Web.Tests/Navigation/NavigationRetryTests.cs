using System.Net;
using Diten.Web.Services.Http;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// BL-294/nav — the retry that stands between a momentary gateway hiccup and three blank surfaces.
///
/// <para>These are behavioural, not source assertions: the number of attempts is the whole contract, and the
/// only way to know it is to count them. A counting handler is enough — no gateway, no tokens, no HttpContext.</para>
///
/// <para>Three properties, three ways to get them wrong, one test each:</para>
/// <list type="bullet">
/// <item>Too few — the retry is dropped and the momentary failure reaches the user again.</item>
/// <item>Too many — a loop hammers a gateway that is already struggling, and turns a two-second blip into an outage.</item>
/// <item>Wrong trigger — retrying a 401 doubles the load on a decision the server will make again identically.</item>
/// </list>
/// </summary>
public sealed class NavigationRetryTests
{
    [Fact]
    public async Task A_transient_failure_is_retried_exactly_ONCE()
    {
        // 503 then 200: the case the whole fix exists for — a gateway restarting mid-request.
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        using var response = await Send(client, handler);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task A_persistent_failure_stops_after_TWO_attempts_and_never_loops()
    {
        /*
         * The mutation this exists for is "make the retry infinite". A handler that returns 503 forever would
         * hang or exhaust the gateway under a loop; here it must be asked exactly twice and the second answer
         * handed back to the caller, which is what turns into the user-visible warning.
         */
        var handler = new CountingHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);

        using var response = await Send(client, handler);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task A_transport_fault_is_retried_once_too()
    {
        // Connection refused / reset — the shape of "the service is down", and the most retry-worthy case there is.
        var handler = new CountingHandler(HttpStatusCode.OK) { ThrowOnFirstAttempt = true };
        using var client = new HttpClient(handler);

        using var response = await Send(client, handler);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task A_decided_answer_is_NOT_retried(HttpStatusCode status)
    {
        /*
         * 401/403/404 are decisions, not faults. The same token a millisecond later gets the same answer, so a
         * retry buys nothing and costs a second round trip on every failing page render. 200 obviously stands.
         */
        var handler = new CountingHandler(status, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        using var response = await Send(client, handler);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task A_cancelled_request_is_never_retried()
    {
        // Cancellation is the caller giving up (the user navigated away), not the server failing. It must
        // propagate on the FIRST attempt rather than being mistaken for a transport fault and retried.
        var handler = new CountingHandler(HttpStatusCode.OK) { CancelOnFirstAttempt = true };
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            NavigationRetry.SendOnceMoreOnTransientAsync(client, handler.NewRequest, cts.Token));

        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task Each_attempt_gets_a_FRESH_request_message()
    {
        /*
         * An HttpRequestMessage cannot be sent twice — reusing one throws InvalidOperationException on the second
         * send, which would turn the retry into a different failure instead of a recovery. The factory shape is
         * what prevents that, so it is asserted rather than assumed.
         */
        var handler = new CountingHandler(HttpStatusCode.BadGateway, HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        using var response = await Send(client, handler);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.SeenRequests.Distinct().Count());
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, CountingHandler handler) =>
        NavigationRetry.SendOnceMoreOnTransientAsync(client, handler.NewRequest, CancellationToken.None);

    private sealed class CountingHandler : HttpMessageHandler
    {
        private const int AttemptCap = 5;

        private readonly HttpStatusCode[] _statuses;

        public CountingHandler(params HttpStatusCode[] statuses) => _statuses = statuses;

        public int Attempts { get; private set; }

        public bool ThrowOnFirstAttempt { get; init; }

        public bool CancelOnFirstAttempt { get; init; }

        public List<HttpRequestMessage> SeenRequests { get; } = new();

        public HttpRequestMessage NewRequest() => new(HttpMethod.Get, "http://gateway.test/api/platform/navigation/menu");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            SeenRequests.Add(request);

            /*
             * A hard stop, so an infinite-retry regression fails FAST and RED instead of hanging the suite.
             * Measured: with a `while (true)` in NavigationRetry, the persistent-failure test below never
             * returned and the whole run had to be killed by hand — a timeout in CI reads as "flaky
             * infrastructure", not as "someone turned the retry into a loop". The cap is well above the two
             * attempts the contract allows, so it can only ever be hit by a genuine loop.
             */
            if (Attempts > AttemptCap)
            {
                throw new InvalidOperationException(
                    $"NavigationRetry sent more than {AttemptCap} requests — the retry has become a loop.");
            }

            if (ThrowOnFirstAttempt && Attempts == 1)
            {
                throw new HttpRequestException("connection refused");
            }

            if (CancelOnFirstAttempt && Attempts == 1)
            {
                throw new OperationCanceledException();
            }

            // Past the end of the script, keep answering with the last status — a loop would run forever
            // against a real gateway, so it must not run forever here either.
            var status = _statuses[Math.Min(Attempts - 1, _statuses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
