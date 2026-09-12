using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

// DCP-005 (Phase 2a) — the in-process CITATION gate the Task Center (MOD-0024) consumes to resolve a task type's
// governing documents, to freeze a task's citations, and to feed the document picker. A thin adapter over the single
// citation resolver (ResolveDocumentCitationQuery / SearchDocumentCitationQuery); it adds no RBAC and makes no decision
// of its own — it returns citation rows + whether each may be cited, and the caller decides (contract §0/§3). The rich
// sibling of IControlledDocumentEffectivenessPort: effectiveness answers "in force?", citation answers "what do I cite?".

/// <summary>
/// DCP-005 — batch citation-resolution input. Deliberately CorrelationId-free (the correlation id is a transport
/// concern the resolver owns, not part of the gate's input — same shape as <c>DocumentEffectivenessQuery</c>). No
/// silent default for <see cref="By"/> (contract §1).
/// </summary>
public sealed record DocumentCitationQuery(
    IReadOnlyList<string> Identifiers,
    DocumentIdentifierKind By);

/// <summary>
/// DCP-005 — the internal citation gate over the register. FROZEN contract (WP-DM-2a): the Task Center's
/// governing-documents resolution, citation freezer and picker all consume THIS.
/// <list type="bullet">
/// <item><see cref="ResolveAsync"/> — resolve identifiers to citation rows (governing-docs + freezer). One item per
/// RESOLVED identifier; unresolved ones are omitted (the caller diffs requested vs returned).</item>
/// <item><see cref="SearchAsync"/> — term search for the picker; blocked rows are returned with
/// <see cref="DocumentCitationItem.Citable"/> = false, never hidden.</item>
/// </list>
/// Fail-closed: a failure of the underlying read propagates from either method as an exception and is never returned
/// as a missing or uncitable item (contract §2/§3/§5).
/// </summary>
public interface IControlledDocumentCitationPort
{
    Task<DocumentCitationResult> ResolveAsync(DocumentCitationQuery query, CancellationToken ct);

    Task<DocumentCitationResult> SearchAsync(string? term, int limit, CancellationToken ct);
}
