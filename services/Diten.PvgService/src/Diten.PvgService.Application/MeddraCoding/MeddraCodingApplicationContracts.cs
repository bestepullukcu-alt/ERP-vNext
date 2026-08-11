using Diten.PvgService.Domain.MeddraCoding;

namespace Diten.PvgService.Application.MeddraCoding;

public sealed record PvgServerTenantContext(string TenantContextReference);

public sealed record PvgActorContext(string ActorReference, string ActorTypeReference);

public sealed record PvgCorrelationContext(string CorrelationReference);

public sealed record PvgPermissionDecision(bool IsAllowed, MeddraCodingReasonCode DeniedReason);

public sealed record PvgGuardDecision(bool IsAllowed, MeddraCodingReasonCode DeniedReason)
{
    public static PvgGuardDecision Allow() => new(true, MeddraCodingReasonCode.None);

    public static PvgGuardDecision Deny(MeddraCodingReasonCode reasonCode) => new(false, reasonCode);
}

public sealed record CreateMeddraCodingWorkItemCommand(
    Mod0231SourceTermReference SourceTermReference,
    PvgServerTenantContext ServerTenantContext,
    PvgActorContext ActorContext,
    PvgPermissionDecision PermissionDecision,
    PvgCorrelationContext CorrelationContext,
    PvgGuardDecision SourceTermHandoffGuard,
    PvgGuardDecision FieldPolicyGuard,
    PvgGuardDecision AuditIntentMetadataGuard,
    PvgGuardDecision DictionaryGovernanceGuard);

public sealed record ProposeMeddraCodedTermCommand(
    string CodingWorkItemReference,
    MeddraCodedTermReference ProposedTerm,
    PvgServerTenantContext ServerTenantContext,
    PvgActorContext ActorContext,
    PvgPermissionDecision PermissionDecision,
    PvgCorrelationContext CorrelationContext,
    PvgGuardDecision SourceTermHandoffGuard,
    PvgGuardDecision FieldPolicyGuard,
    PvgGuardDecision AuditIntentMetadataGuard,
    PvgGuardDecision DictionaryGovernanceGuard);

public sealed record MarkMeddraCodingReviewedCommand(
    string CodingWorkItemReference,
    MeddraCodingReviewStatus ReviewStatus,
    PvgServerTenantContext ServerTenantContext,
    PvgActorContext ActorContext,
    PvgPermissionDecision PermissionDecision,
    PvgCorrelationContext CorrelationContext,
    PvgGuardDecision SourceTermHandoffGuard,
    PvgGuardDecision FieldPolicyGuard,
    PvgGuardDecision AuditIntentMetadataGuard,
    PvgGuardDecision DictionaryGovernanceGuard);

public sealed record GetMeddraCodingMetadataByIdQuery(
    string CodingWorkItemReference,
    PvgServerTenantContext ServerTenantContext,
    PvgActorContext ActorContext,
    PvgPermissionDecision PermissionDecision,
    PvgCorrelationContext CorrelationContext,
    PvgGuardDecision FieldPolicyGuard);

public sealed record GetMeddraCodingMetadataListQuery(
    PvgServerTenantContext ServerTenantContext,
    PvgActorContext ActorContext,
    PvgPermissionDecision PermissionDecision,
    PvgCorrelationContext CorrelationContext,
    PvgGuardDecision FieldPolicyGuard);

public sealed record MeddraCodingSafeResult(
    bool IsAllowed,
    MeddraCodingReasonCode ReasonCode,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static MeddraCodingSafeResult Allowed(MeddraCodingOperation operation) =>
        new(
            true,
            MeddraCodingReasonCode.None,
            new Dictionary<string, string>
            {
                ["module"] = "MOD-0232",
                ["operation"] = operation.ToString(),
                ["result"] = "Allowed"
            });

    public static MeddraCodingSafeResult Blocked(MeddraCodingOperation operation, MeddraCodingReasonCode reasonCode) =>
        new(
            false,
            reasonCode,
            new Dictionary<string, string>
            {
                ["module"] = "MOD-0232",
                ["operation"] = operation.ToString(),
                ["result"] = "Blocked"
            });
}

