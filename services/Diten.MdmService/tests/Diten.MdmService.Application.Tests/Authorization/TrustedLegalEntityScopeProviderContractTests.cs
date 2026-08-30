using System.Net;
using Diten.MdmService.Application.Contracts.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

public sealed class TrustedLegalEntityScopeProviderContractTests
{
    [Fact]
    public async Task Exact_module_and_valid_permission_pair_is_dispatched_once()
    {
        var handler = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("{\"data\":null,\"statusCode\":503,\"isSuccessful\":false,\"errors\":[\"x\"],\"reason_code\":\"x\",\"correlation_id\":\"c\"}", System.Text.Encoding.UTF8, "application/json") });
        var result = await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler)
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.global-products.read");
        Assert.NotEqual("LEGAL_ENTITY_SCOPE_REQUEST_INVALID", result.FailureCode);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("MDM.global-products.read")]
    [InlineData("mdm.global-products.read ")]
    public async Task Unlisted_or_noncanonical_permission_is_rejected_without_http(string permission)
    {
        var handler = new ScopeRecordingHandler(_ => throw new InvalidOperationException("must not dispatch"));
        var result = await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler)
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", permission);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Provider_is_authoritative_for_denial_of_an_exact_allowed_pair()
    {
        var handler = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        { Content = new StringContent("{\"data\":null,\"statusCode\":403,\"isSuccessful\":false,\"errors\":[\"denied\"],\"reason_code\":\"arbitrary-upstream\",\"correlation_id\":\"c\"}", System.Text.Encoding.UTF8, "application/json") });
        var result = await PlatformTrustedLegalEntityScopeProviderClientTests.Client(handler)
            .ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.unknown.read");
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_FORBIDDEN", result.FailureCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Provider_result_contract_contains_only_bounded_resolution_facts()
    {
        var names = typeof(TrustedLegalEntityScopeProviderResult).GetProperties().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "EvaluatedAtUtc", "FailureCode", "IsSuccessful", "LegalEntityIds", "ModuleCode",
            "PermissionKey", "StatusCode", "SubjectId", "TenantId" }.OrderBy(name => name, StringComparer.Ordinal), names);
        Assert.DoesNotContain(names, name => name.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }
}
