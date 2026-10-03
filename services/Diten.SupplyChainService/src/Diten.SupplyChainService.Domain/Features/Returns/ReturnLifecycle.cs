namespace Diten.SupplyChainService.Domain.Features.Returns;
public static class ReturnLifecycle
{
 public static bool Allows(ReturnStatus from, ReturnStatus to) => (from,to) switch {
 (ReturnStatus.Requested, ReturnStatus.Authorized or ReturnStatus.Rejected) => true,
 (ReturnStatus.Authorized, ReturnStatus.InTransit or ReturnStatus.Cancelled) => true,
 (ReturnStatus.InTransit, ReturnStatus.Received) => true,
 (ReturnStatus.Received, ReturnStatus.Dispositioned) => true,
 (ReturnStatus.Dispositioned, ReturnStatus.Closed) => true, _ => false };
 public static bool Releases(ReturnStatus status) => status is ReturnStatus.Rejected or ReturnStatus.Cancelled;
 public static bool Counts(ReturnStatus status) => Enum.IsDefined(status) && !Releases(status);
}
