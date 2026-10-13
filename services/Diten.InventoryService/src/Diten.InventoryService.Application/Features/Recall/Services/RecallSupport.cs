using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// What every MOD-0177 command shares (the MOD-0176 <c>ExpirySupport</c> pattern): the caller scope, the idempotency-key and If-Match
/// rules (RECALL-BUNDLE §1: key 8–100, header = body; stale If-Match → 409 <c>RECALL_CONFLICT</c>), the request fingerprint, the wire
/// formats and the factories of the audit row and the case event. An event / history / audit row is always written INSIDE the
/// transaction of the change it describes.
/// </summary>
public sealed partial class RecallSupport
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    public const string DateFormat = "yyyy-MM-dd";

    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _clock;

    public RecallSupport(ITenantContext tenant, ICorrelationContext correlation, TimeProvider clock)
    {
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid LegalEntityId => _tenant.LegalEntityId;

    public Guid UserId => _tenant.UserId;

    public Guid CorrelationId => _correlation.CorrelationId;

    public DateTimeOffset Now() => _clock.GetUtcNow();

    private static long _sequence = DateTime.UtcNow.Ticks;

    /// <summary>Monotonic event order within this process (at least the current UTC ticks).</summary>
    public static long NextSequence()
    {
        while (true)
        {
            var previous = Interlocked.Read(ref _sequence);
            var next = Math.Max(previous + 1, DateTime.UtcNow.Ticks);
            if (Interlocked.CompareExchange(ref _sequence, next, previous) == previous) return next;
        }
    }

    // ── refusals ──────────────────────────────────────────────────────────────────────────────────────────

    public static Response<T> Fail<T>(RecallViolation violation) => Response<T>.Fail(violation.Code, violation.Message, violation.Status);

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(code, message, status);

    public static Response<T> Invalid<T>(string message) => Response<T>.Fail(InventoryErrorCodes.InvalidRequest, message, 400);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(RecallErrorCodes.Conflict, "The record changed since it was read; reload it and retry with its current ETag.", 409);

    public static Response<T> KeyReused<T>()
        => Response<T>.Fail(InventoryErrorCodes.IdempotencyKeyReused, "This idempotency key was already used for a different request.", 400);

    public static Response<T> CaseNotFound<T>() => Response<T>.Fail(RecallErrorCodes.CaseNotFound, "Recall case not found.", 404);

    public static Response<T> InvalidTransition<T>(string message) => Response<T>.Fail(RecallErrorCodes.InvalidTransition, message, 409);

    public static Response<T> SignatureRequired<T>() => Response<T>.Fail(RecallErrorCodes.SignatureRequired, "This decision requires an e-signature.", 400);

    public static Response<T> SignatureMismatch<T>()
        => Response<T>.Fail(RecallErrorCodes.SignatureMismatch,
            "The signature does not belong to this prepared decision (signer, action, meaning, subject or already used).", 409);

    public static Response<T> PendingMismatch<T>()
        => Response<T>.Fail(RecallErrorCodes.PendingActionMismatch,
            "Prepare this decision first (pending-action) and send the same content within 15 minutes.", 409);

    /// <summary>RECALL-BUNDLE §1 rule 4: header and body key both present, equal after trim, 8–100 characters.</summary>
    public static string? KeyError(string? header, string? body, out string key)
    {
        key = body?.Trim() ?? string.Empty;
        var headerKey = header?.Trim();
        if (string.IsNullOrEmpty(headerKey) || key.Length == 0)
            return "Both the Idempotency-Key header and body idempotencyKey are required.";
        if (!string.Equals(headerKey, key, StringComparison.Ordinal))
            return "The Idempotency-Key header must equal body idempotencyKey.";
        return key.Length is < RecallContract.MinIdempotencyKeyLength or > RecallContract.MaxIdempotencyKeyLength
            ? $"idempotencyKey must be {RecallContract.MinIdempotencyKeyLength}-{RecallContract.MaxIdempotencyKeyLength} characters."
            : null;
    }

    /// <summary><c>W/"n"</c> or <c>"n"</c> → n; anything else → −1 (never matches → 409).</summary>
    public static int ExpectedVersion(string? ifMatch)
    {
        var match = ifMatch is null ? Match.Empty : ETag().Match(ifMatch.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : -1;
    }

    [GeneratedRegex("^(?:W/)?\"([0-9]{1,9})\"$")]
    private static partial Regex ETag();

    /// <summary>SHA-256 over the named fields (order-sensitive) — "same key + same body" is decided on this.</summary>
    public static string Fingerprint(params (string Name, string? Value)[] fields)
    {
        var text = string.Join("\n", fields.Select(f => $"{f.Name}={f.Value ?? "\u0000"}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public static bool TryParseId(string? raw, out Guid id)
        => Guid.TryParseExact(raw, "D", out id) && string.Equals(raw, id.ToString("D"), StringComparison.Ordinal);

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is null ? null : Wire(value.Value);

    public static string WireDate(DateTimeOffset value) => value.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string D(decimal? value) => value is null ? string.Empty : RecallWire.Decimal(value.Value / 1.0000000000000000000000000000m);

    public static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ── record factories ─────────────────────────────────────────────────────────────────────────────────

    public RecallAuditRecord Audit(string action, string subjectType, Guid subjectId, string? number, DateTimeOffset at, string? idempotencyKey,
        IReadOnlyDictionary<string, string?> detail) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        Action = action,
        SubjectType = subjectType,
        SubjectId = subjectId,
        SubjectNumber = number,
        UserId = _tenant.UserId,
        IdempotencyKey = idempotencyKey,
        Detail = new Dictionary<string, string?>(detail, StringComparer.Ordinal),
        CorrelationId = _correlation.CorrelationId,
        LeSource = _tenant.LegalEntitySource,
        OccurredAt = at
    };

    public RecallCaseEvent Event(Guid caseId, RecallCaseEventKind kind, DateTimeOffset at, string? text = null, IReadOnlyDictionary<string, string?>? refs = null,
        string? signatureId = null, string? idempotencyKey = null, string? fingerprint = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        CaseId = caseId,
        Sequence = NextSequence(),
        Kind = kind,
        ActorUserId = _tenant.UserId,
        AtUtc = at,
        Text = text,
        Refs = refs is null ? [] : new Dictionary<string, string?>(refs, StringComparer.Ordinal),
        SignatureId = signatureId,
        IdempotencyKey = idempotencyKey,
        RequestFingerprint = fingerprint,
        CorrelationId = _correlation.CorrelationId
    };
}

/// <summary>Paging of the RECALL-BUNDLE lists: page ≥ 1 (default 1), pageSize 1–200 (default 25). Not numbers → null.</summary>
public static class RecallPaging
{
    public static (int Page, int PageSize)? Parse(string? page, string? pageSize)
    {
        var p = 1;
        var s = RecallContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1))
            return null;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out s) || s is < 1 or > RecallContract.MaxPageSize))
            return null;
        return (p, s);
    }
}
