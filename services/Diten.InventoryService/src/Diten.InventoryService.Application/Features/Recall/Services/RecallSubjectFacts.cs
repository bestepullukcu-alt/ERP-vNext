using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// WP-0177-R4 — the <c>rc-v1</c> inputs that are not on the case row, read the same way by the subject provider and by the signature
/// verification (CT decision F3-A: one function, so a hash at signing equals the hash at the command unless the record changed):
/// <list type="bullet">
/// <item>the scope counts of the case's current revision — <see cref="RecallScopeRules.Excluded"/> and the effectively included Stock / Shipment
/// items (the scope view's rules);</item>
/// <item>the effectiveness totals: for a prepared completion the LIVE result (<see cref="LiveEffectivenessAsync"/>, the same computation the
/// completion stores — a declaration or return recorded after signing changes the hash and refuses the completion), for a prepared closure the
/// stored result, otherwise none.</item>
/// </list>
/// </summary>
public sealed class RecallSubjectFacts
{
    private readonly IRecallRepository _repository;

    public RecallSubjectFacts(IRecallRepository repository) => _repository = repository;

    public async Task<RecallCaseSubjectFacts> FactsAsync(RecallCase c, DateTimeOffset now, CancellationToken ct)
    {
        var items = await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        var stock = items.Count(i => i.Kind == RecallScopeItemKind.Stock && RecallScopeRules.EffectivelyIncluded(i, byKey));
        var shipments = items.Count(i => i.Kind == RecallScopeItemKind.Shipment && RecallScopeRules.EffectivelyIncluded(i, byKey));
        var effectiveness = c.PendingAction?.Action switch
        {
            RecallPendingActionKind.CompleteEffectiveness => await LiveEffectivenessAsync(c, items, now, ct),
            RecallPendingActionKind.Close => c.Effectiveness,
            _ => null
        };
        return new RecallCaseSubjectFacts(RecallScopeRules.Excluded(items).Count, stock, shipments, effectiveness);
    }

    /// <summary>The effectiveness result of the case NOW (pack §8.6) — what <c>completeEffectivenessCheck</c> stores.</summary>
    public async Task<RecallEffectivenessResult> LiveEffectivenessAsync(RecallCase c, IReadOnlyList<RecallScopeItem>? items, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await _repository.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
        items ??= await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        return RecallEffectivenessRule.Compute(c.Class, RecallSettingsRules.TargetFor(settings, c.Class),
            RecallReads.EffectivenessInputs(items, RecallReads.Effective(await _repository.ConsumptionsAsync(c.Id, ct)), await _repository.NotificationsAsync(c.Id, ct),
                await _repository.ReturnLinksAsync(c.Id, ct)), now);
    }
}
