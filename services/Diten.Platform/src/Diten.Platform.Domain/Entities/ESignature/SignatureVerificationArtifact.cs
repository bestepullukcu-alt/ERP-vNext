using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.ESignature;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class SignatureVerificationArtifact : TenantScopedEntity
{
    [SetsRequiredMembers]
    public SignatureVerificationArtifact()
    {
        TenantId = Guid.Empty;
    }

    public Guid EnvelopeId { get; init; }
    public Guid SignedArtifactId { get; init; }
    public string SourceArtifactHash { get; init; } = string.Empty;
    public string SignedArtifactHash { get; init; } = string.Empty;
    public SignatureVerificationStatus VerificationStatus { get; init; }
    public string EvidenceReference { get; init; } = string.Empty;
    public DateTimeOffset VerifiedAt { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
}
