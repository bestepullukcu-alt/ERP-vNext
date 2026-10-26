using System.Globalization;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;

namespace Diten.InventoryService.Domain.Expiry.Rules;

/// <summary>FEFO-BUNDLE v1 §4.21 — the codes this module owns (+ the shared ones it reuses by their INVENTORY name).</summary>
public static class ExpiryErrorCodes
{
    public const string PolicyInvalid = "EXPIRY_POLICY_INVALID";
    public const string SignatureRequired = "EXPIRY_SIGNATURE_REQUIRED";
    public const string SignatureMismatch = "EXPIRY_SIGNATURE_MISMATCH";
    public const string PendingResolutionMismatch = "EXPIRY_PENDING_RESOLUTION_MISMATCH";
    public const string InvalidTransition = "EXPIRY_INVALID_TRANSITION";
    public const string Conflict = "EXPIRY_CONFLICT";
    public const string NoChanges = "EXPIRY_NO_CHANGES";
    public const string ExceptionAlreadyOpen = "EXPIRY_EXCEPTION_ALREADY_OPEN";
    public const string SodViolation = "EXPIRY_SOD_VIOLATION";
    public const string ExceptionNotFound = "EXPIRY_EXCEPTION_NOT_FOUND";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string UnknownLot = "UNKNOWN_LOT";

    // Owned by FEFO-BUNDLE, used from F2 on (frozen with v1.0.0, not produced by F1).
    public const string DateOverrideNotUsable = "EXPIRY_DATE_OVERRIDE_NOT_USABLE";
    public const string NoSuggestion = "EXPIRY_NO_SUGGESTION";
    public const string DateEqualsSuggestion = "EXPIRY_DATE_EQUALS_SUGGESTION";

    // FEFO-BUNDLE 1.2.0 (F5).
    public const string EvidenceExportNotFound = "EXPIRY_EVIDENCE_EXPORT_NOT_FOUND";
}

/// <summary>A refused request: contract code, message, HTTP status.</summary>
public sealed record ExpiryViolation(string Code, string Message, int Status);

/// <summary>Wire names (FEFO-BUNDLE §2): exception types are upper-case codes; the other enums travel by name.</summary>
public static class ExpiryWire
{
    private static readonly IReadOnlyDictionary<ExpiryExceptionType, string> TypeCodes = new Dictionary<ExpiryExceptionType, string>
    {
        [ExpiryExceptionType.FefoBypass] = "FEFO_BYPASS",
        [ExpiryExceptionType.BelowMinRemaining] = "BELOW_MIN_REMAINING",
        [ExpiryExceptionType.ExpiryDateOverride] = "EXPIRY_DATE_OVERRIDE",
        [ExpiryExceptionType.ExpiredOnHand] = "EXPIRED_ON_HAND"
    };

    public static IReadOnlyList<string> AllTypeCodes { get; } = TypeCodes.Values.ToList();

    public static string Code(ExpiryExceptionType type) => TypeCodes[type];

    public static bool TryParseType(string? code, out ExpiryExceptionType type)
    {
        foreach (var (key, value) in TypeCodes)
        {
            if (string.Equals(value, code, StringComparison.Ordinal))
            {
                type = key;
                return true;
            }
        }

        type = default;
        return false;
    }

    /// <summary>Exact C# name only ("Open", never "open" or "0") — an enum on the wire is a closed string set.</summary>
    public static bool TryParseName<T>(string? raw, out T value) where T : struct, Enum
    {
        value = default;
        return raw is not null && Enum.GetNames<T>().Contains(raw, StringComparer.Ordinal) && Enum.TryParse(raw, out value);
    }
}

/// <summary>
/// KF-A…KF-D (owner decisions 2026-10-12) + the CT-accepted SLA / threshold defaults (ASSUMPTION-0176-03/-04, CT §19.7) and the
/// pack §12 ranges. Pure: no I/O.
/// </summary>
public static class ExpiryPolicyRules
{
    public const int MinDays = 1;
    public const int MaxDays = 730;
    public const int MinSlaHours = 1;
    public const int MaxSlaHours = 2160;
    public const int MinThresholdDays = 0;
    public const int MaxThresholdDays = 365;

