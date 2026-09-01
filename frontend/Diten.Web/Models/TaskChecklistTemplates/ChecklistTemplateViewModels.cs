using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TaskChecklistTemplates;

/// <summary>
/// BL-054 — the reusable checklist form.
///
/// <para>This screen exists BEFORE its sibling on purpose. The task-template form carries a checklist picker;
/// shipping that picker with nothing behind it would repeat, one level in, the defect this whole slice closes —
/// a live-looking control that can never be filled.</para>
/// </summary>
public sealed class ChecklistTemplateEditViewModel : IValidatableObject
{
    /// <summary>The three the engine's enum actually has. A fourth would deserialize to nothing on the far side
    /// and the step would silently become Optional.</summary>
    public static readonly string[] AllowedRequirements = ["Optional", "Required", "Blocking"];

    public Guid? Id { get; set; }

    /// <summary>
    /// The stable key. Read-only once saved — the server refuses a changed code rather than ignoring it, because
    /// quietly keeping the old one would report success for a change the user asked for and did not get.
    /// </summary>
    [Required]
    [StringLength(60)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int ExpectedVersion { get; set; }

    /// <summary>
    /// The steps, in the order they appear on screen. <c>SortOrder</c> is deliberately NOT posted: the server
    /// renumbers from arrival order, because a client-supplied numbering leaves gaps and ties the moment a row is
    /// deleted, after which the same checklist reads in a different order on two screens.
    /// </summary>
    public List<ChecklistTemplateItemViewModel> Items { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var items = Items.Where(item => !item.IsBlank).ToList();

        /*
         * AN EMPTY CHECKLIST IS REFUSED HERE TOO, not only on the server.
         *
         * The server is the authority and refuses it as well; this is the pre-check that lets the form say so in
         * the reader's own language rather than surfacing a gateway error. What it protects against is real: an
         * empty checklist instantiates an empty list onto every task bound to it, and on screen an empty
         * checklist is indistinguishable from one that failed to load.
         */
        if (items.Count == 0)
        {
            yield return new ValidationResult("ChecklistTemplateEmpty", [nameof(Items)]);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.LabelText))
            {
                // A step with a code and no words is a checkbox nobody can act on.
                yield return new ValidationResult("ChecklistItemLabelRequired", [nameof(Items)]);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(item.Code))
            {
                yield return new ValidationResult("ChecklistItemCodeRequired", [nameof(Items)]);
                yield break;
            }

            // The code is the join key a ticked run item is matched back by; two items sharing one make every
            // later tick, edit and removal ambiguous, months later and on a live task.
            if (!seen.Add(item.Code.Trim()))
            {
                yield return new ValidationResult("ChecklistItemCodeDuplicate", [nameof(Items)]);
                yield break;
            }

            if (!AllowedRequirements.Contains(item.Requirement, StringComparer.Ordinal))
            {
                yield return new ValidationResult("ChecklistItemRequirementInvalid", [nameof(Items)]);
                yield break;
            }
        }
    }
}

/// <summary>
/// One step, as the form posts it.
///
/// <para>⚠ <b><c>Code</c> and <c>LabelText</c> are NULLABLE, and that is load-bearing rather than lax.</b> A
/// non-nullable string property gets an IMPLICIT <c>[Required]</c> from MVC, and <c>RequiredAttribute</c> refuses
/// the empty string — so the editor's trailing blank row, which exists purely so there is somewhere to type,
/// failed validation on every save. MEASURED live: the create posted, the server refused it, and the refusal was
/// keyed to <c>Items[N].Code</c> — a field with no validation span on screen — so the form came back looking
/// untouched with no message anywhere. A save that silently does nothing is worse than one that says no.</para>
///
/// <para>Nothing is loosened by this: a row somebody actually typed into is still refused without a code and
/// without wording, by <see cref="ChecklistTemplateEditViewModel.Validate"/>, which reports against
/// <c>Items</c> — a field the form DOES render a message for.</para>
/// </summary>
public sealed class ChecklistTemplateItemViewModel
{
    [StringLength(60)]
    public string? Code { get; set; }

    /// <summary>
    /// The author's own words, in the language they typed them in. There is no resource-key field on this form
    /// and there must not be: a tenant administrator cannot add a line to our resx files, so a key they typed
    /// would render as the key itself.
    /// </summary>
    [StringLength(300)]
    public string? LabelText { get; set; }

    public string Requirement { get; set; } = "Optional";

    public bool EvidenceRequired { get; set; }

    /// <summary>
    /// A row the user added and left untouched. Skipped rather than refused: the editor always keeps one blank
    /// row at the bottom to type into, and refusing it would make every save fail on the row that exists so the
    /// next step can be written.
    /// </summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Code) && string.IsNullOrWhiteSpace(LabelText);
}

/// <summary>One row of the list, as the API returns it.</summary>
public sealed class ChecklistTemplateListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ChecklistTemplateItemViewModel> Items { get; set; } = [];
    public int ItemCount { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
