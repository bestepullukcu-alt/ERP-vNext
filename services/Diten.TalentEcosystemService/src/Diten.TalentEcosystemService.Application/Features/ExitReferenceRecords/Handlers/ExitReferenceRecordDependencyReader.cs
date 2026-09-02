using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

internal sealed record ExitReferenceRecordDependencies(
    TepAssociationMembershipRegistry? Association,
    TepConsentVisibilityPolicy? Policy,
    TepVerifiedParticipantAccess? VerifiedAccess,
    TepReviewBoardCaseMetadata? ReviewCase,
    TepTrustLevelPolicyMetadata? TrustPolicy,
    TepCandidateProfileMetadata? CandidateProfile);

internal static class ExitReferenceRecordDependencyReader
{
    public static async Task<ExitReferenceRecordDependencies> ReadAsync(
        Guid tenantId,
        TepExitReferenceRecordMetadata entity,
        ITepAssociationMembershipRegistryRepository associationRepository,
        ITepConsentVisibilityPolicyRepository policyRepository,
        ITepVerifiedParticipantAccessRepository verifiedRepository,
        ITepReviewBoardCaseMetadataRepository reviewRepository,
        ITepTrustLevelPolicyMetadataRepository trustRepository,
        ITepCandidateProfileMetadataRepository candidateRepository,
        CancellationToken ct)
    {
        var association = entity.AssociationMembershipReference is { } associationId
            ? await associationRepository.GetByIdAsync(tenantId, associationId, ct)
            : null;
        var policy = entity.ConsentVisibilityPolicyReference is { } policyId
            ? await policyRepository.GetByIdAsync(tenantId, policyId, ct)
            : null;
        var verified = entity.VerifiedParticipantReference is { } verifiedId
            ? await verifiedRepository.GetByIdAsync(tenantId, verifiedId, ct)
            : null;
        var review = entity.ReviewBoardCaseReference is { } reviewId
            ? await reviewRepository.GetByIdAsync(tenantId, reviewId, ct)
            : null;
        var trust = entity.TrustLevelPolicyReference is { } trustId
            ? await trustRepository.GetByIdAsync(tenantId, trustId, ct)
            : null;
        var candidate = entity.CandidateProfileReference is { } candidateId
            ? await candidateRepository.GetByIdAsync(tenantId, candidateId, ct)
            : null;

        return new ExitReferenceRecordDependencies(association, policy, verified, review, trust, candidate);
    }
}
