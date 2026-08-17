using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class InternalSignatureArtifact : TenantScopedEntity
{
    [SetsRequiredMembers]
    public InternalSignatureArtifact()
    {
        TenantId = Guid.Empty;
    }

    public Guid EnvelopeId { get; init; }
    public string ArtifactType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
    public string Sha256 { get; init; } = string.Empty;
    public DateTimeOffset RetainUntil { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
}
