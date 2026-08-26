using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodRepository
{
    Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinishedGood?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<FinishedGood?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<FinishedGoodPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        CancellationToken cancellationToken = default);
    Task<FinishedGoodCreateResult> CreateDraftAsync(
        FinishedGood finishedGood,
        CancellationToken cancellationToken = default);
}

public sealed record FinishedGoodPage(IReadOnlyList<FinishedGood> Items, long TotalCount);
public sealed record FinishedGoodCreateResult(
    bool Succeeded,
    FinishedGood? FinishedGood,
    string? ErrorCode = null,
    bool WriteOutcomeAmbiguous = false);
