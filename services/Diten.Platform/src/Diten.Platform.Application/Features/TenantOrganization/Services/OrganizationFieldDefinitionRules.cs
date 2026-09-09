using System.Globalization;
using System.Text.RegularExpressions;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TenantOrganization.Services;

/// <summary>
/// MOD-0288-FU02 — what a field definition, a value and a query filter must satisfy before anything is stored
/// or executed. ONE place, every write path, for the reason the Tasks precedent already records: the same
/// check written out twice is how a third path ends up with none.
///
/// <para>⚠ ADAPTED FROM <c>TaskFieldDefinitionRules</c>, NOT SHARED WITH IT (pack §5, §10). Organization code
/// takes no dependency on the Tasks feature, and the Tasks runtime files are a protected path.</para>
///
/// <para>⚠ AND IT CLOSES A GAP THE PRECEDENT LEFT OPEN. <c>TaskFieldDefinitionRules</c> caps sections at six
/// but leaves the field COUNT unbounded. That omission is not copied here: an unbounded count breaks both the
/// screen that renders the fields and the query that filters them, so <see cref="MaxActiveDefinitions"/> is
/// mandatory and the overflow is a 409.</para>
///
/// <para>Every method is pure — it takes its facts as arguments and returns a failure or null. The handlers do
/// the reading and the writing; this class decides nothing about persistence, which is what lets the tests
/// exercise the PRODUCTION rule rather than a copy of it.</para>
/// </summary>
public static class OrganizationFieldDefinitionRules
{
    /// <summary>Per-tenant ceiling on ACTIVE definitions (§8 decision 4). Beyond it: 409.</summary>
    public const int MaxActiveDefinitions = 50;

    /// <summary>Hard paging bound for value queries (§8 decision 5). An unbounded page is refused.</summary>
    public const int MaxPageSize = 200;

    public const string CodeImmutableMessage =
        "A field definition's code cannot be changed after it is created.";

    public const string DefinitionLimitMessage =
        "This tenant already has the maximum of 50 active Organization Unit field definitions.";

    public const string NotQueryableMessage =
        "This field definition is not queryable, so it cannot be used as a filter.";

    /*
     * ⚠ A SECOND CAVEAT THE PACK DID NOT STATE, FOUND WHILE IMPLEMENTING THE FIRST. Even a single explicitly
     * typed definition is not orderable if its canonical form is a string whose lexicographic order differs
     * from its value order — and for Integer and Decimal it does: "10" sorts before "9". Storing a parallel
     * numeric field would fix it and would need an index the pack does not authorize, so v1 REFUSES numeric
     * range and sort instead of answering them wrongly. A wrong ordering looks exactly like a right one,
     * which is the same reason IsQueryable is enforced rather than silently applied.
     */
    public const string NumericOrderingMessage =
        "Range and sort are not supported on numeric fields at v1: values are stored as canonical strings, "
        + "so their order is lexicographic, not numeric. Filter by equality or 'in' instead.";

    public const string MixedRangeMessage =
        "A range or sort needs exactly one explicitly typed field definition; values of different kinds are "
        + "not comparable.";

    /*
     * Codes are normalized so "OU.Permanent Id", "ou.permanent-id" and "OU.PERMANENT_ID" cannot become three
     * definitions that look like one. Lowercase, trimmed, inner runs of separators collapsed to a dot — the
     * lowercase-dotted shape the Tasks precedent uses.
     */
    private static readonly Regex CodeGrammar = new("^[a-z0-9]+(?:[.][a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly Regex Separators = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var lowered = code.Trim().ToLowerInvariant();
        return Separators.Replace(lowered, ".").Trim('.');
    }

    public static (string Message, int StatusCode)? ValidateCode(string normalizedCode)
        => string.IsNullOrWhiteSpace(normalizedCode)
            ? ("A field definition needs a code.", 400)
            : !CodeGrammar.IsMatch(normalizedCode)
                ? ("A field definition code must be lowercase alphanumeric segments separated by dots.", 400)
                : normalizedCode.Length > 80
                    ? ("A field definition code is at most 80 characters.", 400)
                    : null;

