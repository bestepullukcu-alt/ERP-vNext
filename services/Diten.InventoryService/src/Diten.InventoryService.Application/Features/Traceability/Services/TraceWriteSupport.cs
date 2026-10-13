using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>
/// What every MOD-0174 write shares (pack §8 / §12): the idempotency-key and <c>If-Match</c> rules (the same rules as
/// the MOD-0173 ledger and the FU02 location master, re-used from <see cref="LocationWriteSupport"/>), the lot date and
/// id shapes, the request fingerprint, and the history-row factory (actor, correlation, company source). A history row
/// is always written by the caller INSIDE the transaction that makes the change.
/// </summary>
public sealed partial class TraceWriteSupport
{
    public const string SourceApi = "api";
    public const string SourceSystemExpiry = "system-expiry";
    public const string SourceMigration = "migration";

    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;

    public TraceWriteSupport(ITenantContext tenant, ICorrelationContext correlation)
    {
        _tenant = tenant;
        _correlation = correlation;
    }

    public Guid UserId => _tenant.UserId;

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(code, message, status);

    public static Response<T> Invalid<T>(string message) => Response<T>.Fail(TraceErrorCodes.InvalidRequest, message, 400);

    public static Response<T> UnknownLot<T>() => Response<T>.Fail(TraceErrorCodes.UnknownLot, "Lot not found.", 404);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(TraceErrorCodes.LotConflict, "The lot changed since it was read; reload it and retry with its current ETag.", 409);

    public static string? KeyError(string? header, string? body, out string key) => LocationWriteSupport.KeyError(header, body, out key);

    public static int ExpectedVersion(string ifMatch) => LocationWriteSupport.ExpectedVersion(ifMatch);

    public static string Fingerprint(params (string Name, string? Value)[] fields) => LocationWriteSupport.Fingerprint(fields);

    /// <summary>A lot / serial id is the canonical lower-case UUID string this API returns; any other spelling is unknown.</summary>
    public static bool TryParseId(string? raw, out Guid id) => LocationWriteSupport.TryParseId(raw, out id);

    /// <summary>
    /// A lot date: the v1 wire form <c>yyyy-MM-ddT00:00:00Z</c> (UTC midnight, optional fraction of zero) or a plain
    /// <c>yyyy-MM-dd</c>. A time other than midnight is refused, never silently truncated (pack §4: date precision).
    /// </summary>
    public static bool TryParseDate(string raw, out DateOnly day)
    {
        day = default;
        if (DateOnly.TryParseExact(raw, TraceContract.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return true;
        }

        if (!MidnightUtc().IsMatch(raw)
            || !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            || value.Offset != TimeSpan.Zero
            || value.TimeOfDay != TimeSpan.Zero)
        {
            return false;
        }

        day = DateOnly.FromDateTime(value.UtcDateTime);
        return true;
    }

    /// <summary>ISO 3166-1 alpha-2 shape (two upper-case letters).</summary>
    public static bool IsCountryCode(string value) => CountryCode().IsMatch(value);

    public static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public LotChangeEntry History(
        Guid lotId, LotChangeAction action, string? field, string? oldValue, string? newValue, string? reason, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        LotId = lotId,
        Action = action,
        Field = field,
        OldValue = oldValue,
        NewValue = newValue,
        Reason = reason,
        ActorUserId = _tenant.UserId,
        Source = SourceApi,
        CorrelationId = _correlation.CorrelationId,
        LeSource = _tenant.LegalEntitySource,
        OccurredAt = at
    };

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T00:00:00(\.0{1,7})?(Z|[+-]00:00)$", RegexOptions.CultureInvariant)]
    private static partial Regex MidnightUtc();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCode();
}
