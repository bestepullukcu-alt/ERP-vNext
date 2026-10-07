namespace Diten.MdmService.Domain.ValueObjects;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the last stock-behaviour command written to a record, so the same command
/// sent again is answered as a replay (same fingerprint) or refused as a reused key (another fingerprint).
/// </summary>
public sealed record StockBehaviourCommandMark(Guid OperationId, string Fingerprint, int ResultVersion);
