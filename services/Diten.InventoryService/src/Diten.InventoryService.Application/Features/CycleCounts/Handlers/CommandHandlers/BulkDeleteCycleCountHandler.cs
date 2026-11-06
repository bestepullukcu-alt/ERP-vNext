using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>bulkDeleteCycleCounts</c> (pack §8.3, F-1, 02/F-4; AC-10): one reason for every row (400 <c>COUNT_REASON_REQUIRED</c>); each Draft row
/// whose version matches is soft deleted with the same <c>Cancelled</c> event as a single Draft cancel (<c>Data.softDeleted</c>,
/// <c>Data.bulk</c>); every row has its result (draft §3.5). The key is looked up first among this operation's own events: a replay answers the
/// written rows <c>Deleted</c> and re-evaluates the others.
/// </summary>
public sealed class BulkDeleteCycleCountHandler : IRequestHandler<BulkDeleteCycleCountCommand, Response<BulkDeleteCycleCountsResultDto>>
{
    private const string Op = CycleCountContract.OpBulkDeleteCycleCounts;

    private readonly ICycleCountRepository _repository;
    private readonly CycleCountSupport _support;

    public BulkDeleteCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<BulkDeleteCycleCountsResultDto>> Handle(BulkDeleteCycleCountCommand request, CancellationToken ct)
    {
        if (request.Items.Count is < 1 or > CycleCountContract.MaxBulkItems)
            return CycleCountSupport.Invalid<BulkDeleteCycleCountsResultDto>($"items must hold 1-{CycleCountContract.MaxBulkItems} rows.");
        if (request.Items.Select(i => i.CountId).Distinct().Count() != request.Items.Count)
            return CycleCountSupport.Invalid<BulkDeleteCycleCountsResultDto>("items must not repeat a countId.");
        if (CycleCountRules.Reason(request.Reason) is not { } reason) return CycleCountSupport.ReasonRequired<BulkDeleteCycleCountsResultDto>();

        var fingerprint = CycleCountSupport.Fingerprint(("op", Op), ("reason", reason),
            ("items", CycleCountSupport.List(request.Items.OrderBy(i => i.CountId).Select(i => $"{i.CountId:D}:{i.Version.ToString(CultureInfo.InvariantCulture)}"))));
        if (await ReplayAsync(request, fingerprint, ct) is { } replay) return replay;

        var stored = (await _repository.FindManyAsync(request.Items.Select(i => i.CountId).ToList(), ct)).ToDictionary(c => c.Id);
        List<CycleCountBulkRowDto> results = [];
        try
        {
            await _repository.InTransactionAsync(async (session, tx) =>
            {
                results = [];
                var events = new List<CycleCountEvent>();
                var now = _support.Now();
                foreach (var item in request.Items)
                {
                    if (Refusal(item, stored.GetValueOrDefault(item.CountId)) is { } refused)
                    {
                        results.Add(refused);
                        continue;
                    }

                    var count = stored[item.CountId];
                    var copy = CopyForDelete(count, reason, now);
                    if (!await session.ReplaceCountAsync(copy, count.Version, tx))
                    {
                        results.Add(new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowConflict));
                        continue;
                    }

                    events.Add(_support.Event(copy, CycleCountEventKind.Cancelled, now, new Dictionary<string, string?>
                    {
                        ["previousStatus"] = count.Status.ToString(), ["reason"] = reason, ["softDeleted"] = "true", ["bulk"] = "true",
                        ["version"] = CycleCountSupport.Int(copy.Version)
                    }, request.IdempotencyKey, Op, fingerprint));
                    results.Add(new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowDeleted, copy.Version));
                }

                await session.InsertEventsAsync(events, tx);
                return new CycleCountWriteOutcome(events.Count, null);
            }, ct);
        }
        catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
        {
            return await ReplayAsync(request, fingerprint, ct)
                   ?? CycleCountSupport.Fail<BulkDeleteCycleCountsResultDto>(CycleCountErrorCodes.Conflict, "The counts changed while they were deleted; retry.", 409);
        }

        return Response<BulkDeleteCycleCountsResultDto>.Success(new BulkDeleteCycleCountsResultDto(results));
    }

    private static CycleCountBulkRowDto? Refusal(CycleCountBulkItemInput item, CycleCount? count)
    {
        if (count is null) return new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowNotFound);
        if (!CycleCountRules.IsDraft(count)) return new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowInvalidState, count.Version);
        return count.Version != item.Version ? new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowConflict, count.Version) : null;
    }

    private async Task<Response<BulkDeleteCycleCountsResultDto>?> ReplayAsync(BulkDeleteCycleCountCommand request, string fingerprint, CancellationToken ct)
    {
        var earlier = await _repository.FindBulkKeyEventsAsync(_support.UserId, Op, request.IdempotencyKey, ct);
        if (earlier.Count == 0) return null;
        if (earlier.Any(e => e.RequestFingerprint != fingerprint)) return CycleCountSupport.KeyReused<BulkDeleteCycleCountsResultDto>();

        var written = earlier.ToDictionary(e => e.CountId);
        var stored = (await _repository.FindManyAsync(request.Items.Select(i => i.CountId).ToList(), ct)).ToDictionary(c => c.Id);
        var results = request.Items.Select(item =>
        {
            if (written.TryGetValue(item.CountId, out var e))
            {
                return new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowDeleted,
                    e.Data.TryGetValue("version", out var v) && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null);
            }

            return Refusal(item, stored.GetValueOrDefault(item.CountId)) ?? new CycleCountBulkRowDto(item.CountId.ToString("D"), CycleCountContract.RowConflict);
        }).ToList();
        return Response<BulkDeleteCycleCountsResultDto>.Success(new BulkDeleteCycleCountsResultDto(results));
    }

    private CycleCount CopyForDelete(CycleCount c, string reason, DateTimeOffset now) => new()
    {
        Id = c.Id, TenantId = c.TenantId, LegalEntityId = c.LegalEntityId, CreatedAt = c.CreatedAt, UpdatedAt = now, Version = c.Version + 1,
        IsDeleted = true, DeletedAt = now, Number = c.Number, CompanyCodePrefix = c.CompanyCodePrefix, WarehouseId = c.WarehouseId, CountType = c.CountType,
        Blind = c.Blind, ScopeLocationIds = [.. c.ScopeLocationIds], ScopeSkuIds = [.. c.ScopeSkuIds], PlannedDate = c.PlannedDate, AssignedTo = c.AssignedTo,
        Note = c.Note, Status = CycleCountStatus.Cancelled, CreatedBy = c.CreatedBy, CancelledBy = _support.UserId, CancelledAt = now, CancelReason = reason,
        IdempotencyKey = c.IdempotencyKey, RequestFingerprint = c.RequestFingerprint, EventSeq = c.EventSeq, CorrelationId = c.CorrelationId
    };
}
