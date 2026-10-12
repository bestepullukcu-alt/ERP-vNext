namespace Diten.InventoryService.Domain.Expiry.Enums;

// MOD-0176 (pack §4, FEFO-BUNDLE §2). The wire names of the exception types are the contract's upper-case codes
// (ExpiryWire); every other enum travels by its C# name.

/// <summary>KF-B — what an issue that is not from the first eligible FEFO lot gets (hook in F2; F1 stores it).</summary>
public enum FefoOutboundMode
{
    Warn,
    RequireReason,
    Block
}

/// <summary>KF-C — a goods receipt below the SKU's minimum remaining shelf life (hook in F2; F1 stores it).</summary>
public enum BelowMinRemainingMode
{
    Warn,
    AutoQuarantine,
    Reject
}

/// <summary>KF-D — an expiry / retest date that differs from the suggestion at lot creation (hook in F2; F1 stores it).</summary>
public enum DateOverrideMode
{
    Reason,
    ReasonAndSignature
}

/// <summary>Mockup L:7 — FEFO_BYPASS, BELOW_MIN_REMAINING, EXPIRY_DATE_OVERRIDE, EXPIRED_ON_HAND.</summary>
public enum ExpiryExceptionType
{
    FefoBypass,
    BelowMinRemaining,
    ExpiryDateOverride,
    ExpiredOnHand
}

public enum ExpiryExceptionOrigin
{
    System,
    Manual
}

/// <summary>Pack §8.4. <see cref="Closed"/> and <see cref="Cancelled"/> are terminal.</summary>
public enum ExpiryExceptionStatus
{
    Open,
    InReview,
    Accepted,
    Fixed,
    Closed,
    Cancelled
}

/// <summary>Mockup L:9 — "Gerekçeyle kabul", "Düzeltildi", "İptal".</summary>
public enum ExpiryResolution
{
    AcceptedWithReason,
    Fixed,
    Cancelled
}

/// <summary>Insert-only case events (pack §4.4).</summary>
public enum ExpiryExceptionEventAction
{
    Opened,
    Taken,
    Commented,
    Resolved,
    Closed,
    Cancelled,
    SlaBreached
}
