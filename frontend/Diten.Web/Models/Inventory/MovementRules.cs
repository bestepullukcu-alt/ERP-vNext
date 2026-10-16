using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// MOD-0173-FU01 UI-2 — the SHAPE rules of the S-3 movement form (pack §12), as pure functions so they are measured
/// directly. MOD-0173 stays the authority for every business rule (stock, lot, serial, UoM, location): its codes are
/// mapped to sentences, never re-implemented here as silent client rules. What this class decides is only what the
/// direction matrix makes structurally required or forbidden for the 7 manual types (parent §21), so a request the
/// service would refuse as INVALID_REQUEST is answered at the field instead.
/// </summary>
public static partial class MovementRules
{
    public const string ManualSourceType = "MANUAL_MOVEMENT";

    public const string OpeningBalance = "OPENING_BALANCE";
    public const string CountGain = "COUNT_GAIN";
    public const string CountLoss = "COUNT_LOSS";
    public const string Scrap = "SCRAP";
    public const string WriteOff = "WRITE_OFF";
    public const string WarehouseTransfer = "WAREHOUSE_TRANSFER";
    public const string LocationTransfer = "LOCATION_TRANSFER";
    public const string CustomerReturn = "CUSTOMER_RETURN";

    /// <summary>The 7 manual types (parent §21) — STATUS_TRANSFER and the automatic types are never offered.</summary>
    public static readonly IReadOnlyList<string> ManualTypes =
        [OpeningBalance, CountGain, CountLoss, Scrap, WriteOff, WarehouseTransfer, LocationTransfer];

    /// <summary>
    /// WP-0177-R7c (KR-H, CT decision A): the types the NEW-movement form offers = the 7 manual types + <see cref="CustomerReturn"/>. A separate
    /// set on purpose: <see cref="ManualTypes"/> also decides which history rows get "Düzelt" (InventoryListAdapter.IsCorrectable) and what the
    /// correction screen accepts — neither changes.
    /// </summary>
    public static readonly IReadOnlyList<string> FormTypes = [.. ManualTypes, CustomerReturn];

    /// <summary>KD-2: the stock-entry page (StockLedger/Create) offers the inbound manual types only.</summary>
    public static readonly IReadOnlyList<string> EntryTypes = [OpeningBalance, CountGain];

    /// <summary>Inbound target status: exactly these three (parent §21; no status transfer here).</summary>
    public static readonly IReadOnlyList<string> InboundStatuses = ["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE"];

    /// <summary>
    /// WP-0177-R7c (KR-H, CT-R10): a customer return is inspected first — ONLY a held status, never AVAILABLE (MOD-0174's lot rule takes the same
    /// three for a held lot; MOD-0173 / 0174 stay the authority).
    /// </summary>
    public static readonly IReadOnlyList<string> CustomerReturnStatuses = ["QUARANTINE", "QUALITY_INSPECTION", "BLOCKED"];

    /// <summary>The target statuses a type may name (inbound only; empty otherwise).</summary>
    public static IReadOnlyList<string> TargetStatusesOf(string? movementType) => movementType switch
    {
        CustomerReturn => CustomerReturnStatuses,
        OpeningBalance or CountGain => InboundStatuses,
        _ => []
    };

    /// <summary>Every target status the form draws, in its order (each option says which types it belongs to).</summary>
    public static readonly IReadOnlyList<string> FormTargetStatuses = [.. InboundStatuses, "BLOCKED"];

    public enum Direction { Inbound, Outbound, WarehouseTransfer, LocationTransfer }

    public static Direction? DirectionOf(string? movementType) => movementType switch
    {
        OpeningBalance or CountGain or CustomerReturn => Direction.Inbound,
        CountLoss or Scrap or WriteOff => Direction.Outbound,
        WarehouseTransfer => Direction.WarehouseTransfer,
        LocationTransfer => Direction.LocationTransfer,
        _ => null
    };

