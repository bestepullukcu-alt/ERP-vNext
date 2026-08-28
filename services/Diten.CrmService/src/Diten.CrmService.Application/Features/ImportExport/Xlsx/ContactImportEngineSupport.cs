using System.Globalization;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>
/// Shared value parsing + reference checking for the Task 2 import engine. Kept separate from the engine so the
/// "what does this cell mean" rules stay readable and testable on their own.
/// </summary>
internal static class ImportValues
{
    /// <summary>Explicit "erase this field" marker. A blank cell means "leave unchanged" — the safer default, because a
    /// user who deletes a column or trims the file must not silently wipe stored data.</summary>
    public const string ClearToken = "<CLEAR>";

    public static bool IsClear(string? value)
        => value is not null && string.Equals(value.Trim(), ClearToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>null = leave unchanged · (true, null) = clear · (true, value) = set.</summary>
    public static (bool HasValue, string? Value)? ReadOptional(ParsedRow row, string column)
    {
        var raw = row.Get(column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return IsClear(raw) ? (true, null) : (true, raw.Trim());
    }

    public static bool? ReadBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "evet" or "x" => true,
            "false" or "0" or "no" or "n" or "hayır" or "hayir" => false,
            _ => null
        };
    }

    /// <summary>ISO first (what export writes), then a few common human formats. Returns false only for real garbage.</summary>
    public static bool TryReadDate(string? raw, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var text = raw.Trim();
        string[] formats =
        {
            "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss",
            "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy"
        };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(exact, DateTimeKind.Utc));
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
            return true;
        }

        return false;
    }

    public static bool TryReadGuid(string? raw, out Guid? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Guid.TryParse(raw.Trim(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    /// <summary>Link lifecycle markers. NOT a MOD-0048 set (none exists for link status, and inventing one is
    /// forbidden) — a closed internal enum owned by the domain lifecycle helper.</summary>
    public static readonly IReadOnlyList<string> AllowedLinkStatuses =
        new[] { "active" }.Concat(RelationshipLifecycle.ClosedStatuses).ToList();

    public static bool IsAllowedLinkStatus(string value)
        => AllowedLinkStatuses.Any(s => string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Caches MOD-0048 lookups per distinct (set, value) so a bulk import hits the Gateway once per distinct value.
/// Required sets block; optional sets tolerate an unpublished set but still reject an unknown published value —
/// identical to the single-write path (<see cref="ContactReferenceValidation"/>). Never a local fallback list.
/// </summary>
internal sealed class ImportReferenceChecker
{
    private readonly IReferenceDataValidator _validator;
    private readonly Dictionary<(string, string), ReferenceValidationStatus> _cache = new();
    private readonly HashSet<string> _missingRequiredSets = new(StringComparer.OrdinalIgnoreCase);

    public ImportReferenceChecker(IReferenceDataValidator validator) => _validator = validator;

    /// <summary>Required sets that turned out to be unpublished — the import is blocked from applying while any exist.</summary>
    public IReadOnlyCollection<string> MissingRequiredSets => _missingRequiredSets;

    public async Task<string?> CheckAsync(string setCode, string? value, bool required, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? $"'{setCode}' is required." : null;
        }

        var key = (setCode, value.Trim().ToLowerInvariant());
        if (!_cache.TryGetValue(key, out var status))
        {
            status = (await _validator.ValidateAsync(setCode, value.Trim(), ct)).Status;
            _cache[key] = status;
        }

        switch (status)
        {
            case ReferenceValidationStatus.InvalidValue:
                // The value itself is deliberately NOT echoed: a reference code is low risk, but keeping every import
                // message value-free is the simplest rule to audit.
                return $"The value is not a published value of reference set '{setCode}'.";

            case ReferenceValidationStatus.SetMissing when required:
                _missingRequiredSets.Add(setCode);
                return $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).";

            default:
                return null;
        }
    }
}
