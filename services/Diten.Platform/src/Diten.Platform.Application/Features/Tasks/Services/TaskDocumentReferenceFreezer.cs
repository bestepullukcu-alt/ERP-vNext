using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Domain.Entities.Tasks;

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
///
/// <para><b>DCP-005 Step 2 — repointed from the CSV list to the live Document Master Register</b>
/// (<see cref="IControlledDocumentCitationPort"/>, WP-PSS-DCP005-STEP2-CITATION-REPOINT-01). The source moved;
/// the two refusals did not:
/// <list type="bullet">
/// <item><b>Unresolved is refused.</b> A UID the register does not list at all cannot be frozen — writing one
/// would be a citation nobody can reproduce.</item>
/// <item><b>Blocked is refused.</b> The register's own citable judgment (<c>DocumentCitationItem.Citable</c> —
/// Effective ∨ UnderRevision, the SAME rule the effectiveness gate uses) replaces the CSV-era <c>LinkableInErp</c>
/// flag at citation time. The picker shows a blocked row with its reason so the reader sees WHY; this refuses it
/// because a screen is not a boundary — an API caller never passes the picker. Control Tower decision
/// (2026-09-11): the WP's AC4 first read "Blocked freezes with its true status", which would have let a task be
/// opened under a Superseded or Retired procedure through the API while the screen refused the same row. The
/// task-TYPE activation gate (Step 3, "Kural 4" / G3) is a different question and stays separate.</item>
/// </list>
/// </para>
/// </summary>
public sealed class TaskDocumentReferenceFreezer
{
    private readonly IControlledDocumentCitationPort _citations;

    public TaskDocumentReferenceFreezer(IControlledDocumentCitationPort citations) => _citations = citations;

    /// <summary>
    /// Work out the task's new citation list from the UIDs the caller asked for.
    ///
    /// <para>A UID already cited keeps its FROZEN object; a UID the register resolves is frozen now, whatever its
    /// lifecycle; a UID the register does not list at all is refused with a reason code, because silently
    /// dropping it would tell the author they cited something they did not.</para>
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

        // Fail-closed by construction: a register read that cannot complete throws from ResolveAsync (contract
        // §2/§3/§5) rather than resolving into an empty/Unresolved answer, and that exception is left to
        // propagate — never caught here and turned into a fabricated refusal or a fabricated success.
        var result = await _citations.ResolveAsync(
            new DocumentCitationQuery(toFreeze, DocumentIdentifierKind.Uid), ct);
        var byUid = result.Items.ToDictionary(i => i.Uid, StringComparer.OrdinalIgnoreCase);

        foreach (var uid in toFreeze)
        {
            // ResolveAsync omits identifiers with no register row — Unresolved is "absent", not a status value.
            if (!byUid.TryGetValue(uid, out var item))
            {
                return TaskDocumentFreezeResult.Failed(TaskReasonCodes.DocumentReferenceNotFound, uid);
            }

            /*
             * ⚠ THE BLOCK IS ENFORCED HERE, not only in the picker. The screen refuses a blocked row because a
             * reader must see why; this refuses it because a screen is not a boundary — an API caller, an
             * import, or a future slice reaches the same handler without passing the picker at all.
             */
            if (!item.Citable)
            {
                return TaskDocumentFreezeResult.Failed(TaskReasonCodes.DocumentReferenceBlocked, uid);
            }

            kept.Add(new TaskDocumentReference
            {
                DocumentUid = item.Uid,
                DocumentCode = item.Code,
                Title = item.Title,
                DocumentVersion = item.Version,
                // The register's own lifecycle word, frozen as written — Effective or UnderRevision, since a
                // blocked row was refused above.
                Status = item.Lifecycle,
                // The moment of citation, not of saving: an edit that touches the title does not re-date a
                // document the author chose last week.
                ReferencedAt = now,
                // No CSV list version behind a register-sourced citation (see TaskItem.DocumentReferences remark).
                ListVersionId = null,
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
