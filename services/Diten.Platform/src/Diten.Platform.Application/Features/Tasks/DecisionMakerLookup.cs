using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks;

/// <summary>
/// BL-512 (WP-PLATFORM-DECISION-MAKERS-SEARCH-01) — the approver / reviewer picker's data source, SEARCH-ONLY.
///
/// <para>It used to hand every person with a live position — name, position, unit and company — to anyone who may
/// create a task, in one request: the whole directory of every company in the group. The rule of the list is
/// unchanged (tenant-bound, a live position, exempt from the company scope because an approver may sit in another
/// company — BL-057); what changes is how much of it one request can see:</para>
/// <list type="bullet">
/// <item><b>search</b>: at least <see cref="MinimumSearchLength"/> characters (trimmed), at most
/// <see cref="MaximumResults"/> rows, matched inside the display name or the position name, case- and
/// Turkish-letter-insensitively (<see cref="Fold"/>). Without it — or with fewer characters — 400
/// <see cref="ReasonCodes.SearchTooShort"/>: the whole list is never returned.</item>
/// <item><b>ids</b>: at most <see cref="MaximumIds"/> ids, answered with only those ids — how an edit form and a
/// summary turn a stored id back into a name. An id with no live position (or of another tenant) simply drops.</item>
/// </list>
/// <para>Each row carries exactly four fields — <see cref="DecisionMakerDto"/>. The id a selection posts back is the
/// user id; e-mail, company id and position id are not needed to show or pick a person and are not sent.
/// (Comparable: SAP and Oracle person pickers are always search-driven and paged; none hands out the directory.)</para>
/// </summary>
public static class DecisionMakerLookup
{
    public const int MinimumSearchLength = 2;
    public const int MaximumResults = 20;
    public const int MaximumIds = 10;

    public static class ReasonCodes
    {
        public const string SearchTooShort = "PEOPLE_SEARCH_TOO_SHORT";
        public const string SearchAndIds = "PEOPLE_LOOKUP_SEARCH_AND_IDS";
        public const string TooManyIds = "PEOPLE_LOOKUP_TOO_MANY_IDS";
        public const string IdsInvalid = "PEOPLE_LOOKUP_IDS_INVALID";
        public const string RateLimited = "PEOPLE_SEARCH_RATE_LIMITED";
        /// <summary>BL-512 FIX1 — the names behind the directory (AuthService) could not be read: 503, never rows
        /// that all read "person not found" and match on position names only.</summary>
        public const string DirectoryUnavailable = "PEOPLE_DIRECTORY_UNAVAILABLE";
    }

    /// <summary>
    /// The comparison form of a text: an EXPLICIT letter map, not a culture. Turkish dotted and dotless i (İ, I, ı, i)
    /// all become "i", and ş/ğ/ü/ö/ç become s/g/u/o/c, in both cases; everything else is lower-cased invariantly.
    /// A culture-dependent ToLower would fold "I" differently on a Turkish server than on an English one.
    /// </summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var folded = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            folded.Append(c switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                _ => char.ToLowerInvariant(c)
            });
        }

        return folded.ToString();
    }
}

/// <summary>One person in the approver / reviewer picker — the four fields, nothing else.</summary>
public sealed record DecisionMakerDto(Guid UserId, string? DisplayName, string PositionName, string OrganizationUnitName);

public sealed record DecisionMakerLookupDto(IReadOnlyList<DecisionMakerDto> People);

/// <summary>The picker's question: <paramref name="Search"/> OR <paramref name="Ids"/> (raw, as the query string carried them).</summary>
public sealed record GetTaskDecisionMakerLookupQuery(string CorrelationId, string? Search, string? Ids)
    : IRequest<Response<DecisionMakerLookupDto>>;

public sealed class GetTaskDecisionMakerLookupHandler : IRequestHandler<GetTaskDecisionMakerLookupQuery, Response<DecisionMakerLookupDto>>
{
    private readonly IMediator _mediator;
    private readonly DecisionMakerDirectoryCache _directory;
    private readonly Diten.Platform.Common.Tenancy.ITenantContext _tenant;
    private readonly Diten.Platform.Application.Contracts.IUserDisplayNameChecker _names;

    public GetTaskDecisionMakerLookupHandler(
        IMediator mediator, DecisionMakerDirectoryCache directory, Diten.Platform.Common.Tenancy.ITenantContext tenant,
        Diten.Platform.Application.Contracts.IUserDisplayNameChecker names)
    {
        _mediator = mediator;
        _directory = directory;
        _tenant = tenant;
        _names = names;
    }

