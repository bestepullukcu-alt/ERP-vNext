using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>createCycleCount</c> (pack §4.1, §8.12, §12; AC-1, AC-8, AC-12). Order (SB3/F-1, MOD-0178 <c>CreatePutawayTaskHandler</c> :68 → :181 →
/// :239 → :249): the caller's create key FIRST (replay / 400 — a replay takes no number); the business rules; the company prefix; THEN the
/// number from the counter (not part of the transaction); then ONE transaction (count + <c>Created</c> event + central record); a unique-index
/// refusal re-reads the key first (the concurrent twin's replay). A number whose write failed is skipped, never reissued.
/// </summary>
public sealed class CreateCycleCountHandler : CycleCountCommandHandler, IRequestHandler<CreateCycleCountCommand, Response<CycleCountDto>>
{
    private readonly CycleCountChecks _checks;

    public CreateCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountChecks checks)
        : base(repository, support) => _checks = checks;

    public async Task<Response<CycleCountDto>> Handle(CreateCycleCountCommand request, CancellationToken ct)
    {
        var form = CycleCountChecks.Normalize(request.Count);
        var fingerprint = CycleCountSupport.Fingerprint([("op", CycleCountContract.OpCreateCycleCount), .. form.Fields()]);
        if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        if (await _checks.FormAsync<CycleCountDto>(form, ct) is { } refused) return refused;
        var (companyCode, companyRefusal) = await _checks.CompanyCodeAsync<CycleCountDto>(ct);
        if (companyRefusal is not null) return companyRefusal;
        var (number, numberRefusal) = await _checks.NumberAsync<CycleCountDto>(companyCode!, ct);
        if (numberRefusal is not null) return numberRefusal;

        var now = Support.Now();
        var count = new CycleCount
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            Number = number!,
            CompanyCodePrefix = companyCode!,
            WarehouseId = form.WarehouseId,
            CountType = form.CountType,
            Blind = form.Blind,
            ScopeLocationIds = [.. form.ScopeLocationIds],
            ScopeSkuIds = [.. form.ScopeSkuIds],
            PlannedDate = form.PlannedDate,
            AssignedTo = form.AssignedTo,
            Note = form.Note,
            Status = CycleCountStatus.Draft,
            CreatedBy = Support.UserId,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = Support.CorrelationId
        };
        var created = Support.Event(count, CycleCountEventKind.Created, now, After(count), request.IdempotencyKey, CycleCountContract.OpCreateCycleCount, fingerprint);

        try
        {
            await Repository.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertCountAsync(count, tx);
                await session.InsertEventsAsync([created], tx);
                return new CycleCountWriteOutcome(1, count.Id);
            }, ct);
        }
        catch (CycleCountDuplicateException)
        {
            // whichever unique index refused it — the create key of a concurrent twin, or a number the store already holds
            if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.RecordCodeUnavailable, "The count could not be stored; retry.", 503);
        }

        return Response<CycleCountDto>.Success(CycleCountReads.Dto(count), 201);
    }

    private async Task<Response<CycleCountDto>?> CreateReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindByCreateKeyAsync(Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint
            ? Response<CycleCountDto>.Success(CycleCountReads.Dto(earlier))
            : CycleCountSupport.KeyReused<CycleCountDto>();
    }

    /// <summary>The <c>Created</c> event's values (pack §21: number, warehouse, type, blind, scope, planned date, assignee, note).</summary>
    internal static Dictionary<string, string?> After(CycleCount c) => new(StringComparer.Ordinal)
    {
        ["number"] = c.Number, ["warehouseId"] = c.WarehouseId.ToString("D"), ["countType"] = c.CountType.ToString(), ["blind"] = c.Blind ? "true" : "false",
        ["scopeLocationIds"] = CycleCountSupport.List(c.ScopeLocationIds.Select(x => x.ToString("D"))),
        ["scopeSkuIds"] = CycleCountSupport.List(c.ScopeSkuIds.Select(x => x.ToString("D"))), ["plannedDate"] = c.PlannedDate,
        ["assignedTo"] = c.AssignedTo?.ToString("D"), ["note"] = c.Note
    };
}
