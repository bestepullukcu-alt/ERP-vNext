using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

public sealed class SignatureAuditExport : TenantScopedEntity
{
    [SetsRequiredMembers]
    public SignatureAuditExport()
    {
        TenantId = Guid.Empty;
    }

    public Guid EnvelopeId { get; init; }
    public Guid ArtifactId { get; init; }
    public Guid RequestedBy { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
}
