namespace Diten.MdmService.Domain.ValueObjects;

public enum LskuLifecycleOperationKind { Retirement = 1 }

public sealed record LskuActiveLifecycleOperationBinding(
    LskuLifecycleOperationKind Kind, Guid OperationId, int BaseLskuVersion);
