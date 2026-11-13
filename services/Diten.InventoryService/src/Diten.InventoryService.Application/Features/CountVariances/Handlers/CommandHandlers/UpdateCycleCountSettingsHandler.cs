using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>updateCycleCountSettings</c> — the ONE PUT (pack §4.4, §12; AC-49, AC-51). Shape: <c>toleranceAbsolute</c> a decimal string ≥ 0 (≤ 6 fraction
/// digits), <c>tolerancePercent</c> 0..100, <c>maxRecounts</c> 0..10, <c>maxLinesPerCount</c> 1..5000 (400, nothing written) → the caller's key event of
/// the settings (same fingerprint → 200 + the settings now, other → 400) → If-Match = the settings' version (<c>W/"0"</c> while the defaults hold;
/// stale → 409 <c>COUNT_CONFLICT</c>). ONE transaction: the record (inserted the first time — the company index refuses a twin — or a compare-and-set)
/// and an <c>Updated</c> row with the before / after values.
/// </summary>
public sealed class UpdateCycleCountSettingsHandler : IRequestHandler<UpdateCycleCountSettingsCommand, Response<CycleCountSettingsDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CycleCountSupport _support;

    public UpdateCycleCountSettingsHandler(ICycleCountRepository repository, CycleCountSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<CycleCountSettingsDto>> Handle(UpdateCycleCountSettingsCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpUpdateCycleCountSettings;
        var input = request.Input;
        if (!CycleCountRules.TryParseCounted(input.ToleranceAbsolute, out var absolute))
            return Invalid("toleranceAbsolute", "toleranceAbsolute must be a decimal string ≥ 0 with at most 6 fraction digits.");
        if (!CycleCountRules.TryParseCounted(input.TolerancePercent, out var percent))
            return Invalid("tolerancePercent", "tolerancePercent must be a decimal string between 0 and 100.");
        if (CycleCountDecisionRules.SettingsError(absolute, percent, input.MaxRecounts, input.MaxLinesPerCount) is { } field)
            return Invalid(field, $"{field} is out of range (tolerancePercent 0-100, maxRecounts 0-{CycleCountDecisionRules.MaxRecountsLimit}, " +
                                  $"maxLinesPerCount 1-{CycleCountDecisionRules.MaxLinesPerCountLimit}).");

        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("toleranceAbsolute", InventoryQuantity.ToWire(absolute)),
            ("tolerancePercent", InventoryQuantity.ToWire(percent)), ("maxRecounts", CycleCountSupport.Int(input.MaxRecounts)),
            ("maxLinesPerCount", CycleCountSupport.Int(input.MaxLinesPerCount)));
        var stored = await _repository.FindSettingsAsync(ct);
        if (await ReplayAsync(stored, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if ((stored?.Version ?? 0) != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountSettingsDto>();

        var now = _support.Now();
        var next = stored?.Copy() ?? new CycleCountSettings { Id = Guid.NewGuid(), CreatedAt = now, Version = 0 };
        next.ToleranceAbsolute = absolute;
        next.TolerancePercent = percent;
        next.MaxRecounts = input.MaxRecounts;
        next.MaxLinesPerCount = input.MaxLinesPerCount;
        next.UpdatedBy = _support.UserId;
        next.UpdatedAt = now;
        next.Version = (stored?.Version ?? 0) + 1;
        var before = CycleCountPolicy.Of(stored);
        var row = _support.SettingsEvent(next, now, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["previousToleranceAbsolute"] = InventoryQuantity.ToWire(before.ToleranceAbsolute), ["toleranceAbsolute"] = InventoryQuantity.ToWire(absolute),
            ["previousTolerancePercent"] = InventoryQuantity.ToWire(before.TolerancePercent), ["tolerancePercent"] = InventoryQuantity.ToWire(percent),
            ["previousMaxRecounts"] = CycleCountSupport.Int(before.MaxRecounts), ["maxRecounts"] = CycleCountSupport.Int(input.MaxRecounts),
            ["previousMaxLinesPerCount"] = CycleCountSupport.Int(before.MaxLinesPerCount), ["maxLinesPerCount"] = CycleCountSupport.Int(input.MaxLinesPerCount)
        }, request.IdempotencyKey, op, fingerprint);

        try
        {
            var outcome = await _repository.InTransactionAsync(async (session, tx) =>
            {
                if (stored is null) await session.InsertSettingsAsync(next, tx);
                else if (!await session.ReplaceSettingsAsync(next, stored.Version, tx)) return new CycleCountWriteOutcome(0, next.Id);
                await session.InsertEventsAsync([row], tx);
                return new CycleCountWriteOutcome(1, next.Id);
            }, ct);
            if (outcome.Affected == 1) return Response<CycleCountSettingsDto>.Success(CycleCountReads.Settings(next));
        }
        catch (CycleCountDuplicateException ex) when (ex.Index is CycleCountUniqueIndex.Settings or CycleCountUniqueIndex.Event)
        {
            // a concurrent first write (or a twin of this request) committed first
        }

        return await ReplayAsync(await _repository.FindSettingsAsync(ct), op, request.IdempotencyKey, fingerprint, ct)
               ?? CycleCountSupport.Conflict<CycleCountSettingsDto>();
    }

    private async Task<Response<CycleCountSettingsDto>?> ReplayAsync(CycleCountSettings? stored, string op, string key, string fingerprint, CancellationToken ct)
    {
        if (stored is null) return null;
        var earlier = await _repository.FindKeyEventAsync(stored.Id, _support.UserId, op, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint != fingerprint
            ? CycleCountSupport.KeyReused<CycleCountSettingsDto>()
            : Response<CycleCountSettingsDto>.Success(CycleCountReads.Settings(stored));
    }

    private static Response<CycleCountSettingsDto> Invalid(string field, string message)
        => CycleCountSupport.Fail<CycleCountSettingsDto>(Common.InventoryErrorCodes.InvalidRequest, message, 400,
            new Dictionary<string, object?> { ["field"] = field });
}