    /// <summary>
    /// Parses a wire enum STRICTLY. ⚠ No fallback to the default: a mistyped classification that quietly
    /// became <c>Normal</c> would publish a restricted value in the clear, and a mistyped data type would
    /// store a number as text with nothing to notice.
    /// </summary>
    public static bool TryParseDataType(string? raw, out OrganizationFieldDataType parsed)
        => Enum.TryParse(raw?.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    public static bool TryParseClassification(string? raw, out OrganizationFieldClassification parsed)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            parsed = OrganizationFieldClassification.Normal;
            return true;
        }

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    public static bool TryParseReferenceTarget(string? raw, out OrganizationFieldReferenceTarget parsed)
        => Enum.TryParse(raw?.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    public static bool TryParseOperator(string? raw, out OrganizationFieldFilterOperator parsed)
    {
        parsed = OrganizationFieldFilterOperator.Equals;
        var key = raw?.Trim().ToLowerInvariant();
        switch (key)
        {
            case "eq" or "equals": parsed = OrganizationFieldFilterOperator.Equals; return true;
            case "in": parsed = OrganizationFieldFilterOperator.In; return true;
            case "startswith" or "contains": parsed = OrganizationFieldFilterOperator.StartsWith; return true;
            case "lt": parsed = OrganizationFieldFilterOperator.LessThan; return true;
            case "lte": parsed = OrganizationFieldFilterOperator.LessThanOrEqual; return true;
            case "gt": parsed = OrganizationFieldFilterOperator.GreaterThan; return true;
            case "gte": parsed = OrganizationFieldFilterOperator.GreaterThanOrEqual; return true;
            default: return false;
        }
    }

    /// <summary>Whether the tenant may add one more ACTIVE definition. Inactive and deleted ones do not count.</summary>
    public static (string Message, int StatusCode)? ValidateDefinitionCount(
        IReadOnlyCollection<OrganizationFieldDefinition> existing,
        Guid? excludingId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var active = existing.Count(d =>
            d.IsActive && d.DeletedAt is null && !d.IsDeleted && d.Id != excludingId);

        return active >= MaxActiveDefinitions ? (DefinitionLimitMessage, 409) : null;
    }

    /// <summary>Constraints must be the ones the declared type can actually carry.</summary>
    public static (string Message, int StatusCode)? ValidateConstraints(
        OrganizationFieldDataType dataType,
        OrganizationFieldConstraints? constraints)
    {
        if (dataType == OrganizationFieldDataType.SingleSelect)
        {
            var options = constraints?.Options;
            if (options is null || options.Count == 0)
            {
                return ("A single-select field needs at least one option.", 400);
            }

            if (options.Any(string.IsNullOrWhiteSpace))
            {
                return ("A single-select option cannot be blank.", 400);
            }

            if (options.Select(o => o.Trim()).Distinct(StringComparer.Ordinal).Count() != options.Count)
            {
                return ("Single-select options must be distinct.", 400);
            }
        }

        if (dataType == OrganizationFieldDataType.Reference && constraints?.ReferenceTarget is null)
        {
            return ("A reference field must name what it points at: OrganizationUnit or Position.", 400);
        }

        if (constraints is null)
        {
            return null;
        }

        if (constraints.MinLength is { } min && constraints.MaxLength is { } max && min > max)
        {
            return ("Minimum length cannot exceed maximum length.", 400);
        }

        if (constraints.MinValue is { } lo && constraints.MaxValue is { } hi && lo > hi)
        {
            return ("Minimum value cannot exceed maximum value.", 400);
        }

        var lengthApplies = dataType is OrganizationFieldDataType.Text or OrganizationFieldDataType.MultilineText;
        if (!lengthApplies && (constraints.MinLength.HasValue || constraints.MaxLength.HasValue))
        {
            return ($"Length constraints do not apply to a {dataType} field.", 400);
        }

        var rangeApplies = dataType is OrganizationFieldDataType.Integer or OrganizationFieldDataType.Decimal;
        if (!rangeApplies && (constraints.MinValue.HasValue || constraints.MaxValue.HasValue))
        {
            return ($"Value range constraints do not apply to a {dataType} field.", 400);
        }

        if (dataType != OrganizationFieldDataType.SingleSelect && constraints.Options is { Count: > 0 })
        {
            return ($"Options do not apply to a {dataType} field.", 400);
        }

        if (dataType != OrganizationFieldDataType.Reference && constraints.ReferenceTarget is not null)
        {
            return ($"A reference target does not apply to a {dataType} field.", 400);
        }

        return null;
    }

    /// <summary>
    /// Turns a raw value into the CANONICAL string that is stored and indexed, or explains why it cannot.
    /// <c>null</c> canonical means "cleared".
    ///
    /// <para>⚠ CANONICAL, NOT AS-TYPED. <c>"1"</c>, <c>"01"</c> and <c>" 1 "</c> are the same integer, and an
    /// equality filter that matched only one of the three would return a wrong result set that looks correct.
    /// The same reasoning fixes dates to <c>yyyy-MM-dd</c> and booleans to <c>true</c>/<c>false</c>.</para>
    /// </summary>
    public static (string? Canonical, string? Message, int StatusCode) CanonicalizeValue(
        OrganizationFieldDefinition definition,
        string? raw)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return definition.IsRequired
                ? (null, $"'{definition.Name}' is required and cannot be cleared.", 400)
                : (null, null, 200);
        }

