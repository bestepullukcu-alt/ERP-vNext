using Diten.Platform.Domain.Entities.ESignature;

namespace Diten.Platform.Application.Features.ESignature;

public sealed record CreateSignatureParticipantRequest(
    Guid SignerUserId,
    string SignerEmail,
    string SignerDisplayName,
    int SigningOrder,
    string Role);

public sealed record CreateSignatureEnvelopeRequest(
    string SubjectType,
    Guid SubjectId,
    string SubjectVersion,
    Guid DocumentArtifactId,
    string SourceArtifactHash,
    byte[] SourceArtifactContent,
    Guid CorrelationId,
    IReadOnlyList<CreateSignatureParticipantRequest> Participants);

public sealed record RecordInternalAttestationRequest(
    Guid ParticipantId,
    string AttestationText,
    int ExpectedEnvelopeVersion,
    int ExpectedParticipantVersion,
    Guid CorrelationId);

public sealed record CancelSignatureEnvelopeRequest(
    string Reason,
    int ExpectedVersion,
    Guid CorrelationId);

public sealed record SignatureEnvelopeListItemDto(
    Guid Id,
    Guid TenantId,
    string EnvelopeNumber,
    string SubjectType,
    Guid SubjectId,
    string SubjectVersion,
    string Status,
    int ParticipantCount,
    int SignedParticipantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int Version);

public sealed record SignatureParticipantDto(
    Guid Id,
    Guid SignerUserId,
    string SignerEmail,
    string SignerDisplayName,
    int SigningOrder,
    string Role,
    string Status,
    DateTimeOffset? SignedAt,
    int Version);

public sealed record SignerAttestationDto(
    Guid Id,
    Guid ParticipantId,
    Guid SignerUserId,
    string SignerEmailSnapshot,
    string SignerDisplayNameSnapshot,
    string AttestationText,
    string PolicyCode,
    DateTimeOffset AttestedAt,
    Guid CorrelationId);

public sealed record SignatureVerificationDto(
    Guid Id,
    Guid SignedArtifactId,
    string SourceArtifactHash,
    string SignedArtifactHash,
    string VerificationStatus,
    string EvidenceReference,
    DateTimeOffset VerifiedAt,
    Guid CorrelationId);

public sealed record SignatureEnvelopeDetailDto(
    Guid Id,
    Guid TenantId,
    string EnvelopeNumber,
    string SubjectType,
    Guid SubjectId,
    string SubjectVersion,
    Guid DocumentArtifactId,
    string SourceArtifactHash,
    string PolicyCode,
    string Status,
    Guid RequestedBy,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    Guid CorrelationId,
    int Version,
    IReadOnlyList<SignatureParticipantDto> Participants,
    IReadOnlyList<SignerAttestationDto> Attestations,
    IReadOnlyList<SignatureVerificationDto> Verifications);

public sealed record SignatureEnvelopePageDto(
    IReadOnlyList<SignatureEnvelopeListItemDto> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record SignatureArtifactDto(
    Guid Id,
    string FileName,
    string ContentType,
    byte[] Content,
    string Sha256);

internal static class ESignatureMappings
{
    public static SignatureParticipantDto ToDto(SignatureParticipant participant) =>
        new(
            participant.Id,
            participant.SignerUserId,
            participant.SignerEmail,
            participant.SignerDisplayName,
            participant.SigningOrder,
            participant.Role,
            participant.Status.ToString(),
            participant.SignedAt,
            participant.Version);

    public static SignerAttestationDto ToDto(SignerAttestation attestation) =>
        new(
            attestation.Id,
            attestation.ParticipantId,
            attestation.SignerUserId,
            attestation.SignerEmailSnapshot,
            attestation.SignerDisplayNameSnapshot,
            attestation.AttestationText,
            attestation.PolicyCode,
            attestation.AttestedAt,
            attestation.CorrelationId);

    public static SignatureVerificationDto ToDto(SignatureVerificationArtifact verification) =>
        new(
            verification.Id,
            verification.SignedArtifactId,
            verification.SourceArtifactHash,
            verification.SignedArtifactHash,
            verification.VerificationStatus.ToString(),
            verification.EvidenceReference,
            verification.VerifiedAt,
            verification.CorrelationId);
}
