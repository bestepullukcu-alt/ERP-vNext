namespace Diten.MdmService.Domain.ValueObjects;

public enum GskuLifecycleOperationKind
{
    Correction = 1,
    Retirement = 2
}

public sealed record GskuActiveLifecycleOperationBinding(
    GskuLifecycleOperationKind Kind,
    Guid OperationId,
    int BaseGskuVersion);