        var c = definition.ValidationRules;

        switch (definition.DataType)
        {
            case OrganizationFieldDataType.Text:
            case OrganizationFieldDataType.MultilineText:
                if (definition.DataType == OrganizationFieldDataType.Text && trimmed.Contains('\n'))
                {
                    return (null, $"'{definition.Name}' is a single-line field.", 400);
                }

                if (c?.MinLength is { } minLen && trimmed.Length < minLen)
                {
                    return (null, $"'{definition.Name}' needs at least {minLen} characters.", 400);
                }

                if (c?.MaxLength is { } maxLen && trimmed.Length > maxLen)
                {
                    return (null, $"'{definition.Name}' allows at most {maxLen} characters.", 400);
                }

                return (trimmed, null, 200);

            case OrganizationFieldDataType.Integer:
                if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    return (null, $"'{definition.Name}' expects a whole number.", 400);
                }

                return RangeChecked(definition, i, c, i.ToString(CultureInfo.InvariantCulture));

            case OrganizationFieldDataType.Decimal:
                if (!decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    return (null, $"'{definition.Name}' expects a number.", 400);
                }

                return RangeChecked(definition, d, c, d.ToString(CultureInfo.InvariantCulture));

            case OrganizationFieldDataType.Boolean:
                return bool.TryParse(trimmed, out var b)
                    ? (b ? "true" : "false", null, 200)
                    : (null, $"'{definition.Name}' expects true or false.", 400);

            case OrganizationFieldDataType.Date:
                return DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? (date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null, 200)
                    : (null, $"'{definition.Name}' expects a date as yyyy-MM-dd.", 400);

            case OrganizationFieldDataType.SingleSelect:
                var match = c?.Options?.FirstOrDefault(o =>
                    string.Equals(o.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
                return match is null
                    ? (null, $"'{trimmed}' is not one of the options declared for '{definition.Name}'.", 400)
                    : (match.Trim(), null, 200);

            case OrganizationFieldDataType.Reference:
                return Guid.TryParse(trimmed, out var reference) && reference != Guid.Empty
                    ? (reference.ToString("D"), null, 200)
                    : (null, $"'{definition.Name}' expects the identity of a {c?.ReferenceTarget}.", 400);

            default:
                // Unreachable while the enum stays closed — and loud rather than silent if it ever does not.
                return (null, $"'{definition.Name}' has an unsupported data type.", 400);
        }
    }

