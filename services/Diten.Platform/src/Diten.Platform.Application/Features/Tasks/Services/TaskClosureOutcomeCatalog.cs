using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The SYSTEM closure outcomes — the ones we ship, translated in all seven tenant languages, identical in every
/// tenant.
///
/// <para><b>DELIBERATELY SHORT, and the shortness is the design.</b> Every entry here appears in every tenant's
/// picker, in seven languages, forever. So the bar is: does this outcome answer a question the LIFECYCLE cannot,
/// in words no business has to reinterpret? Five do. "Approved/Rejected", "Resolved/Unresolved",
/// "Passed/Failed" do not — they are a TYPE's vocabulary, and Oracle's rule (outcomes belong to the task
/// definition) is what says so. A tenant writes those itself, in its own words, via
/// <see cref="TaskClosureOutcome.LabelText"/>.</para>
///
/// <para><b>Nothing here is seeded onto a type.</b> The catalogue is the MENU an administrator may pick from;
/// <see cref="TaskType.ClosureOutcomes"/> stays empty until somebody chooses. That is what keeps every task type
/// written before this feature behaving exactly as it does today.</para>
///
/// <para><b>A static catalogue rather than rows</b>, for the reason <see cref="TaskTransitionCodes"/> gives about
/// its own map: these codes are code-owned, they ship with the resx entries that name them, and a tenant editing
/// one would break the translation it is bound to. Tenant-owned outcomes are a different thing and have their own
/// half of <see cref="TaskClosureOutcome"/>.</para>
/// </summary>
public static class TaskClosureOutcomeCatalog
{
    /// <summary>The prefix every system outcome's label key carries — the convention server-emitted labels already
    /// use (<c>WorkAggregation_NativeStatus_*</c>, <c>WorkAggregation_ActionDisabled_*</c>).</summary>
    public const string ResourceKeyPrefix = "WorkAggregation_ClosureOutcome_";

    /// <summary>The work was finished the way it was asked for. The ordinary close, and the reason the picker is
    /// not an interrogation: the common case costs one click and no sentence.</summary>
    public const string CompletedAsRequested = "COMPLETED_AS_REQUESTED";

    /// <summary>Finished, but not all of it. Requires a reason — "partially" with no account of WHICH part is a
    /// worse record than no outcome at all, because it looks like one.</summary>
    public const string CompletedPartially = "COMPLETED_PARTIALLY";

    /// <summary>The work turned out not to be needed. No reason required: "it was not needed" is already the whole
    /// sentence, and demanding a second one is how a field fills with "not needed".</summary>
    public const string CancelledNotRequired = "CANCELLED_NOT_REQUIRED";

    /// <summary>Replaced by other work. Requires a reason, because the record is useless without naming what
    /// replaced it.</summary>
    public const string CancelledSuperseded = "CANCELLED_SUPERSEDED";

    /// <summary>The same work was already raised elsewhere. Requires a reason, for the same reason as superseded:
    /// a duplicate that does not say of WHAT cannot be followed.</summary>
    public const string CancelledDuplicate = "CANCELLED_DUPLICATE";

    private static readonly IReadOnlyList<TaskClosureOutcome> Entries =
    [
        Entry(CompletedAsRequested, "CompletedAsRequested", TaskClosureDisposition.Completed, requiresReason: false, 10),
        Entry(CompletedPartially, "CompletedPartially", TaskClosureDisposition.Completed, requiresReason: true, 20),
        Entry(CancelledNotRequired, "CancelledNotRequired", TaskClosureDisposition.Cancelled, requiresReason: false, 30),
        Entry(CancelledSuperseded, "CancelledSuperseded", TaskClosureDisposition.Cancelled, requiresReason: true, 40),
        Entry(CancelledDuplicate, "CancelledDuplicate", TaskClosureDisposition.Cancelled, requiresReason: true, 50)
    ];

    /// <summary>The whole catalogue. Fresh instances per call — these are mutable entities, and handing out the
    /// shared ones would let a caller edit the catalogue by accident.</summary>
    public static IReadOnlyList<TaskClosureOutcome> All =>
        Entries.Select(entry => new TaskClosureOutcome
        {
            Code = entry.Code,
            LabelResourceKey = entry.LabelResourceKey,
            Disposition = entry.Disposition,
            RequiresReason = entry.RequiresReason,
            SortOrder = entry.SortOrder
        }).ToList();

    /// <summary>Whether a code is one of ours. Used to refuse a tenant outcome that squats on a system code — the
    /// squatter would inherit our translation and mean something else in it.</summary>
    public static bool IsSystemCode(string? code) =>
        Entries.Any(entry => string.Equals(entry.Code, (code ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The canonical resource key for a system code, or null when the code is not ours.</summary>
    public static string? ResourceKeyFor(string? code) =>
        Entries.FirstOrDefault(entry =>
            string.Equals(entry.Code, (code ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            ?.LabelResourceKey;

    private static TaskClosureOutcome Entry(
        string code, string keySuffix, TaskClosureDisposition disposition, bool requiresReason, int sortOrder) =>
        new()
        {
            Code = code,
            LabelResourceKey = ResourceKeyPrefix + keySuffix,
            Disposition = disposition,
            RequiresReason = requiresReason,
            SortOrder = sortOrder
        };
}
