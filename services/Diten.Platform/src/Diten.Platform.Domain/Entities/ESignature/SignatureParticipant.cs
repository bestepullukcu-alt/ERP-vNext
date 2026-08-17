using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.ESignature;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class SignatureParticipant : TenantScopedEntity
{
    [SetsRequiredMembers]
    public SignatureParticipant()
    {
        TenantId = Guid.Empty;
    }

    public Guid EnvelopeId { get; init; }
    public Guid SignerUserId { get; init; }
    public string SignerEmail { get; init; } = string.Empty;
    public string SignerDisplayName { get; init; } = string.Empty;
    public int SigningOrder { get; init; }
    public string Role { get; init; } = string.Empty;
    public SignatureParticipantStatus Status { get; private set; } = SignatureParticipantStatus.Pending;
    public DateTimeOffset? SignedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public void MarkSigned(DateTimeOffset now)
    {
        if (Status != SignatureParticipantStatus.Pending)
        {
            throw new InvalidOperationException($"Participant status '{Status}' cannot be signed.");
        }

        Status = SignatureParticipantStatus.Signed;
        SignedAt = now;
        UpdatedAt = now;
        Version++;
    }
}