    /// <summary>
    /// Whether a filter clause is legal against its definition — the SERVER-SIDE enforcement §16 criterion 9
    /// requires. A non-queryable definition is a 400 here, never a quietly dropped clause.
    /// </summary>
    public static (string Message, int StatusCode)? ValidateFilter(
        OrganizationFieldDefinition definition,
        OrganizationFieldFilterOperator op,
        IReadOnlyList<string> values,
        bool isTheOnlyDefinitionInTheQuery)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        if (!definition.IsActive || definition.DeletedAt is not null)
        {
            return ($"'{definition.Code}' is not an active field definition.", 400);
        }

        if (!definition.IsQueryable)
        {
            return (NotQueryableMessage, 400);
        }

        if (values.Count == 0)
        {
            return ("A filter needs at least one value.", 400);
        }

        var isString = definition.DataType
            is OrganizationFieldDataType.Text
            or OrganizationFieldDataType.MultilineText
            or OrganizationFieldDataType.SingleSelect;

        switch (op)
        {
            case OrganizationFieldFilterOperator.StartsWith when !isString:
                return ($"Prefix search applies to text fields only, and '{definition.Code}' is {definition.DataType}.", 400);

            case OrganizationFieldFilterOperator.In when values.Count > MaxPageSize:
                return ($"An 'in' filter takes at most {MaxPageSize} values.", 400);

            /*
             * ⚠ THE REAL TECHNICAL CAVEAT (§8 decision 5). `Value` holds text, numbers and dates in ONE field,
             * so a range operator across a mixed set compares BSON type order rather than values — silently,
             * and the result looks like an answer. Range is therefore admitted only when the whole query is
             * one explicitly typed definition, which is the only case where the comparison is total.
             */
            case OrganizationFieldFilterOperator.LessThan:
            case OrganizationFieldFilterOperator.LessThanOrEqual:
            case OrganizationFieldFilterOperator.GreaterThan:
            case OrganizationFieldFilterOperator.GreaterThanOrEqual:
                if (!isTheOnlyDefinitionInTheQuery)
                {
                    return (MixedRangeMessage, 400);
                }

                if (values.Count != 1)
                {
                    return ("A range filter takes exactly one value.", 400);
                }

                if (!SupportsOrdering(definition.DataType))
                {
                    return (NumericOrderingMessage, 400);
                }

                break;
        }

        return null;
    }

    /// <summary>
    /// Whether this type's canonical string form orders the same way its values do. Text, single-select and
    /// ISO dates do; Integer, Decimal, Boolean and Reference do not (or have no meaningful order).
    /// </summary>
    public static bool SupportsOrdering(OrganizationFieldDataType dataType)
        => dataType is OrganizationFieldDataType.Text
            or OrganizationFieldDataType.MultilineText
            or OrganizationFieldDataType.SingleSelect
            or OrganizationFieldDataType.Date;

    /// <summary>A sort names one definition, and that definition must be queryable and orderable.</summary>
    public static (string Message, int StatusCode)? ValidateSort(OrganizationFieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.IsActive || definition.DeletedAt is not null)
        {
            return ($"'{definition.Code}' is not an active field definition.", 400);
        }

        return !definition.IsQueryable
            ? (NotQueryableMessage, 400)
            : !SupportsOrdering(definition.DataType)
                ? (NumericOrderingMessage, 400)
                : null;
    }

    /// <summary>Paging is bounded; an unbounded page is refused rather than quietly capped.</summary>
    public static (string Message, int StatusCode)? ValidatePaging(int page, int pageSize)
        => page < 1
            ? ("Page numbering starts at 1.", 400)
            : pageSize < 1
                ? ("Page size must be at least 1.", 400)
                : pageSize > MaxPageSize
                    ? ($"Page size is bounded at {MaxPageSize}.", 400)
                    : null;

    private static (string?, string?, int) RangeChecked(
        OrganizationFieldDefinition definition,
        decimal parsed,
        OrganizationFieldConstraints? c,
        string canonical)
    {
        if (c?.MinValue is { } lo && parsed < lo)
        {
            return (null, $"'{definition.Name}' must be at least {lo}.", 400);
        }

        if (c?.MaxValue is { } hi && parsed > hi)
        {
            return (null, $"'{definition.Name}' must be at most {hi}.", 400);
        }

        return (canonical, null, 200);
    }
}
