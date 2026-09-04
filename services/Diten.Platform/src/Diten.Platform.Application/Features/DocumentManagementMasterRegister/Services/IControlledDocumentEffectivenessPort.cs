using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

// DCP-005 (document-management side, Phase 1) — the in-process gate the Task Center (MOD-0024) calls to check
// controlled-document effectiveness at task-type activation. It is a THIN adapter over the single application-layer
// resolver (ResolveDocumentEffectivenessQuery); it adds no RBAC (the /active screen already sits under
// TaskTypesManage) and makes no decision of its own — it returns state + reason and lets the caller apply the
// activation rule (contract §0/§3).

/// <summary>
/// DCP-005 — batch effectiveness query for the in-process port. Deliberately CorrelationId-free (contract §3): the
/// correlation id is a transport/logging concern the resolver owns, not part of the gate's input.
/// </summary>
public sealed record DocumentEffectivenessQuery(
    IReadOnlyList<string> Identifiers,
    DocumentIdentifierKind By);

/// <summary>
/// DCP-005 — the internal gate over the effectiveness resolver. The activation rule (allow ONLY when every document is
/// Effective; any Blocked / Unresolved / thrown exception ⇒ refuse) is applied by the CALLER using this result; a
/// failure of the underlying read propagates from <see cref="ResolveAsync"/> as an exception and is never returned as
/// an Unresolved item (fail-closed — contract §2/§3/§5).
/// </summary>
public interface IControlledDocumentEffectivenessPort
{
    Task<DocumentEffectivenessResult> ResolveAsync(DocumentEffectivenessQuery query, CancellationToken ct);
}
