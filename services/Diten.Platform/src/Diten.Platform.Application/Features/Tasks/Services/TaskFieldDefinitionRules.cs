using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

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

    public const string RecordSourceValueTypeMessage =
        "A field whose values come from another module's records stores an identity, so its value type must be "
        + "Reference.";

    /// <summary>
    /// A record-backed definition has to name a source some module actually registered, and it has to be the
    /// value type that can hold an identity.
    ///
    /// <para>Refused HERE rather than left to the reader. The form already drops a field whose source will not
    /// resolve — correctly, because an empty picker is worse than no field — but the administrator who saved that
    /// definition then watches it never appear and has nothing to go on. The typo used to be possible because the
    /// key was typed; it is now chosen, and this is the second lock on the same door: a client can still POST
    /// anything.</para>
    /// </summary>
    /// <param name="isRegisteredSource">
    /// Whether the registry knows this key. Passed in rather than resolved here so this class stays a pure rule —
    /// the same reason every other check in it takes its facts as arguments.
    /// </param>
    public static (string ReasonCode, string Message)? ValidateOptionSource(
        TaskFieldOptionsSourceKind kind,
        string? sourceKey,
        TaskFieldValueType valueType,
        Func<string, bool> isRegisteredSource)
    {
        ArgumentNullException.ThrowIfNull(isRegisteredSource);

        if (kind != TaskFieldOptionsSourceKind.ModuleRecord)
        {
            return null;
        }

        if (valueType != TaskFieldValueType.Reference)
        {
            return (TaskReasonCodes.FieldOptionSourceInvalid, RecordSourceValueTypeMessage);
        }

        var key = sourceKey?.Trim();
        return string.IsNullOrWhiteSpace(key) || !isRegisteredSource(key)
            ? (TaskReasonCodes.FieldOptionSourceInvalid,
                $"No module offers records under the source '{key ?? string.Empty}'.")
            : null;
    }

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
