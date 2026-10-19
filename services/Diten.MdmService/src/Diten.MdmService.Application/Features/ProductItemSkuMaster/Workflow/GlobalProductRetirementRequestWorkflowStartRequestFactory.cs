using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductRetirementRequestStartConfiguration(Guid? TemplateId, string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds, string ReasonCode, bool CommentRequired, bool EvidenceRequired,
    TimeSpan? DueAfter, Guid? IdentityTemplateId, string? IdentityTemplateCode,
    Guid? CorrectionTemplateId, string? CorrectionTemplateCode);
public sealed record GlobalProductRetirementRequestStartPlan(GlobalProductRetirementRequestOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class GlobalProductRetirementRequestWorkflowStartRequestFactory(
    GlobalProductRetirementRequestStartConfiguration configuration, TimeProvider timeProvider)
{
    public const string ObjectType = "GlobalProductRetirement";
    public GlobalProductRetirementRequestStartPlan Create(GlobalProduct product, Guid operationId,
        Guid makerSubjectId, string requestReason)
    {
        if (!HasValidReason(requestReason))
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_REASON_INVALID");
        var cleanReason = NormalizeReason(requestReason);
        if (product.Id == Guid.Empty || product.TenantId == Guid.Empty || product.IsDeleted
            || product.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || product.ActiveLifecycleOperation is not null || operationId == Guid.Empty || makerSubjectId == Guid.Empty)
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_FACTS_INVALID");
        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        var dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : (DateTimeOffset?)null;
        var key = $"global-product-retirement:{product.TenantId:D}:{operationId:D}";
        var candidates = configuration.CandidatePrincipalIds.Order().ToArray();
        var fingerprint = ComputeFingerprint(product.TenantId, product.Id, product.Version, operationId,
            makerSubjectId, cleanReason, configuration.TemplateId, configuration.TemplateCode, candidates,
            configuration.ReasonCode, configuration.CommentRequired, configuration.EvidenceRequired,
            configuration.DueAfter.HasValue ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            dueAt?.UtcTicks, ObjectType, operationId.ToString("D"), product.CanonicalCode, key);
        var request = new ProductIdentityWorkflowStartRequest(configuration.TemplateId, configuration.TemplateCode,
            ObjectType, operationId.ToString("D"), product.CanonicalCode,
            candidates.Select(x => x.ToString("D")).ToArray(), configuration.ReasonCode,
            key, configuration.CommentRequired, configuration.EvidenceRequired, dueAt);
        var operation = new GlobalProductRetirementRequestOperation
        {
            Id = operationId, TenantId = product.TenantId, OperationId = operationId,
            GlobalProductId = product.Id, BaseProductVersion = product.Version, MakerSubjectId = makerSubjectId,
            RequestReason = cleanReason,
            WorkflowTemplateId = configuration.TemplateId, WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. candidates], ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired, EvidenceRequired = configuration.EvidenceRequired,
            ConfiguredDueAfterSeconds = configuration.DueAfter.HasValue
                ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            DueAtUtcTicksV1 = dueAt?.UtcTicks, ObjectType = ObjectType, ObjectId = operationId.ToString("D"),
            ObjectRef = product.CanonicalCode, StartIdempotencyKey = key, OperationFingerprint = fingerprint,
            Checkpoint = GlobalProductRetirementRequestCheckpoint.Prepared,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public static string ComputeFingerprint(GlobalProductRetirementRequestOperation operation) =>
        ComputeFingerprint(operation.TenantId, operation.GlobalProductId, operation.BaseProductVersion,
            operation.OperationId, operation.MakerSubjectId, operation.RequestReason,
            operation.WorkflowTemplateId, operation.WorkflowTemplateCode, operation.CandidatePrincipalIds.Order(),
            operation.ReasonCode, operation.CommentRequired, operation.EvidenceRequired,
            operation.ConfiguredDueAfterSeconds, operation.DueAtUtcTicksV1, operation.ObjectType,
            operation.ObjectId, operation.ObjectRef, operation.StartIdempotencyKey);

    private static string ComputeFingerprint(Guid tenantId, Guid productId, long baseVersion, Guid operationId,
        Guid makerSubjectId, string requestReason, Guid? templateId, string? templateCode,
        IEnumerable<Guid> candidatePrincipalIds, string reasonCode, bool commentRequired, bool evidenceRequired,
        int? configuredDueAfterSeconds, long? dueAtUtcTicks, string objectType, string objectId, string objectRef,
        string idempotencyKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            "contract=global-product-retirement-v1",
            $"tenant={tenantId:D}", $"global-product={productId:D}", $"base-version={baseVersion}",
            $"operation={operationId:D}", $"maker={makerSubjectId:D}",
            $"request-reason={Convert.ToBase64String(Encoding.UTF8.GetBytes(requestReason))}",
            $"template-id={templateId?.ToString("D") ?? string.Empty}",
            $"template-code={templateCode ?? string.Empty}",
            $"candidates={string.Join(',', candidatePrincipalIds.Select(x => x.ToString("D")))}",
            $"reason-code={Convert.ToBase64String(Encoding.UTF8.GetBytes(reasonCode))}",
            $"comment-required={commentRequired}", $"evidence-required={evidenceRequired}",
            $"due-after-seconds={configuredDueAfterSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}",
            $"due-at-utc-ticks={dueAtUtcTicks?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}",
            $"object-type={objectType}", $"object-id={objectId}",
            $"object-ref={Convert.ToBase64String(Encoding.UTF8.GetBytes(objectRef))}",
            $"idempotency-key={idempotencyKey}")))).ToLowerInvariant();

    public static string NormalizeReason(string? value) => value?.Trim() ?? string.Empty;
    public static bool HasValidReason(string? value)
    {
        if (value is null || !string.Equals(value, NormalizeReason(value), StringComparison.Ordinal)) return false;
        var clean = NormalizeReason(value);
        return clean.Length > 0 && clean.EnumerateRunes().Count() <= 2000
            && !clean.Any(char.IsControl);
    }
    public ProductIdentityWorkflowStartRequest Rehydrate(GlobalProductRetirementRequestOperation operation) => new(
        operation.WorkflowTemplateId, operation.WorkflowTemplateCode, operation.ObjectType, operation.ObjectId,
        operation.ObjectRef, operation.CandidatePrincipalIds.Select(x => x.ToString("D")).ToArray(),
        operation.ReasonCode, operation.StartIdempotencyKey, operation.CommentRequired,
        operation.EvidenceRequired, operation.DueAtUtcTicksV1.HasValue
            ? new(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);

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
            || !ExactOptional(configuration.TemplateCode, 128)
            || !ExactOptional(configuration.IdentityTemplateCode, 128)
            || !ExactOptional(configuration.CorrectionTemplateCode, 128)
            || configuration.IdentityTemplateId == Guid.Empty || configuration.CorrectionTemplateId == Guid.Empty
            || configuration.DueAfter is { } dueAfter
               && (dueAfter < TimeSpan.FromMinutes(1) || dueAfter > TimeSpan.FromDays(30)
                   || dueAfter.Ticks % TimeSpan.TicksPerSecond != 0)
            || Same(retirement, identity) || Same(retirement, correction) || Same(identity, correction))
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_CONFIGURATION_INVALID");
    }
    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ExactOptional(string? value, int max) => value is null || Exact(value, max);
    private static bool Same((Guid? Id, string? Code) a, (Guid? Id, string? Code) b) =>
        a.Id.HasValue && b.Id.HasValue && a.Id == b.Id
        || !string.IsNullOrWhiteSpace(a.Code) && !string.IsNullOrWhiteSpace(b.Code)
           && string.Equals(a.Code, b.Code, StringComparison.OrdinalIgnoreCase)
        || a.Id.HasValue && !string.IsNullOrWhiteSpace(b.Code)
        || !string.IsNullOrWhiteSpace(a.Code) && b.Id.HasValue;
}
