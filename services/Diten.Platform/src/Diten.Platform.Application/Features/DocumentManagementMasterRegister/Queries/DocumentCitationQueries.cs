using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;

// DCP-005 (Phase 2a) — the SINGLE source of truth for controlled-document CITATION reads. Both consumers are thin
// adapters over these: the in-process IControlledDocumentCitationPort (governing-docs resolution + the citation
// freezer + the document picker). No side effects (read). Mirrors the effectiveness resolver's shape.

/// <summary>
/// DCP-005 — resolve a batch of controlled-document identifiers to their CITATION rows against the live Master Register.
/// <paramref name="By"/> is explicit (no silent default — contract §1). Returns one item per RESOLVED identifier
/// (unresolved identifiers are omitted). An infrastructure failure of the underlying read propagates — it is never
/// folded into a missing/uncitable result (fail-closed).
/// </summary>
public sealed record ResolveDocumentCitationQuery(
    IReadOnlyList<string> Identifiers,
    DocumentIdentifierKind By,
    string CorrelationId) : IRequest<Response<DocumentCitationResult>>;

/// <summary>
/// DCP-005 — search the register for citable documents (the picker). Blocked documents are RETURNED (shown, with
/// <c>Citable=false</c>), never hidden. The result is bounded so a blank term is a short list, not the whole register.
/// </summary>
public sealed record SearchDocumentCitationQuery(
    string? Term,
    int Limit,
    string CorrelationId) : IRequest<Response<DocumentCitationResult>>;
