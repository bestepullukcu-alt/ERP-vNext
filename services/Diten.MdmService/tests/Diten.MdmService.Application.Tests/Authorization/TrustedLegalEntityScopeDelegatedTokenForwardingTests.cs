using System.Net;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

public sealed class TrustedLegalEntityScopeDelegatedTokenForwardingTests
{
    [Fact]
    public async Task Bearer_and_dedicated_credential_are_forwarded_but_tenant_scope_headers_and_secrets_are_not_in_body()
    {
        var handler = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("{\"data\":null,\"statusCode\":503,\"isSuccessful\":false,\"errors\":[\"x\"],\"reason_code\":\"x\",\"correlation_id\":\"c\"}", System.Text.Encoding.UTF8, "application/json") });
        var context = PlatformTrustedLegalEntityScopeProviderClientTests.AuthenticatedContext("sensitive-jwt");
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Legal-Entity-Id"] = Guid.NewGuid().ToString("D");
        context.Request.Headers["X-Legal-Entity-Ids"] = Guid.NewGuid().ToString("D");

        await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler, context: context)
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.global-products.read");

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("Bearer sensitive-jwt", sent.Authorization);
        Assert.Equal("scope-id", Assert.Single(sent.Headers["X-Legal-Entity-Scope-Credential-Id"]));
        Assert.Equal("scope-secret", Assert.Single(sent.Headers["X-Legal-Entity-Scope-Credential"]));
        Assert.Equal("TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE", Assert.Single(sent.Headers["X-Legal-Entity-Scope-Audience"]));
        Assert.DoesNotContain("X-Tenant-Id", sent.Headers.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Legal-Entity-Id", sent.Headers.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Legal-Entity-Ids", sent.Headers.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive-jwt", sent.Body!, StringComparison.Ordinal);
        Assert.DoesNotContain("scope-secret", sent.Body!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Basic value")]
    [InlineData("Bearer ")]
    public async Task Missing_or_invalid_bearer_wins_over_invalid_configuration_and_never_dispatches(string authorization)
    {
        var handler = new ScopeRecordingHandler(_ => throw new InvalidOperationException("must not dispatch"));
        var context = PlatformTrustedLegalEntityScopeProviderClientTests.AuthenticatedContext();
        if (authorization.Length == 0) context.Request.Headers.Remove("Authorization");
        else context.Request.Headers.Authorization = authorization;
        var result = await PlatformTrustedLegalEntityScopeProviderClientTests.Client(
                handler, context: context, options: new TrustedLegalEntityScopeProviderOptions())
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.global-products.read");

        Assert.Equal(401, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_UNAUTHENTICATED", result.FailureCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_or_multiple_delegated_bearer_fails_before_dispatch()
    {
        foreach (var header in new[] { "", "Bearer first, Bearer second", "Basic value", "Bearer " })
        {
            var handler = new ScopeRecordingHandler(_ => throw new InvalidOperationException("must not dispatch"));
            var context = PlatformTrustedLegalEntityScopeProviderClientTests.AuthenticatedContext();
            if (header.Length == 0) context.Request.Headers.Remove("Authorization");
            else context.Request.Headers.Authorization = header;
            var result = await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler, context: context)
                .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.global-products.read");
            Assert.Equal(401, result.StatusCode);
            Assert.Empty(handler.Requests);
        }
    }

    [Fact]
    public async Task Separate_requests_forward_only_their_current_bearer()
    {
        var handler = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("{\"data\":null,\"statusCode\":503,\"isSuccessful\":false,\"errors\":[\"x\"],\"reason_code\":\"x\",\"correlation_id\":\"c\"}", System.Text.Encoding.UTF8, "application/json") });
        await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler,
            context: PlatformTrustedLegalEntityScopeProviderClientTests.AuthenticatedContext("first"))
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.gskus.read");
        await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler,
            context: PlatformTrustedLegalEntityScopeProviderClientTests.AuthenticatedContext("second"))
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.gskus.read");
        Assert.Equal(["Bearer first", "Bearer second"], handler.Requests.Select(item => item.Authorization));
    }
}
