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
    Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default);
}

public sealed record TaskFieldValidationResult(
    bool IsValid,
    IReadOnlyList<TaskFieldValue> Values,
    string? ReasonCode,
    string? Message);

public sealed class TaskFieldDefinitionService : ITaskFieldDefinitionService
{
    private readonly ITaskFieldDefinitionRepository _definitions;

    public TaskFieldDefinitionService(ITaskFieldDefinitionRepository definitions)
    {
        _definitions = definitions;
    }

    public async Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default)
    {
        if (values is null || values.Count == 0)
        {
            return new TaskFieldValidationResult(true, [], null, null);
        }

        var active = await _definitions.ListActiveAsync(ct);
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

            if (!IsWellFormed(definition.ValueType, supplied.Value))
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

        return new TaskFieldValidationResult(true, materialized, null, null);
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
