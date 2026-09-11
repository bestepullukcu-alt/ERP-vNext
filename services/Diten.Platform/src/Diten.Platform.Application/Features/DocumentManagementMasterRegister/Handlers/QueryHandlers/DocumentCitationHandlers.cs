using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.QueryHandlers;

/// <summary>
/// DCP-005 Phase 2a — resolve identifiers to CITATION rows against the tenant's Document Master Register. The rich
/// sibling of <c>ResolveDocumentEffectivenessHandler</c>: same batched <c>$in</c> read, same single citable judgment
/// (<see cref="ControlledDocumentLifecyclePolicy.IsOperationallyEffective"/>), but it returns the full citation shape
/// (uid/code/title/version/lifecycle + citable + reason). One item per RESOLVED identifier; an identifier with no
/// register row is OMITTED (the caller diffs requested vs returned to find the unresolved ones).
///
/// FAIL-CLOSED: the register read is NOT wrapped in a try/catch — an infrastructure failure propagates as a thrown
/// exception, never a silently missing or uncitable item (contract §2/§5).
/// </summary>
public sealed class ResolveDocumentCitationHandler(
    IDocumentMasterRegisterRepository register,
    ITenantContext tenantContext)
    : IRequestHandler<ResolveDocumentCitationQuery, Response<DocumentCitationResult>>
{
    public async Task<Response<DocumentCitationResult>> Handle(ResolveDocumentCitationQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(tenantContext);

        // Trim + drop blanks + de-duplicate: "resolve these identifiers" is a set question, and a found row is emitted
        // once. (Blank screening only; full request rejection is the HTTP boundary's job.)
        var requested = request.Identifiers
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Batch $in over ONLY the requested identifiers (shares the effectiveness resolver's repository seam). The read
        // is NOT guarded — a failure surfaces as a thrown exception, never a silent omission.
        var rows = request.By == DocumentIdentifierKind.Uid
            ? await register.GetByPermanentUidsAsync(requested, ct)
            : await register.GetByDocumentCodesAsync(requested, ct);

        var index = new Dictionary<string, DocumentMasterRegisterEntry>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = request.By == DocumentIdentifierKind.Uid ? row.PermanentUid : row.DocumentCode;
            if (!string.IsNullOrWhiteSpace(key))
            {
                index[key.Trim()] = row;
            }
        }

        var items = new List<DocumentCitationItem>(requested.Count);
        foreach (var identifier in requested)
        {
            // Unresolved identifiers are omitted (not a citation). Rows missing the opposite identity field are not
            // citable and are likewise omitted — DocumentCitationMapping returns null for them.
            if (index.TryGetValue(identifier, out var entry)
                && DocumentCitationMapping.ToCitation(entry) is { } item)
            {
                items.Add(item);
            }
        }

        return Response<DocumentCitationResult>.Success(
            new DocumentCitationResult(items), correlationId: request.CorrelationId);
    }
}

/// <summary>
/// DCP-005 Phase 2a — the picker search over the register. Blocked documents are RETURNED (shown, <c>Citable=false</c>),
/// never hidden — so "why can I not cite this SOP" is answerable on screen. The register is small (contract §6.1
/// rationale), so the term filter runs in memory over the existing tenant-scoped read; the repository interface is
/// untouched. FAIL-CLOSED: the read is not guarded.
/// </summary>
public sealed class SearchDocumentCitationHandler(
    IDocumentMasterRegisterRepository register,
    ITenantContext tenantContext)
    : IRequestHandler<SearchDocumentCitationQuery, Response<DocumentCitationResult>>
{
    public async Task<Response<DocumentCitationResult>> Handle(SearchDocumentCitationQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(tenantContext);

        // Bounded: a picker that returns the whole register to a blank box is a scroll, not a search. Mirrors the
        // retired lookup's 1..200 / default-50 bound.
        var limit = request.Limit is > 0 and <= 200 ? request.Limit : 50;
        var term = request.Term?.Trim();

        var rows = await register.GetAllForTenantAsync(ct);

        var matched = rows
            .Select(DocumentCitationMapping.ToCitation)
            .Where(c => c is not null)
            .Select(c => c!)
            .Where(c => string.IsNullOrEmpty(term)
                        || c.Uid.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || c.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || c.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return Response<DocumentCitationResult>.Success(
            new DocumentCitationResult(matched), correlationId: request.CorrelationId);
    }
}

/// <summary>DCP-005 Phase 2a — the ONE register-row → citation mapping, shared by resolve and search.</summary>
internal static class DocumentCitationMapping
{
    /// <summary>
    /// DCP-005 Step 0, Part B (owner decision 2026-09-11) — the CITATION-only judgment. Widens the shared
    /// effectiveness judgment (<see cref="ControlledDocumentLifecyclePolicy.IsOperationallyEffective"/>, unchanged
    /// and untouched — the activation gate reads only that) with Quality's own historical call, but ONLY for a
    /// document still on its way toward Effective: <c>Draft</c>, <c>InReview</c>, <c>ApprovedPendingEffective</c>.
    /// <c>Retired</c> (also the mapped target of the CSV's <c>Void</c> — <c>DocumentRegisterIngestMapping</c>) and
    /// every other terminal/blocked status are ALWAYS non-citable regardless of the quality decision: a withdrawn
    /// or never-valid document does not become citable because Quality once flagged the CSV row linkable.
    /// </summary>
    private static bool IsCitable(ControlledDocumentLifecycleStatus status, bool citableByQualityDecision) =>
        status.IsOperationallyEffective()
        || (citableByQualityDecision && status is ControlledDocumentLifecycleStatus.Draft
            or ControlledDocumentLifecycleStatus.InReview
            or ControlledDocumentLifecycleStatus.ApprovedPendingEffective);

    /// <summary>
    /// Maps a register row to a citation item, or null when the row cannot form a citation — a citation needs BOTH a
    /// Permanent UID and a Document Code (identity), so an incomplete (pre-allocation) row is not citable/displayable.
    /// <c>BlockedReason</c> is the LifecycleStatus name when not citable — unchanged by Part B.
    /// </summary>
    public static DocumentCitationItem? ToCitation(DocumentMasterRegisterEntry e)
    {
        var uid = e.PermanentUid?.Trim();
        var code = e.DocumentCode?.Trim();
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var citable = IsCitable(e.LifecycleStatus, e.CitableByQualityDecision);
        var lifecycle = e.LifecycleStatus.ToString();
        return new DocumentCitationItem(
            uid,
            code,
            e.DocumentTitle,
            e.CurrentVersionLabel,
            lifecycle,
            citable,
            citable ? null : lifecycle,
            DocumentCitationSource.MasterRegister,
            e.Id);
    }
}
