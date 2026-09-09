using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuRetirementRequestStartConfiguration(Guid? TemplateId, string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds, string ReasonCode, bool CommentRequired, bool EvidenceRequired,
    TimeSpan? DueAfter);
public sealed record LskuRetirementRequestStartPlan(LskuRetirementRequestOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class LskuRetirementRequestWorkflowStartRequestFactory(
    LskuRetirementRequestStartConfiguration configuration, TimeProvider timeProvider)
{
    public const string ObjectType = "LskuRetirementRequest";

    public LskuRetirementRequestStartPlan Create(Lsku lsku, Guid operationId, Guid makerSubjectId,
        string requestReason)
    {
        if (!HasValidReason(requestReason)) throw new InvalidOperationException("LSKU_RETIREMENT_REASON_INVALID");
        if (lsku.Id == Guid.Empty || lsku.TenantId == Guid.Empty || lsku.IsDeleted
            || lsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || lsku.ActiveLifecycleOperation is not null || operationId == Guid.Empty || makerSubjectId == Guid.Empty)
            throw new InvalidOperationException("LSKU_RETIREMENT_FACTS_INVALID");
        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        var dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : (DateTimeOffset?)null;
        var operation = new LskuRetirementRequestOperation
        {
            Id = operationId, TenantId = lsku.TenantId, OperationId = operationId, LskuId = lsku.Id,
            BaseLskuVersion = lsku.Version, MakerSubjectId = makerSubjectId,
            RequestReason = NormalizeReason(requestReason), WorkflowTemplateId = configuration.TemplateId,
            WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. configuration.CandidatePrincipalIds.Order()],
            ReasonCode = configuration.ReasonCode, CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            ConfiguredDueAfterSeconds = configuration.DueAfter.HasValue
                ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            DueAtUtcTicksV1 = dueAt?.UtcTicks, ObjectType = ObjectType, ObjectId = operationId.ToString("D"),
            ObjectRef = lsku.CanonicalCode,
            StartIdempotencyKey = $"lsku-retirement:{lsku.TenantId:D}:{operationId:D}",
            Checkpoint = LskuRetirementRequestCheckpoint.Prepared,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        operation.OperationFingerprint = ComputeFingerprint(operation);
        return new(operation, Rehydrate(operation));
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(LskuRetirementRequestOperation x) => new(
        x.WorkflowTemplateId, x.WorkflowTemplateCode, x.ObjectType, x.ObjectId, x.ObjectRef,
        x.CandidatePrincipalIds.Select(y => y.ToString("D")).ToArray(), x.ReasonCode, x.StartIdempotencyKey,
        x.CommentRequired, x.EvidenceRequired,
        x.DueAtUtcTicksV1.HasValue ? new(x.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);

    public static string ComputeFingerprint(LskuRetirementRequestOperation x) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            "contract=lsku-retirement-v1", $"tenant={x.TenantId:D}", $"lsku={x.LskuId:D}",
            $"base-version={x.BaseLskuVersion}", $"operation={x.OperationId:D}", $"maker={x.MakerSubjectId:D}",
            $"request-reason={Convert.ToBase64String(Encoding.UTF8.GetBytes(x.RequestReason))}",
            $"template-id={x.WorkflowTemplateId?.ToString("D") ?? string.Empty}",
            $"template-code={x.WorkflowTemplateCode ?? string.Empty}",
            $"candidates={string.Join(',', x.CandidatePrincipalIds.Order().Select(y => y.ToString("D")))}",
            $"reason-code={Convert.ToBase64String(Encoding.UTF8.GetBytes(x.ReasonCode))}",
            $"comment-required={x.CommentRequired}", $"evidence-required={x.EvidenceRequired}",
            $"due-after-seconds={x.ConfiguredDueAfterSeconds}", $"due-at-utc-ticks={x.DueAtUtcTicksV1}",
            $"object-type={x.ObjectType}", $"object-id={x.ObjectId}",
            $"object-ref={Convert.ToBase64String(Encoding.UTF8.GetBytes(x.ObjectRef))}",
            $"idempotency-key={x.StartIdempotencyKey}")))).ToLowerInvariant();

    public static string NormalizeReason(string? value) => value?.Trim() ?? string.Empty;
    public static bool HasValidReason(string? value) => value is not null
        && string.Equals(value, NormalizeReason(value), StringComparison.Ordinal)
        && value.Length is > 0 and <= 2000 && !value.Any(char.IsControl);

    private void EnsureConfiguration()
    {
        if ((configuration.TemplateId.HasValue == !string.IsNullOrWhiteSpace(configuration.TemplateCode))
            || configuration.TemplateId == Guid.Empty || configuration.CandidatePrincipalIds.Count is < 1 or > 100
            || configuration.CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || configuration.CandidatePrincipalIds.Distinct().Count() != configuration.CandidatePrincipalIds.Count
            || !Exact(configuration.ReasonCode, 128) || !ExactOptional(configuration.TemplateCode, 128)
            || configuration.DueAfter is { } due && (due < TimeSpan.FromMinutes(1)
                || due > TimeSpan.FromDays(30) || due.Ticks % TimeSpan.TicksPerSecond != 0))
            throw new InvalidOperationException("LSKU_RETIREMENT_CONFIGURATION_INVALID");
    }

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ExactOptional(string? value, int max) => value is null || Exact(value, max);
}
