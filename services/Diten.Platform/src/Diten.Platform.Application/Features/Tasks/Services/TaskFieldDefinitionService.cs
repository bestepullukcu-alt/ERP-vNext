using Diten.Platform.Application.Contracts;
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
    /// <param name="existing">
    /// BL-024 Phase 2 — the values ALREADY STORED on the task, for an update. Null for a create.
    ///
    /// <para><b>Why an update must supply this.</b> Redaction and full-replace are individually fine and lethal
    /// together: the payload replaces <c>task.FieldValues</c> wholesale, and a caller who may not see a field
    /// never received its value, so an ordinary "change the title" round-trip would post the field back MISSING
    /// and delete it. Nobody would see an error. The stored values are handed in so a field the caller may not
    /// write is CARRIED THROUGH untouched instead of being read out of a payload that could not contain it.</para>
    /// </param>
    Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default,
        bool enforceRequired = true,
        IReadOnlyList<TaskFieldValue>? existing = null);
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

    /// <summary>BL-024 Phase 2 — who is writing. Required, never defaulted: a fail-open default on a security
    /// decision compiles everywhere and leaks silently.</summary>
    private readonly IActorPermissionContext _actor;

    public TaskFieldDefinitionService(
        ITaskFieldDefinitionRepository definitions,
        ITaskRecordSourceRegistry recordSources,
        IActorPermissionContext actor)
    {
        _definitions = definitions;
        _recordSources = recordSources;
        _actor = actor;
    }

    public async Task<TaskFieldValidationResult> ValidateAndMaterializeAsync(
        IReadOnlyList<TaskFieldValueDto>? values,
        CancellationToken ct = default,
        bool enforceRequired = true,
        IReadOnlyList<TaskFieldValue>? existing = null)
    {
        var active = await _definitions.ListActiveAsync(ct);

        /*
         * BL-024 Phase 2 — values the caller MAY NOT WRITE are carried through from the stored task.
         *
         * Resolved from ListAllAsync, not `active`: a value written under a since-retired definition must still
         * be preserved, or retiring a definition would quietly delete its data on the next edit.
         *
         * This is the half that has no attacker in it and does the most damage — an ordinary edit by an ordinary
         * user, silently dropping a field they were never shown.
         */
        var allDefinitions = (existing is { Count: > 0 })
            ? (await _definitions.ListAllAsync(ct)).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, TaskFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        var preserved = (existing ?? [])
            .Where(value => !TaskFieldAccessRules.CanEdit(
                allDefinitions.GetValueOrDefault(value.DefinitionCode), _actor))
            .ToList();
        var preservedCodes = preserved
            .Select(value => value.DefinitionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (values is null || values.Count == 0)
        {
            /*
             * An EMPTY payload used to be accepted unconditionally, which made "required" a client-side opinion:
             * the form blocked the empty field, and anything that skipped the form — curl, a stale page, another
             * client — stored a task with the required field simply absent. Requiredness was only ever checked
             * for a field that had been SUPPLIED, so omitting it was the way around it.
             */
            /*
             * A PRESERVED field counts as supplied here too — the same rule the partial-payload check below
             * applies, and it has to be stated twice because these are two different early exits.
             *
             * Missing it made a required RESTRICTED field refuse every edit by exactly the people the
             * restriction was aimed at: they cannot see the value, so they cannot send it, so their save is
             * rejected for omitting it. The task becomes unsavable and nothing on screen connects that to a
             * permission. Found by the test below, not by review.
             */
            var missingEntirely = enforceRequired
                ? active.FirstOrDefault(definition =>
                    definition.IsRequired && !preservedCodes.Contains(definition.Code))
                : null;

            // Preserved values survive an empty payload too — "the caller sent no fields" must not mean
            // "delete the ones they could not see".
            return missingEntirely is null
                ? new TaskFieldValidationResult(true, preserved, null, null)
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

            /*
             * BL-024 Phase 2 — A FIELD THE CALLER MAY NOT WRITE IS REFUSED, not ignored.
             *
             * Ignoring it would answer 204 to somebody who just tried to set a value they have no authority
             * over, and they would have every reason to believe it took. A refusal is the only honest answer,
             * and it is the one that shows up in a log.
             *
             * ⚠ READ ACCESS IS NOT WRITE ACCESS. This is a separate check with its own key, deliberately not
             * derived from whether the value came back redacted: an approver who may READ a salary band is not
             * thereby allowed to change it.
             */
            if (!TaskFieldAccessRules.CanEdit(definition, _actor))
            {
                return Invalid(
                    TaskReasonCodes.FieldAccessDenied,
                    $"You are not permitted to write field '{definition.Code}'.");
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
            // A PRESERVED field counts as supplied. It is required, it is still on the task, and the caller was
            // never able to send it — refusing their edit for omitting a value they may not see would make the
            // task uneditable by exactly the people the restriction was aimed at.
            var omitted = active.FirstOrDefault(definition =>
                definition.IsRequired
                && !seen.Contains(definition.Code)
                && !preservedCodes.Contains(definition.Code));
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

        /*
         * The preserved values join the result LAST, so the payload's own values are validated on their own
         * terms and the untouchable ones simply come along. Their order relative to each other is kept; a
         * checklist of field values has no meaningful order anyway (the SECTION and SortOrder on the definition
         * decide how they render).
         *
         * They are appended rather than merged by code because nothing can collide: a preserved value is by
         * definition one the caller may not write, and any attempt to write one was already refused above.
         */
        return new TaskFieldValidationResult(true, [.. materialized, .. preserved], null, null);
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
