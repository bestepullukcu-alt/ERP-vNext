using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>
/// What every MOD-0178 command shares (the MOD-0177 <c>RecallSupport</c> pattern, own copy so the modules stay apart): the caller scope, the
/// idempotency-key and If-Match rules (WMS draft §1 rules 5–6: key 8–100, header = body; stale If-Match → 409 <c>WMS_CONFLICT</c>), the
/// request fingerprint and the refusals the WMS wire knows.
/// </summary>
public sealed partial class WarehouseSupport
{
    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _clock;
    private readonly IPostingActorName? _actorName;

    public WarehouseSupport(ITenantContext tenant, ICorrelationContext correlation, TimeProvider clock, IPostingActorName? actorName = null)
    {
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock;
        _actorName = actorName;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid LegalEntityId => _tenant.LegalEntityId;

    public Guid UserId => _tenant.UserId;

    /// <summary>The caller's display name now (token name); null when the token has none.</summary>
    public string? ActorName => _actorName?.Current;

    public Guid CorrelationId => _correlation.CorrelationId;

    public DateTimeOffset Now() => _clock.GetUtcNow();

    // ── refusals ──────────────────────────────────────────────────────────────────────────────────────────

    public static Response<T> Fail<T>(string code, string message, int status, IReadOnlyDictionary<string, object?>? details = null)
        => Response<T>.Fail(code, message, status, details);

    public static Response<T> Invalid<T>(string message) => Response<T>.Fail(InventoryErrorCodes.InvalidRequest, message, 400);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(WarehouseErrorCodes.Conflict, "The record changed since it was read; reload it and retry with its current ETag.", 409);

    public static Response<T> KeyReused<T>()
        => Response<T>.Fail(InventoryErrorCodes.IdempotencyKeyReused, "This idempotency key was already used for a different request.", 400);

    public static Response<T> RoleNotFound<T>() => Response<T>.Fail(WarehouseErrorCodes.LocationRoleNotFound, "Location role not found.", 404);

    // ── request rules ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>WMS draft §1 rule 5: header and body key both present, equal after trim, 8–100 characters.</summary>
    public static string? KeyError(string? header, string? body, out string key)
    {
        key = body?.Trim() ?? string.Empty;
        var headerKey = header?.Trim();
        if (string.IsNullOrEmpty(headerKey) || key.Length == 0)
            return "Both the Idempotency-Key header and body idempotencyKey are required.";
        if (!string.Equals(headerKey, key, StringComparison.Ordinal))
            return "The Idempotency-Key header must equal body idempotencyKey.";
        return KeyLengthError(key);
    }

    /// <summary>DELETE carries no body: the header alone, 8–100 characters.</summary>
    public static string? HeaderKeyError(string? header, out string key)
    {
        key = header?.Trim() ?? string.Empty;
        return key.Length == 0 ? "The Idempotency-Key header is required." : KeyLengthError(key);
    }

    private static string? KeyLengthError(string key)
        => key.Length is < WarehouseContract.MinIdempotencyKeyLength or > WarehouseContract.MaxIdempotencyKeyLength
            ? $"idempotencyKey must be {WarehouseContract.MinIdempotencyKeyLength}-{WarehouseContract.MaxIdempotencyKeyLength} characters."
            : null;

    /// <summary><c>W/"n"</c> or <c>"n"</c> → n; anything else → −1 (never matches → 409).</summary>
    public static int ExpectedVersion(string? ifMatch)
    {
        var match = ifMatch is null ? Match.Empty : ETag().Match(ifMatch.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : -1;
    }

    [GeneratedRegex("^(?:W/)?\"([0-9]{1,9})\"$")]
    private static partial Regex ETag();

    /// <summary>
    /// SHA-256 over the named fields (order-sensitive) — "same key + same body" is decided on this (pack §8.1: operation + target + normalized
    /// body; neither <c>Idempotency-Key</c> nor <c>If-Match</c> enters).
    /// </summary>
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
}

/// <summary>Paging of the WMS lists: page ≥ 1 (default 1), pageSize 1–200 (default 25). Not numbers → null (400).</summary>
public static class WarehousePaging
{
    public static (int Page, int PageSize)? Parse(string? page, string? pageSize)
    {
        var p = 1;
        var s = WarehouseContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1))
            return null;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out s) || s is < 1 or > WarehouseContract.MaxPageSize))
            return null;
        return (p, s);
    }
}
