namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// The four GS1 Application Identifiers MOD-0174 reads from an ITS / GS1 DataMatrix (K-B): (01) GTIN, (21) serial,
/// (17) expiry, (10) lot. A missing AI is null. <see cref="Expiry"/> is null when (17) is absent or not a real date
/// (then <see cref="ExpiryRaw"/> still carries the six digits).
/// </summary>
public sealed record Gs1Code(string? Gtin, string? Serial, string? ExpiryRaw, DateOnly? Expiry, string? Lot);
