using System.Net;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class VerifiedGskuDelegatedTokenForwardingTests
{
    [Fact]
    public async Task DelegatedBearerAndResolverCredential_ArePerRequestAndTenantHeaderIsNeverForwarded()
    {
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"reason_code\":\"REFERENCE_PROVIDER_UNAVAILABLE\"}")
        });
        var context = PlatformVerifiedGskuResolverClientTests.CreateAuthenticatedContext("sensitive-jwt");
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        await PlatformVerifiedGskuResolverClientTests.CreateClient(handler, context)
            .ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("Bearer sensitive-jwt", sent.Authorization);
        Assert.Equal("resolver-id", Assert.Single(sent.Headers["X-Verified-Gsku-Credential-Id"]));
        Assert.Equal("resolver-secret", Assert.Single(sent.Headers["X-Verified-Gsku-Credential"]));
        Assert.Equal("VERIFIED_GSKU_RESOLVE", Assert.Single(sent.Headers["X-Verified-Gsku-Audience"]));
        Assert.DoesNotContain("X-Tenant-Id", sent.Headers.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive-jwt", sent.Body ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("resolver-secret", sent.Body ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingDelegatedBearer_FailsBeforeHttpDispatch()
    {
        var handler = new ResolverRecordingHandler(_ => throw new InvalidOperationException("must not dispatch"));
        var context = PlatformVerifiedGskuResolverClientTests.CreateAuthenticatedContext();
        context.Request.Headers.Remove("Authorization");

        var result = await PlatformVerifiedGskuResolverClientTests.CreateClient(handler, context)
            .ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.Equal("REFERENCE_UNAUTHENTICATED", result.FailureCode);
        Assert.Empty(handler.Requests);
    }
}
