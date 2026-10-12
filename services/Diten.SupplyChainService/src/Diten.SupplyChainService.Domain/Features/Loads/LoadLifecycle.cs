namespace Diten.SupplyChainService.Domain.Features.Loads;
public static class LoadLifecycle
{
 public static bool Allows(LoadStatus from, LoadStatus to) => (from,to) switch {
 (LoadStatus.Draft,LoadStatus.Planned or LoadStatus.Cancelled) => true,
 (LoadStatus.Planned,LoadStatus.Tendered or LoadStatus.Cancelled) => true,
 (LoadStatus.Tendered,LoadStatus.Accepted or LoadStatus.Cancelled) => true,
 (LoadStatus.Accepted,LoadStatus.Dispatched or LoadStatus.Cancelled) => true,
 (LoadStatus.Dispatched,LoadStatus.Completed) => true, _ => false };
 public static bool NeedsReferences(LoadStatus target) => target is LoadStatus.Planned or LoadStatus.Tendered or LoadStatus.Accepted or LoadStatus.Dispatched;
}