public sealed record MeddraCodingOperationResult(
    MeddraCodingSafeResult Result,
    IReadOnlyList<MeddraCodingMetadataRecord> Records)
{
    public static MeddraCodingOperationResult Allowed(
        MeddraCodingOperation operation,
        IReadOnlyList<MeddraCodingMetadataRecord>? records = null) =>
        new(MeddraCodingSafeResult.Allowed(operation), records ?? Array.Empty<MeddraCodingMetadataRecord>());

    public static MeddraCodingOperationResult Blocked(
        MeddraCodingOperation operation,
        MeddraCodingReasonCode reasonCode) =>
        new(MeddraCodingSafeResult.Blocked(operation, reasonCode), Array.Empty<MeddraCodingMetadataRecord>());

    public static MeddraCodingOperationResult FromSafeResult(MeddraCodingSafeResult result) =>
        new(result, Array.Empty<MeddraCodingMetadataRecord>());
}

public sealed record MeddraCodingMetadataRecord(
    string CodingWorkItemReference,
    MeddraCodingReviewStatus ReviewStatus,
    bool HasProposedTerm);

public enum MeddraCodingOperation
{
    CreateWorkItem = 0,
    ProposeCodedTerm = 1,
    MarkReviewed = 2,
    GetById = 3,
    List = 4
}

public enum MeddraCodingReasonCode
{
    None = 0,
    MissingServerTenantContext = 1,
    MissingActorContext = 2,
    PermissionDenied = 3,
    MissingCorrelationContext = 4,
    InvalidCorrelationContext = 5,
    MissingSourceTermHandoff = 6,
    SourceTermHandoffDenied = 7,
    FieldPolicyDenied = 8,
    AuditIntentDenied = 9,
    DictionaryGovernanceMissing = 10,
    DictionaryGovernanceDenied = 11,
    InvalidRequest = 12,
    NotFound = 13
}

public static class MeddraCodingContractGuard
{
    private const int MaxCorrelationReferenceLength = 128;

    public static MeddraCodingSafeResult Evaluate(CreateMeddraCodingWorkItemCommand command)
    {
        var commonResult = EvaluateMutationGuards(
            MeddraCodingOperation.CreateWorkItem,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.SourceTermHandoffGuard,
            command.FieldPolicyGuard,
            command.AuditIntentMetadataGuard,
            command.DictionaryGovernanceGuard);

        if (!commonResult.IsAllowed)
        {
            return commonResult;
        }

        return IsValid(command.SourceTermReference)
            ? MeddraCodingSafeResult.Allowed(MeddraCodingOperation.CreateWorkItem)
            : MeddraCodingSafeResult.Blocked(MeddraCodingOperation.CreateWorkItem, MeddraCodingReasonCode.InvalidRequest);
    }

    public static MeddraCodingSafeResult Evaluate(ProposeMeddraCodedTermCommand command)
    {
        var commonResult = EvaluateMutationGuards(
            MeddraCodingOperation.ProposeCodedTerm,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.SourceTermHandoffGuard,
            command.FieldPolicyGuard,
            command.AuditIntentMetadataGuard,
            command.DictionaryGovernanceGuard);

        if (!commonResult.IsAllowed)
        {
            return commonResult;
        }

        return HasValue(command.CodingWorkItemReference) && IsValid(command.ProposedTerm)
            ? MeddraCodingSafeResult.Allowed(MeddraCodingOperation.ProposeCodedTerm)
            : MeddraCodingSafeResult.Blocked(MeddraCodingOperation.ProposeCodedTerm, MeddraCodingReasonCode.InvalidRequest);
    }

    public static MeddraCodingSafeResult Evaluate(MarkMeddraCodingReviewedCommand command)
    {
        var commonResult = EvaluateMutationGuards(
            MeddraCodingOperation.MarkReviewed,
            command.ServerTenantContext,
            command.ActorContext,
            command.PermissionDecision,
            command.CorrelationContext,
            command.SourceTermHandoffGuard,
            command.FieldPolicyGuard,
            command.AuditIntentMetadataGuard,
            command.DictionaryGovernanceGuard);

        if (!commonResult.IsAllowed)
        {
            return commonResult;
        }

        return HasValue(command.CodingWorkItemReference) && command.ReviewStatus == MeddraCodingReviewStatus.Reviewed
            ? MeddraCodingSafeResult.Allowed(MeddraCodingOperation.MarkReviewed)
            : MeddraCodingSafeResult.Blocked(MeddraCodingOperation.MarkReviewed, MeddraCodingReasonCode.InvalidRequest);
    }

    public static MeddraCodingSafeResult Evaluate(GetMeddraCodingMetadataByIdQuery query)
    {
        var commonResult = EvaluateReadGuards(
            MeddraCodingOperation.GetById,
            query.ServerTenantContext,
            query.ActorContext,
            query.PermissionDecision,
            query.CorrelationContext,
            query.FieldPolicyGuard);

        if (!commonResult.IsAllowed)
        {
            return commonResult;
        }

        return HasValue(query.CodingWorkItemReference)
            ? MeddraCodingSafeResult.Allowed(MeddraCodingOperation.GetById)
            : MeddraCodingSafeResult.Blocked(MeddraCodingOperation.GetById, MeddraCodingReasonCode.InvalidRequest);
    }

