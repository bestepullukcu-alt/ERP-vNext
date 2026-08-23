namespace Diten.Platform.Domain.Entities.Tasks;

/// <summary>
/// ONE field that changed in ONE save, recorded on the <see cref="TaskTransition"/> that save produced.
///
/// <para><b>Why it hangs off a transition rather than living in a collection of its own.</b> The reader's question
/// is "what happened to this task", and it has exactly one answer — a single feed. Two collections would mean two
/// streams to merge on screen, ordered by two clocks, with the merge rule written somewhere neither of them can
/// see. The transition log already models an act that changed nothing about the lifecycle: <c>FromLifecycle</c>
/// equals <c>ToLifecycle</c> for ownership moves, and that state is documented on the entity.</para>
///
/// <para><b>Values are SHORT or absent.</b> A due date, a priority, a person's name — those are the answer.
/// A four-thousand character description is not: keeping both versions of it would turn a log into a backup, and
/// the row would be larger than the task it describes. See <see cref="TaskFieldChangeLimits.MaxRecordedValue"/>
/// for the measured threshold.</para>
/// </summary>
public sealed class TaskFieldChange
{
    /// <summary>
    /// WHICH field, as a stable code from <see cref="TaskFieldChangeCodes"/> — never a localized name and never
    /// a C# property name. The screen turns it into a sentence in the reader's own language.
    /// </summary>
    public required string Field { get; set; }

    /// <summary>
    /// The value BEFORE, rendered short. Null when there was none, and also null when
    /// <see cref="ValuesOmitted"/> is set — those two are told apart by that flag rather than by guessing.
    /// </summary>
    public string? From { get; set; }

    /// <summary>The value AFTER, on the same terms as <see cref="From"/>.</summary>
    public string? To { get; set; }

    /// <summary>
    /// The values were too long to keep, so this row says only that the field changed.
    ///
    /// <para>Stated rather than inferred: "no from, no to" would otherwise be indistinguishable from "it was
    /// empty and is still empty", which is not a change at all and would never have been recorded.</para>
    /// </summary>
    public bool ValuesOmitted { get; set; }

    /// <summary>
    /// For a CONFIGURABLE field (<see cref="TaskFieldChangeCodes.CustomField"/>), the definition's code — the
    /// key the catalogue is looked up by.
    ///
    /// <para>⚠ It is what makes the READ path able to enforce BL-024: a field whose value the reader may not see
    /// must not have its old and new values readable in the history either. The projection resolves the
    /// definition from this and asks the same <c>TaskFieldAccessRules</c> the value itself goes through.</para>
    /// </summary>
    public string? DefinitionCode { get; set; }
}

/// <summary>
/// The fields whose changes are RECORDED. Declared as a closed vocabulary, in one place, because the alternative
/// is a diff that quietly grows to cover everything on the entity — including counters, version stamps and
/// internal state, none of which anybody asked "who changed this" about.
/// </summary>
public static class TaskFieldChangeCodes
{
    public const string DueAt = "dueAt";
    public const string StartAt = "startAt";
    public const string PlannedDate = "plannedDate";
    public const string Priority = "priority";
    public const string Assignee = "assignee";
    public const string Title = "title";
    public const string Description = "description";
    public const string EstimateHours = "estimateHours";
    public const string Tags = "tags";

    /// <summary>A tenant-defined field. Its identity travels in <see cref="TaskFieldChange.DefinitionCode"/>.</summary>
    public const string CustomField = "customField";

    /// <summary>
    /// Every code, for the tests that walk the vocabulary — and for the guard that proves nothing outside this
    /// set is ever written.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        DueAt, StartAt, PlannedDate, Priority, Assignee, Title, Description, EstimateHours, Tags, CustomField
    };
}

public static class TaskFieldChangeLimits
{
    /// <summary>
    /// How long a value may be and still be recorded verbatim. Longer, and the row keeps only the FACT of the
    /// change (<see cref="TaskFieldChange.ValuesOmitted"/>).
    ///
    /// <para><b>MEASURED against the live corpus (2026-08-23), not chosen for roundness.</b> Of 139 task titles
    /// the longest is 46 characters and the 90th percentile is 37 — so every real title keeps its values. Of 11
    /// descriptions the longest is 86 and the median is 30, so a description crosses this only when somebody
    /// writes a paragraph, which is exactly the case where two copies in a log row stop being useful. The
    /// stored ceilings are 200 (title) and 4000 (description): a before/after pair at the top of that range
    /// would make one history row twenty times the size of the task it describes.</para>
    /// </summary>
    public const int MaxRecordedValue = 60;
}
