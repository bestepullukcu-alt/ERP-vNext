using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the stock-behaviour writes of GSKU, LSKU and Finished Good. Each is ONE
/// conditional write that moves the record's version (CT decision A), and only while no operation has pinned that
/// version: the record is Draft or IdentityApproved as read, no lifecycle operation holds it and no identity operation
/// of it is unfinished — otherwise <see cref="StockBehaviourStoreCodes.BlockedByOperation"/>. A field filled for the
/// first time on a non-Draft record is written only while it is still empty (missing or null — O-8, R2 / R3). The
/// identity flow's own writes and filters are not touched — with ONE narrow, CT-decided exception elsewhere: the Finished
/// Good draft cancel unsets the GTIN and keeps it as ReleasedGtin (S3 FIX1 R2 / FIX2); no audit intent is written (path b
/// — CT decision 2).
/// </summary>
public interface IProductStockBehaviourRepository
{
    Task<StockBehaviourWriteResult> WriteGskuAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default);

    Task<StockBehaviourWriteResult> WriteLskuAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default);

    Task<StockBehaviourWriteResult> WriteFinishedGoodAsync(StockBehaviourWrite write, CancellationToken cancellationToken = default);

    /// <summary>
    /// The GTIN goes through the same write (field <see cref="StockBehaviourFields.Gtin"/>, mark
    /// <see cref="StockBehaviourWrite.MarksGtin"/>); a GTIN another non-retired Finished Good of the tenant holds is
    /// <see cref="StockBehaviourStoreCodes.GtinConflict"/> (pre-check, and the unique partial index under a race).
    /// </summary>
    Task<bool> GtinHeldByAnotherAsync(string gtin, Guid finishedGoodId, CancellationToken cancellationToken = default);
}

/// <summary>The stored field names a stock-behaviour write may set (the entity property names).</summary>
public static class StockBehaviourFields
{
    public const string BaseUomCode = "BaseUomCode";
    public const string UomConversions = "UomConversions";
    public const string ShelfLifeDays = "ShelfLifeDays";
    public const string StorageConditionCodes = "StorageConditionCodes";
    public const string LotControlled = "LotControlled";
    public const string SerialControlled = "SerialControlled";
    public const string Stockable = "Stockable";
    public const string Gtin = "Gtin";
    public const string StockReferenceSelections = "StockReferenceSelections";
}

/// <summary>
/// One write: <see cref="Sets"/> are the fields to set (name → value, already validated); <see cref="FirstFills"/> are
/// those of them that must still be empty in the store (a non-Draft record's first fill). <see cref="ExpectedStatus"/>
/// is the status the record was read in — the write matches only that status.
/// </summary>
public sealed record StockBehaviourWrite(
    Guid RecordId,
    int ExpectedVersion,
    ProductIdentityLifecycleStatus ExpectedStatus,
    IReadOnlyDictionary<string, object?> Sets,
    IReadOnlySet<string> FirstFills,
    StockBehaviourCommandMark Mark,
    DateTimeOffset AtUtc,
    bool MarksGtin = false);

public sealed record StockBehaviourWriteResult(bool Succeeded, int? Version, string? ErrorCode, bool IsReplay = false)
{
    public static StockBehaviourWriteResult Fail(string code) => new(false, null, code);
}

public static class StockBehaviourStoreCodes
{
    public const string NotFound = "STOCK_BEHAVIOUR_RECORD_NOT_FOUND";
    public const string BlockedByOperation = "STOCK_BEHAVIOUR_EDIT_BLOCKED_BY_OPERATION";
    /// <summary>S3 FIX1 M4: pending identity approval, retired, or a cancelled Finished Good draft — not editable at all.</summary>
    public const string RecordNotEditable = "STOCK_BEHAVIOUR_RECORD_NOT_EDITABLE";
    public const string FieldLocked = "STOCK_BEHAVIOUR_FIELD_LOCKED";
    public const string ConcurrencyConflict = "STOCK_BEHAVIOUR_CONCURRENCY_CONFLICT";
    public const string IdempotencyConflict = "IDEMPOTENCY_KEY_CONFLICT";
    public const string GtinConflict = "FG_GTIN_CONFLICT";
    public const string ContractInvalid = "STOCK_BEHAVIOUR_WRITE_CONTRACT_INVALID";
}
