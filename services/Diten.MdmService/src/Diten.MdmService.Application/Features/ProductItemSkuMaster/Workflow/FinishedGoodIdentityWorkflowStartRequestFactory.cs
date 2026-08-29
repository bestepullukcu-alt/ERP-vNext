using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record FinishedGoodIdentityWorkflowStartConfiguration(
    Guid? TemplateId,
    string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds,
    string ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    TimeSpan? DueAfter);

public sealed record FinishedGoodIdentityWorkflowStartPlan(
    FinishedGoodIdentityWorkflowOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class FinishedGoodIdentityWorkflowStartRequestFactory(
    FinishedGoodIdentityWorkflowStartConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string FinishedGoodObjectType = "finished-good";

    public FinishedGoodIdentityWorkflowStartPlan Create(
        Guid tenantId,
        FinishedGood finishedGood,
        Gsku gsku,
        ProductDefinitionRevision revision,
        Guid operationId,
        int expectedVersion,
        Guid makerSubjectId)
    {
        ArgumentNullException.ThrowIfNull(finishedGood);
        ArgumentNullException.ThrowIfNull(gsku);
        ArgumentNullException.ThrowIfNull(revision);
        if (tenantId == Guid.Empty || finishedGood.Id == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0 || finishedGood.Version != expectedVersion
            || finishedGood.TenantId != tenantId || finishedGood.IsDeleted
            || finishedGood.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || string.IsNullOrWhiteSpace(finishedGood.CanonicalCode)
            || gsku.TenantId != tenantId || gsku.IsDeleted || gsku.Id != finishedGood.GskuId
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || revision.TenantId != tenantId || revision.IsDeleted
            || revision.Id != gsku.ProductDefinitionRevisionId
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            throw new InvalidOperationException("FINISHED_GOOD_IDENTITY_WORKFLOW_START_FACTS_INVALID");
        }

        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? dueAt = configuration.DueAfter.HasValue
            ? now.Add(configuration.DueAfter.Value)
            : null;
        var startKey = $"finished-good-identity:{tenantId:D}:{operationId:D}";
        var fingerprint = Fingerprint(
            tenantId,
            operationId,
            finishedGood.Id,
            expectedVersion,
            makerSubjectId,
            finishedGood);
        var request = new ProductIdentityWorkflowStartRequest(
            configuration.TemplateId,
            configuration.TemplateCode,
            FinishedGoodObjectType,
            finishedGood.Id.ToString("D"),
            finishedGood.CanonicalCode,
            configuration.CandidatePrincipalIds.Select(id => id.ToString("D")).ToArray(),
            configuration.ReasonCode,
            startKey,
            configuration.CommentRequired,
            configuration.EvidenceRequired,
            dueAt);
        var operation = new FinishedGoodIdentityWorkflowOperation
        {
            Id = operationId,
            TenantId = tenantId,
            OperationId = operationId,
            FinishedGoodId = finishedGood.Id,
            GskuId = gsku.Id,
            ProductDefinitionRevisionId = revision.Id,
            ExpectedFinishedGoodVersion = expectedVersion,
            MakerSubjectId = makerSubjectId,
            WorkflowTemplateId = configuration.TemplateId,
            WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. configuration.CandidatePrincipalIds],
            ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            DueAtUtcTicksV1 = dueAt?.UtcTicks,
            ObjectType = FinishedGoodObjectType,
            ObjectId = finishedGood.Id.ToString("D"),
            ObjectRef = finishedGood.CanonicalCode,
            StartIdempotencyKey = startKey,
            OperationFingerprint = fingerprint,
            Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks,
            UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(FinishedGoodIdentityWorkflowOperation operation)
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
            throw new InvalidOperationException("FINISHED_GOOD_IDENTITY_WORKFLOW_CONFIGURATION_INVALID");
        }
    }

    private string Fingerprint(
        Guid tenantId,
        Guid operationId,
        Guid finishedGoodId,
        int expectedVersion,
        Guid makerSubjectId,
        FinishedGood finishedGood)
    {
        var facts = string.Join('\n',
            "contract=finished-good-identity-workflow-start-v1",
            $"tenantId={tenantId:D}",
            $"operationId={operationId:D}",
            $"finishedGoodId={finishedGoodId:D}",
            $"expectedVersion={expectedVersion.ToString(CultureInfo.InvariantCulture)}",
            $"makerSubjectId={makerSubjectId:D}",
            $"templateId={configuration.TemplateId?.ToString("D") ?? string.Empty}",
            $"templateCode={Encode(configuration.TemplateCode)}",
            $"candidates={string.Join(',', configuration.CandidatePrincipalIds.Select(id => id.ToString("D")))}",
            $"reason={Encode(configuration.ReasonCode)}",
            $"commentRequired={configuration.CommentRequired}",
            $"evidenceRequired={configuration.EvidenceRequired}",
            $"objectType={FinishedGoodObjectType}",
            $"objectRef={Encode(finishedGood.CanonicalCode)}",
            $"gskuId={finishedGood.GskuId:D}",
            $"dueAfterTicks={configuration.DueAfter?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static string Encode(string? value) =>
        value is null ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

}