    /// <summary>The policy of a company that never saved one (version 0).</summary>
    public static ExpiryPolicy Defaults() => new()
    {
        ApproachingDays = 90,
        CriticalDays = 30,
        FefoOutboundMode = FefoOutboundMode.RequireReason,
        BelowMinRemainingMode = BelowMinRemainingMode.AutoQuarantine,
        DateOverrideMode = DateOverrideMode.ReasonAndSignature,
        SlaHours = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["FEFO_BYPASS"] = 24,
            ["BELOW_MIN_REMAINING"] = 72,
            ["EXPIRY_DATE_OVERRIDE"] = 48,
            ["EXPIRED_ON_HAND"] = 120
        },
        ExpiredOnHandThresholdDays = 14,
        Version = 0
    };

    /// <summary>Pack §12: ranges, critical &lt; approaching, all four SLA types and nothing else.</summary>
    public static ExpiryViolation? Validate(ExpiryPolicy policy)
    {
        if (policy.ApproachingDays is < MinDays or > MaxDays || policy.CriticalDays is < MinDays or > MaxDays)
        {
            return Invalid($"approachingDays and criticalDays must be {MinDays}-{MaxDays}.");
        }

        if (policy.CriticalDays >= policy.ApproachingDays)
        {
            return Invalid("criticalDays must be less than approachingDays.");
        }

        if (policy.SlaHours.Count != ExpiryWire.AllTypeCodes.Count || ExpiryWire.AllTypeCodes.Any(t => !policy.SlaHours.ContainsKey(t)))
        {
            return Invalid("slaHours must name exactly FEFO_BYPASS, BELOW_MIN_REMAINING, EXPIRY_DATE_OVERRIDE and EXPIRED_ON_HAND.");
        }

        if (policy.SlaHours.Values.Any(h => h is < MinSlaHours or > MaxSlaHours))
        {
            return Invalid($"every slaHours value must be {MinSlaHours}-{MaxSlaHours}.");
        }

        return policy.ExpiredOnHandThresholdDays is < MinThresholdDays or > MaxThresholdDays
            ? Invalid($"expiredOnHandThresholdDays must be {MinThresholdDays}-{MaxThresholdDays}.")
            : null;
    }

    /// <summary>The changed fields, one entry each (field, old, new) — the policy history rows of one save.</summary>
    public static IReadOnlyList<(string Field, string Old, string New)> Changes(ExpiryPolicy before, ExpiryPolicy after)
    {
        var changes = new List<(string, string, string)>();

        void Compare(string field, string old, string @new)
        {
            if (!string.Equals(old, @new, StringComparison.Ordinal)) changes.Add((field, old, @new));
        }

        Compare("approachingDays", I(before.ApproachingDays), I(after.ApproachingDays));
        Compare("criticalDays", I(before.CriticalDays), I(after.CriticalDays));
        Compare("fefoOutboundMode", before.FefoOutboundMode.ToString(), after.FefoOutboundMode.ToString());
        Compare("belowMinRemainingMode", before.BelowMinRemainingMode.ToString(), after.BelowMinRemainingMode.ToString());
        Compare("dateOverrideMode", before.DateOverrideMode.ToString(), after.DateOverrideMode.ToString());
        foreach (var type in ExpiryWire.AllTypeCodes)
        {
            Compare($"slaHours.{type}", I(before.SlaHours.GetValueOrDefault(type)), I(after.SlaHours.GetValueOrDefault(type)));
        }

        Compare("expiredOnHandThresholdDays", I(before.ExpiredOnHandThresholdDays), I(after.ExpiredOnHandThresholdDays));
        return changes;
    }

    /// <summary>The SLA hours of a type under a policy (defaults when the policy lacks it).</summary>
    public static int SlaHoursFor(ExpiryPolicy policy, ExpiryExceptionType type)
        => policy.SlaHours.TryGetValue(ExpiryWire.Code(type), out var hours) ? hours : Defaults().SlaHours[ExpiryWire.Code(type)];

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ExpiryViolation Invalid(string message) => new(ExpiryErrorCodes.PolicyInvalid, message, 400);
}

