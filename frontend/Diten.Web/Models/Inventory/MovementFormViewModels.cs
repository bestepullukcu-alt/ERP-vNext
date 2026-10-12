using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Diten.Web.Models.Inventory;

// MOD-0173-FU01 UI-2 — S-3 New movement, Edit = "Correct" (KD-1), StockLedger/Create = "New stock entry" (KD-2) and the
// Details receipt. One form model for the three pages (one shared _Form partial, pack §19 KD-2), one body for the JSON
// post, one receipt carried from the POST to GET Details (PRG — the receipt is not addressable by id later, BG-06).

/// <summary>The three pages that host the ONE movement form.</summary>
public enum MovementFormMode { Create, Entry, Correct }

/// <summary>The page model of Create / Edit (Correct) / StockLedger Create.</summary>
public sealed class StockMovementFormViewModel
{
    public MovementFormMode Mode { get; init; }

    /// <summary>Minted on GET (pack §4/§8): one key per opened form; a double submit of THIS form is a replay.</summary>
    public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString("D");

    /// <summary>Correct only: the key of step (1), the REVERSAL — the second of the two keys (KD-1).</summary>
    public string? ReversalKey { get; init; }

    /// <summary>
    /// Correct only: the reversal's transaction / posting date, fixed when the page opened. The service fingerprints
    /// the dates, so a retry of the same correction must send the same instant to be a replay, not a key reuse.
    /// </summary>
    public string? ReversalAt { get; init; }

    public IReadOnlyList<string> AllowedTypes { get; init; } = MovementRules.ManualTypes;

    /// <summary>BG-03: no MOD-0048 movement-reason set and no tenant lookup exist — the Reason field is not drawn.</summary>
    public bool ReasonCodesAvailable => false;

    // Prefill (S-1 row "Movement", S-2 row "Correct") — display labels travel with the ids so a picker can show them.
    public string? MovementType { get; init; }
    public string? ItemId { get; init; }
    public string? ItemText { get; init; }
    public string? Sku { get; init; }
    public string? SkuText { get; init; }
    public string? Source { get; init; }
    public string? ToWarehouseId { get; init; }
    public string? ToLocationId { get; init; }
    public string? ToWarehouseText { get; init; }
    public string? ToLocationText { get; init; }
    public string? ToStockStatus { get; init; }
    public string? LotId { get; init; }
    public string? Quantity { get; init; }
    public string? UomId { get; init; }
    public string? DocumentNo { get; init; }

    /// <summary>Correct only: the movement being corrected, from the history row (BG-06: no GET by id).</summary>
    public CorrectionOriginalViewModel? Original { get; init; }

    /// <summary>Correct opened without a usable original (typed URL, automatic or reversal row): no form is drawn.</summary>
    public bool OriginalMissing => Mode == MovementFormMode.Correct && Original is null;

    public string BackUrl => Mode == MovementFormMode.Entry ? "/StockLedger" : "/StockMovements";

    public string PostUrl => Mode == MovementFormMode.Correct ? "/StockMovements/Edit" : "/StockMovements/Create";

    public static StockMovementFormViewModel ForCreate(IQueryCollection query) => Prefilled(MovementFormMode.Create, MovementRules.ManualTypes, query);

    public static StockMovementFormViewModel ForEntry(IQueryCollection query) => Prefilled(MovementFormMode.Entry, MovementRules.EntryTypes, query);

    /// <summary>
    /// S-2 "Correct" opens <c>/StockMovements/Edit?id=…</c> with the history row's own fields. Only a manual type of the
    /// seven can be corrected (a REVERSAL never; an automatic movement has no Correct button). The identity is
    /// re-checked by MOD-0173 against the original when the reversal posts, so a hand-edited URL reverses nothing else.
    /// </summary>
    public static StockMovementFormViewModel ForCorrection(IQueryCollection query)
    {
        var original = CorrectionOriginalViewModel.From(query);
        return new StockMovementFormViewModel
        {
            Mode = MovementFormMode.Correct,
            ReversalKey = Guid.NewGuid().ToString("D"),
            ReversalAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            Original = original,
            MovementType = original?.MovementType,
            ItemId = original?.ItemId,
            ItemText = Text(query, "itemText"),
            Sku = original is null ? null : original.SkuLevel + ":" + original.SkuId,
            SkuText = Text(query, "skuText"),
            ToWarehouseId = original?.ToWarehouseId,
            ToLocationId = original?.ToLocationId,
            ToWarehouseText = Text(query, "toWarehouseText"),
            ToLocationText = Text(query, "toLocationText"),
            ToStockStatus = original?.ToStockStatus,
            LotId = original?.LotId,
            Quantity = MovementRules.PlainQuantity(original?.Quantity),
            UomId = original?.UomId,
            DocumentNo = Text(query, "doc")
        };
    }

