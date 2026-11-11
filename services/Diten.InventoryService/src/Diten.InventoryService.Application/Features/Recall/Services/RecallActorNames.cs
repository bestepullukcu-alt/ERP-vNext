using System.Globalization;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Application.Features.Recall.Handlers;
using Diten.InventoryService.Domain.Recall.Repositories;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// WP-MVP1-NAMES1 (RECALL 1.8.0, owner decision A) — the name AT THE TIME OF THE ACTION of every person who DID something in a recall answer:
/// the action record's own <c>ActorName</c> (case events), else the central audit queue's <c>ActorDisplayName</c> of the SAME correlation id and
/// the SAME user, this tenant AND this company (<see cref="Common.IAuditActorNames"/>); neither → no name (the screen says "unknown user",
/// never the id). A command writes the changed field and its record with ONE time, so a person is found by (user, the second of the action):
/// the time is the person's own (<c>UserAt.atUtc</c>) or the one beside it (<c>openedAtUtc</c>, <c>startedAtUtc</c>, <c>completedAtUtc</c>,
/// <c>setAtUtc</c>, <c>changedAtUtc</c>, <c>updatedAtUtc</c>, the timeline entry's <c>atUtc</c>). The one person shown without a time — the
/// preparer of a case action — is found by the prepared audit row of the same case and user. An ASSIGNED person (DrillPlan.owner) is never
/// named by the service. The service never calls Auth. One answer = one read per store + ONE queue read, whatever the number of rows.
/// </summary>
public sealed class RecallActorNames
{
    private readonly Common.IAuditActorNames _audit;
    private readonly IRecallActorRecords _records;
    private readonly RecallSupport _support;

    public RecallActorNames(Common.IAuditActorNames audit, IRecallActorRecords records, RecallSupport support)
    {
        _audit = audit;
        _records = records;
        _support = support;
    }

    /// <summary>True for the answer types that carry people (the behavior passes every other answer through untouched).</summary>
    public static bool Names(Type data) => Known.Contains(data);

    private static readonly HashSet<Type> Known =
    [
        typeof(RecallCaseDetailDto), typeof(RecallCasePageDto), typeof(RecallDashboardDto), typeof(RecallTimelinePageDto),
        typeof(RecallNotificationDto), typeof(RecallConsumptionDto), typeof(RecallSegregationDto), typeof(IReadOnlyList<RecallCustomerRowDto>),
        typeof(RecallReturnLinkDto), typeof(RecallReturnsDto), typeof(RecallDrillRunDto), typeof(RecallDrillRunPageDto),
        typeof(RecallEvidenceExportDto), typeof(RecallEvidenceExportPageDto), typeof(RecallSettingsDto), typeof(RecallSettingsHistoryPageDto)
    ];

    /// <summary>The answer with its people named (same type, same everything else).</summary>
    public async Task<object> NameAsync(object data, CancellationToken ct)
    {
        var people = new People();
        Walk(data, people, null);
        if (people.Timed.Count == 0 && people.Prepared.Count == 0) return data;

        var set = await _records.AtAsync(people.Timed.Select(p => (p.User, p.Second)).ToList(), ct);
        var prepared = await _records.AuditRecordsAsync(people.Prepared.Select(p => p.Case).Distinct().ToList(), ct);
        var records = set.CaseEvents.Select(e => e.ActorUserId is { } u ? new Rec(u, RecallSupport.Wire(e.AtUtc), e.CorrelationId, Clean(e.ActorName), e.CaseId, e.Kind.ToString()) : (Rec?)null)
            .Concat(set.DrillEvents.Select(e => (Rec?)new Rec(e.ActorUserId, RecallSupport.Wire(e.AtUtc), e.CorrelationId, null, e.SubjectId, e.Kind.ToString())))
            .Concat(set.AuditRecords.Concat(prepared).Select(a => a.UserId is { } u && a.CorrelationId is { } c ? new Rec(u, RecallSupport.Wire(a.OccurredAt), c, null, a.SubjectId, a.Action) : (Rec?)null))
            .Concat(set.SettingsChanges.Select(h => (Rec?)new Rec(h.ChangedBy, RecallSupport.Wire(h.ChangedAtUtc), h.CorrelationId, null, Guid.Empty, RecallContract.SubjectSettings)))
            .OfType<Rec>().ToList();

        var keys = records.Where(r => r.Stored is null && r.Correlation != Guid.Empty).Select(r => (r.Correlation, r.User)).Distinct().ToList();
        var queue = keys.Count == 0
            ? new Dictionary<(Guid CorrelationId, Guid ActorId), string>()
            : await _audit.NamesAsync(keys, _support.LegalEntityId, ct);
        var book = new Book(records, queue);
        return Walk(data, null, book);
    }

