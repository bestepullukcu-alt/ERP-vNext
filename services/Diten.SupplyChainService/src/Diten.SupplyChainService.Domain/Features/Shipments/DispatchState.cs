namespace Diten.SupplyChainService.Domain.Features.Shipments;
/// <summary>MOD-0183 §23.4: state of a stored dispatch attempt. The wire shows an open one as <c>dispatchState</c>.</summary>
public enum DispatchState { InProgress, CompensationFailed, Compensated, Completed }
