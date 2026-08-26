using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The ONE place a <see cref="TaskDocumentReference"/> is created (DCP-005 §6.2).
///
/// <para><b>Why a single seam.</b> Two handlers write task documents today (create and update) and slice 4 will
/// add more. Freezing is a rule that fails silently when it is only mostly followed: a second call site that
/// resolves a title straight from the register looks identical in review and quietly re-dates a citation. There
/// is one function here, and every caller goes through it.</para>
///
/// <para>⚠ <b>THIS CLASS NEVER TOUCHES AN EXISTING REFERENCE.</b> <see cref="ResolveNewAsync"/> is handed the
/// citations a task ALREADY carries and returns only the ones being added; the existing objects are passed back
/// untouched, not re-read and rebuilt. That is what makes "the title is frozen" true rather than aspirational —
/// an update cannot refresh what it never resolves.</para>
/// </summary>
public sealed class TaskDocumentReferenceFreezer
{
    private readonly IDocumentReferenceListRepository _lists;

    public TaskDocumentReferenceFreezer(IDocumentReferenceListRepository lists) => _lists = lists;

    /// <summary>
    /// Work out the task's new citation list from the UIDs the caller asked for.
    ///
    /// <para>Three outcomes, and they are deliberately different: a UID already cited keeps its FROZEN object; a
    /// UID that resolves in the current version is frozen now; a UID that is missing or blocked is refused with a
    /// reason code, because silently dropping it would tell the author they cited something they did not.</para>
    ///
    /// <para>Removal needs no work: a UID absent from <paramref name="requestedUids"/> is simply absent from the
    /// result. Removal is not a change to a frozen value — it is the task no longer making the claim.</para>
    /// </summary>
    public async Task<TaskDocumentFreezeResult> ResolveNewAsync(
        IReadOnlyList<TaskDocumentReference> existing,
        IReadOnlyList<string>? requestedUids,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        // NULL means "the caller is not choosing" and keeps whatever the task already had. An EMPTY list means
        // "no documents" and clears them. The two are different answers and a task that loses its citations
        // because a payload omitted a field is the kind of loss nobody reports until an audit.
        if (requestedUids is null) { return TaskDocumentFreezeResult.Unchanged(existing); }

        var wanted = requestedUids
            .Select(u => (u ?? string.Empty).Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var kept = existing
            .Where(e => wanted.Contains(e.DocumentUid, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var toFreeze = wanted
            .Where(u => !kept.Any(k => string.Equals(k.DocumentUid, u, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toFreeze.Count == 0) { return TaskDocumentFreezeResult.Ok(kept); }

        var current = await _lists.GetLatestVersionAsync(ct);
        if (current is null)
        {
            // No list means nothing can be cited. Saying so is better than writing a citation with no register
            // behind it, which would be a frozen row nobody can reproduce.
            return TaskDocumentFreezeResult.Failed(TaskReasonCodes.DocumentListNotImported, toFreeze[0]);
        }

        var entries = await _lists.GetEntriesByUidsAsync(current.Id, toFreeze, ct);
        var byUid = entries.ToDictionary(e => e.DocumentUid, StringComparer.OrdinalIgnoreCase);

        foreach (var uid in toFreeze)
        {
            if (!byUid.TryGetValue(uid, out var entry))
            {
                return TaskDocumentFreezeResult.Failed(TaskReasonCodes.DocumentReferenceNotFound, uid);
            }

            /*
             * ⚠ THE BLOCK IS ENFORCED HERE, not only in the picker. The screen refuses a blocked row because a
             * reader must see why; this refuses it because a screen is not a boundary — an API caller, an
             * import, or a future slice reaches the same handler without passing the picker at all.
             */
            if (!entry.LinkableInErp)
            {
                return TaskDocumentFreezeResult.Failed(TaskReasonCodes.DocumentReferenceBlocked, uid);
            }

            kept.Add(new TaskDocumentReference
            {
                DocumentUid = entry.DocumentUid,
                DocumentCode = entry.DocumentCode,
                Title = entry.Title,
                DocumentVersion = entry.DocumentVersion,
                Status = entry.Status,
                // The moment of citation, not of saving: an edit that touches the title does not re-date a
                // document the author chose last week.
                ReferencedAt = now,
                ListVersionId = current.Id,
            });
        }

        return TaskDocumentFreezeResult.Ok(kept);
    }
}

/// <summary>The freezer's answer: either the task's new citation list, or the reason one UID could not be cited.</summary>
public sealed record TaskDocumentFreezeResult(
    bool Success,
    List<TaskDocumentReference> References,
    string? ReasonCode,
    string? OffendingUid)
{
    public static TaskDocumentFreezeResult Ok(List<TaskDocumentReference> references) =>
        new(true, references, null, null);

    public static TaskDocumentFreezeResult Unchanged(IReadOnlyList<TaskDocumentReference> existing) =>
        new(true, [.. existing], null, null);

    public static TaskDocumentFreezeResult Failed(string reasonCode, string uid) =>
        new(false, [], reasonCode, uid);
}
