using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Repositories;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// The shared skeleton of the count commands (draft §1 rules 5–6; MOD-0178 <c>PutawayCommandHandler</c> pattern): the caller's key event of this
/// count and operation answers first (same fingerprint → 200 + the current count, other → 400), BEFORE If-Match and every business rule; a keyed
/// write that loses its compare-and-set or hits the key index re-reads the key before answering 409.
/// </summary>
public abstract class CycleCountCommandHandler
{
    protected CycleCountCommandHandler(ICycleCountRepository repository, CycleCountSupport support)
    {
        Repository = repository;
        Support = support;
    }

    protected ICycleCountRepository Repository { get; }

    protected CycleCountSupport Support { get; }

    /// <summary>null = not a replay; otherwise the answer (200 + the current count — a Draft cancel's replay answers with the deleted record).</summary>
    protected async Task<Response<CycleCountDto>?> ReplayAsync(Guid countId, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(countId, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return CycleCountSupport.KeyReused<CycleCountDto>();
        var count = await Repository.FindIncludingDeletedAsync(countId, ct);
        return count is null ? CycleCountSupport.NotFound<CycleCountDto>() : Response<CycleCountDto>.Success(CycleCountReads.Dto(count));
    }

    /// <summary>
    /// A command's own keyed write: the count (version + 1) and its rows with a compare-and-set on <paramref name="expectedVersion"/>, plus
    /// <paramref name="more"/> in the same transaction. Lost (0 records or a duplicate key event) → the key is re-read first: found → its replay;
    /// not found → 409 <c>COUNT_CONFLICT</c>.
    /// </summary>
    protected async Task<Response<CycleCountDto>> SaveKeyedAsync(CycleCount copy, int expectedVersion, IReadOnlyList<CycleCountEvent> events, string op, string key,
        string fingerprint, Func<ICycleCountWriteSession, CancellationToken, Task>? more, CancellationToken ct)
    {
        try
        {
            var outcome = await Repository.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceCountAsync(copy, expectedVersion, tx)) return new CycleCountWriteOutcome(0, copy.Id);
                if (more is not null) await more(session, tx);
                await session.InsertEventsAsync(events, tx);
                return new CycleCountWriteOutcome(1, copy.Id);
            }, ct);
            if (outcome.Affected == 1) return Response<CycleCountDto>.Success(CycleCountReads.Dto(copy));
        }
        catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
        {
            // a concurrent twin of this request committed its key event
        }

        return await ReplayAsync(copy.Id, op, key, fingerprint, ct) ?? CycleCountSupport.Conflict<CycleCountDto>();
    }

    /// <summary>
    /// A working copy of the stored count (the stored instance stays as read for the compare-and-set). DL-3: every member is copied (the DL-1
    /// member list lost <c>SubmittedBy</c> / <c>SubmittedAt</c>, which only writes after submit — DL-3's — would have noticed).
    /// </summary>
    protected static CycleCount Copy(CycleCount c) => c.Copy();
}
