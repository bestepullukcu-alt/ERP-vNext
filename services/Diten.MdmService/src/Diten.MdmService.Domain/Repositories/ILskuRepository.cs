using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface ILskuRepository
{
    Task<Lsku?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("LSKU_READ_CONTRACT_NOT_IMPLEMENTED");

    Task<LskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("LSKU_READ_CONTRACT_NOT_IMPLEMENTED");

    Task<Lsku?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default);

    Task<Lsku?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<Lsku?> GetByIdentityKeyAsync(
        Guid gskuId,
        string marketCode,
        CancellationToken cancellationToken = default);

    Task<LskuCreateResult> CreateDraftAsync(
        Lsku lsku,
        CancellationToken cancellationToken = default);
}

public sealed record LskuPage(
    IReadOnlyList<Lsku> Items,
    long TotalCount);

public sealed record LskuCreateResult(
    bool Succeeded,
    Lsku? Lsku,
    string? ErrorCode = null,
    bool WriteOutcomeAmbiguous = false,
    LskuCreateConflictKind ConflictKind = LskuCreateConflictKind.None);

public enum LskuCreateConflictKind
{
    None = 0,
    CommandOrPayload = 1,
    IdentityKey = 2
}
