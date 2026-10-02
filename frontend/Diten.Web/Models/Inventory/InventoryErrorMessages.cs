namespace Diten.Web.Models.Inventory;

/// <summary>
/// Contract error code → resx key (pack §13: every failure is a localized sentence that says what to do; the code is
/// never the message, and "403" / "Forbidden" never reach the reader — UAS-001 §5). The keys exist in BOTH screen
/// resx families in all seven languages (StockLedgerIndex, StockMovementsIndex); a test holds that.
/// </summary>
public static class InventoryErrorMessages
{
    public static readonly IReadOnlyDictionary<string, string> ByCode = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // parent pack §12 / §13 codes
        ["INSUFFICIENT_STOCK"] = "ErrInsufficientStock",
        ["ALREADY_REVERSED"] = "ErrAlreadyReversed",
        ["PRODUCT_MASTER_UNAVAILABLE"] = "ErrMasterUnavailable",
        ["LOCATION_MASTER_UNAVAILABLE"] = "ErrMasterUnavailable",
        ["CROSS_LE_FORBIDDEN"] = "ErrCrossLegalEntity",
        ["IDEMPOTENCY_KEY_REUSED"] = "ErrKeyReused",
        ["INVALID_REQUEST"] = "ErrInvalidRequest",
        ["INVALID_MOVEMENT_TYPE"] = "ErrInvalidRequest",
        ["UNKNOWN_ITEM"] = "ErrUnknownItem",
        ["SKU_ITEM_MISMATCH"] = "ErrUnknownItem",
        ["UNKNOWN_LOCATION"] = "ErrUnknownLocation",
        ["UOM_CONVERSION_MISSING"] = "ErrUomMissing",
        ["LOT_REQUIRED"] = "ErrLotRequired",
        ["SERIAL_COUNT_MISMATCH"] = "ErrSerialMismatch",
        ["RESERVATION_CONFLICT"] = "ErrConflict",
        ["POSTING_PERIOD_CLOSED"] = "ErrPeriodClosed",
        // W-0173-02 company selector
        ["LEGAL_ENTITY_MASTER_UNAVAILABLE"] = "ErrCompanyCheckUnavailable",
        [InventoryGatewayClient.CompanyNotSelected] = "ErrSelectCompany",
        // the MVC adapter's own refusals (never produced by the shipped JS)
        [InventoryListAdapter.SearchNotSupported] = "ErrInvalidRequest",
        [InventoryListAdapter.OrderNotSupported] = "ErrInvalidRequest",
        [InventoryListAdapter.PageMisaligned] = "ErrInvalidRequest"
    };

    public const string NoPermission = "ErrNoPermission";
    public const string SessionExpired = "ErrSessionExpired";
    public const string ServiceUnavailable = "ErrServiceUnavailable";
    public const string Conflict = "ErrConflict";
    public const string Generic = "ErrGeneric";

    /// <summary>UI-2: the amounts of an INSUFFICIENT_STOCK refusal (<c>error.details.available / requested</c>).</summary>
    public const string InsufficientStockAmounts = "ErrInsufficientStockAmounts";

    /// <summary>Every key this class can return — the resx parity test asserts each is in all 14 files.</summary>
    public static IEnumerable<string> AllKeys =>
        ByCode.Values.Concat([NoPermission, SessionExpired, ServiceUnavailable, Conflict, Generic, InsufficientStockAmounts]).Distinct(StringComparer.Ordinal);

    /// <summary>The key for an answer: the contract code first, then the HTTP status.</summary>
    public static string KeyFor(string? code, int status)
    {
        if (code is not null && ByCode.TryGetValue(code, out var key)) return key;
        return status switch
        {
            401 => SessionExpired,
            403 => NoPermission,
            409 => Conflict,
            0 or 502 or 503 or 504 => ServiceUnavailable,
            _ => Generic
        };
    }
}
