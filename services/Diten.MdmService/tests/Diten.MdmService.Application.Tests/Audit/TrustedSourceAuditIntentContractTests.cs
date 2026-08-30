using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentContractTests
{
    [Fact]
    public void Envelope_is_exact_provider_eighteen_field_contract()
    {
        Assert.Equal(18, typeof(TrustedSourceAuditIntentEnvelope).GetProperties().Length);
        Assert.Equal(
            ["SourceService", "ContractVersion", "IntentId", "TenantId", "AggregateType", "AggregateId",
             "PreVersion", "PostVersion", "Operation", "ActorId", "CorrelationId", "CausationId", "CommandId",
             "Sequence", "TimestampUtc", "EvidenceHash", "SnapshotReference", "IdempotencyKey"],
            typeof(TrustedSourceAuditIntentEnvelope).GetProperties().Select(property => property.Name));
        Assert.Equal("Diten.MDM", AuditIntentContract.SourceService);
        Assert.Equal("mod-0290.audit-intent.v1", AuditIntentDeliveryProcessor.RequiredContractVersion);
        Assert.Equal("TRUSTED_AUDIT_SOURCE_INGEST", AuditIntentDeliveryProcessor.RequiredAudience);
    }

    [Fact]
    public void Central_key_is_exact_ordinal_and_rejects_missing_identity()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var intent = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.Equal(
            "Diten.MDM:11111111111111111111111111111111:22222222222222222222222222222222:mod-0290.audit-intent.v1",
            AuditIntentContract.BuildCentralIdempotencyKey(tenant, intent, "mod-0290.audit-intent.v1"));
        Assert.Throws<ArgumentException>(() => AuditIntentContract.BuildCentralIdempotencyKey(Guid.Empty, intent, "v1"));
    }
}
