using System.Text.RegularExpressions;

namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// AUD-001 §3 #10 — an <see cref="IAuditableCommand"/> that also says WHAT changed. Called by the behavior AFTER the
/// handler ran, with the handler's response (<c>null</c> when the handler threw), so the handler itself is never touched.
/// Must not throw for a refused response (its data is then usually null).
/// </summary>
public interface IAuditDetailsProvider
{
    void DescribeAuditDetails(AuditDetails details, object? response);
}

/// <summary>
/// Per-execution audit facts. AUD-001 §3: a changed field is recorded by its NAME (<see cref="ChangedFields"/>) unless
/// its value is needed to understand the event; a secret never enters — a key that names one (password, token, secret,
/// API key, payload, signature …) is stored as <see cref="Redacted"/> whatever value it was given.
/// </summary>
public sealed class AuditDetails
{
    public const string Redacted = "[REDACTED]";
    public const string RefusalCodeKey = "refusalCode";
    public const string StatusCodeKey = "statusCode";
    public const string ErrorTypeKey = "errorType";
    public const string ObjectVersionKey = "objectVersion";
    public const string ChangedFieldsKey = "changedFields";

    private static readonly string[] SecretFragments =
    [
        "password", "passwd", "secret", "token", "apikey", "credential", "mfa", "otp", "recoverycode", "hash",
        "payload", "signature", "privatekey"
    ];

    private readonly List<string> _changedFields = [];

    /// <summary>The object's id when the command learns it from its response (a create).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>The object's version after the command (recorded as <c>objectVersion</c>).</summary>
    public int? ObjectVersion { get; set; }

    public Dictionary<string, object?>? BeforeState { get; private set; }
    public Dictionary<string, object?>? AfterState { get; private set; }
    public Dictionary<string, object?> Metadata { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<string> ChangedFields => _changedFields;

    /// <summary>A field changed; only its name is recorded.</summary>
    public AuditDetails Changed(params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!string.IsNullOrWhiteSpace(field) && !_changedFields.Contains(field, StringComparer.Ordinal))
            {
                _changedFields.Add(field);
            }
        }

        return this;
    }

    /// <summary>A field changed and its before / after values are part of the event (never personal data).</summary>
    public AuditDetails Change(string field, object? before, object? after)
    {
        (BeforeState ??= new(StringComparer.Ordinal))[field] = Guard(field, before);
        (AfterState ??= new(StringComparer.Ordinal))[field] = Guard(field, after);
        return Changed(field);
    }

    public AuditDetails Set(string key, object? value)
    {
        Metadata[key] = Guard(key, value);
        return this;
    }

    /// <summary>Does the key name a secret? Case, '_' and '-' are ignored: <c>apiKey</c>, <c>API_KEY</c>, <c>api-key</c>.</summary>
    public static bool NamesASecret(string key)
    {
        var normalised = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return SecretFragments.Any(fragment => normalised.Contains(fragment, StringComparison.Ordinal));
    }

    private static object? Guard(string key, object? value) => value is not null && NamesASecret(key) ? Redacted : value;
}

/// <summary>
/// What may enter an audit record as a refusal code: a code shape (<c>visit_qr_version_conflict</c>,
/// <c>CONCURRENCY_CONFLICT</c>, <c>crm.visit.not-found</c>), never a sentence — a message is not forwarded at all.
/// </summary>
public static class AuditCodes
{
    private static readonly Regex Shape = new("^[A-Za-z][A-Za-z0-9_.\\-]{1,63}$", RegexOptions.CultureInvariant);

    public static bool IsCode(string? value) => value is not null && Shape.IsMatch(value);

    public static string? FirstCode(IEnumerable<string>? errors) => errors?.FirstOrDefault(IsCode);
}
