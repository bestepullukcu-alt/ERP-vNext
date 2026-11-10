using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>
/// What every MOD-0181 command shares (MOD-0178 <c>WarehouseSupport</c> pattern, own copy so the modules stay apart): the caller scope, the
/// clock, the idempotency-key and If-Match rules (draft §1 rules 5–6), the request fingerprint, the timeline row factory and the refusals.
/// </summary>
public sealed partial class CycleCountSupport
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly CycleCountTime _clock;

    public CycleCountSupport(ITenantContext tenant, ICorrelationContext correlation, CycleCountTime clock)
    {
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid LegalEntityId => _tenant.LegalEntityId;

    public Guid UserId => _tenant.UserId;

    public Guid CorrelationId => _correlation.CorrelationId;

    public DateTimeOffset Now() => _clock.Now();

    // ── timeline rows ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A timeline row of <paramref name="count"/> by the caller (its <c>Seq</c> = the count's next event seq).</summary>
    public CycleCountEvent Event(CycleCount count, CycleCountEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null,
        string? key = null, string? op = null, string? fingerprint = null, Guid? lineId = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        CountId = count.Id,
        LineId = lineId,
        Seq = ++count.EventSeq,
        Kind = kind,
        ActorUserId = UserId,
        At = at,
        Data = data is null ? [] : new Dictionary<string, string?>(data, StringComparer.Ordinal),
        CorrelationId = CorrelationId,
        IdempotencyKey = key,
        RequestOp = op,
        RequestFingerprint = fingerprint
    };

    // ── refusals ──────────────────────────────────────────────────────────────────────────────────────────

    public static Response<T> Fail<T>(string code, string message, int status, IReadOnlyDictionary<string, object?>? details = null)
        => Response<T>.Fail(code, message, status, details);

    public static Response<T> Invalid<T>(string message) => Response<T>.Fail(InventoryErrorCodes.InvalidRequest, message, 400);

    public static Response<T> NotFound<T>() => Response<T>.Fail(CycleCountErrorCodes.NotFound, "Cycle count not found.", 404);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(CycleCountErrorCodes.Conflict, "The count changed since it was read; reload it and retry with its current ETag.", 409);

    public static Response<T> InvalidState<T>(CycleCountStatus status)
        => Response<T>.Fail(CycleCountErrorCodes.InvalidState, $"The count is {status}; this command is not allowed in that state.", 409,
            new Dictionary<string, object?> { ["status"] = status.ToString() });

    public static Response<T> KeyReused<T>()
        => Response<T>.Fail(InventoryErrorCodes.IdempotencyKeyReused, "This idempotency key was already used for a different request.", 400);

    public static Response<T> ReasonRequired<T>()
        => Response<T>.Fail(CycleCountErrorCodes.ReasonRequired, "reason is required (1-500 characters).", 400);

    // ── request rules ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Draft §1 rule 5: header and body key both present, equal after trim, 8–100 characters.</summary>
    public static string? KeyError(string? header, string? body, out string key)
    {
        key = body?.Trim() ?? string.Empty;
        var headerKey = header?.Trim();
        if (string.IsNullOrEmpty(headerKey) || key.Length == 0)
            return "Both the Idempotency-Key header and body idempotencyKey are required.";
        if (!string.Equals(headerKey, key, StringComparison.Ordinal))
            return "The Idempotency-Key header must equal body idempotencyKey.";
        return key.Length is < CycleCountContract.MinIdempotencyKeyLength or > CycleCountContract.MaxIdempotencyKeyLength
            ? $"idempotencyKey must be {CycleCountContract.MinIdempotencyKeyLength}-{CycleCountContract.MaxIdempotencyKeyLength} characters."
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

    /// <summary>SHA-256 over the named fields (order-sensitive): operation + target + normalized body; no header enters (draft §1 rule 5).</summary>
    public static string Fingerprint(params (string Name, string? Value)[] fields)
    {
        var text = string.Join("\n", fields.Select(f => $"{f.Name}={f.Value ?? "\u0000"}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>A list as one fingerprint field (the caller passes it in its canonical order).</summary>
    public static string List(IEnumerable<string> values) => "[" + string.Join(",", values) + "]";

    public static bool TryParseId(string? raw, out Guid id)
        => Guid.TryParseExact(raw, "D", out id) && string.Equals(raw, id.ToString("D"), StringComparison.Ordinal);

    public static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is null ? null : Wire(value.Value);

    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Paging of the cycle-count lists: page ≥ 1 (default 1), pageSize 1–200 (default 25). Not numbers → null (400).</summary>
public static class CycleCountPaging
{
    public static (int Page, int PageSize)? Parse(string? page, string? pageSize)
    {
        var p = 1;
        var s = CycleCountContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1))
            return null;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out s) || s is < 1 or > CycleCountContract.MaxPageSize))
            return null;
        return (p, s);
    }
}
