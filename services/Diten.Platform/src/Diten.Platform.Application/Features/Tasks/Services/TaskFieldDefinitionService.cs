using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 — validates configurable field VALUES against their definitions and the executable contract's bounds
/// (pack §12 K1). This is what keeps the engine generic: Phase, Work Type, Market/Country, Domain and External
/// Party are definitions, never columns, so a new consuming module never forces a schema change.
///
/// <para>Phase 1 validates shape + limits. Field-level AUTHORIZATION (who may see/write a field) is BL-024: the
/// classification metadata is copied onto the stored value here so that later work is additive, but no access
/// decision is made yet.</para>
/// </summary>
public interface ITaskFieldDefinitionService
{
    /// <summary>
    /// Validate supplied values against their definitions.
    /// </summary>
    /// <param name="enforceRequired">
    /// Whether a REQUIRED definition the payload never mentions is a refusal. True for every act a person
    /// performs — the form blocks the empty field and the server refuses it, deliberately the same rule in two
    /// places, because a client-only rule is a suggestion.
    ///
    /// <para>FALSE for machine-made tasks (the recurrence sweep, creation from a template). A sweep has nobody
    /// to ask: refusing there would not collect the missing value, it would silently stop the recurrence and
    /// consume the period anyway. Those paths are named at their call sites.</para>
    /// </param>
    Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default,
        bool enforceRequired = true);
}

public sealed record TaskFieldValidationResult(
    bool IsValid,
    IReadOnlyList<TaskFieldValue> Values,
    string? ReasonCode,
    string? Message);