    /// <summary>Form field ids — the same names the view, the script and the error map use.</summary>
    public static class Fields
    {
        public const string MovementType = "movementType";
        public const string Reason = "movementReason";
        public const string Product = "itemId";
        public const string Sku = "sku";
        public const string Source = "source";
        public const string TargetWarehouse = "toWarehouseId";
        public const string TargetLocation = "toLocationId";
        public const string TargetStatus = "toStockStatus";
        public const string Lot = "lotId";
        public const string Serials = "serialNumbers";
        public const string Quantity = "quantity";
        public const string Unit = "uomId";
        public const string DocumentNo = "documentNo";
        public const string TransactionDate = "transactionDate";
        public const string PostingDate = "postingDate";
    }

    public const int MaxTextLength = 200;
    public const int MaxLotLength = 40;

    /// <summary>
    /// W-UI-02 (owner waiver): a typed lot is trimmed and upper-cased, then must be 1–40 characters of
    /// <c>[A-Z0-9./-]</c>. Blank = no lot (the service decides whether one is required).
    /// </summary>
    public static bool TryNormalizeLot(string? raw, out string? lot)
    {
        lot = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        // MOD-0174 P3 (pack §11.4): a REGISTERED lot travels as its canonical lower-case id (TRACE lotId, ASSUMPTION-0174-03)
        // and is passed unchanged — upper-casing it would turn it into a typed text that names no lot.
        if (IsRegisteredLotId(raw.Trim()))
        {
            lot = raw.Trim();
            return true;
        }

        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length > MaxLotLength || !LotPattern().IsMatch(normalized)) return false;
        lot = normalized;
        return true;
    }

    /// <summary>
    /// A registered MOD-0174 lot id: the canonical lower-case UUID the TRACE API returns (the W-UI-02 typed lot is
    /// upper-case <c>[A-Z0-9./-]</c> text, so the two forms do not overlap — the service applies the same rule, P2).
    /// </summary>
    public static bool IsRegisteredLotId(string? value) =>
        Guid.TryParseExact(value, "D", out var id) && string.Equals(value, id.ToString("D"), StringComparison.Ordinal);

    /// <summary>One serial number per line, trimmed; blank lines are dropped; a duplicate is refused.</summary>
    public static bool TryParseSerials(string? text, out IReadOnlyList<string> serials)
    {
        var list = (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        serials = list;
        return list.Count == list.Distinct(StringComparer.Ordinal).Count() && list.All(s => s.Length <= MaxTextLength);
    }

    public static bool IsQuantity(string? value) =>
        value is not null
        && QuantityPattern().IsMatch(value)
        && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
        && amount > 0m;

    /// <summary>A contract decimal without trailing zeros ("1.000" → "1") — the same quantity, typed back unambiguously.</summary>
    public static string? PlainQuantity(string? value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount.ToString("0.######", CultureInfo.InvariantCulture)
            : value;

    /// <summary>A contract decimal in the READER's number format ("1.000" is one thousand in tr): display only.</summary>
    public static string ReadableQuantity(string? value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount.ToString("#,0.######", CultureInfo.CurrentCulture)
            : value ?? string.Empty;

    /// <summary>How far a dialog's instant may run ahead of the server clock (browser clock skew).</summary>
    public static readonly TimeSpan DialogInstantMaxAhead = TimeSpan.FromMinutes(5);

    /// <summary>How old a dialog's instant may be — an S-2 Reversal dialog left open longer is stale; reload.</summary>
    public static readonly TimeSpan DialogInstantMaxAge = TimeSpan.FromHours(24);

    /// <summary>
    /// FU-UI2-05: an instant minted by the browser when a confirmation opened — UTC, not more than
    /// <see cref="DialogInstantMaxAhead"/> in the future and not older than <see cref="DialogInstantMaxAge"/>.
    /// </summary>
    public static bool TryDialogInstant(string? value, DateTimeOffset now, out string utc)
    {
        if (!TryUtc(value, out utc)) return false;
        var instant = DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        if (instant > now + DialogInstantMaxAhead || instant < now - DialogInstantMaxAge)
        {
            utc = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>A UTC ISO-8601 instant as the contract wants it (the browser converts the reader's local time).</summary>
    public static bool TryUtc(string? value, out string utc)
    {
        utc = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith('Z')) return false;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            return false;
        utc = parsed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// The source of an outbound / transfer = ONE balance row, carried as <c>warehouseId|locationId|stockStatus|lotId</c>
    /// (the lot part may be empty). The service re-reads the row when it posts; this only names it.
    /// </summary>
    public sealed record SourceRow(string WarehouseId, string LocationId, string StockStatus, string? LotId)
    {
        public string Key => string.Join("|", WarehouseId, LocationId, StockStatus, LotId ?? string.Empty);

        public static bool TryParse(string? key, out SourceRow? row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            var parts = key.Split('|');
            if (parts.Length != 4 || parts[0].Length == 0 || parts[1].Length == 0) return false;
            if (!InventoryListAdapter.StockStatuses.Contains(parts[2], StringComparer.Ordinal)) return false;
            if (parts.Take(2).Concat([parts[3]]).Any(p => p.Length > MaxTextLength)) return false;
            row = new SourceRow(parts[0], parts[1], parts[2], parts[3].Length == 0 ? null : parts[3]);
            return true;
        }
    }

    /// <summary>A checked request: the contract body to post, or the field errors (field id → resx key).</summary>
    public sealed record Checked(IReadOnlyDictionary<string, object?>? Payload, IReadOnlyDictionary<string, string> Errors, SourceRow? Source)
    {
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>
    /// The direction matrix (pack §12) on a submitted form, restricted to <paramref name="allowedTypes"/>. A field that
    /// the matrix hides for the chosen type is IGNORED (the screen hid it), never sent — so an inbound never carries a
    /// source side and a transfer never carries a target status.
    /// </summary>
    public static Checked Check(MovementSubmitViewModel form, IReadOnlyList<string> allowedTypes, string idempotencyKey)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var type = form.MovementType?.Trim();
        var direction = type is not null && allowedTypes.Contains(type, StringComparer.Ordinal) ? DirectionOf(type) : null;
        if (direction is null) errors[Fields.MovementType] = "ValMovementType";

        if (!Guid.TryParse(form.ItemId, out var itemId)) errors[Fields.Product] = "ValRequired";
        if (!InventoryListAdapter.TryParseSku(form.Sku ?? string.Empty, out var skuLevel, out var skuId)) errors[Fields.Sku] = "ValRequired";
        if (!IsQuantity(form.Quantity?.Trim())) errors[Fields.Quantity] = "ValQuantity";
        if (string.IsNullOrWhiteSpace(form.UomId) || form.UomId.Trim().Length > MaxTextLength) errors[Fields.Unit] = "ValRequired";
        if (form.DocumentNo is { Length: > MaxTextLength }) errors[Fields.DocumentNo] = "ValTooLong";
        if (!TryUtc(form.TransactionDate, out var transactionDate)) errors[Fields.TransactionDate] = "ValDate";
        if (!TryUtc(form.PostingDate, out var postingDate)) errors[Fields.PostingDate] = "ValDate";
        if (!TryParseSerials(form.SerialNumbers, out var serials)) errors[Fields.Serials] = "ValSerialDuplicate";

        SourceRow? source = null;
        string? lot = null;
        string? toWarehouse = null, toLocation = null, toStatus = null;
        switch (direction)
        {
            case Direction.Inbound:
                toWarehouse = Required(form.ToWarehouseId, Fields.TargetWarehouse, errors);
                toLocation = Required(form.ToLocationId, Fields.TargetLocation, errors);
                toStatus = form.ToStockStatus?.Trim();
                if (toStatus is null || !TargetStatusesOf(type).Contains(toStatus, StringComparer.Ordinal)) errors[Fields.TargetStatus] = "ValRequired";
                if (!TryNormalizeLot(form.LotId, out lot)) errors[Fields.Lot] = "ValLotFormat";
                break;

            case Direction.Outbound:
                if (!SourceRow.TryParse(form.Source, out source)) errors[Fields.Source] = "ValRequired";
                break;

            case Direction.WarehouseTransfer:
                if (!SourceRow.TryParse(form.Source, out source)) errors[Fields.Source] = "ValRequired";
                toWarehouse = Required(form.ToWarehouseId, Fields.TargetWarehouse, errors);
                toLocation = Required(form.ToLocationId, Fields.TargetLocation, errors);
                if (source is not null && toWarehouse is not null && string.Equals(source.WarehouseId, toWarehouse, StringComparison.Ordinal))
                    errors[Fields.TargetWarehouse] = "ValSameWarehouse";
                break;

            case Direction.LocationTransfer:
                if (!SourceRow.TryParse(form.Source, out source)) errors[Fields.Source] = "ValRequired";
                // The warehouse is the source's own (locked on screen); a different one would be a warehouse transfer.
                toWarehouse = source?.WarehouseId;
                toLocation = Required(form.ToLocationId, Fields.TargetLocation, errors);
                if (source is not null && form.ToWarehouseId is { Length: > 0 } named && !string.Equals(named.Trim(), source.WarehouseId, StringComparison.Ordinal))
                    errors[Fields.TargetWarehouse] = "ValSameWarehouseRequired";
                if (source is not null && toLocation is not null && string.Equals(source.LocationId, toLocation, StringComparison.Ordinal))
                    errors[Fields.TargetLocation] = "ValSameLocation";
                break;
        }

        if (errors.Count > 0) return new Checked(null, errors, source);

        // Outbound and transfers move the SOURCE row's own lot; only an inbound names one (W-UI-02).
        if (direction != Direction.Inbound) lot = source!.LotId;

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["idempotencyKey"] = idempotencyKey,
            ["movementType"] = type,
            ["itemId"] = itemId.ToString("D"),
            ["skuId"] = skuId,
            ["skuLevel"] = skuLevel,
            ["quantity"] = form.Quantity!.Trim(),
            ["uomId"] = form.UomId!.Trim(),
            ["sourceModule"] = InventoryListAdapter.ManualSourceModule,
            ["sourceType"] = ManualSourceType,
            ["transactionDate"] = transactionDate,
            ["postingDate"] = postingDate
        };
        if (source is not null)
        {
            payload["fromWarehouseId"] = source.WarehouseId;
            payload["fromLocationId"] = source.LocationId;
            payload["fromStockStatus"] = source.StockStatus;
        }

        if (toWarehouse is not null) payload["toWarehouseId"] = toWarehouse;
        if (toLocation is not null) payload["toLocationId"] = toLocation;
        if (direction == Direction.Inbound) payload["toStockStatus"] = toStatus;
        if (lot is not null) payload["lotId"] = lot;
        if (serials.Count > 0) payload["serialIds"] = serials;
        if (!string.IsNullOrWhiteSpace(form.DocumentNo)) payload["sourceDocumentId"] = form.DocumentNo.Trim();
        // WP-0176-F5 seam (CT decision A): the FEFO bypass reason only when sent — absent, the payload is the pre-F5 one
        if (!string.IsNullOrWhiteSpace(form.FefoBypassReason)) payload["fefoBypassReason"] = form.FefoBypassReason.Trim();

        return new Checked(payload, errors, source);
    }

    private static string? Required(string? value, string field, Dictionary<string, string> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxTextLength)
        {
            errors[field] = "ValRequired";
            return null;
        }

        return trimmed;
    }

    [GeneratedRegex(@"^[A-Z0-9./-]{1,40}$")]
    private static partial Regex LotPattern();

    [GeneratedRegex(@"^\d+(\.\d{1,6})?$")]
    private static partial Regex QuantityPattern();
}
