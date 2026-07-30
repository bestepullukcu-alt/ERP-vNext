using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// Phase 5 — what a field DEFINITION must satisfy before it is stored. One place, both write paths, for the
/// reason this session has now met three times: the same check written out twice is how a third path ends up
/// with none.
/// </summary>
public static class TaskFieldDefinitionRules
{
    /// <summary>
    /// The contract caps <c>businessContext</c> at six sections (<c>LIMITS.maxSections</c>), and a projection
    /// that breaks the cap is not merely ugly — <c>validateItems</c> DROPS an item it cannot validate, so the
    /// seventh section would make every task carrying it disappear from the surface. BL-038's lesson, enforced at
    /// the WRITE where it can still be refused rather than at the read where it can only delete.
    /// </summary>
    public const int MaxSections = 6;

    public const string LabelSourceMessage =
        "A field definition needs exactly one label source: a resource key for a system field, or text for a "
        + "tenant field.";

    public const string SectionLimitMessage =
        "A task can show at most six business-context sections; this definition would need a seventh.";

    public const string CodeImmutableMessage =
        "A field definition's code cannot be changed after it is created.";

    /// <summary>
    /// EXACTLY ONE label source. Both set is ambiguous — the projection would have to guess which one the screen
    /// gets — and neither leaves the field with nothing to render but its code.
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateLabel(string? labelResourceKey, string? labelText)
    {
        var hasKey = !string.IsNullOrWhiteSpace(labelResourceKey);
        var hasText = !string.IsNullOrWhiteSpace(labelText);

        return hasKey == hasText
            ? (TaskReasonCodes.FieldLabelSourceInvalid, LabelSourceMessage)
            : null;
    }

    /// <summary>
    /// Whether adding <paramref name="section"/> would take this tenant past the cap. Existing sections are
    /// counted from the definitions that are still live: a retired definition's section is not occupied.
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateSection(
        string section,
        IReadOnlyCollection<TaskFieldDefinition> existing,
        Guid? excludingId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(section))
        {
            return (TaskReasonCodes.ValidationFailed, "A field definition needs a section.");
        }

        var sections = existing
            .Where(d => d.DeletedAt is null && d.Id != excludingId)
            .Select(d => d.Section)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Already one of the tenant's sections: joining it costs nothing.
        sections.Add(section);

        return sections.Count > MaxSections
            ? (TaskReasonCodes.FieldSectionLimitExceeded, SectionLimitMessage)
            : null;
    }
}
