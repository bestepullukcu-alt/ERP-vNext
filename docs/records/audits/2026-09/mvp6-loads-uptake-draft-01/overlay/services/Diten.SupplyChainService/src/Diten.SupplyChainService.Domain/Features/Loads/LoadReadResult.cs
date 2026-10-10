namespace Diten.SupplyChainService.Domain.Features.Loads;

public enum LoadRootState { Missing, ExplicitNull, InvalidStoredValue, PresentStoredUuid }

// Load is deserialized from a detached copy without the stored root field, so Load.CorrelationRoot is
// NOT authoritative here. Callers use RootState and LifecycleCorrelationId only (MOD-0185 D185-ROOT-ACQ-01).
public sealed record LoadReadResult(LoadPlan Load, LoadRootState RootState, Guid? LifecycleCorrelationId);