public sealed class TaskFieldDefinitionService : ITaskFieldDefinitionService
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly ITaskRecordSourceRegistry _recordSources;

    public TaskFieldDefinitionService(
        ITaskFieldDefinitionRepository definitions,
        ITaskRecordSourceRegistry recordSources)
    {
        _definitions = definitions;
        _recordSources = recordSources;
    }

    public async Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default,
        bool enforceRequired = true)
    {
        var active = await _definitions.ListActiveAsync(ct);

        if (values is null || values.Count == 0)
        {
            /*
             * An EMPTY payload used to be accepted unconditionally, which made "required" a client-side opinion:
             * the form blocked the empty field, and anything that skipped the form — curl, a stale page, another
             * client — stored a task with the required field simply absent. Requiredness was only ever checked
             * for a field that had been SUPPLIED, so omitting it was the way around it.
             */
            var missingEntirely = enforceRequired
                ? active.FirstOrDefault(definition => definition.IsRequired)
                : null;

            return missingEntirely is null
                ? new TaskFieldValidationResult(true, [], null, null)
                : Invalid(TaskReasonCodes.FieldValueInvalid, $"Field '{missingEntirely.Code}' is required.");
        }

        var byCode = active.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

        var materialized = new List<TaskFieldValue>(values.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var supplied in values)
        {
            if (string.IsNullOrWhiteSpace(supplied.DefinitionCode))
            {
                return Invalid(TaskReasonCodes.FieldValueInvalid, "A field value is missing its definition code.");
            }

            if (!byCode.TryGetValue(supplied.DefinitionCode, out var definition))
            {
                // Unknown code = a field nobody defined. Accepting it would smuggle an ad-hoc column into the
                // engine, which is exactly what K1 forbids.
                return Invalid(
                    TaskReasonCodes.FieldDefinitionUnknown,
                    $"Unknown task field definition '{supplied.DefinitionCode}'.");
            }

            if (!seen.Add(definition.Code))
            {
                return Invalid(TaskReasonCodes.FieldValueInvalid, $"Duplicate value for '{definition.Code}'.");
            }

            if (supplied.ValueType != definition.ValueType)
            {
                return Invalid(
                    TaskReasonCodes.FieldValueInvalid,
                    $"Field '{definition.Code}' expects {definition.ValueType}, got {supplied.ValueType}.");
            }

            if (definition.IsRequired && string.IsNullOrWhiteSpace(supplied.Value))
            {
                return Invalid(TaskReasonCodes.FieldValueInvalid, $"Field '{definition.Code}' is required.");
            }

            if (supplied.Value is { Length: > TaskFieldLimits.MaxTextLengthPerField })
            {
                return Invalid(
                    TaskReasonCodes.FieldLimitExceeded,
                    $"Field '{definition.Code}' exceeds {TaskFieldLimits.MaxTextLengthPerField} characters.");
            }

            if (!IsRecordBacked(definition) && !IsWellFormed(definition.ValueType, supplied.Value))
            {
                return Invalid(
                    TaskReasonCodes.FieldValueInvalid,
                    $"Field '{definition.Code}' value is not a valid {definition.ValueType}.");
            }

            materialized.Add(new TaskFieldValue
            {
                DefinitionCode = definition.Code,
                ValueType = definition.ValueType,
                Value = supplied.Value,
                // Copied from the definition so BL-024 can evaluate later without touching stored values.
                Classification = definition.Classification,
                AccessState = definition.DefaultAccessState,
                Redacted = false
            });
        }

        // The same rule for a PARTIAL payload: a required definition nobody mentioned is missing, not absent by
        // agreement. Checked after the loop so a supplied-but-empty value reports first, with its own message.
        if (enforceRequired)
        {
            var omitted = active.FirstOrDefault(definition => definition.IsRequired && !seen.Contains(definition.Code));
            if (omitted is not null)
            {
                return Invalid(TaskReasonCodes.FieldValueInvalid, $"Field '{omitted.Code}' is required.");
            }
        }

        // Contract bounds: sections ≤ 6, fields per section ≤ 8, primary fields ≤ 8 across the whole item.
        var bySection = active
            .Where(d => seen.Contains(d.Code))
            .GroupBy(d => d.Section, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bySection.Count > TaskFieldLimits.MaxSections)
        {
            return Invalid(
                TaskReasonCodes.FieldLimitExceeded,
                $"A task may span at most {TaskFieldLimits.MaxSections} field sections.");
        }

        if (bySection.Any(g => g.Count() > TaskFieldLimits.MaxFieldsPerSection))
        {
            return Invalid(
                TaskReasonCodes.FieldLimitExceeded,
                $"A section may hold at most {TaskFieldLimits.MaxFieldsPerSection} fields.");
        }

        var primaryCount = active.Count(d => seen.Contains(d.Code) && d.Importance == TaskFieldImportance.Primary);
        if (primaryCount > TaskFieldLimits.MaxPrimaryFields)
        {
            return Invalid(
                TaskReasonCodes.FieldLimitExceeded,
                $"At most {TaskFieldLimits.MaxPrimaryFields} primary fields are allowed.");
        }

        /*
         * A record-backed value must name a record that EXISTS. This is the check table's whole purpose in every
         * system that has one: the field does not merely look like an identity, it points at something.
         *
         * Done here rather than in IsWellFormed because it is an I/O question, and done in ONE batch per source
         * rather than per value because a task with six record fields would otherwise make six round trips.
         */
        var recordCheck = await ValidateRecordValuesAsync(materialized, byCode, ct);
        if (recordCheck is not null)
        {
            return recordCheck;
        }

        return new TaskFieldValidationResult(true, materialized, null, null);
    }

    private static bool IsRecordBacked(TaskFieldDefinition definition) =>
        definition.OptionsSourceKind == TaskFieldOptionsSourceKind.ModuleRecord;

    /// <summary>
    /// Every record-backed value resolved against the source its own definition names. Null when they all check
    /// out; a refusal otherwise.
    ///
    /// <para>An UNREGISTERED source is refused too. A definition can only be saved with a registered source, so
    /// reaching this means the module that owned it is gone — and accepting the value then would store a pointer
    /// into nothing while the form, which drops the field, shows the user no way to correct it.</para>
    /// </summary>
    private async Task<TaskFieldValidationResult?> ValidateRecordValuesAsync(
        IReadOnlyList<TaskFieldValue> values,
        Dictionary<string, TaskFieldDefinition> byCode,
        CancellationToken ct)
    {
        var recordValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Value)
                            && byCode.TryGetValue(value.DefinitionCode, out var definition)
                            && IsRecordBacked(definition))
            .ToList();

        if (recordValues.Count == 0)
        {
            return null;
        }

        foreach (var group in recordValues.GroupBy(value => byCode[value.DefinitionCode].OptionsSourceKey?.Trim()))
        {
            var source = _recordSources.Find(group.Key);
            if (source is null)
            {
                return Invalid(
                    TaskReasonCodes.FieldOptionSourceInvalid,
                    $"No module offers records under the source '{group.Key ?? string.Empty}'.");
            }

            var ids = group.Select(value => value.Value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var resolved = (await source.ResolveAsync(ids, ct))
                .Select(record => record.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = group.FirstOrDefault(value => !resolved.Contains(value.Value!));
            if (missing is not null)
            {
                // The identity is named in the SERVER's message, never in the reader's: this text reaches a log
                // and a developer, while the browser is told by reason code (BL-049).
                return Invalid(
                    TaskReasonCodes.FieldValueInvalid,
                    $"Field '{missing.DefinitionCode}' points at no record in source '{group.Key}'.");
            }
        }

        return null;
    }

    private static TaskFieldValidationResult Invalid(string reasonCode, string message)
        => new(false, [], reasonCode, message);

    /// <summary>Shape check per allowlisted value type. Empty is permitted here (requiredness is separate).</summary>
    private static bool IsWellFormed(TaskFieldValueType type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return type switch
        {
            TaskFieldValueType.Number or TaskFieldValueType.Currency or TaskFieldValueType.Percentage =>
                decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out _),
            TaskFieldValueType.Date or TaskFieldValueType.DateTime =>
                DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _),
            TaskFieldValueType.Boolean => bool.TryParse(value, out _),
            TaskFieldValueType.Person or TaskFieldValueType.Reference => Guid.TryParse(value, out _),
            // Same-origin relative route or https only — mirrors the contract's isSafeLink rule.
            TaskFieldValueType.Link =>
                (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal))
                || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps),
            _ => true
        };
    }
}
