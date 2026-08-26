using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// DCP-005 slice 1 — what a task TYPE must satisfy before it is stored. One place, both write paths, for the
/// reason its sibling <see cref="TaskFieldDefinitionRules"/> gives: the same check written out twice is how a
/// third path ends up with none.
///
/// <para>Pure functions that take their facts as arguments — no repository, no clock — so both handlers and the
/// tests can ask the same question without arranging a world first.</para>
/// </summary>
public static class TaskTypeRules
{
    /// <summary>Long enough to be an identifier, short enough to read in a chip.</summary>
    public const int CodeMaxLength = 40;
    public const int NameMaxLength = 160;
    public const int FunctionCodeMaxLength = 40;

    public const string CodeRequiredMessage = "A task type needs a code.";
    public const string CodeImmutableMessage =
        "A task type's code cannot be changed after it is created: tasks already opened with it carry that code "
        + "as their identity.";
    public const string CodeDuplicateMessage = "Another task type already uses this code.";
    public const string NameRequiredMessage = "A task type needs a name.";

    public const string QualityRecordNeedsDomainMessage =
        "A GxP quality record must name the quality domain that governs it.";
    public const string DomainNeedsQualityRecordMessage =
        "A task type with a quality domain records GxP work, so its record class cannot be NOT_A_RECORD.";
    public const string FunctionCodeInvalidMessage =
        "That is not one of the business function codes this organisation uses.";

    /// <summary>
    /// The code, normalised the way it will be stored and compared: trimmed and upper-cased. Codes are read
    /// aloud and typed into spreadsheets, so `dev-qms` and `DEV-QMS` must not become two types.
    /// </summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// The function code, parsed from whatever the caller sent.
    ///
    /// <para>⚠ <b>THE SEAM IS CLOSED</b> (DCP-005 §6.7). This used to trim-and-uppercase a free string, because
    /// the closed list did not exist yet. It now parses into <see cref="TaskFunctionCode"/> and REFUSES what is
    /// not in it — an unrecognised code is returned as a refusal rather than dropped, because silently storing
    /// null would report success for a classification the caller believed they had set.</para>
    /// </summary>
    public static (TaskFunctionCode? Value, (string ReasonCode, string Message)? Error) ParseFunctionCode(
        string? functionCode)
    {
        var trimmed = (functionCode ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            // Absent is legitimate: not every type belongs to a named function.
            return (null, null);
        }

        return Enum.TryParse<TaskFunctionCode>(trimmed, ignoreCase: true, out var parsed)
            ? (parsed, null)
            : (null, (TaskReasonCodes.TaskTypeFunctionCodeInvalid, FunctionCodeInvalidMessage));
    }

    /// <summary>
    /// Shape checks that do not need to look at anything else.
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateShape(
        string? code, string? name, string? functionCode)
    {
        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return (TaskReasonCodes.ValidationFailed, CodeRequiredMessage);
        }

        if (normalizedCode.Length > CodeMaxLength)
        {
            return (TaskReasonCodes.ValidationFailed,
                $"A task type's code cannot be longer than {CodeMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return (TaskReasonCodes.ValidationFailed, NameRequiredMessage);
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return (TaskReasonCodes.ValidationFailed,
                $"A task type's name cannot be longer than {NameMaxLength} characters.");
        }

        /*
         * ⚠ MEMBERSHIP, NOT LENGTH. The seam left here for the closed list is now filled: a code outside
         * DCP-005 §6.7 is refused rather than stored, so a type cannot claim a function the organisation does
         * not have.
         */
        return ParseFunctionCode(functionCode).Error;
    }

    /// <summary>
    /// Classification and domain have to agree, in both directions.
    ///
    /// <para>A GxP record with no domain cannot be filed — the folder rule DCP-005 §6.3 describes is computed
    /// from the domain, so the record would have nowhere to go. And a type that names a domain is by definition
    /// doing quality work, so it cannot also claim to produce no record. The pair is refused at the write, where
    /// it can still be corrected, rather than discovered at the read, where it can only be reported.</para>
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateClassification(
        TaskRecordClass recordClass, TaskGqmsDomain? domain)
    {
        if (recordClass == TaskRecordClass.GXP_QUALITY_RECORD && domain is null)
        {
            return (TaskReasonCodes.TaskTypeClassificationInvalid, QualityRecordNeedsDomainMessage);
        }

        return domain is not null && recordClass == TaskRecordClass.NOT_A_RECORD
            ? (TaskReasonCodes.TaskTypeClassificationInvalid, DomainNeedsQualityRecordMessage)
            : null;
    }

    /// <summary>
    /// Whether this code is already taken by a LIVE type in the tenant.
    ///
    /// <para>Retired types still hold their codes: a code freed by deactivation could be re-used for different
    /// work, and every task opened under the old meaning would silently join the new one.</para>
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateCodeUnique(
        string? code, IReadOnlyCollection<TaskType> existing, Guid? excludingId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var normalized = NormalizeCode(code);

        var taken = existing.Any(type => type.DeletedAt is null
            && type.Id != excludingId
            && string.Equals(type.Code, normalized, StringComparison.OrdinalIgnoreCase));

        return taken ? (TaskReasonCodes.TaskTypeCodeTaken, CodeDuplicateMessage) : null;
    }

    /// <summary>
    /// A code arriving on an UPDATE must be the one already stored.
    ///
    /// <para>Refused rather than ignored: silently keeping the old code would report success for a change the
    /// caller asked for and did not get, which is the failure shape this repository has spent a session
    /// removing.</para>
    /// </summary>
    public static (string ReasonCode, string Message)? ValidateCodeUnchanged(string storedCode, string? incoming)
    {
        // Absent means "not attempting to change it" — the screen sends the field read-only, and a client that
        // omits it is not asking for anything.
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return null;
        }

        return string.Equals(NormalizeCode(storedCode), NormalizeCode(incoming), StringComparison.Ordinal)
            ? null
            : (TaskReasonCodes.TaskTypeCodeImmutable, CodeImmutableMessage);
    }

    /// <summary>
    /// The governing documents, cleaned: trimmed, de-duplicated, empties dropped, order preserved.
    ///
    /// <para>They are UIDs into a lookup, so there is nothing to validate them against here — the document list
    /// is slice 2. What CAN be wrong today is a blank row or the same UID twice, and both are silently corrected
    /// rather than refused: neither changes what the administrator meant.</para>
    /// </summary>
    public static List<string> NormalizeDocuments(IEnumerable<string>? uids)
    {
        if (uids is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return uids
            .Select(uid => (uid ?? string.Empty).Trim())
            .Where(uid => uid.Length > 0 && seen.Add(uid))
            .ToList();
    }

    /// <summary>Same cleaning for the sparse per-organisation layer; an org whose list empties is dropped.</summary>
    public static Dictionary<string, List<string>> NormalizeLocalDocuments(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? local)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (local is null)
        {
            return result;
        }

        foreach (var (org, uids) in local)
        {
            var key = (org ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var cleaned = NormalizeDocuments(uids);
            if (cleaned.Count > 0)
            {
                result[key] = cleaned;
            }
        }

        return result;
    }
}
