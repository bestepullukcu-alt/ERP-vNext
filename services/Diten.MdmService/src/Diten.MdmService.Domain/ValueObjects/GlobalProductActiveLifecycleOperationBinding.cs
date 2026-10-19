using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed record GlobalProductActiveLifecycleOperationBinding(
    GlobalProductLifecycleOperationKind Kind,
    Guid OperationId,
    int BaseProductVersion);
