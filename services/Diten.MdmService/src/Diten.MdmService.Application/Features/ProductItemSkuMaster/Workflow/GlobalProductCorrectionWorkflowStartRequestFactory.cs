using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductCorrectionStartConfiguration(
    Guid? TemplateId,
    string? TemplateCode,
    IReadOnlyList<Guid> CandidatePrincipalIds,
    string ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    TimeSpan? DueAfter,
    Guid? IdentityTemplateId,
    string? IdentityTemplateCode);

public sealed record GlobalProductCorrectionStartPlan(
    GlobalProductCorrectionOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class GlobalProductCorrectionWorkflowStartRequestFactory(
    GlobalProductCorrectionStartConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string ObjectType = "GlobalProductCorrection";

    public GlobalProductCorrectionStartPlan Create(
        GlobalProduct product,
        Guid operationId,
        Guid makerSubjectId,
        string proposedName)
    {
        ArgumentNullException.ThrowIfNull(product);
        var clean = GlobalProductNameRules.CleanVisible(proposedName);
        if (product.Id == Guid.Empty || product.TenantId == Guid.Empty || product.IsDeleted
            || product.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved
            || product.ActiveLifecycleOperation is not null || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || !GlobalProductNameRules.HasValidLength(clean))
            throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_FACTS_INVALID");
        EnsureConfiguration();
        var normalized = GlobalProductNameRules.NormalizeDuplicateKey(clean);
        if (normalized == product.GlobalProductNameNormalized)
            throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_NO_CHANGE");
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : null;
        var key = $"global-product-correction:{product.TenantId:D}:{operationId:D}";
        var candidates = configuration.CandidatePrincipalIds.Order().ToArray();
        var fingerprint = ComputeFingerprint(product.TenantId, product.Id, product.Version, operationId,
            makerSubjectId, normalized, configuration.TemplateId, configuration.TemplateCode, candidates,
            configuration.ReasonCode, configuration.CommentRequired, configuration.EvidenceRequired,
            configuration.DueAfter.HasValue ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            dueAt?.UtcTicks, ObjectType, operationId.ToString("D"), product.CanonicalCode, key);
        var request = new ProductIdentityWorkflowStartRequest(configuration.TemplateId,
            configuration.TemplateCode, ObjectType, operationId.ToString("D"), product.CanonicalCode,
            candidates.Select(x => x.ToString("D")).ToArray(),
            configuration.ReasonCode, key, configuration.CommentRequired,
            configuration.EvidenceRequired, dueAt);
        var operation = new GlobalProductCorrectionOperation
        {
            Id = operationId, TenantId = product.TenantId, OperationId = operationId,
            GlobalProductId = product.Id, BaseProductVersion = product.Version,
            MakerSubjectId = makerSubjectId, ProposedGlobalProductName = clean,
            ProposedGlobalProductNameNormalized = normalized,
            WorkflowTemplateId = configuration.TemplateId, WorkflowTemplateCode = configuration.TemplateCode,
            CandidatePrincipalIds = [.. candidates],
            ReasonCode = configuration.ReasonCode, CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            ConfiguredDueAfterSeconds = configuration.DueAfter.HasValue
                ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null,
            DueAtUtcTicksV1 = dueAt?.UtcTicks,
            ObjectType = ObjectType, ObjectId = operationId.ToString("D"), ObjectRef = product.CanonicalCode,
            StartIdempotencyKey = key, OperationFingerprint = fingerprint,
            Checkpoint = GlobalProductCorrectionCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            CreatedAtUtcTicksV1 = now.UtcTicks, UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public static string ComputeFingerprint(GlobalProductCorrectionOperation operation) =>
        ComputeFingerprint(operation.TenantId, operation.GlobalProductId, operation.BaseProductVersion,
            operation.OperationId, operation.MakerSubjectId, operation.ProposedGlobalProductNameNormalized,
            operation.WorkflowTemplateId, operation.WorkflowTemplateCode, operation.CandidatePrincipalIds.Order(),
            operation.ReasonCode, operation.CommentRequired, operation.EvidenceRequired,
            operation.ConfiguredDueAfterSeconds, operation.DueAtUtcTicksV1, operation.ObjectType,
            operation.ObjectId, operation.ObjectRef, operation.StartIdempotencyKey);

    private static string ComputeFingerprint(Guid tenantId, Guid productId, long baseVersion, Guid operationId,
        Guid makerSubjectId, string normalizedName, Guid? templateId, string? templateCode,
        IEnumerable<Guid> candidatePrincipalIds, string reasonCode, bool commentRequired, bool evidenceRequired,
        int? configuredDueAfterSeconds, long? dueAtUtcTicks, string objectType, string objectId, string objectRef,
        string idempotencyKey) => Hash(string.Join('\n', "contract=global-product-correction-v1",
            $"tenant={tenantId:D}", $"global-product={productId:D}", $"base-version={baseVersion}",
            $"operation={operationId:D}", $"maker={makerSubjectId:D}",
            $"proposed-name={Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedName))}",
            $"template-id={templateId?.ToString("D") ?? string.Empty}",
            $"template-code={templateCode ?? string.Empty}",
            $"candidates={string.Join(',', candidatePrincipalIds.Select(x => x.ToString("D")))}",
            $"reason={Convert.ToBase64String(Encoding.UTF8.GetBytes(reasonCode))}",
            $"comment-required={commentRequired}", $"evidence-required={evidenceRequired}",
            $"due-after-seconds={configuredDueAfterSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}",
            $"due-at-utc-ticks={dueAtUtcTicks?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}",
            $"object-type={objectType}", $"object-id={objectId}",
            $"object-ref={Convert.ToBase64String(Encoding.UTF8.GetBytes(objectRef))}",
            $"idempotency-key={idempotencyKey}"));

    public ProductIdentityWorkflowStartRequest Rehydrate(GlobalProductCorrectionOperation operation) => new(
        operation.WorkflowTemplateId, operation.WorkflowTemplateCode, operation.ObjectType, operation.ObjectId,
        operation.ObjectRef, operation.CandidatePrincipalIds.Select(x => x.ToString("D")).ToArray(),
        operation.ReasonCode, operation.StartIdempotencyKey, operation.CommentRequired,
        operation.EvidenceRequired, operation.DueAtUtcTicksV1.HasValue
            ? new(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);

    public bool MatchesConfiguration(GlobalProductCorrectionOperation operation) =>
        operation.WorkflowTemplateId == configuration.TemplateId
        && string.Equals(operation.WorkflowTemplateCode, configuration.TemplateCode, StringComparison.Ordinal)
        && operation.CandidatePrincipalIds.Order().SequenceEqual(configuration.CandidatePrincipalIds.Order())
        && string.Equals(operation.ReasonCode, configuration.ReasonCode, StringComparison.Ordinal)
        && operation.CommentRequired == configuration.CommentRequired
        && operation.EvidenceRequired == configuration.EvidenceRequired
        && operation.ConfiguredDueAfterSeconds == (configuration.DueAfter.HasValue
            ? checked((int)configuration.DueAfter.Value.TotalSeconds) : null);

    private void EnsureConfiguration()
    {
        if ((configuration.TemplateId.HasValue == !string.IsNullOrWhiteSpace(configuration.TemplateCode))
            || configuration.TemplateId == Guid.Empty
            || configuration.CandidatePrincipalIds is not { Count: >= 1 and <= 100 }
            || configuration.CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || configuration.CandidatePrincipalIds.Distinct().Count() != configuration.CandidatePrincipalIds.Count
            || !Exact(configuration.ReasonCode, 128)
            || !ExactOptional(configuration.TemplateCode, 128)
            || !ExactOptional(configuration.IdentityTemplateCode, 128)
            || configuration.IdentityTemplateId == Guid.Empty
            || configuration.DueAfter is { } dueAfter
               && (dueAfter < TimeSpan.FromMinutes(1) || dueAfter > TimeSpan.FromDays(30)
                   || dueAfter.Ticks % TimeSpan.TicksPerSecond != 0)
            || configuration.TemplateId.HasValue && !string.IsNullOrWhiteSpace(configuration.IdentityTemplateCode)
            || !string.IsNullOrWhiteSpace(configuration.TemplateCode) && configuration.IdentityTemplateId.HasValue
            || configuration.TemplateId.HasValue && configuration.IdentityTemplateId.HasValue
               && configuration.TemplateId == configuration.IdentityTemplateId
            || !string.IsNullOrWhiteSpace(configuration.TemplateCode)
               && !string.IsNullOrWhiteSpace(configuration.IdentityTemplateCode)
               && string.Equals(configuration.TemplateCode, configuration.IdentityTemplateCode,
                   StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_CONFIGURATION_INVALID");
    }

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ExactOptional(string? value, int max) => value is null || Exact(value, max);
    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