    public static MeddraCodingSafeResult Evaluate(GetMeddraCodingMetadataListQuery query) =>
        EvaluateReadGuards(
            MeddraCodingOperation.List,
            query.ServerTenantContext,
            query.ActorContext,
            query.PermissionDecision,
            query.CorrelationContext,
            query.FieldPolicyGuard);

    private static MeddraCodingSafeResult EvaluateMutationGuards(
        MeddraCodingOperation operation,
        PvgServerTenantContext serverTenantContext,
        PvgActorContext actorContext,
        PvgPermissionDecision permissionDecision,
        PvgCorrelationContext correlationContext,
        PvgGuardDecision sourceTermHandoffGuard,
        PvgGuardDecision fieldPolicyGuard,
        PvgGuardDecision auditIntentMetadataGuard,
        PvgGuardDecision dictionaryGovernanceGuard)
    {
        var readResult = EvaluateReadGuards(operation, serverTenantContext, actorContext, permissionDecision, correlationContext, fieldPolicyGuard);
        if (!readResult.IsAllowed)
        {
            return readResult;
        }

        if (!sourceTermHandoffGuard.IsAllowed)
        {
            return MeddraCodingSafeResult.Blocked(operation, sourceTermHandoffGuard.DeniedReason);
        }

        if (!auditIntentMetadataGuard.IsAllowed)
        {
            return MeddraCodingSafeResult.Blocked(operation, auditIntentMetadataGuard.DeniedReason);
        }

        if (!dictionaryGovernanceGuard.IsAllowed)
        {
            return MeddraCodingSafeResult.Blocked(operation, dictionaryGovernanceGuard.DeniedReason);
        }

        return MeddraCodingSafeResult.Allowed(operation);
    }

    private static MeddraCodingSafeResult EvaluateReadGuards(
        MeddraCodingOperation operation,
        PvgServerTenantContext serverTenantContext,
        PvgActorContext actorContext,
        PvgPermissionDecision permissionDecision,
        PvgCorrelationContext correlationContext,
        PvgGuardDecision fieldPolicyGuard)
    {
        if (!HasValue(serverTenantContext.TenantContextReference))
        {
            return MeddraCodingSafeResult.Blocked(operation, MeddraCodingReasonCode.MissingServerTenantContext);
        }

        if (!HasValue(actorContext.ActorReference) || !HasValue(actorContext.ActorTypeReference))
        {
            return MeddraCodingSafeResult.Blocked(operation, MeddraCodingReasonCode.MissingActorContext);
        }

        if (!permissionDecision.IsAllowed)
        {
            return MeddraCodingSafeResult.Blocked(operation, permissionDecision.DeniedReason);
        }

        if (!HasValue(correlationContext.CorrelationReference))
        {
            return MeddraCodingSafeResult.Blocked(operation, MeddraCodingReasonCode.MissingCorrelationContext);
        }

        if (correlationContext.CorrelationReference.Length > MaxCorrelationReferenceLength)
        {
            return MeddraCodingSafeResult.Blocked(operation, MeddraCodingReasonCode.InvalidCorrelationContext);
        }

        if (!fieldPolicyGuard.IsAllowed)
        {
            return MeddraCodingSafeResult.Blocked(operation, fieldPolicyGuard.DeniedReason);
        }

        return MeddraCodingSafeResult.Allowed(operation);
    }

    private static bool IsValid(Mod0231SourceTermReference sourceTermReference) =>
        HasValue(sourceTermReference.SourceTermReference) &&
        HasValue(sourceTermReference.CaseProcessingReference) &&
        HasValue(sourceTermReference.LifecycleStateReference) &&
        sourceTermReference.IsApprovedForCoding;

    private static bool IsValid(MeddraCodedTermReference codedTermReference) =>
        IsValid(codedTermReference.DictionaryVersion) &&
        HasValue(codedTermReference.CodeReferenceToken) &&
        HasValue(codedTermReference.HierarchyReferenceToken);

    private static bool IsValid(MeddraDictionaryVersionReference dictionaryVersionReference) =>
        HasValue(dictionaryVersionReference.DictionaryVersionReference) &&
        HasValue(dictionaryVersionReference.CodesetVersionReference) &&
        dictionaryVersionReference.IsGovernanceApproved;

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
