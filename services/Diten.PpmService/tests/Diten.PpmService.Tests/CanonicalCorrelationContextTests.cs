using Diten.PpmService.Infrastructure.Correlation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class CanonicalCorrelationContextTests
{
    [Theory]
    [InlineData("D")]
    [InlineData("N")]
    public void Canonical_non_empty_D_or_N_guid_header_is_preserved_for_entire_request(
        string format)
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] =
            expected.ToString(format);
        var context = new CanonicalCorrelationContext(
            new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal(expected, context.CorrelationId);
        Assert.Equal(expected, context.CorrelationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{01234567-89ab-cdef-0123-456789abcdef}")]
    [InlineData("(01234567-89ab-cdef-0123-456789abcdef)")]
    [InlineData(" 01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef ")]
    [InlineData("01234567_89ab_cdef_0123_456789abcdef")]
    [InlineData("safe-but-not-a-guid")]
    public void Missing_malformed_empty_or_non_canonical_header_generates_one_server_guid(
        string incoming)
    {
        var httpContext = new DefaultHttpContext();
        if (incoming.Length != 0)
        {
            httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] = incoming;
        }

        var context = new CanonicalCorrelationContext(
            new HttpContextAccessor { HttpContext = httpContext });

        var first = context.CorrelationId;
        var second = context.CorrelationId;

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Background_scope_generates_one_non_empty_guid()
    {
        var context = new CanonicalCorrelationContext(new HttpContextAccessor());

        var first = context.CorrelationId;

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, context.CorrelationId);
    }

    [Fact]
    public async Task Concurrent_reads_within_scope_resolve_one_stable_id()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] =
            expected.ToString("N");
        var context = new CanonicalCorrelationContext(
            new HttpContextAccessor { HttpContext = httpContext });

        var reads = await Task.WhenAll(Enumerable.Range(0, 128)
            .Select(_ => Task.Run(() => context.CorrelationId)));

        Assert.All(reads, value => Assert.Equal(expected, value));
        Assert.Equal(expected.ToString("D"), reads[0].ToString("D"));
    }

    [Fact]
    public void Invalid_input_fallback_is_stable_but_isolated_between_scopes()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] = "invalid";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var firstScope = new CanonicalCorrelationContext(accessor);
        var secondScope = new CanonicalCorrelationContext(accessor);

        var first = firstScope.CorrelationId;

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, firstScope.CorrelationId);
        Assert.NotEqual(first, secondScope.CorrelationId);
    }
}
