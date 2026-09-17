using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

// WP-DM-DCP005-DEADCODE-01 — GetDocumentReferenceListVersionsHandler and SearchDocumentReferencesHandler (the
// CSV list's own read handlers) were removed here: their endpoints (document-list/versions, document-list/search)
// and the screen that called them were retired in WP-DM-DCP005-RETIRE-CSV-01, and a repo-wide search found no
// other caller of either class. SearchDocumentCitationsHandler below is the CURRENT, live picker source (the
// Document Master Register) and is untouched.

/// <summary>
/// DCP-005 Step 2 — the picker's search, against the live Document Master Register.
///
/// <para>A thin pass-through to <see cref="IControlledDocumentCitationPort.SearchAsync"/>: no RBAC of its own
/// (the controller action carries the permission), no reject decision (blocked rows are returned, never
/// hidden — the same rule the retired CSV list's own search used).</para>
/// </summary>
public sealed class SearchDocumentCitationsHandler
    : IRequestHandler<SearchDocumentCitationsQuery, Response<IReadOnlyList<DocumentReferenceEntryDto>>>
{
    private readonly IControlledDocumentCitationPort _citations;

    public SearchDocumentCitationsHandler(IControlledDocumentCitationPort citations) => _citations = citations;

    public async Task<Response<IReadOnlyList<DocumentReferenceEntryDto>>> Handle(
        SearchDocumentCitationsQuery query, CancellationToken ct)
    {
        // Bounded the same way the CSV search was: a picker that returns hundreds of rows to a blank box is a
        // scroll, not a search.
        var limit = query.Limit is > 0 and <= 200 ? query.Limit : 50;
        var result = await _citations.SearchAsync(query.Term, limit, ct);

        return Response<IReadOnlyList<DocumentReferenceEntryDto>>.Success(
            result.Items.Select(ToDto).ToList(), 200, query.CorrelationId);
    }

    /// <summary>
    /// <see cref="DocumentCitationItem"/> is shaped to mirror this DTO field-for-field (see its own doc comment)
    /// except for two CSV-only concepts the register does not track — <c>GqmsDomain</c> and
    /// <c>IsMandatoryGroupSop</c> — which go through as "we don't have this" (null / false), not a guess.
    /// </summary>
    private static DocumentReferenceEntryDto ToDto(DocumentCitationItem item) => new(
        item.Uid, item.Code, item.Title, item.Version, item.Lifecycle,
        GqmsDomain: null, IsMandatoryGroupSop: false,
        LinkableInErp: item.Citable, LinkBlockedReason: item.BlockedReason);
}
