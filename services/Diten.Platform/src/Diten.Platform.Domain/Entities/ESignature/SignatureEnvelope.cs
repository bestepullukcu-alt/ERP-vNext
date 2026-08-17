using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.ESignature;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class SignatureEnvelope : TenantScopedEntity
{
    public const string InternalPolicyCode = "INTERNAL_APPROVAL_EVIDENCE_V1";

    [SetsRequiredMembers]
    public SignatureEnvelope()
    {
        TenantId = Guid.Empty;
    }

    public string EnvelopeNumber { get; init; } = string.Empty;
    public string SubjectType { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectVersion { get; init; } = string.Empty;
    public Guid DocumentArtifactId { get; init; }
    public string SourceArtifactHash { get; init; } = string.Empty;
    public string PolicyCode { get; init; } = InternalPolicyCode;
    public SignatureEnvelopeStatus Status { get; private set; } = SignatureEnvelopeStatus.Draft;
    public Guid RequestedBy { get; init; }
    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }

    public void Send(DateTimeOffset now)
    {
        EnsureStatus(SignatureEnvelopeStatus.Draft);
        Status = SignatureEnvelopeStatus.Pending;
        RequestedAt = now;
        Touch(now);
    }

    public void RecordSignatureProgress(bool allParticipantsSigned, DateTimeOffset now)
    {
        if (Status is not SignatureEnvelopeStatus.Pending and not SignatureEnvelopeStatus.PartiallySigned)
        {
            throw new InvalidOperationException($"Envelope status '{Status}' does not accept attestations.");
        }

        Status = allParticipantsSigned
            ? SignatureEnvelopeStatus.Completed
            : SignatureEnvelopeStatus.PartiallySigned;
        CompletedAt = allParticipantsSigned ? now : null;
        Touch(now);
    }

    public void Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        if (Status is SignatureEnvelopeStatus.Completed or SignatureEnvelopeStatus.Cancelled)
        {
            throw new InvalidOperationException($"Envelope status '{Status}' cannot be cancelled.");
        }

        Status = SignatureEnvelopeStatus.Cancelled;
        CancelledAt = now;
        CancelledBy = actorId;
        CancelReason = reason.Trim();
        Touch(now);
    }

    private void EnsureStatus(SignatureEnvelopeStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Envelope status must be '{expected}' but was '{Status}'.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }
}
