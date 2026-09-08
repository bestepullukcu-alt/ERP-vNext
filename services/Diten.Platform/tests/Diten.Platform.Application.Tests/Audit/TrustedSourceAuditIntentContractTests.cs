using System.Text;
using System.Text.Json;
using Diten.Platform.API.Models.Audit;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentContractTests
{
    public static IEnumerable<object[]> ExactMappings()
    {
        yield return ["CodeReservation", "CodeReserved", "CodeReservation", AuditOperation.Create];
        yield return ["CodeReservation", "CodeConsumed", "CodeReservation", AuditOperation.Update];
        yield return ["CodeReservation", "CodeBindingConfirmed", "CodeReservation", AuditOperation.Update];
        yield return ["CodeReservation", "CodeBurned", "CodeReservation", AuditOperation.Deactivate];
        yield return ["GlobalProduct", "GlobalProductDraftCreated", "GlobalProduct", AuditOperation.Create];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionDraftCreated", "ProductDefinitionRevision", AuditOperation.Create];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionIdentitySubmitted", "ProductDefinitionRevision", AuditOperation.LifecycleTransition];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionIdentityApproved", "ProductDefinitionRevision", AuditOperation.LifecycleTransition];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionIdentityRejected", "ProductDefinitionRevision", AuditOperation.LifecycleTransition];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionIdentityApprovalWithdrawn", "ProductDefinitionRevision", AuditOperation.LifecycleTransition];
        yield return ["ProductDefinitionRevision", "ProductDefinitionRevisionIdentityRetired", "ProductDefinitionRevision", AuditOperation.Deactivate];
        yield return ["Gsku", "GskuDraftCreated", "Gsku", AuditOperation.Create];
        yield return ["Gsku", "GskuDraftUpdated", "Gsku", AuditOperation.Update];
        yield return ["Gsku", "GskuIdentitySubmitted", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuIdentityApproved", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuIdentityRejected", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuIdentityApprovalWithdrawn", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuCorrectionRequested", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuCorrectionApplied", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuCorrectionRejected", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuCorrectionManualReconciliationRequired", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuRetirementRequested", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuRetirementRejected", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuRetirementManualReconciliationRequired", "Gsku", AuditOperation.LifecycleTransition];
        yield return ["Gsku", "GskuIdentityRetired", "Gsku", AuditOperation.Deactivate];
        yield return ["FinishedGood", "FinishedGoodDraftCreated", "FinishedGood", AuditOperation.Create];
        yield return ["Lsku", "LskuDraftCreated", "Lsku", AuditOperation.Create];
        yield return ["Lsku", "LskuIdentitySubmitted", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["Lsku", "LskuIdentityApproved", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["Lsku", "LskuIdentityRejected", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["Lsku", "LskuIdentityRetired", "Lsku", AuditOperation.Deactivate];
        yield return ["Lsku", "LskuIdentityApprovalWithdrawn", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["Lsku", "LskuRetirementRequested", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["Lsku", "LskuRetirementRejected", "Lsku", AuditOperation.LifecycleTransition];
        yield return ["ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyCreated", "ProductLegalEntityScopePolicy", AuditOperation.Create];
        yield return ["ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyReplaced", "ProductLegalEntityScopePolicy", AuditOperation.Update];
        yield return ["ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyEnded", "ProductLegalEntityScopePolicy", AuditOperation.Deactivate];
        yield return ["ProductLegalEntityScopeRolloutState", "ProductLegalEntityScopeEnforcementActivated", "ProductLegalEntityScopeRolloutState", AuditOperation.Activate];
        yield return ["ProductLegalEntityScopeRolloutState", "ProductLegalEntityScopeEnforcementSuspended", "ProductLegalEntityScopeRolloutState", AuditOperation.Suspend];
    }

    [Theory]
    [MemberData(nameof(ExactMappings))]
    public void OperationMap_ContainsOnlyApprovedPairs(
        string aggregateType,
        string operation,
        string entityType,
        AuditOperation mappedOperation)
    {
        Assert.True(TrustedSourceAuditIntentOperationMap.TryMap(aggregateType, operation, out var actualType, out var actualOperation));
        Assert.Equal(entityType, actualType);
        Assert.Equal(mappedOperation, actualOperation);
    }

    [Theory]
    [InlineData("gsku", "GskuDraftCreated")]
    [InlineData("Gsku", "gskudraftcreated")]
    [InlineData("7", "8")]
    [InlineData("GlobalProduct", "CodeReserved")]
    [InlineData("Gsku", "21")]
    [InlineData("Gsku", "gskuidentitysubmitted")]
    [InlineData("ProductDefinitionRevision", "GskuIdentitySubmitted")]
    [InlineData("ProductDefinitionRevision", "GskuIdentityApprovalWithdrawn")]
    [InlineData("Gsku", "ProductDefinitionRevisionIdentityApprovalWithdrawn")]
    [InlineData("ProductDefinitionRevision", "GskuCorrectionApplied")]
    [InlineData("Gsku ", "GskuIdentitySubmitted")]
    [InlineData("ProductLegalEntityScopeRolloutState ", "ProductLegalEntityScopeEnforcementActivated")]
    public void OperationMap_RejectsAliasCaseNumericAndInvalidPairs(string aggregateType, string operation)
    {
        Assert.False(TrustedSourceAuditIntentOperationMap.TryMap(aggregateType, operation, out _, out _));
    }

    [Theory]
    [InlineData("LskuDraftCreated", "10")]
    [InlineData("LskuIdentitySubmitted", "28")]
    [InlineData("LskuIdentityApproved", "29")]
    [InlineData("LskuIdentityRejected", "30")]
    [InlineData("LskuIdentityRetired", "31")]
    [InlineData("LskuIdentityApprovalWithdrawn", "66")]
    [InlineData("LskuRetirementRequested", "67")]
    [InlineData("LskuRetirementRejected", "68")]
    public void Lsku_mapping_rejects_wrong_aggregate_case_and_numeric_aliases(string operation, string ordinal)
    {
        foreach (var aggregate in new[] { "Gsku", "ProductDefinitionRevision", "GlobalProduct", "FinishedGood", "lsku", "LSKU", "Lsku " })
            Assert.False(TrustedSourceAuditIntentOperationMap.TryMap(aggregate, operation, out _, out _));
        foreach (var invalid in new[] { operation.ToLowerInvariant(), operation + ".extra", ordinal, "9999", "LskuUnknown", "*" })
            Assert.False(TrustedSourceAuditIntentOperationMap.TryMap("Lsku", invalid, out _, out _));
    }

    [Fact]
    public void Parser_AcceptsExactWireShapeWithOptionalSnapshot()
    {
        var parser = new TrustedSourceAuditIntentRequestParser();
        var json = TrustedSourceAuditIntentTestData.Json();

        Assert.True(parser.TryParse(Encoding.UTF8.GetBytes(json), out var request));
        Assert.Equal("snapshot://policy/42", request!.SnapshotReference);
        Assert.Equal("ProductLegalEntityScopeEnforcementActivated", request.Operation);
    }

    [Theory]
    [InlineData("unknown", "true")]
    [InlineData("Operation", "\"ProductLegalEntityScopeEnforcementActivated\"")]
    [InlineData("operation", "8")]
    [InlineData("aggregateType", "7")]
    [InlineData("snapshotReference", "42")]
    public void Parser_RejectsUnknownCaseNumericAndWrongOptionalTypes(string property, string rawValue)
    {
        var json = TrustedSourceAuditIntentTestData.Json();
        json = property is "unknown" or "Operation"
            ? json.Insert(json.LastIndexOf('}'), $",\"{property}\":{rawValue}")
            : TrustedSourceAuditIntentTestData.ReplaceProperty(json, property, rawValue);

        Assert.False(new TrustedSourceAuditIntentRequestParser().TryParse(Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void Parser_RejectsDuplicateProperty()
    {
        var json = TrustedSourceAuditIntentTestData.Json().Insert(
            TrustedSourceAuditIntentTestData.Json().LastIndexOf('}'),
            ",\"operation\":\"ProductLegalEntityScopeEnforcementActivated\"");

        Assert.False(new TrustedSourceAuditIntentRequestParser().TryParse(Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void Canonicalizer_IsStableAndSensitiveToEverySemanticField()
    {
        var baseline = TrustedSourceAuditIntentTestData.Envelope();
        var fingerprint = TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(baseline);

        Assert.Equal(fingerprint, TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(baseline with { }));
        Assert.NotEqual(fingerprint, TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(baseline with { Sequence = baseline.Sequence + 1 }));
        Assert.NotEqual(fingerprint, TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(baseline with { SnapshotReference = null }));
        Assert.NotEqual(fingerprint, TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(baseline with { EvidenceHash = new string('B', 64) }));
        Assert.Equal(
            $"Diten.MDM:{baseline.TenantId:N}:{baseline.IntentId:N}:mod-0290.audit-intent.v1",
            TrustedSourceAuditIntentCanonicalizer.BuildCentralIdempotencyKey(baseline));
    }
}
internal static class TrustedSourceAuditIntentTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    internal static TrustedSourceAuditIntentEnvelope Envelope(
        Guid? tenantId = null,
        Guid? intentId = null,
        string operation = "ProductLegalEntityScopeEnforcementActivated") => new(
        "Diten.MDM",
        "mod-0290.audit-intent.v1",
        intentId ?? Guid.Parse("10000000-0000-0000-0000-000000000001"),
        tenantId ?? Guid.Parse("20000000-0000-0000-0000-000000000002"),
        "ProductLegalEntityScopeRolloutState",
        Guid.Parse("30000000-0000-0000-0000-000000000003"),
        6,
        7,
        operation,
        "product-data-owner",
        Guid.Parse("40000000-0000-0000-0000-000000000004"),
        "causation-42",
        "command-42",
        8,
        Now,
        new string('A', 64),
        "snapshot://policy/42",
        "source-key-42");

    internal static string Json(TrustedSourceAuditIntentEnvelope? value = null)
    {
        var envelope = value ?? Envelope();
        return JsonSerializer.Serialize(new
        {
            sourceService = envelope.SourceService,
            contractVersion = envelope.ContractVersion,
            intentId = envelope.IntentId,
            tenantId = envelope.TenantId,
            aggregateType = envelope.AggregateType,
            aggregateId = envelope.AggregateId,
            preVersion = envelope.PreVersion,
            postVersion = envelope.PostVersion,
            operation = envelope.Operation,
            actorId = envelope.ActorId,
            correlationId = envelope.CorrelationId,
            causationId = envelope.CausationId,
            commandId = envelope.CommandId,
            sequence = envelope.Sequence,
            timestampUtc = envelope.TimestampUtc,
            evidenceHash = envelope.EvidenceHash,
            snapshotReference = envelope.SnapshotReference,
            idempotencyKey = envelope.IdempotencyKey
        });
    }

    internal static string ReplaceProperty(string json, string property, string rawValue)
    {
        using var document = JsonDocument.Parse(json);
        var parts = document.RootElement.EnumerateObject().Select(item =>
            $"{JsonSerializer.Serialize(item.Name)}:{(item.Name == property ? rawValue : item.Value.GetRawText())}");
        return "{" + string.Join(',', parts) + "}";
    }
}
