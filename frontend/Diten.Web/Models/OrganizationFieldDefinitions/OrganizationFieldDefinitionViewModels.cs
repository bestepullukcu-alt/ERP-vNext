using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.OrganizationFieldDefinitions;

/// <summary>
/// MOD-0288-FU04 — the Organization field-definition authoring form.
///
/// <para>Modelled on <c>TaskFieldDefinitionEditViewModel</c> and sharing nothing with it. The two carry
/// different fields (this one has <c>IsQueryable</c> and declarative constraints; the Task one has label
/// sources and an access state), so a common base would exist only to be widened by both and understood by
/// neither. FU02 §7 settled this boundary for the backend and it holds on the screen.</para>
/// </summary>
public sealed class OrganizationFieldDefinitionEditViewModel : IValidatableObject
{
    public Guid? Id { get; set; }

    /// <summary>
    /// ⚠ IMMUTABLE AFTER CREATION, AND THE FORM MUST MAKE THAT TRUE RATHER THAN SAY IT. Stored values join to
    /// their definition, and the FU02 update request has no <c>Code</c> member at all — so a code typed on an
    /// edit form would be accepted by this model, ignored by the server, and reported to the user as a saved
    /// change that never happened. The edit view renders it disabled; <see cref="IsEdit"/> is what the
    /// server-side guard reads so the rule does not depend on the markup alone.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The tenant's own words. ⚠ NOT a resource key: a tenant cannot add a line to our resx files, so a key
    /// here would render as the key itself on screen. FU02 keeps <c>Name</c> free text for exactly that reason.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string DataType { get; set; } = "Text";

    public bool IsRequired { get; set; }

    /// <summary>Server-enforced (FU02 §8 decision 5), not a display preference. The help text says so.</summary>
    public bool IsQueryable { get; set; }

    public string Classification { get; set; } = "Normal";

    // Nullable ON PURPOSE (UI-020, same reason as the Task precedent): a non-nullable int makes MVC emit
    // data-val-required, so a field the form presents as optional refuses to submit when left blank — a
    // required rule nobody wrote and nobody can see.
    [Range(0, 999)]
    public int? DisplayOrder { get; set; }

    // ── Declarative constraints. Bounded per type; there is deliberately no expression input (FU02 §4). ──
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? ReferenceTarget { get; set; }

    /// <summary>The choices for a <c>SingleSelect</c>. Empty for every other type.</summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// Read-only on the form. FU02 has no "activate" endpoint — deactivation is one-way and lives on its own
    /// route — so rendering a switch here would offer a change the server cannot make.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int ExpectedVersion { get; set; }

    /// <summary>True when this model came from an existing definition. Drives the immutability guard below.</summary>
    public bool IsEdit => Id.HasValue && Id.Value != Guid.Empty;

    /// <summary>
    /// The code as it is stored, carried on the edit form so the POST can prove the disabled input was not
    /// worked around. A disabled input posts nothing, so without this the guard would have nothing to compare.
    /// </summary>
    public string? OriginalCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        /*
         * ⚠ THE IMMUTABILITY IS ENFORCED HERE, NOT ONLY IN THE MARKUP. A disabled input is a UI courtesy: a
         * crafted POST reaches this model with any code at all. The server refuses the change anyway — the
         * FU02 update request cannot carry a code — but refusing it silently would tell the user their edit
         * was saved. This turns that into a visible validation error.
         */
        if (IsEdit
            && !string.IsNullOrWhiteSpace(OriginalCode)
            && !string.Equals(Code?.Trim(), OriginalCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "The code cannot be changed after the field is created.",
                [nameof(Code)]);
        }

        // A single-choice field with no choices is an empty dropdown, which the server refuses (FU02
        // ValidateConstraints). Caught here so the message arrives before the round trip, not instead of it.
        if (string.Equals(DataType, "SingleSelect", StringComparison.OrdinalIgnoreCase)
            && Options.All(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "A single-choice field needs at least one choice.",
                [nameof(Options)]);
        }

        if (MinLength.HasValue && MaxLength.HasValue && MinLength > MaxLength)
        {
            yield return new ValidationResult(
                "The minimum length cannot exceed the maximum length.",
                [nameof(MinLength), nameof(MaxLength)]);
        }

        if (MinValue.HasValue && MaxValue.HasValue && MinValue > MaxValue)
        {
            yield return new ValidationResult(
                "The minimum value cannot exceed the maximum value.",
                [nameof(MinValue), nameof(MaxValue)]);
        }
    }
}

/// <summary>
/// One definition as the list and details surfaces read it. Separate from the edit model because the read
/// shape carries what the server computed (version, activity) and the write shape carries what the user typed.
/// </summary>
public sealed class OrganizationFieldDefinitionListItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
    public bool IsQueryable { get; set; }

    // Nullable for the same reason the edit model's is (UI-020): one name, one nullability, so the
    // optional-field rule cannot read as broken in the half of the file nobody was looking at.
    public int? DisplayOrder { get; set; }
    public string Classification { get; set; } = string.Empty;
    public OrganizationFieldConstraintsViewModel? ValidationRules { get; set; }
    public int Version { get; set; }
}

public sealed class OrganizationFieldConstraintsViewModel
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public List<string>? Options { get; set; }
    public string? ReferenceTarget { get; set; }
}

/// <summary>
/// The Platform envelope, declared in this feature namespace exactly as the Task precedent declares its own.
/// The shape is shared; the type is not, so one module's response contract cannot silently change another's.
/// </summary>
public sealed class OrganizationFieldDefinitionGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string>? Errors { get; set; }
}