    private static StockMovementFormViewModel Prefilled(MovementFormMode mode, IReadOnlyList<string> allowed, IQueryCollection query)
    {
        var type = Text(query, "type");
        return new StockMovementFormViewModel
        {
            Mode = mode,
            AllowedTypes = allowed,
            MovementType = type is not null && allowed.Contains(type, StringComparer.Ordinal) ? type : null,
            ItemId = Guid.TryParse(Text(query, "itemId"), out var item) ? item.ToString("D") : null,
            ItemText = Text(query, "itemText"),
            Sku = InventoryListAdapter.TryParseSku(Text(query, "sku") ?? string.Empty, out var level, out var skuId) ? level + ":" + skuId : null,
            SkuText = Text(query, "skuText"),
            Source = MovementRules.SourceRow.TryParse(Text(query, "source"), out var row) ? row!.Key : null
        };
    }

    internal static string? Text(IQueryCollection query, string name)
    {
        var value = query[name].ToString();
        return string.IsNullOrWhiteSpace(value) || value.Length > MovementRules.MaxTextLength ? null : value.Trim();
    }
}

/// <summary>The history row a correction starts from (pack §19 KD-1; UI-09 identity fields + the prefill).</summary>
public sealed class CorrectionOriginalViewModel
{
    public string TransactionId { get; init; } = string.Empty;
    public string? TransactionNumber { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string SkuId { get; init; } = string.Empty;
    public string SkuLevel { get; init; } = string.Empty;
    public string Quantity { get; init; } = string.Empty;
    public string UomId { get; init; } = string.Empty;
    public string? ToWarehouseId { get; init; }
    public string? ToLocationId { get; init; }
    public string? ToStockStatus { get; init; }
    public string? LotId { get; init; }
    public string? TransactionDate { get; init; }

    public static CorrectionOriginalViewModel? From(IQueryCollection query)
    {
        var type = StockMovementFormViewModel.Text(query, "type");
        var quantity = StockMovementFormViewModel.Text(query, "quantity");
        var uom = StockMovementFormViewModel.Text(query, "uomId");
        var level = StockMovementFormViewModel.Text(query, "skuLevel");
        if (!Guid.TryParse(StockMovementFormViewModel.Text(query, "id"), out var id)
            || !Guid.TryParse(StockMovementFormViewModel.Text(query, "itemId"), out var item)
            || !Guid.TryParse(StockMovementFormViewModel.Text(query, "skuId"), out var sku)
            || level is null || !InventoryListAdapter.SkuLevels.Contains(level, StringComparer.Ordinal)
            || type is null || !MovementRules.ManualTypes.Contains(type, StringComparer.Ordinal)
            || quantity is null || !MovementRules.IsQuantity(quantity)
            || uom is null)
            return null;

        var status = StockMovementFormViewModel.Text(query, "toStatus");
        return new CorrectionOriginalViewModel
        {
            TransactionId = id.ToString("D"),
            TransactionNumber = StockMovementFormViewModel.Text(query, "no"),
            MovementType = type,
            ItemId = item.ToString("D"),
            SkuId = sku.ToString("D"),
            SkuLevel = level,
            Quantity = quantity,
            UomId = uom,
            ToWarehouseId = StockMovementFormViewModel.Text(query, "toWarehouseId"),
            ToLocationId = StockMovementFormViewModel.Text(query, "toLocationId"),
            ToStockStatus = status is not null && InventoryListAdapter.StockStatuses.Contains(status, StringComparer.Ordinal) ? status : null,
            LotId = StockMovementFormViewModel.Text(query, "lotId"),
            TransactionDate = StockMovementFormViewModel.Text(query, "txDate")
        };
    }
}

/// <summary>Body of <c>POST /StockMovements/Create</c> — the 15 fields (Reason absent while BG-03 is open).</summary>
public sealed class MovementSubmitViewModel
{
    public string? IdempotencyKey { get; set; }
    public string? MovementType { get; set; }
    public string? ItemId { get; set; }
    /// <summary><c>Level:skuId</c> — the contract needs both.</summary>
    public string? Sku { get; set; }
    /// <summary>One balance row: <c>warehouseId|locationId|stockStatus|lotId</c>.</summary>
    public string? Source { get; set; }
    public string? ToWarehouseId { get; set; }
    public string? ToLocationId { get; set; }
    public string? ToStockStatus { get; set; }
    public string? LotId { get; set; }
    public string? SerialNumbers { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? DocumentNo { get; set; }
    /// <summary>UTC ISO-8601 — the browser converts the reader's local date-time (it alone knows the time zone).</summary>
    public string? TransactionDate { get; set; }
    public string? PostingDate { get; set; }
    /// <summary>KD-2: the stock-entry page posts with <c>entry = true</c>, which narrows the types to the inbound two.</summary>
    public bool Entry { get; set; }
    /// <summary>Display only (the Model A picker's labels) — for the receipt; never sent to the service.</summary>
    public string? ItemText { get; set; }
    public string? SkuText { get; set; }
}

/// <summary>Body of <c>POST /StockMovements/Edit</c> — step (1) reverses the original, step (2) posts the movement.</summary>
public sealed class CorrectionSubmitViewModel
{
    public string? TransactionId { get; set; }
    public string? TransactionNumber { get; set; }
    public string? ReversalKey { get; set; }
    public string? ReversalAt { get; set; }
    /// <summary>The original's identity, exactly as the history row showed it (the service re-checks it).</summary>
    public string? OriginalItemId { get; set; }
    public string? OriginalSkuId { get; set; }
    public string? OriginalSkuLevel { get; set; }
    public string? OriginalQuantity { get; set; }
    public string? OriginalUomId { get; set; }
    public MovementSubmitViewModel? Movement { get; set; }
}

/// <summary>Contract <c>MovementResponse</c> with the balance snapshot the receipt shows (pack §12 "current balance").</summary>
public sealed class PostedMovementDto
{
    public string? TransactionId { get; set; }
    public string? TransactionNumber { get; set; }
    public string? PostedAt { get; set; }
    public bool IdempotentReplay { get; set; }
    public BalanceViewDto? BalanceSnapshot { get; set; }
}

/// <summary>The Details receipt (PRG via TempData). Labels are resolved when it is written; quantities stay strings.</summary>
public sealed class MovementReceiptViewModel
{
    public const string TempDataKey = "Inventory.MovementReceipt";

