using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class SignerAttestation : TenantScopedEntity
{
    [SetsRequiredMembers]
    public SignerAttestation()
    {
        TenantId = Guid.Empty;
    }

    public Guid EnvelopeId { get; init; }
    public Guid ParticipantId { get; init; }
    public Guid SignerUserId { get; init; }
    public string SignerEmailSnapshot { get; init; } = string.Empty;
    public string SignerDisplayNameSnapshot { get; init; } = string.Empty;
    public string AttestationText { get; init; } = string.Empty;
    public string PolicyCode { get; init; } = SignatureEnvelope.InternalPolicyCode;
    public DateTimeOffset AttestedAt { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
}
