using Diten.Platform.Domain.Entities.ESignature;

namespace Diten.Platform.Domain.Repositories;

public interface IESignatureRepository
{
    Task<string> GenerateEnvelopeNumberAsync(CancellationToken ct = default);
    Task CreateEnvelopeAsync(SignatureEnvelope envelope, IReadOnlyList<SignatureParticipant> participants, CancellationToken ct = default);
    Task<SignatureEnvelope?> GetEnvelopeAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesAsync(int skip, int take, CancellationToken ct = default);
    Task<long> CountEnvelopesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SignatureParticipant>> GetParticipantsAsync(Guid envelopeId, CancellationToken ct = default);
    Task<SignatureParticipant?> GetParticipantAsync(Guid envelopeId, Guid participantId, CancellationToken ct = default);
    Task<bool> UpdateParticipantAsync(SignatureParticipant participant, int expectedVersion, CancellationToken ct = default);
    Task<bool> UpdateEnvelopeAsync(SignatureEnvelope envelope, int expectedVersion, CancellationToken ct = default);
    Task<bool> CommitAttestationAsync(
        SignatureEnvelope envelope,
        int expectedEnvelopeVersion,
        SignatureParticipant participant,
        int expectedParticipantVersion,
        SignerAttestation attestation,
        InternalSignatureArtifact? artifact,
        SignatureVerificationArtifact? verification,
        CancellationToken ct = default);
    Task AddAttestationAsync(SignerAttestation attestation, CancellationToken ct = default);
    Task<IReadOnlyList<SignerAttestation>> GetAttestationsAsync(Guid envelopeId, CancellationToken ct = default);
    Task AddArtifactAsync(InternalSignatureArtifact artifact, CancellationToken ct = default);
    Task<InternalSignatureArtifact?> GetArtifactAsync(Guid artifactId, CancellationToken ct = default);
    Task AddVerificationAsync(SignatureVerificationArtifact verification, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureVerificationArtifact>> GetVerificationsAsync(Guid envelopeId, CancellationToken ct = default);
    Task AddAuditExportAsync(SignatureAuditExport auditExport, CancellationToken ct = default);
}