/// <summary>
/// Pack §8.4 — the exception state machine, CT-F10 (SoD) and the SLA clock. Pure. The ONLY place a status transition is decided:
/// <code>
/// Open ──take──▶ InReview
/// Open | InReview ──resolve(AcceptedWithReason)──▶ Accepted ──close──▶ Closed
/// Open | InReview ──resolve(Fixed)──────────────▶ Fixed    ──close──▶ Closed
/// Open | InReview ──resolve(Cancelled)──────────▶ Cancelled
/// </code>
/// </summary>
public static class ExpiryExceptionRules
{
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 1000;
    public const int MaxCommentLength = 2000;
    public static readonly TimeSpan PendingResolutionLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(6);

    public static bool IsTerminal(ExpiryExceptionStatus status) => status is ExpiryExceptionStatus.Closed or ExpiryExceptionStatus.Cancelled;

    /// <summary>SLA-active: still waiting for a decision.</summary>
    public static bool IsAwaitingDecision(ExpiryExceptionStatus status) => status is ExpiryExceptionStatus.Open or ExpiryExceptionStatus.InReview;

    public static bool CanTake(ExpiryExceptionStatus status) => status == ExpiryExceptionStatus.Open;

    public static bool CanResolve(ExpiryExceptionStatus status) => IsAwaitingDecision(status);

    public static bool CanClose(ExpiryExceptionStatus status) => status is ExpiryExceptionStatus.Accepted or ExpiryExceptionStatus.Fixed;

    public static bool CanComment(ExpiryExceptionStatus status) => !IsTerminal(status);

    public static ExpiryExceptionStatus StatusAfter(ExpiryResolution resolution) => resolution switch
    {
        ExpiryResolution.AcceptedWithReason => ExpiryExceptionStatus.Accepted,
        ExpiryResolution.Fixed => ExpiryExceptionStatus.Fixed,
        ExpiryResolution.Cancelled => ExpiryExceptionStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };

    /// <summary>CT-F10: the user whose action opened the case may not resolve it. System cases (no origin user) have no such user.</summary>
    public static ExpiryViolation? SeparationOfDuties(FefoExceptionCase exceptionCase, Guid resolver)
        => exceptionCase.OriginUserId is { } origin && origin == resolver
            ? new ExpiryViolation(ExpiryErrorCodes.SodViolation,
                "The user whose action opened this exception cannot resolve it (four-eyes).", 409)
            : null;

    /// <summary>Reason 10–1000 after trim (mockup "gerekçe ≥ 10").</summary>
    public static ExpiryViolation? Reason(string? reason)
        => reason?.Trim() is { Length: >= MinReasonLength and <= MaxReasonLength }
            ? null
            : new ExpiryViolation(ExpiryErrorCodes.ReasonRequired, $"reason must be {MinReasonLength}-{MaxReasonLength} characters.", 400);

    public static DateTimeOffset DueAt(DateTimeOffset openedAt, ExpiryPolicy policyAtOpening, ExpiryExceptionType type)
        => openedAt.AddHours(ExpiryPolicyRules.SlaHoursFor(policyAtOpening, type));

    public static bool IsLate(FefoExceptionCase c, DateTimeOffset now) => IsAwaitingDecision(c.Status) && now > c.DueAtUtc;

    public static bool IsDueSoon(FefoExceptionCase c, DateTimeOffset now)
        => IsAwaitingDecision(c.Status) && now <= c.DueAtUtc && c.DueAtUtc - now < DueSoonWindow;

    /// <summary>Manual and sweeper cases: one active case per type + lot + warehouse.</summary>
    public static string DedupKey(ExpiryExceptionType type, string lotId, string? warehouseId)
        => $"{ExpiryWire.Code(type)}:{lotId}:{warehouseId ?? "-"}";

    /// <summary>
    /// The signable action of a resolution (registered with MOD-0022 in F3, module <c>expiry</c>; default meaning APPROVE — CT-F8).
    /// </summary>
    public static string ActionCode(ExpiryResolution resolution) => resolution switch
    {
        ExpiryResolution.AcceptedWithReason => "expiry.exception.accept",
        ExpiryResolution.Fixed => "expiry.exception.fix",
        ExpiryResolution.Cancelled => "expiry.exception.cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };
}
