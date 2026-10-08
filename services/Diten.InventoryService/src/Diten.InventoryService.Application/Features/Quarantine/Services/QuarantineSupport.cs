using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// What every MOD-0175 command shares: the caller scope (tenant, company, actor), the idempotency-key / If-Match rules
/// (contract §1: key 8–100, header = body; stale If-Match → 409 <c>QUARANTINE_CONFLICT</c>), the request fingerprint, the
/// wire formats and the factories of history rows and decision events (actor, correlation, company source). A history row
/// or an event is always written INSIDE the transaction that makes the change.
/// </summary>
public sealed class QuarantineSupport
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    public const string DateFormat = "yyyy-MM-dd";

    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;

    public QuarantineSupport(ITenantContext tenant, ICorrelationContext correlation)
    {
        _tenant = tenant;
        _correlation = correlation;
    }

    public Guid TenantId => _tenant.TenantId;

    public Guid LegalEntityId => _tenant.LegalEntityId;

    public Guid UserId => _tenant.UserId;

    public static DateTimeOffset Now() => DateTimeOffset.UtcNow;

    private static long _sequence = DateTime.UtcNow.Ticks;

    /// <summary>Monotonic event order: at least the current UTC ticks, always above the previous value of this process.</summary>
    public static long NextSequence()
    {
        while (true)
        {
            var previous = Interlocked.Read(ref _sequence);
            var next = Math.Max(previous + 1, DateTime.UtcNow.Ticks);
            if (Interlocked.CompareExchange(ref _sequence, next, previous) == previous) return next;
        }
    }

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    // ── refusals ──────────────────────────────────────────────────────────────────────────────────────────

    public static Response<T> Fail<T>(string code, string message, int status, IReadOnlyDictionary<string, object?>? details = null)
        => Response<T>.Fail(code, message, status, details);

    public static Response<T> Fail<T>(QuarantineViolation violation) => Response<T>.Fail(violation.Code, violation.Message, violation.Status);

    public static Response<T> Invalid<T>(string message) => Response<T>.Fail(InventoryErrorCodes.InvalidRequest, message, 400);

    public static Response<T> Conflict<T>()
        => Response<T>.Fail(QuarantineErrorCodes.Conflict, "The record changed since it was read; reload it and retry with its current ETag.", 409);

    public static Response<T> KeyReused<T>()
        => Response<T>.Fail(InventoryErrorCodes.IdempotencyKeyReused, "This idempotency key was already used for a different request.", 400);

    /// <summary>Contract §1 rule 4: header and body key both present, equal after trim, 8–100 characters.</summary>
    public static string? KeyError(string? header, string? body, out string key)
    {
        key = body?.Trim() ?? string.Empty;
        var headerKey = header?.Trim();
        if (string.IsNullOrEmpty(headerKey) || key.Length == 0)
            return "Both the Idempotency-Key header and body idempotencyKey are required.";
        if (!string.Equals(headerKey, key, StringComparison.Ordinal))
            return "The Idempotency-Key header must equal body idempotencyKey.";
        return key.Length is < QuarantineContract.MinIdempotencyKeyLength or > QuarantineContract.MaxIdempotencyKeyLength
            ? $"idempotencyKey must be {QuarantineContract.MinIdempotencyKeyLength}-{QuarantineContract.MaxIdempotencyKeyLength} characters."
            : null;
    }

    /// <summary><c>W/"n"</c> → n; anything else → −1 (never matches → 409).</summary>
    public static int ExpectedVersion(string? ifMatch) => ifMatch is null ? -1 : LocationWriteSupport.ExpectedVersion(ifMatch);

    public static string Fingerprint(params (string Name, string? Value)[] fields) => LocationWriteSupport.Fingerprint(fields);

    public static bool TryParseId(string? raw, out Guid id) => LocationWriteSupport.TryParseId(raw, out id);

    public static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is null ? null : Wire(value.Value);

    public static string? WireDate(DateTimeOffset? value)
        => value?.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset Midnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    // ── record factories ─────────────────────────────────────────────────────────────────────────────────

    public QuarantineHistoryEntry History(
        HistorySubject subject, Guid subjectId, HistoryAction action, DateTimeOffset at, string? reason = null, Guid? decisionId = null,
        Guid? byUserId = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        SubjectType = subject,
        SubjectId = subjectId,
        AtUtc = at,
        ByUserId = byUserId ?? _tenant.UserId,
        Action = action,
        Reason = reason,
        DecisionId = decisionId,
        CorrelationId = _correlation.CorrelationId,
        LeSource = _tenant.LegalEntitySource
    };

    public static QuarantineDecisionEvent Event(
        Guid decisionId, DecisionEventKind kind, DateTimeOffset at, Guid? byUserId, string? errorCode = null, string? detail = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        DecisionId = decisionId,
        Kind = kind,
        // Submitted repeats after StartFailed (contract §5); the two failures repeat on every retry. Every other kind
        // happens once per decision — the unique index makes a concurrent second writer fail, not double-apply.
        OnceKey = kind is DecisionEventKind.Submitted or DecisionEventKind.StartFailed or DecisionEventKind.ApplyFailed ? null : kind.ToString(),
        AtUtc = at,
        Sequence = NextSequence(),
        ByUserId = byUserId,
        ErrorCode = errorCode,
        Detail = detail
    };
}