    public string? TransactionId { get; set; }
    public string? TransactionNumber { get; set; }
    public string? PostedAt { get; set; }
    public bool IdempotentReplay { get; set; }
    public string? MovementType { get; set; }
    public string? ProductText { get; set; }
    public string? SkuText { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? FromText { get; set; }
    public string? ToText { get; set; }
    public string? ToStockStatus { get; set; }
    public string? LotId { get; set; }
    public int SerialCount { get; set; }
    public string? DocumentNo { get; set; }
    public string? TransactionDate { get; set; }
    public string? PostingDate { get; set; }
    /// <summary>The balance row the movement left (MovementResponse.balanceSnapshot).</summary>
    public string? BalanceLocationText { get; set; }
    public string? BalanceLotId { get; set; }
    public string? BalanceStatus { get; set; }
    public string? BalanceOnHand { get; set; }
    public string? BalanceAvailable { get; set; }
    public string? BaseQuantity { get; set; }
    public string? BaseUomId { get; set; }
    /// <summary>KD-1: the REVERSAL number of a completed correction (two records, two keys).</summary>
    public string? ReversalNumber { get; set; }
    public string? CorrectedNumber { get; set; }
    /// <summary>KD-2: the receipt of a stock entry links back to the stock list.</summary>
    public bool FromEntry { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(TransactionNumber);

    public static MovementReceiptViewModel From(ITempDataDictionary tempData)
    {
        // Peek, not read: a refresh of the receipt shows it again (PRG), the next post replaces it.
        if (tempData.Peek(TempDataKey) is not string json) return new MovementReceiptViewModel();
        try { return JsonSerializer.Deserialize<MovementReceiptViewModel>(json, InventoryGatewayClient.Json) ?? new MovementReceiptViewModel(); }
        catch (JsonException) { return new MovementReceiptViewModel(); }
    }

    public void Store(ITempDataDictionary tempData) =>
        tempData[TempDataKey] = JsonSerializer.Serialize(this, InventoryGatewayClient.Json);
}
