using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TaskRecurrenceRules;

/// <summary>
/// MOD-0024 Phase 4 — the recurring-task rule form (BL-052).
///
/// <para>The engine has been complete for a while: the entity, the hourly sweep that generates exactly once per
/// period, and five CRUD endpoints. What did not exist was any way for a person to define a rule — it could only
/// be done by calling the API. This is that screen's model.</para>
/// </summary>
public sealed class TaskRecurrenceRuleEditViewModel : IValidatableObject
{
    /// <summary>
    /// WHO generated work goes to. <b>"Myself" is deliberately absent</b>, and the reason is in the engine:
    /// a background sweep has no "self". A rule that said so produced tasks assigned to nobody — invisible in
    /// every list, while still consuming the period, so the work could never be generated again. The server
    /// refuses it (<c>allowSelfAssigned: false</c>); this list is why the form cannot even offer it.
    /// </summary>
    public static readonly string[] AllowedAssignmentTargets = ["Person", "PositionPool"];

    public static readonly string[] AllowedFrequencies = ["Daily", "Weekly", "Monthly", "Quarterly", "Yearly"];

    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Frequency { get; set; } = "Monthly";

    /// <summary>
    /// "Every N periods" — 2 + Weekly reads "every two weeks". Nullable ON PURPOSE (UI-020): a non-nullable int
    /// makes MVC emit data-val-required, so a blank box refuses to submit under a rule nobody wrote.
    /// </summary>
    [Range(1, 365)]
    public int? Interval { get; set; } = 1;

    public DateTime? StartsAt { get; set; }

    /// <summary>
    /// Empty means <b>open-ended</b>, and that is a supported answer rather than a missing one — most real
    /// recurring work ("monthly close") has no end date at all.
    /// </summary>
    public DateTime? EndsAt { get; set; }

    public string AssignmentTarget { get; set; } = "Person";

    public Guid? AssigneeUserId { get; set; }

    public Guid? PoolPositionId { get; set; }

    /// <summary>Optional. With a template the generated task carries its checklist; without one it is a bare
    /// reminder holding the rule's name, which is a legitimate simple case.</summary>
    public Guid? TaskTemplateId { get; set; }

    public bool IsActive { get; set; } = true;

    public int ExpectedVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AllowedFrequencies.Contains(Frequency, StringComparer.Ordinal))
        {
            // A frequency the engine's enum does not have deserializes to nothing useful on the far side, and
            // the rule then sits there looking saved while never firing.
            yield return new ValidationResult("RecurrenceFrequencyInvalid", [nameof(Frequency)]);
        }

        /*
         * The assignment rule, and the reason it is here and not only in the <select>.
         *
         * A hidden option is one devtools edit away; what the browser SENDS is the only thing that is actually
         * true. The server refuses SelfAssigned too — this is the pre-check that lets the form say so in the
         * reader's own language instead of surfacing a gateway error.
         */
        if (!AllowedAssignmentTargets.Contains(AssignmentTarget, StringComparer.Ordinal))
        {
            yield return new ValidationResult("RecurrenceAssignmentTargetInvalid", [nameof(AssignmentTarget)]);
        }
        else if (AssignmentTarget == "Person" && AssigneeUserId is null)
        {
            // "To a person" with no person generates work nobody receives — the same invisible-work outcome
            // under a legal target.
            yield return new ValidationResult("RecurrenceAssigneeRequired", [nameof(AssigneeUserId)]);
        }
        else if (AssignmentTarget == "PositionPool" && PoolPositionId is null)
        {
            yield return new ValidationResult("RecurrencePoolRequired", [nameof(PoolPositionId)]);
        }

        // EndsAt absent is OPEN-ENDED and entirely legal. Only a window that cannot contain a period is refused.
        if (StartsAt is not null && EndsAt is not null && EndsAt <= StartsAt)
        {
            yield return new ValidationResult("RecurrenceWindowInvalid", [nameof(EndsAt)]);
        }
    }
}

/// <summary>One row of the list, as the API returns it.</summary>
public sealed class TaskRecurrenceRuleListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int Interval { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public Guid? TaskTemplateId { get; set; }
    public string AssignmentTarget { get; set; } = string.Empty;
    public Guid? AssigneeUserId { get; set; }
    public Guid? PoolPositionId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastGeneratedAt { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
