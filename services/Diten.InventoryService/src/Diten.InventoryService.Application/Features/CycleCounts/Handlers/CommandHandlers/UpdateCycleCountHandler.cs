using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>updateCycleCount</c> (pack §8.3 Draft only; AC-8, AC-9): replay first, then 404 / If-Match (409 <c>COUNT_CONFLICT</c>) / state (409
/// <c>COUNT_INVALID_STATE</c>) / the form's business checks; the change is one compare-and-set write with its <c>Updated</c> event (before / after
/// of the changed fields).
/// </summary>
public sealed class UpdateCycleCountHandler : CycleCountCommandHandler, IRequestHandler<UpdateCycleCountCommand, Response<CycleCountDto>>
{
    private readonly CycleCountChecks _checks;

    public UpdateCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountChecks checks)
        : base(repository, support) => _checks = checks;

    public async Task<Response<CycleCountDto>> Handle(UpdateCycleCountCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpUpdateCycleCount;
        var form = CycleCountChecks.Normalize(request.Count);
        var fingerprint = CycleCountSupport.Fingerprint([("op", op), ("countId", request.CountId.ToString("D")), .. form.Fields()]);
        if (await ReplayAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var count = await Repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountDto>();
        if (count.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountDto>();
        if (!CycleCountRules.IsDraft(count)) return CycleCountSupport.InvalidState<CycleCountDto>(count.Status);
        if (await _checks.FormAsync<CycleCountDto>(form, ct) is { } refused) return refused;

        var before = CreateCycleCountHandler.After(count);
        var copy = Copy(count);
        copy.WarehouseId = form.WarehouseId;
        copy.CountType = form.CountType;
        copy.Blind = form.Blind;
        copy.ScopeLocationIds = [.. form.ScopeLocationIds];
        copy.ScopeSkuIds = [.. form.ScopeSkuIds];
        copy.PlannedDate = form.PlannedDate;
        copy.AssignedTo = form.AssignedTo;
        copy.Note = form.Note;
        var after = CreateCycleCountHandler.After(copy);

        var now = Support.Now();
        copy.Version = count.Version + 1;
        copy.UpdatedAt = now;
        var changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (field, value) in after.Where(a => !string.Equals(a.Value, before[a.Key], StringComparison.Ordinal)))
        {
            changes["before." + field] = before[field];
            changes["after." + field] = value;
        }

        var updated = Support.Event(copy, CycleCountEventKind.Updated, now, changes, request.IdempotencyKey, op, fingerprint);
        return await SaveKeyedAsync(copy, count.Version, [updated], op, request.IdempotencyKey, fingerprint, null, ct);
    }
}
