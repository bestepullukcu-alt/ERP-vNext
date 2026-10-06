namespace Diten.MdmService.Domain.ValueObjects;

// LSKU FIX2 item 8: IdentitySubmit fences a DRAFT while its submit may reach the engine — a draft cancel is refused
// atomically by the fence (cancel ↔ first start race), and the submit clears it when the LSKU becomes pending, or when
// the submit ends before anything was started.
public enum LskuLifecycleOperationKind { Retirement = 1, IdentitySubmit = 2 }

public sealed record LskuActiveLifecycleOperationBinding(
    LskuLifecycleOperationKind Kind, Guid OperationId, int BaseLskuVersion);
