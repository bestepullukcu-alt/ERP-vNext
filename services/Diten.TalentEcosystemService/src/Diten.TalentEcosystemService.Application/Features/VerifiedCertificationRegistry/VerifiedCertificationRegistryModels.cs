using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry;

public class VerifiedCertificationRegistryReadinessCreateRequest
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public VerifiedCertificationRegistryReadinessState VerifiedCertificationRegistryReadinessState { get; init; } = VerifiedCertificationRegistryReadinessState.Draft;
    public VerifiedCertificationRegistryReadinessState CertificationCatalogBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState VerificationIntakeBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState IssuerBindingScopeBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState VisibilityControlBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState RegistryReviewBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState AutomatedDecisionBoundaryState { get; init; } = VerifiedCertificationRegistryReadinessState.Blocked;
    public VerifiedCertificationRegistryReadinessState TalentDataSourceDependencyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState ConsentPolicyDependencyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState SkillPassportSourceDependencyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState NotificationDependencyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState ConsentPreconditionState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState DataMinimizationState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState RetentionPolicyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public VerifiedCertificationRegistryReadinessState PublicationPolicyState { get; init; } = VerifiedCertificationRegistryReadinessState.Deferred;
    public IReadOnlyDictionary<string, VerifiedCertificationRegistryReadinessState> DependencyStates { get; init; } =
        new Dictionary<string, VerifiedCertificationRegistryReadinessState>();
    public string SourceContractVersion { get; init; } = string.Empty;
    public long VerifiedCertificationRegistryReadinessVersion { get; init; } = 1;
    public string? DeferredReason { get; init; }
}

public sealed record VerifiedCertificationRegistryReadinessDto(
    Guid Id,
    string Code,
    string DisplayName,
    VerifiedCertificationRegistryReadinessState VerifiedCertificationRegistryReadinessState,
    VerifiedCertificationRegistryReadinessState CertificationCatalogBoundaryState,
    VerifiedCertificationRegistryReadinessState VerificationIntakeBoundaryState,
    VerifiedCertificationRegistryReadinessState IssuerBindingScopeBoundaryState,
    VerifiedCertificationRegistryReadinessState VisibilityControlBoundaryState,
    VerifiedCertificationRegistryReadinessState RegistryReviewBoundaryState,
    VerifiedCertificationRegistryReadinessState AutomatedDecisionBoundaryState,
    VerifiedCertificationRegistryReadinessState TalentDataSourceDependencyState,
    VerifiedCertificationRegistryReadinessState ConsentPolicyDependencyState,
    VerifiedCertificationRegistryReadinessState SkillPassportSourceDependencyState,
    VerifiedCertificationRegistryReadinessState NotificationDependencyState,
    VerifiedCertificationRegistryReadinessState ConsentPreconditionState,
    VerifiedCertificationRegistryReadinessState DataMinimizationState,
    VerifiedCertificationRegistryReadinessState RetentionPolicyState,
    VerifiedCertificationRegistryReadinessState PublicationPolicyState,
    IReadOnlyDictionary<string, VerifiedCertificationRegistryReadinessState> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    long VerifiedCertificationRegistryReadinessVersion,
    string? DeferredReason);

public sealed record VerifiedCertificationRegistryReadinessListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    VerifiedCertificationRegistryReadinessState VerifiedCertificationRegistryReadinessState,
    VerifiedCertificationRegistryReadinessState CertificationCatalogBoundaryState,
    VerifiedCertificationRegistryReadinessState TalentDataSourceDependencyState,
    VerifiedCertificationRegistryReadinessState VisibilityControlBoundaryState,
    VerifiedCertificationRegistryReadinessState AutomatedDecisionBoundaryState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt);

public sealed record VerifiedCertificationRegistryAuditMetadataDto(
    Guid Id,
    string Code,
    VerifiedCertificationRegistryReadinessState RetentionPolicyState,
    VerifiedCertificationRegistryReadinessState PublicationPolicyState,
    DateTimeOffset? LastEvaluatedAt,
    string? DeferredReason);

internal static class VerifiedCertificationRegistryMapper
{
    public static VerifiedCertificationRegistryReadinessDto ToDto(VerifiedCertificationRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.VerifiedCertificationRegistryReadinessState,
            entity.CertificationCatalogBoundaryState,
            entity.VerificationIntakeBoundaryState,
            entity.IssuerBindingScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.RegistryReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.SkillPassportSourceDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt,
            entity.VerifiedCertificationRegistryReadinessVersion,
            entity.DeferredReason);

    public static VerifiedCertificationRegistryReadinessListItemDto ToListItem(VerifiedCertificationRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.DisplayName,
            entity.VerifiedCertificationRegistryReadinessState,
            entity.CertificationCatalogBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.VisibilityControlBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.SourceContractVersion,
            entity.LastEvaluatedAt);

    public static VerifiedCertificationRegistryAuditMetadataDto ToAuditMetadata(VerifiedCertificationRegistryReadinessMetadata entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.LastEvaluatedAt,
            entity.DeferredReason);
}
