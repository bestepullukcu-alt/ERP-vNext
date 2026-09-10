using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Commands;

/// <summary>
/// MOD-0262-FU01 — ⛔ COMPENSATION ONLY (DCP-008 AD-6).
/// <para>
/// Removes a stored object whose consumer-side metadata commit failed, so no orphan binary is left behind.
/// This is deliberately <b>not</b> named Delete/Purge/Destroy: it performs no retention evaluation, no
/// legal-hold check and produces no destruction evidence. Physical destruction under a disposition decision is
/// MOD-0262-FU05 and must re-verify holds at execution time.
/// </para>
/// </summary>
public sealed record CompensateRepositoryObjectCommand(Guid ContentId, string CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Delete,
        "RepositoryObject",
        ContentId,
        SourceModule: "MOD-0262-FU01");
}