    private static string? Clean(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private readonly record struct Rec(Guid User, string AtUtc, Guid Correlation, string? Stored, Guid Subject, string What);

    private sealed class People
    {
        public readonly List<(Guid User, DateTimeOffset Second)> Timed = [];
        public readonly List<(Guid User, Guid Case)> Prepared = [];
    }

    private sealed class Book(List<Rec> records, IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string> queue)
    {
        /// <summary>The record of the same user at the same second; one that kept its own name (a case event) first.</summary>
        public string? At(string userId, string? atUtc)
        {
            if (atUtc is null || !Guid.TryParse(userId, out var user)) return null;
            var hits = records.Where(r => r.User == user && r.AtUtc == atUtc).OrderBy(r => r.Stored is null).ToList();
            return hits.Select(Name).FirstOrDefault(n => n is not null);
        }

        /// <summary>The latest prepared-action audit row of the same case and user.</summary>
        public string? Prepared(string userId, string caseId)
        {
            if (!Guid.TryParse(userId, out var user) || !Guid.TryParse(caseId, out var id)) return null;
            return records.Where(r => r.User == user && r.Subject == id && r.What == RecallContract.AuditActionPrepared)
                .OrderByDescending(r => r.AtUtc, StringComparer.Ordinal).Select(Name).FirstOrDefault(n => n is not null);
        }

        private string? Name(Rec r) => r.Stored ?? (queue.TryGetValue((r.Correlation, r.User), out var n) ? n : null);
    }

    // ── one walk, two passes: collect (book = null) or name (book given) ─────────────────────────────────────────────────────────

    private static object Walk(object data, People? people, Book? book) => data switch
    {
        RecallCaseDetailDto d => Detail(d, people, book),
        RecallCaseRefDto r => r with { Owner = Ref(r.Owner, r.OpenedAtUtc, people, book) },
        RecallCasePageDto p => p with
        {
            Items = p.Items.Select(r => (RecallCaseRefDto)Walk(r, people, book)).ToList(),
            DrillRows = p.DrillRows?.Select(r => r with { Owner = Ref(r.Owner, r.StartedAtUtc, people, book) }).ToList()
        },
        RecallDashboardDto d => d with { OpenCaseRows = d.OpenCaseRows.Select(r => (RecallCaseRefDto)Walk(r, people, book)).ToList() },
        RecallTimelinePageDto t => t with { Items = t.Items.Select(e => e with { Actor = e.Actor is null ? null : Ref(e.Actor, e.AtUtc, people, book) }).ToList() },
        RecallNotificationDto n => n with { RecordedBy = At(n.RecordedBy, people, book) },
        RecallConsumptionDto c => c with { RecordedBy = At(c.RecordedBy, people, book) },
        RecallSegregationDto s => s with { ConfirmedBy = At(s.ConfirmedBy, people, book) },
        IReadOnlyList<RecallCustomerRowDto> rows => rows.Select(r => r with
        {
            Notification = r.Notification is null ? null : (RecallNotificationDto)Walk(r.Notification, people, book),
            Notifications = r.Notifications.Select(n => (RecallNotificationDto)Walk(n, people, book)).ToList(),
            Consumptions = r.Consumptions.Select(c => (RecallConsumptionDto)Walk(c, people, book)).ToList()
        }).ToList(),
        RecallReturnLinkDto l => l with { LinkedBy = At(l.LinkedBy, people, book) },
        RecallReturnsDto r => r with { Links = r.Links.Select(l => (RecallReturnLinkDto)Walk(l, people, book)).ToList() },
        RecallDrillRunDto run => run with
        {
            StartedBy = Ref(run.StartedBy, run.StartedAtUtc, people, book),
            Steps = run.Steps.Select(s => s with { CompletedBy = Ref(s.CompletedBy, s.CompletedAtUtc, people, book) }).ToList(),
            PendingResult = run.PendingResult is { } pr ? pr with { SetBy = Ref(pr.SetBy, pr.SetAtUtc, people, book) } : null,
            Completed = run.Completed is null ? null : At(run.Completed, people, book),
            Cancelled = run.Cancelled is null ? null : At(run.Cancelled, people, book)
        },
        RecallDrillRunPageDto p => p with { Items = p.Items.Select(r => (RecallDrillRunDto)Walk(r, people, book)).ToList() },
        RecallEvidenceExportDto e => e with { ExportedBy = At(e.ExportedBy, people, book) },
        RecallEvidenceExportPageDto p => p with { Items = p.Items.Select(e => (RecallEvidenceExportDto)Walk(e, people, book)).ToList() },
        RecallSettingsDto s => s with { UpdatedBy = s.UpdatedBy is null ? null : Ref(s.UpdatedBy, s.UpdatedAtUtc, people, book) },
        RecallSettingsHistoryPageDto h => h with { Items = h.Items.Select(i => i with { ChangedBy = Ref(i.ChangedBy, i.ChangedAtUtc, people, book) }).ToList() },
        _ => data
    };

    private static RecallCaseDetailDto Detail(RecallCaseDetailDto d, People? people, Book? book) => d with
    {
        Owner = Ref(d.Owner, d.OpenedAtUtc, people, book),
        ScopeSubmittedBy = d.ScopeSubmittedBy is null ? null : At(d.ScopeSubmittedBy, people, book),
        ScopeApprovedBy = d.ScopeApprovedBy is null ? null : At(d.ScopeApprovedBy, people, book),
        EffectivenessCompletedBy = d.EffectivenessCompletedBy is null ? null : At(d.EffectivenessCompletedBy, people, book),
        Closure = d.Closure is { } closure ? closure with { ClosedBy = At(closure.ClosedBy, people, book) } : null,
        Cancellation = d.Cancellation is { } cancel ? cancel with { CancelledBy = At(cancel.CancelledBy, people, book) } : null,
        PendingRevision = d.PendingRevision is { } revision ? revision with { RevisedBy = At(revision.RevisedBy, people, book) } : null,
        PendingAction = d.PendingAction is { } pending ? pending with { SetBy = Prepared(pending.SetBy, d.CaseId, people, book) } : null
    };

    private static RecallUserAtDto At(RecallUserAtDto person, People? people, Book? book)
    {
        Collect(person.UserId, person.AtUtc, people);
        return book is null ? person : person with { Name = book.At(person.UserId, person.AtUtc) };
    }

    private static RecallUserRefDto Ref(RecallUserRefDto person, string? atUtc, People? people, Book? book)
    {
        Collect(person.UserId, atUtc, people);
        return book is null ? person : person with { Name = book.At(person.UserId, atUtc) };
    }

    private static RecallUserRefDto Prepared(RecallUserRefDto person, string caseId, People? people, Book? book)
    {
        if (people is not null && Guid.TryParse(person.UserId, out var user) && Guid.TryParse(caseId, out var id)) people.Prepared.Add((user, id));
        return book is null ? person : person with { Name = book.Prepared(person.UserId, caseId) };
    }

    private static void Collect(string userId, string? atUtc, People? people)
    {
        if (people is null || atUtc is null || !Guid.TryParse(userId, out var user)) return;
        if (DateTimeOffset.TryParseExact(atUtc, RecallSupport.DateTimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var second))
        {
            people.Timed.Add((user, second));
        }
    }
}
