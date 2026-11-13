using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Commands;

/// <summary>
/// MOD-0262-FU01 DL-2 — ⛔ COMPENSATION ONLY (DCP-008 AD-6), by a trusted consumer service. Not a purge: no retention
/// evaluation, no legal-hold check, no destruction evidence.
/// <para><b>Repeat-safe (r4 F-11; r5 F-3).</b> <see cref="AlreadyCompensated"/> and <see cref="Scope"/> come from the
/// executor's pre-read of the row (the read that also decides visibility). <c>AuditBehavior</c> takes the metadata
/// before the handler runs, so the record says "at the pre-read this row was already compensated"; the handler
/// decides again on its own read.</para>
/// </summary>
public sealed record CompensateTrustedRepositoryObjectCommand(
    Guid ContentId,
    TrustedRepositoryConsumer Consumer,
    Guid CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    /// <summary>The executor's pre-read: the row was already compensated when this call looked at it.</summary>
    public bool AlreadyCompensated { get; init; }

    /// <summary>The pre-read row's scope (§21 metadata <c>scope</c>).</summary>
    public string Scope { get; init; } = string.Empty;

    public AuditRequestMetadata GetAuditMetadata()
    {
        var metadata = TrustedRepositoryAuditMetadata.For(Consumer, Scope, "compensate");
        metadata["alreadyCompensated"] = AlreadyCompensated;

        return new AuditRequestMetadata(
            AuditCategory.DocumentManagement,
            AuditOperation.Delete,
            "RepositoryObject",
            ContentId,
            SourceModule: "MOD-0262-FU01",
            CorrelationId: CorrelationId,
            BeforeState: new Dictionary<string, object?>
            {
                ["ContentId"] = ContentId,
                ["Scope"] = Scope
            },
            Metadata: metadata);
    }
}
