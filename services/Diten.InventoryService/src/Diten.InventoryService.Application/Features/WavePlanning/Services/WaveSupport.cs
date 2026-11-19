using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Rules;

namespace Diten.InventoryService.Application.Features.WavePlanning.Services;

/// <summary>
/// What every MOD-0180 command shares (the MOD-0181 <c>CycleCountSupport</c> pattern, own copy so the modules stay apart): the caller scope, the
/// clock, the idempotency-key and If-Match rules (draft §1 rules 5–6), the request fingerprint, the timeline row factory and the refusals.
/// </summary>
public sealed partial class WaveSupport
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly WaveTime _clock;

    public WaveSupport(ITenantContext tenant, ICorrelationContext correlation, WaveTime clock)
    {
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock;
    }

    public Guid UserId => _tenant.UserId;

    public Guid LegalEntityId => _tenant.LegalEntityId;

    public Guid CorrelationId => _correlation.CorrelationId;

    public DateTimeOffset Now() => _clock.Now();

    // ── timeline rows ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A row of <paramref name="wave"/>'s timeline by the caller (its <c>Seq</c> = the wave's next event seq).</summary>
    public WaveEvent Event(Wave wave, WaveEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null, string? key = null,
        string? op = null, string? fingerprint = null)
        => Row(wave.Id, WaveEventStream.Wave, ++wave.EventSeq, kind, at, data, key, op, fingerprint);

    /// <summary>A row of the warehouse setting's own stream (pack §4.3: the stream id is the setting record's id).</summary>
    public WaveEvent Event(WaveWarehouseSettings settings, WaveEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data = null,
        string? key = null, string? op = null, string? fingerprint = null)
        => Row(settings.Id, WaveEventStream.Settings, ++settings.EventSeq, kind, at, data, key, op, fingerprint);

    private WaveEvent Row(Guid streamId, WaveEventStream stream, int seq, WaveEventKind kind, DateTimeOffset at, IReadOnlyDictionary<string, string?>? data,
        string? key, string? op, string? fingerprint) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        WaveId = streamId,
        Stream = stream,
        Seq = seq,
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

    public static Response<T> NotFound<T>() => Response<T>.Fail(WaveErrorCodes.NotFound, "Wave not found.", 404);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(WaveErrorCodes.Conflict, "The record changed since it was read; reload it and retry with its current ETag.", 409);

    public static Response<T> InvalidState<T>(WaveStatus status)
        => Response<T>.Fail(WaveErrorCodes.InvalidState, $"The wave is {status}; this command is not allowed in that state.", 409,
            new Dictionary<string, object?> { ["status"] = status.ToString() });

    public static Response<T> KeyReused<T>()
        => Response<T>.Fail(InventoryErrorCodes.IdempotencyKeyReused, "This idempotency key was already used for a different request.", 400);

    public static Response<T> ReasonRequired<T>()
        => Response<T>.Fail(WaveErrorCodes.ReasonRequired, $"reason is required ({WaveRules.MinReasonLength}-{WaveRules.MaxReasonLength} characters).", 400);

    public static Response<T> SettingsRequired<T>()
        => Response<T>.Fail(WaveErrorCodes.SettingsRequired, "The warehouse is not wave managed or has no time zone; set its wave setting first.", 422);

    public static Response<T> UnknownWarehouse<T>(Guid warehouseId, int status)
        => Response<T>.Fail(InventoryErrorCodes.UnknownLocation, "The warehouse does not exist as an active warehouse of the selected company.", status,
            new Dictionary<string, object?> { ["locationId"] = warehouseId.ToString("D") });

    public static Response<T> InventoryUnavailable<T>()
        => Response<T>.Fail(WaveErrorCodes.InventoryUnavailable, "The warehouse data could not be read; nothing was written.", 503);

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
        return key.Length is < WaveContract.MinIdempotencyKeyLength or > WaveContract.MaxIdempotencyKeyLength
            ? $"idempotencyKey must be {WaveContract.MinIdempotencyKeyLength}-{WaveContract.MaxIdempotencyKeyLength} characters."
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

    /// <summary>A list as one fingerprint / event field (the caller passes it in its canonical order).</summary>
    public static string List(IEnumerable<string> values) => "[" + string.Join(",", values) + "]";

    public static bool TryParseId(string? raw, out Guid id)
        => Guid.TryParseExact(raw, "D", out id) && string.Equals(raw, id.ToString("D"), StringComparison.Ordinal);

    public static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is null ? null : Wire(value.Value);

    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string? Int(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Paging of the wave lists: page ≥ 1 (default 1), pageSize 1–200 (default 25). Not numbers → null (400).</summary>
public static class WavePaging
{
    public static (int Page, int PageSize)? Parse(string? page, string? pageSize)
    {
        var p = 1;
        var s = WaveContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1))
            return null;
        if (pageSize is not null && (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out s) || s is < 1 or > WaveContract.MaxPageSize))
            return null;
        return (p, s);
    }
}
