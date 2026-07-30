using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TaskFieldDefinitions;

/// <summary>
/// MOD-0024 Phase 5 — the configurable field definition form.
///
/// <para><b>Two label sources, exactly one filled.</b> A SYSTEM definition names a resource key we ship
/// translations for; a TENANT definition carries the administrator's own words. A tenant cannot add a line to our
/// resx files, so demanding a key from them would render the key itself on screen — which has happened in this
/// codebase before. The server enforces the same rule; this is the pre-check.</para>
/// </summary>
public sealed class TaskFieldDefinitionEditViewModel : IValidatableObject
{
    public Guid? Id { get; set; }

    /// <summary>
    /// Read-only after creation. Every stored value joins to its definition BY CODE, so an edited code orphans
    /// them all: the data survives, its label does not.
    /// </summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    public string? LabelResourceKey { get; set; }

    public string? LabelText { get; set; }

    [Required]
    public string ValueType { get; set; } = "Text";

    [Required]
    public string Section { get; set; } = string.Empty;

    public string Importance { get; set; } = "Secondary";

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public string OptionsSourceKind { get; set; } = "None";

    public string? OptionsSourceKey { get; set; }

    public string? AppliesToModuleCode { get; set; }

    /// <summary>Stored, never evaluated — field-level authorization is BL-024.</summary>
    public string Classification { get; set; } = "Normal";

    public string DefaultAccessState { get; set; } = "Visible";

    public bool IsActive { get; set; } = true;

    public int ExpectedVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasKey = !string.IsNullOrWhiteSpace(LabelResourceKey);
        var hasText = !string.IsNullOrWhiteSpace(LabelText);

        // Exactly one. Both is ambiguous — the screen would have to guess which one to render — and neither
        // leaves the field with nothing but its code to show.
        if (hasKey == hasText)
        {
            yield return new ValidationResult(
                "Provide exactly one label source: a resource key for a system field, or text for a tenant field.",
                [nameof(LabelResourceKey), nameof(LabelText)]);
        }
    }
}

/// <summary>
/// The Platform envelope. Declared per feature namespace exactly as the reference does — the shape is shared,
/// the type is not, so one module's response contract cannot silently change another's.
/// </summary>
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string>? Errors { get; set; }
}
