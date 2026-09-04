using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;

// DCP-005 (document-management side, Phase 1) — the SINGLE source of truth for controlled-document effectiveness.
// Both consumers are thin adapters over this one query: the in-process IControlledDocumentEffectivenessPort (the
// activation gate) and, later (Phase 3), the HTTP controller action (screen only). No side effects (read).

/// <summary>
/// DCP-005 — resolve the effectiveness of a batch of controlled-document identifiers against the live Document Master
/// Register. <paramref name="By"/> is explicit (no silent default — contract §1); <paramref name="Identifiers"/> are
/// matched against the corresponding register field. Returns a per-identifier <see cref="DocumentEffectivenessResult"/>;
/// an infrastructure failure of the underlying read propagates (it is never folded into an Unresolved result).
/// </summary>
public sealed record ResolveDocumentEffectivenessQuery(
    IReadOnlyList<string> Identifiers,
    DocumentIdentifierKind By,
    string CorrelationId) : IRequest<Response<DocumentEffectivenessResult>>;
