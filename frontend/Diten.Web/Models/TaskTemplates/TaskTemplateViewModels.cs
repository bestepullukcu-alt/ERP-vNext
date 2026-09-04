using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TaskTemplates;

/// <summary>
/// BL-054 — the reusable task-shape form. The second half of the missing link: a recurrence rule bound to one of
/// these generates a task carrying a priority, a due date and a checklist instead of a bare title.
/// </summary>
public sealed class TaskTemplateEditViewModel : IValidatableObject
{
    public static readonly string[] AllowedPriorities = ["Low", "Medium", "High"];

    /// <summary>
    /// The default holders a TEMPLATE may carry. <b>"A person" is deliberately absent</b>, and the reason is
    /// structural rather than stylistic: a template has a pool position and no assignee field, so "assign to a
    /// person" would name nobody — and the generated task would land in the failure the recurrence rule already
    /// paid for once, created for nobody and visible in no list. A rule that names a person still works; it
    /// passes its own assignment, which OVERRIDES this.
    /// </summary>
    public static readonly string[] AllowedAssignmentTargets = ["SelfAssigned", "PositionPool"];

    public Guid? Id { get; set; }

    /// <summary>Read-only once saved. The server refuses a changed code rather than ignoring it.</summary>
    [Required]
    [StringLength(60)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The title each generated task gets. Falls back to <see cref="Name"/> when empty.</summary>
    [StringLength(300)]
    public string? TitleTemplate { get; set; }

    [StringLength(2000)]
    public string? DescriptionTemplate { get; set; }

    public string DefaultPriority { get; set; } = "Medium";

    public string DefaultAssignmentTarget { get; set; } = "SelfAssigned";

    public Guid? DefaultPoolPositionId { get; set; }

    /// <summary>
    /// "Due N days after the task is created." Nullable ON PURPOSE (UI-020): a non-nullable int makes MVC emit
    /// data-val-required, so a blank box would refuse to submit under a rule nobody wrote — and no due offset is
    /// a legitimate answer.
    /// </summary>
    [Range(1, 3650)]
    public int? DefaultDueInDays { get; set; }

    /// <summary>Optional. Empty means the generated task carries no checklist at all.</summary>
    public Guid? ChecklistTemplateId { get; set; }

    /// <summary>
    /// WHICH COMPANY this template belongs to. Empty = every company.
    ///
    /// <para><b>A single id, never a list.</b> A multi-select rots: the day a new company is opened, every
    /// template that should also cover it has to be found and edited one at a time, and nobody does that — so
    /// the list comes to mean "the companies we had when somebody last looked". A shape three companies share is
    /// three templates, each changeable in its own company without touching the other two.</para>
    /// </summary>
    public Guid? LegalEntityId { get; set; }

    public bool IsActive { get; set; } = true;

    public int ExpectedVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AllowedPriorities.Contains(DefaultPriority, StringComparer.Ordinal))
        {
            // A priority the engine's enum does not have deserializes to nothing useful on the far side, and the
            // template then sits there looking saved while generating tasks at a priority nobody chose.
            yield return new ValidationResult("TemplatePriorityInvalid", [nameof(DefaultPriority)]);
        }

        /*
         * The assignment rule, and why it is here as well as in the <select>.
         *
         * A hidden option is one devtools edit away; what the browser SENDS is the only thing that is actually
         * true. The server refuses "a person" too — this is the pre-check that lets the form say so in the
         * reader's own language instead of surfacing a gateway error.
         */
        if (!AllowedAssignmentTargets.Contains(DefaultAssignmentTarget, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "TemplateAssignmentTargetInvalid", [nameof(DefaultAssignmentTarget)]);
        }
        else if (DefaultAssignmentTarget == "PositionPool"
                 && (DefaultPoolPositionId is null || DefaultPoolPositionId == Guid.Empty))
        {
            // "To a pool" with no position generates work nobody is offered.
            yield return new ValidationResult("TemplatePoolRequired", [nameof(DefaultPoolPositionId)]);
        }
    }
}

/// <summary>One row of the list, as the API returns it.</summary>
public sealed class TaskTemplateListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TitleTemplate { get; set; }
    public string DefaultPriority { get; set; } = string.Empty;
    public string DefaultAssignmentTarget { get; set; } = string.Empty;
    public int? DefaultDueInDays { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
