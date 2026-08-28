using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>DCP-005 slice 2 — reading the list.</summary>
public sealed class GetDocumentReferenceListVersionsHandler
    : IRequestHandler<GetDocumentReferenceListVersionsQuery, Response<IReadOnlyList<DocumentReferenceListVersionDto>>>
{
    private readonly IDocumentReferenceListRepository _lists;

    public GetDocumentReferenceListVersionsHandler(IDocumentReferenceListRepository lists) => _lists = lists;

    public async Task<Response<IReadOnlyList<DocumentReferenceListVersionDto>>> Handle(
        GetDocumentReferenceListVersionsQuery query, CancellationToken ct)
    {
        var versions = await _lists.ListVersionsAsync(ct);
        return Response<IReadOnlyList<DocumentReferenceListVersionDto>>.Success(
            versions.Select(v => new DocumentReferenceListVersionDto(
                v.Id, v.ListVersion, v.SourceKey, v.FileName, v.ContentHash,
                v.EntryCount, v.LinkableCount, v.ImportedAt,
                // Withdrawn versions stay in this list, marked. Hiding them would erase the record of what the
                // tenant used to cite against.
                v.WithdrawnAt, v.WithdrawnReason, v.WithdrawnBy)).ToList(),
            200, query.CorrelationId);
    }
}

/// <summary>
/// Search the current list.
///
/// <para>⚠ Blocked rows are RETURNED. The caller shows them and refuses to let them be chosen — hiding them
/// would leave "why can I not cite this SOP" unanswerable, which is the opposite of what a zero-count chip
/// does and deliberately so.</para>
/// </summary>
public sealed class SearchDocumentReferencesHandler
    : IRequestHandler<SearchDocumentReferencesQuery, Response<IReadOnlyList<DocumentReferenceEntryDto>>>
{
    private readonly IDocumentReferenceListRepository _lists;

    public SearchDocumentReferencesHandler(IDocumentReferenceListRepository lists) => _lists = lists;

    public async Task<Response<IReadOnlyList<DocumentReferenceEntryDto>>> Handle(
        SearchDocumentReferencesQuery query, CancellationToken ct)
    {
        var current = await _lists.GetLatestVersionAsync(ct);
        if (current is null)
        {
            // Nothing imported yet is an empty answer, not a failure: the list is a prerequisite, not a fault.
            return Response<IReadOnlyList<DocumentReferenceEntryDto>>.Success([], 200, query.CorrelationId);
        }

        // Bounded: a picker that returns 358 rows to a blank box is a scroll, not a search.
        var limit = query.Limit is > 0 and <= 200 ? query.Limit : 50;
        var entries = await _lists.SearchAsync(current.Id, query.Term, limit, ct);

        return Response<IReadOnlyList<DocumentReferenceEntryDto>>.Success(
            entries.Select(e => new DocumentReferenceEntryDto(
                e.DocumentUid, e.DocumentCode, e.Title, e.DocumentVersion, e.Status, e.GqmsDomain,
                e.IsMandatoryGroupSop, e.LinkableInErp, e.LinkBlockedReason)).ToList(),
            200, query.CorrelationId);
    }
}
