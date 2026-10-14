namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// GET /transactions filters (contract <c>getTransactions</c>). <see cref="LocationId"/> matches either side of a
/// movement; <see cref="PostingFromUtc"/> / <see cref="PostingToUtcExclusive"/> bound PostingDate by UTC day,
/// inclusive of both named days (ASSUMPTION-0173-10).
/// </summary>
public sealed record TransactionQuery(
    Guid? ItemId,
    string? LotId,
    string? LocationId,
    DateTimeOffset? PostingFromUtc,
    DateTimeOffset? PostingToUtcExclusive,
    int Page,
    int PageSize);
