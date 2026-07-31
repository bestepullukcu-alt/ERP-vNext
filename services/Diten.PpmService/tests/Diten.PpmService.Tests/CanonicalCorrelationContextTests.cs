using Diten.PpmService.Infrastructure.Correlation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class CanonicalCorrelationContextTests
{
    [Fact]
    public void Canonical_non_empty_guid_header_is_preserved_for_entire_request()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] =
            expected.ToString("D");
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
    public void Missing_malformed_empty_or_non_D_header_generates_one_server_guid(
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
}
