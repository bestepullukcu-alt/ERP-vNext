using System.Text.Json;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentTwoServiceContractTests
{
    [Fact]
    public void Mdm_envelope_matches_frozen_provider_contract_without_transport_tenant_header()
    {
        var envelope = PlatformTrustedSourceAuditIntentClientTests.Envelope();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(envelope));

        Assert.Equal(18, json.RootElement.EnumerateObject().Count());
        Assert.Equal(AuditIntentContract.SourceService, envelope.SourceService);
        Assert.Equal(AuditIntentDeliveryProcessor.RequiredContractVersion, envelope.ContractVersion);
        Assert.Equal(
            $"{AuditIntentContract.SourceService}:{envelope.TenantId:N}:{envelope.IntentId:N}:{envelope.ContractVersion}",
            AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion));
        Assert.DoesNotContain(json.RootElement.EnumerateObject(), property =>
            property.Name.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("token", StringComparison.OrdinalIgnoreCase));
    }
}
