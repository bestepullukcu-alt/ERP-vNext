using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuIdentityWorkflowStartConfiguration(
    Guid? TemplateId,
    string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds,
    string ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    TimeSpan? DueAfter);

public sealed record LskuIdentityWorkflowStartPlan(
    LskuIdentityWorkflowOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class LskuIdentityWorkflowStartRequestFactory(
    LskuIdentityWorkflowStartConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string LskuObjectType = "lsku";

    public LskuIdentityWorkflowStartPlan Create(
        Guid tenantId,
        Lsku lsku,
        Gsku gsku,
        ProductDefinitionRevision revision,
        Guid operationId,
        int expectedVersion,
        Guid makerSubjectId)
    {
        ArgumentNullException.ThrowIfNull(lsku);
        ArgumentNullException.ThrowIfNull(gsku);
        ArgumentNullException.ThrowIfNull(revision);
        if (tenantId == Guid.Empty || lsku.Id == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0 || lsku.Version != expectedVersion
            || lsku.TenantId != tenantId || lsku.IsDeleted
            || lsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || string.IsNullOrWhiteSpace(lsku.CanonicalCode)
            || gsku.TenantId != tenantId || gsku.IsDeleted || gsku.Id != lsku.GskuId
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || revision.TenantId != tenantId || revision.IsDeleted
            || revision.Id != gsku.ProductDefinitionRevisionId
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || lsku.MarketCode is not { Length: 2 }
            || lsku.MarketCode.Any(character => character is < 'A' or > 'Z')
            || lsku.MarketSelection.SetCode != "market"
            || lsku.MarketSelection.ValueCode != lsku.MarketCode
            || lsku.MarketSelection.CatalogVersionId == Guid.Empty
            || lsku.MarketSelection.CatalogVersionNumber <= 0
            || lsku.MarketSelection.ResolutionMode != ReferenceCatalogResolutionMode.Latest
            || lsku.MarketSelection.ResolvedAtUtc == default
            || lsku.MarketSelection.ResolvedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_START_FACTS_INVALID");
        }

        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? dueAt = configuration.DueAfter.HasValue
            ? now.Add(configuration.DueAfter.Value)
            : null;
        var startKey = $"lsku-identity:{tenantId:D}:{operationId:D}";
        var fingerprint = Fingerprint(
            tenantId,
            operationId,
            lsku.Id,
            expectedVersion,
            makerSubjectId,
            lsku);
        var request = new ProductIdentityWorkflowStartRequest(
            configuration.TemplateId,
            configuration.TemplateCode,
            LskuObjectType,
            lsku.Id.ToString("D"),
            lsku.CanonicalCode,
            configuration.CandidatePrincipalIds.Select(id => id.ToString("D")).ToArray(),
            configuration.ReasonCode,
            startKey,
            configuration.CommentRequired,
            configuration.EvidenceRequired,
            dueAt);
        var operation = new LskuIdentityWorkflowOperation
        {
            Id = operationId,
            TenantId = tenantId,
            OperationId = operationId,
            LskuId = lsku.Id,
            GskuId = gsku.Id,
            ProductDefinitionRevisionId = revision.Id,
            ExpectedLskuVersion = expectedVersion,
            MakerSubjectId = makerSubjectId,
            WorkflowTemplateId = configuration.TemplateId,
            WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. configuration.CandidatePrincipalIds],
            ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            DueAtUtcTicksV1 = dueAt?.UtcTicks,
            ObjectType = LskuObjectType,
            ObjectId = lsku.Id.ToString("D"),
            ObjectRef = lsku.CanonicalCode,
            StartIdempotencyKey = startKey,
            OperationFingerprint = fingerprint,
            MarketCode = lsku.MarketCode,
            MarketSelection = Clone(lsku.MarketSelection),
            Checkpoint = LskuIdentityWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks,
            UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(LskuIdentityWorkflowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new(
            operation.WorkflowTemplateId,
            operation.WorkflowTemplateCode,
            operation.ObjectType,
            operation.ObjectId,
            operation.ObjectRef,
            operation.CandidatePrincipalIds.Select(id => id.ToString("D")).ToArray(),
            operation.ReasonCode,
            operation.StartIdempotencyKey,
            operation.CommentRequired,
            operation.EvidenceRequired,
            operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null);
    }

    private void EnsureConfiguration()
    {
        if ((configuration.TemplateId.HasValue == !string.IsNullOrEmpty(configuration.TemplateCode))
            || configuration.TemplateId == Guid.Empty
            || configuration.CandidatePrincipalIds is not { Count: >= 1 and <= 100 }
            || configuration.CandidatePrincipalIds.Any(id => id == Guid.Empty)
            || configuration.CandidatePrincipalIds.Distinct().Count() != configuration.CandidatePrincipalIds.Count
            || string.IsNullOrWhiteSpace(configuration.ReasonCode)
            || configuration.ReasonCode.Length > 128
            || configuration.ReasonCode != configuration.ReasonCode.Trim()
            || configuration.ReasonCode.Any(char.IsControl)
            || configuration.DueAfter is { } dueAfter &&
                (dueAfter < TimeSpan.FromMinutes(1) || dueAfter > TimeSpan.FromDays(30)))
        {
            throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID");
        }
    }

    private string Fingerprint(
        Guid tenantId,
        Guid operationId,
        Guid lskuId,
        int expectedVersion,
        Guid makerSubjectId,
        Lsku lsku)
    {
        var facts = string.Join('\n',
            "contract=lsku-identity-workflow-start-v1",
            $"tenantId={tenantId:D}",
            $"operationId={operationId:D}",
            $"lskuId={lskuId:D}",
            $"expectedVersion={expectedVersion.ToString(CultureInfo.InvariantCulture)}",
            $"makerSubjectId={makerSubjectId:D}",
            $"templateId={configuration.TemplateId?.ToString("D") ?? string.Empty}",
            $"templateCode={Encode(configuration.TemplateCode)}",
            $"candidates={string.Join(',', configuration.CandidatePrincipalIds.Select(id => id.ToString("D")))}",
            $"reason={Encode(configuration.ReasonCode)}",
            $"commentRequired={configuration.CommentRequired}",
            $"evidenceRequired={configuration.EvidenceRequired}",
            $"objectType={LskuObjectType}",
            $"objectRef={Encode(lsku.CanonicalCode)}",
            $"gskuId={lsku.GskuId:D}",
            $"marketCode={lsku.MarketCode}",
            $"marketVersionId={lsku.MarketSelection.CatalogVersionId:D}",
            $"marketVersion={lsku.MarketSelection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture)}",
            $"marketMode={((int)lsku.MarketSelection.ResolutionMode).ToString(CultureInfo.InvariantCulture)}",
            $"marketResolvedAt={lsku.MarketSelection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}",
            $"dueAfterTicks={configuration.DueAfter?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static string Encode(string? value) =>
        value is null ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static Diten.MdmService.Domain.ValueObjects.ReferenceCatalogSelection Clone(
        Diten.MdmService.Domain.ValueObjects.ReferenceCatalogSelection source) => new()
        {
            SetCode = source.SetCode,
            ValueCode = source.ValueCode,
            CatalogVersionId = source.CatalogVersionId,
            CatalogVersionNumber = source.CatalogVersionNumber,
            ResolutionMode = source.ResolutionMode,
            ResolvedAtUtc = source.ResolvedAtUtc
        };
}
