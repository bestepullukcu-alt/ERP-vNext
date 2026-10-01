using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GskuRetirementRequestStartConfiguration(Guid? TemplateId, string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds, string ReasonCode, bool CommentRequired,
    bool EvidenceRequired, TimeSpan? DueAfter, Guid? IdentityTemplateId, string? IdentityTemplateCode,
    Guid? CorrectionTemplateId, string? CorrectionTemplateCode);
public sealed record GskuRetirementRequestStartPlan(GskuRetirementRequestOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class GskuRetirementRequestWorkflowStartRequestFactory(
    GskuRetirementRequestStartConfiguration configuration, TimeProvider timeProvider)
{
    public const string ObjectType = "GskuRetirementRequest";

    public GskuRetirementRequestStartPlan Create(Gsku gsku, ProductDefinitionRevision revision,
        Guid operationId, Guid makerSubjectId, string requestReason)
    {
        EnsureConfiguration();
        var reason = NormalizeReason(requestReason);
        if (!HasValidReason(requestReason) || gsku.Id == Guid.Empty || gsku.TenantId == Guid.Empty
            || revision.Id == Guid.Empty || revision.TenantId != gsku.TenantId
            || revision.Id != gsku.ProductDefinitionRevisionId || revision.IsDeleted || gsku.IsDeleted
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || gsku.ActiveLifecycleOperation is not null || gsku.ChildCreationAdmissions.Count != 0
            || operationId == Guid.Empty || makerSubjectId == Guid.Empty
            || gsku.IdentityWorkflowBinding is null || revision.IdentityWorkflowBinding is null
            || gsku.IdentityWorkflowBinding.GskuId != gsku.Id
            || revision.IdentityWorkflowBinding.GskuId != gsku.Id)
            throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_FACTS_INVALID");
        var now = timeProvider.GetUtcNow();
        var dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : (DateTimeOffset?)null;
        var candidates = configuration.CandidatePrincipalIds.Order().ToArray();
        var key = $"gsku-retirement-request:{gsku.TenantId:D}:{operationId:D}";
        var operation = new GskuRetirementRequestOperation
        {
            Id = operationId, TenantId = gsku.TenantId, OperationId = operationId, GskuId = gsku.Id,
            ProductDefinitionRevisionId = revision.Id, GlobalProductId = revision.GlobalProductId,
            BaseGskuVersion = gsku.Version, BaseRevisionVersion = revision.Version,
            MakerSubjectId = makerSubjectId, RequestReason = reason,
            WorkflowTemplateId = configuration.TemplateId, WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. candidates], ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired, EvidenceRequired = configuration.EvidenceRequired,
            ConfiguredDueAfterSeconds = configuration.DueAfter.HasValue
                ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            DueAtUtcTicksV1 = dueAt?.UtcTicks, ObjectType = ObjectType,
            ObjectId = operationId.ToString("D"), ObjectRef = gsku.CanonicalCode,
            StartIdempotencyKey = key, Checkpoint = GskuRetirementRequestCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        operation.OperationFingerprint = ComputeFingerprint(operation);
        return new(operation, Rehydrate(operation));
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(GskuRetirementRequestOperation x) => new(
        x.WorkflowTemplateId, x.WorkflowTemplateCode, x.ObjectType, x.ObjectId, x.ObjectRef,
        x.CandidatePrincipalIds.Select(v => v.ToString("D")).ToArray(), x.ReasonCode,
        x.StartIdempotencyKey, x.CommentRequired, x.EvidenceRequired,
        x.DueAtUtcTicksV1.HasValue ? new(x.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);

    public bool MatchesConfiguration(GskuRetirementRequestOperation x) =>
        x.WorkflowTemplateId == configuration.TemplateId && x.WorkflowTemplateCode == configuration.TemplateCode
        && x.CandidatePrincipalIds.Order().SequenceEqual(configuration.CandidatePrincipalIds.Order())
        && x.ReasonCode == configuration.ReasonCode && x.CommentRequired == configuration.CommentRequired
        && x.EvidenceRequired == configuration.EvidenceRequired;

    public static string ComputeFingerprint(GskuRetirementRequestOperation x) => Hash(string.Join('\n',
        "contract=gsku-retirement-request-v1", x.TenantId, x.GskuId, x.ProductDefinitionRevisionId,
        x.GlobalProductId, x.BaseGskuVersion, x.BaseRevisionVersion, x.OperationId, x.MakerSubjectId,
        Convert.ToBase64String(Encoding.UTF8.GetBytes(x.RequestReason)), x.WorkflowTemplateId,
        x.WorkflowTemplateCode, string.Join(',', x.CandidatePrincipalIds.Order()), x.ReasonCode,
        x.CommentRequired, x.EvidenceRequired, x.ConfiguredDueAfterSeconds, x.DueAtUtcTicksV1,
        x.ObjectType, x.ObjectRef, x.StartIdempotencyKey));

    public static string NormalizeReason(string? value) => value?.Trim() ?? string.Empty;
    public static bool HasValidReason(string? value) => value is not null && value == value.Trim()
        && value.EnumerateRunes().Count() is >= 1 and <= 2000 && !value.Any(char.IsControl);

    private void EnsureConfiguration()
    {
        var retirement = (configuration.TemplateId, configuration.TemplateCode);
        var identity = (configuration.IdentityTemplateId, configuration.IdentityTemplateCode);
        var correction = (configuration.CorrectionTemplateId, configuration.CorrectionTemplateCode);
        if ((configuration.TemplateId.HasValue == !string.IsNullOrWhiteSpace(configuration.TemplateCode))
            || configuration.TemplateId == Guid.Empty || configuration.CandidatePrincipalIds.Count is < 1 or > 100
            || configuration.CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || configuration.CandidatePrincipalIds.Distinct().Count() != configuration.CandidatePrincipalIds.Count
            || !Exact(configuration.ReasonCode, 128)
            || configuration.DueAfter is { } due && (due < TimeSpan.FromMinutes(1) || due > TimeSpan.FromDays(30)
                || due.Ticks % TimeSpan.TicksPerSecond != 0)
            || Same(retirement, identity) || Same(retirement, correction) || Same(identity, correction))
            throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_CONFIGURATION_INVALID");
    }

    private static bool Exact(string? x, int max) => x is { Length: > 0 } && x.Length <= max
        && x == x.Trim() && !x.Any(char.IsControl);
    private static bool Same((Guid? Id, string? Code) a, (Guid? Id, string? Code) b) =>
        a.Id.HasValue && b.Id.HasValue && a.Id == b.Id
        || !string.IsNullOrWhiteSpace(a.Code) && !string.IsNullOrWhiteSpace(b.Code)
           && string.Equals(a.Code, b.Code, StringComparison.OrdinalIgnoreCase)
        || a.Id.HasValue && !string.IsNullOrWhiteSpace(b.Code)
        || !string.IsNullOrWhiteSpace(a.Code) && b.Id.HasValue;
    private static string Hash(string x) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x))).ToLowerInvariant();
}