    public async Task<Response<DecisionMakerLookupDto>> Handle(GetTaskDecisionMakerLookupQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim();
        var hasIds = !string.IsNullOrWhiteSpace(request.Ids);
        if (hasIds && !string.IsNullOrEmpty(search))
        {
            return Fail("Ask by search or by ids, not both.", DecisionMakerLookup.ReasonCodes.SearchAndIds, request);
        }

        HashSet<Guid>? ids = null;
        if (hasIds)
        {
            ids = [];
            foreach (var part in request.Ids!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id) || id == Guid.Empty)
                {
                    return Fail("An id is not a valid person id.", DecisionMakerLookup.ReasonCodes.IdsInvalid, request);
                }

                ids.Add(id);
            }

            if (ids.Count == 0)
            {
                return Fail("An id is not a valid person id.", DecisionMakerLookup.ReasonCodes.IdsInvalid, request);
            }

            if (ids.Count > DecisionMakerLookup.MaximumIds)
            {
                return Fail($"At most {DecisionMakerLookup.MaximumIds} ids can be resolved at once.", DecisionMakerLookup.ReasonCodes.TooManyIds, request);
            }
        }
        else if (string.IsNullOrEmpty(search) || search.Length < DecisionMakerLookup.MinimumSearchLength)
        {
            return Fail($"Type at least {DecisionMakerLookup.MinimumSearchLength} characters to search.", DecisionMakerLookup.ReasonCodes.SearchTooShort, request);
        }

        // The eligibility rule itself is the existing one (tenant, live position, company-scope exempt) — one source —
        // and its answer is kept per tenant for a minute (DecisionMakerDirectoryCache), not rebuilt on every pause.
        var tenantId = _tenant.IsResolved && !_tenant.IsPlatformContext ? _tenant.TenantId : Guid.Empty;
        // ATT-FIX1 E3 — an outage seen moments ago is answered at once, not rebuilt and re-awaited per keystroke.
        if (_directory.IsUnavailable(tenantId))
        {
            return Unavailable(request);
        }

        if (!_directory.TryGet(tenantId, out var directory))
        {
            // The rows come UNNAMED (ResolveNames: false): naming them is ONE call here that also says whether it
            // was complete (ATT-FIX2 — a second call afterwards could see AuthService recover, report "complete"
            // and leave the first call's gaps in a kept directory).
            var all = await _mediator.Send(new GetTaskAssignmentPersonLookupQuery(
                request.CorrelationId, TaskPersonLookupPurpose.Decision, ResolveNames: false), ct);
            if (!all.IsSuccessful || all.Data is null)
            {
                return Response<DecisionMakerLookupDto>.Fail(all.Errors ?? [], all.StatusCode, all.ReasonCode, request.CorrelationId);
            }

            /*
             * A missing name is EITHER "this person has no name there" OR "AuthService did not answer" (down, a chunk
             * failed, a few names left in its own cache, the bound ran out). The checked, bounded call says which:
             *  - complete → the directory is kept (a nameless row then truly has no name);
             *  - incomplete but someone is named → served, NOT kept: the next search after AuthService is back is whole;
             *  - incomplete and nobody named → 503 the screen can say, remembered briefly (E3).
             */
            var resolution = await BoundedDisplayNames.ResolveCheckedAsync(_names, all.Data.People.Select(p => p.UserId), ct);
            var named = GetTaskAssignmentPersonLookupHandler.NameAndOrder(all.Data.People, resolution.Names);
            var anyNamed = named.Any(p => !string.IsNullOrWhiteSpace(p.DisplayName));
            if (!resolution.Complete && named.Count > 0 && !anyNamed)
            {
                _directory.MarkUnavailable(tenantId);
                return Unavailable(request);
            }

            directory = named;
            if (resolution.Complete)
            {
                _directory.Set(tenantId, directory);
            }
        }

        IEnumerable<AssignablePersonDto> rows = directory;
        if (ids is not null)
        {
            rows = rows.Where(row => ids.Contains(row.UserId));
        }
        else
        {
            var needle = DecisionMakerLookup.Fold(search);
            rows = rows
                .Where(row => DecisionMakerLookup.Fold(row.DisplayName).Contains(needle, StringComparison.Ordinal)
                              || DecisionMakerLookup.Fold(row.PositionName).Contains(needle, StringComparison.Ordinal))
                .Take(DecisionMakerLookup.MaximumResults);
        }

        var people = rows
            .Select(row => new DecisionMakerDto(row.UserId, row.DisplayName, row.PositionName, row.OrganizationUnitName))
            .ToList();
        return Response<DecisionMakerLookupDto>.Success(new DecisionMakerLookupDto(people), correlationId: request.CorrelationId);
    }

    private static Response<DecisionMakerLookupDto> Unavailable(GetTaskDecisionMakerLookupQuery request) =>
        Response<DecisionMakerLookupDto>.Fail(
            "The people directory cannot be read right now.", 503,
            DecisionMakerLookup.ReasonCodes.DirectoryUnavailable, request.CorrelationId);

    private static Response<DecisionMakerLookupDto> Fail(string error, string reasonCode, GetTaskDecisionMakerLookupQuery request) =>
        Response<DecisionMakerLookupDto>.Fail(error, 400, reasonCode, request.CorrelationId);
}
