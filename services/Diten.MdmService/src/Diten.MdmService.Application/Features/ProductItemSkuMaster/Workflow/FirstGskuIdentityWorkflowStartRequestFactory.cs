using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record FirstGskuIdentityWorkflowStartConfiguration(
    Guid? TemplateId,
    string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds,
    string ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    TimeSpan? DueAfter);

public sealed record FirstGskuIdentityWorkflowStartPlan(
    FirstGskuIdentityWorkflowOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class FirstGskuIdentityWorkflowStartRequestFactory(
    FirstGskuIdentityWorkflowStartConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string ObjectType = "gsku";

    public FirstGskuIdentityWorkflowStartPlan Create(
        Guid tenantId,
        ProductDefinitionRevision revision,
        Gsku gsku,
        Guid operationId,
        int expectedGskuVersion,
        Guid makerSubjectId)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(gsku);
        if (tenantId == Guid.Empty || revision.Id == Guid.Empty || gsku.Id == Guid.Empty
            || operationId == Guid.Empty || makerSubjectId == Guid.Empty || expectedGskuVersion < 0
            || revision.TenantId != tenantId || gsku.TenantId != tenantId
            || revision.IsDeleted || gsku.IsDeleted
            || gsku.ProductDefinitionRevisionId != revision.Id
            || gsku.Version != expectedGskuVersion
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || string.IsNullOrWhiteSpace(revision.CreationCommandId)
            || !string.Equals(revision.CreationCommandId, gsku.CreationCommandId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(gsku.CanonicalCode))
        {
            throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_START_FACTS_INVALID");
        }

        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? dueAt = configuration.DueAfter.HasValue
            ? now.Add(configuration.DueAfter.Value)
            : null;
        var startKey = $"first-gsku-identity:{tenantId:D}:{operationId:D}";
        var fingerprint = Fingerprint(tenantId, operationId, revision, gsku, makerSubjectId);
        var request = new ProductIdentityWorkflowStartRequest(
            configuration.TemplateId,
            configuration.TemplateCode,
            ObjectType,
            gsku.Id.ToString("D"),
            gsku.CanonicalCode,
            configuration.CandidatePrincipalIds.Select(id => id.ToString("D")).ToArray(),
            configuration.ReasonCode,
            startKey,
            configuration.CommentRequired,
            configuration.EvidenceRequired,
            dueAt);
        var operation = new FirstGskuIdentityWorkflowOperation
        {
            Id = operationId,
            TenantId = tenantId,
            OperationId = operationId,
            ProductDefinitionRevisionId = revision.Id,
            GskuId = gsku.Id,
            GlobalProductId = revision.GlobalProductId,
            CreationCommandId = revision.CreationCommandId,
            ExpectedRevisionVersion = revision.Version,
            ExpectedGskuVersion = expectedGskuVersion,
            MakerSubjectId = makerSubjectId,
            WorkflowTemplateId = configuration.TemplateId,
            WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. configuration.CandidatePrincipalIds],
            ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            DueAtUtcTicksV1 = dueAt?.UtcTicks,
            ObjectType = ObjectType,
            ObjectId = gsku.Id.ToString("D"),
            ObjectRef = gsku.CanonicalCode,
            StartIdempotencyKey = startKey,
            OperationFingerprint = fingerprint,
            PackApplicabilityCode = gsku.PackApplicabilityCode,
            PackQuantity = gsku.PackQuantity,
            PackUomCode = gsku.PackUomCode,
            PackApplicabilitySelection = Clone(gsku.PackApplicabilitySelection),
            PackUomSelection = Clone(gsku.PackUomSelection),
            Checkpoint = FirstGskuIdentityWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks,
            UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(FirstGskuIdentityWorkflowOperation operation)
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
            || !Exact(configuration.ReasonCode, 128)
            || configuration.TemplateCode is not null && !Exact(configuration.TemplateCode, 128)
            || configuration.DueAfter is { } dueAfter
                && (dueAfter < TimeSpan.FromMinutes(1) || dueAfter > TimeSpan.FromDays(30)))
        {
            throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID");
        }
    }

    private string Fingerprint(
        Guid tenantId,
        Guid operationId,
        ProductDefinitionRevision revision,
        Gsku gsku,
        Guid makerSubjectId)
    {
        var facts = string.Join('\n',
            "contract=first-gsku-identity-workflow-start-v1",
            $"tenantId={tenantId:D}",
            $"operationId={operationId:D}",
            $"revisionId={revision.Id:D}",
            $"gskuId={gsku.Id:D}",
            $"globalProductId={revision.GlobalProductId:D}",
            $"creationCommandId={Encode(revision.CreationCommandId)}",
            $"expectedRevisionVersion={revision.Version}",
            $"expectedGskuVersion={gsku.Version}",
            $"makerSubjectId={makerSubjectId:D}",
            $"templateId={configuration.TemplateId?.ToString("D") ?? string.Empty}",
            $"templateCode={Encode(configuration.TemplateCode)}",
            $"candidates={string.Join(',', configuration.CandidatePrincipalIds.Select(id => id.ToString("D")))}",
            $"reason={Encode(configuration.ReasonCode)}",
            $"commentRequired={configuration.CommentRequired}",
            $"evidenceRequired={configuration.EvidenceRequired}",
            $"objectType={ObjectType}",
            $"objectRef={Encode(gsku.CanonicalCode)}",
            $"packApplicability={Selection(gsku.PackApplicabilitySelection)}",
            $"packQuantity={gsku.PackQuantity.ToString("G29", CultureInfo.InvariantCulture)}",
            $"packUom={Selection(gsku.PackUomSelection)}",
            $"dueAfterTicks={configuration.DueAfter?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static string Selection(ReferenceCatalogSelection selection) => string.Join(':',
        Encode(selection.SetCode), Encode(selection.ValueCode), selection.CatalogVersionId.ToString("D"),
        selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
        ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture), selection.ResolvedAtUtc.UtcTicks);

    private static ReferenceCatalogSelection Clone(ReferenceCatalogSelection source) => new()
    {
        SetCode = source.SetCode,
        ValueCode = source.ValueCode,
        CatalogVersionId = source.CatalogVersionId,
        CatalogVersionNumber = source.CatalogVersionNumber,
        ResolutionMode = source.ResolutionMode,
        ResolvedAtUtc = source.ResolvedAtUtc
    };

    private static bool Exact(string? value, int maximumLength) =>
        value is { Length: > 0 } && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static string Encode(string? value) =>
        value is null ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
