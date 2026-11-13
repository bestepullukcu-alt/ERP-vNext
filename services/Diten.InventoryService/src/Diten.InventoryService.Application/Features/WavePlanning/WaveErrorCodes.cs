namespace Diten.InventoryService.Application.Features.WavePlanning;

/// <summary>
/// WMS-WAVE-BUNDLE's own error codes (draft §5.1; pack §8.17) — the DL-1 ones. The codes it reuses (<c>INVALID_REQUEST</c>,
/// <c>IDEMPOTENCY_KEY_REUSED</c>, <c>UNKNOWN_LOCATION</c>, <c>LEGAL_ENTITY_MASTER_UNAVAILABLE</c>) stay <c>InventoryErrorCodes</c>'.
/// </summary>
public static class WaveErrorCodes
{
    /// <summary>404 — no such wave, or another tenant's / company's.</summary>
    public const string NotFound = "WAVE_NOT_FOUND";

    /// <summary>409 — stale If-Match (D-14).</summary>
    public const string Conflict = "WAVE_CONFLICT";

    /// <summary>409 — the command is not allowed in the wave's state.</summary>
    public const string InvalidState = "WAVE_INVALID_STATE";

    /// <summary>422 — the warehouse is not wave managed or has no time zone.</summary>
    public const string SettingsRequired = "WAVE_SETTINGS_REQUIRED";

    /// <summary>400 — the local cutoff does not exist (daylight-saving gap) or lies more than an hour in the past.</summary>
    public const string CutoffInvalid = "WAVE_CUTOFF_INVALID";

    /// <summary>400 — an update names another warehouse than the wave's.</summary>
    public const string WarehouseImmutable = "WAVE_WAREHOUSE_IMMUTABLE";

    /// <summary>422 — an added outbound is not eligible (<c>details.orders[]</c>).</summary>
    public const string OrderNotEligible = "WAVE_ORDER_NOT_ELIGIBLE";

    /// <summary>409 — the outbound is in an active wave, or a concurrent add won (D-8).</summary>
    public const string OrderInOtherWave = "WAVE_ORDER_IN_OTHER_WAVE";

    /// <summary>422 — the technical ceiling (500 orders / 2.000 planned tasks).</summary>
    public const string TooLarge = "WAVE_TOO_LARGE";

    /// <summary>400 — a cancel / setting change without a reason of 10..500 characters.</summary>
    public const string ReasonRequired = "WAVE_REASON_REQUIRED";

    /// <summary>422 — the MDM company code is not <c>^[A-Z]{2,5}$</c>.</summary>
    public const string CompanyCodeInvalid = "WAVE_COMPANY_CODE_INVALID";

    /// <summary>503 — the counter could not issue a number; no wave is created.</summary>
    public const string RecordCodeUnavailable = "WAVE_RECORD_CODE_UNAVAILABLE";

    /// <summary>503 — a MOD-0178 / LOCATION read could not be done; nothing written.</summary>
    public const string InventoryUnavailable = "WAVE_INVENTORY_UNAVAILABLE";

    /// <summary>422 — the responsible user is not an active user of the tenant.</summary>
    public const string ResponsibleInvalid = "WAVE_RESPONSIBLE_INVALID";

    /// <summary>503 — the user directory did not answer; nothing written.</summary>
    public const string ResponsibleDirectoryUnavailable = "WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE";
}
