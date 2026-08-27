using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// BL-294 — the access token no longer fits in one cookie, and six call sites did not know.
///
/// <para>A browser cookie caps out well below the length of a real access token (the tenant token is past 3800
/// characters with today's claim set), so <see cref="AuthTokenCookies.Append"/> splits it: the base cookie holds
/// the literal marker <c>chunks-N</c> and the token lives in <c>access_tokenC1..CN</c>. Anything that reads
/// <c>Request.Cookies["access_token"]</c> directly therefore gets the COUNTER, not the token, and sends
/// <c>Bearer chunks-4</c> to the gateway.</para>
///
/// <para>The first test is the defect itself, written down so nobody has to rediscover it; the rest are the
/// contract the six fixed call sites now depend on.</para>
/// </summary>
public sealed class ChunkedAccessTokenTests
{
    // Comfortably past the 3800-character chunk size, so this token is written as four chunks — the exact shape
    // every login produces today.
    private static readonly string LongToken = new('a', 3800 * 3 + 17);

    [Fact]
    public void A_direct_cookie_read_of_a_chunked_token_yields_the_COUNTER_not_the_token()
    {
        var request = ChunkedRequest(LongToken);

        // This is what the six broken call sites were sending as their bearer credential.
        Assert.Equal("chunks-4", request.Cookies[AuthTokenCookies.AccessTokenCookie]);
        Assert.NotEqual(LongToken, request.Cookies[AuthTokenCookies.AccessTokenCookie]);
    }

    [Fact]
    public void GetAccessToken_reassembles_the_chunks_into_the_original_token()
    {
        var request = ChunkedRequest(LongToken);

        Assert.Equal(LongToken, AuthTokenCookies.GetAccessToken(request));
    }

    [Fact]
    public void GetAccessToken_returns_a_short_token_unchanged()
    {
        // The fix must work in BOTH shapes: a token under the chunk size is still written as a single cookie.
        var request = ChunkedRequest("short.token.value");

        Assert.Equal("short.token.value", AuthTokenCookies.GetAccessToken(request));
    }

    [Fact]
    public void A_missing_chunk_yields_no_token_rather_than_a_corrupt_one()
    {
        /*
         * A half-written cookie jar (an eviction, a partially cleared session) must read as "no token" so the
         * caller falls through its no-token path. Concatenating whatever chunks survived would send a mangled
         * credential and turn a session problem into an unexplained 401.
         */
        var request = ChunkedRequest(LongToken);
        request.Cookies = new FakeCookieCollection(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AuthTokenCookies.AccessTokenCookie] = "chunks-4",
                [AuthTokenCookies.AccessTokenCookie + "C1"] = "aaa",
                [AuthTokenCookies.AccessTokenCookie + "C2"] = "bbb"
                // C3 and C4 are gone.
            });

        Assert.Null(AuthTokenCookies.GetAccessToken(request));
    }

    /// <summary>Writes <paramref name="token"/> through the real Append, then hands the produced cookies back as a request.</summary>
    private static HttpRequest ChunkedRequest(string token)
    {
        var writeContext = new DefaultHttpContext();
        AuthTokenCookies.Append(writeContext.Response, AuthTokenCookies.AccessTokenCookie, token, new CookieOptions());

        var jar = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in writeContext.Response.Headers.SetCookie)
        {
            var pair = (header ?? string.Empty).Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator > 0)
            {
                jar[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        var readContext = new DefaultHttpContext();
        readContext.Request.Cookies = new FakeCookieCollection(jar);
        return readContext.Request;
    }

    private sealed class FakeCookieCollection : IRequestCookieCollection
    {
        private readonly IDictionary<string, string> _values;

        public FakeCookieCollection(IDictionary<string, string> values) => _values = values;

        public string? this[string key] => _values.TryGetValue(key, out var value) ? value : null;

        public int Count => _values.Count;

        public ICollection<string> Keys => _values.Keys;

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGetValue(string key, out string? value)
        {
            var found = _values.TryGetValue(key, out var raw);
            value = raw;
            return found;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
