using MediatR;

namespace Diten.MdmService.Application.Features.Compositions;

public sealed class CreateCompositionCommand : CompositionUpsertRequestBase, IRequest<Guid> { }

public sealed class UpdateCompositionCommand : CompositionUpsertRequestBase, IRequest<bool>
{
    public Guid Id { get; set; }
}

public sealed class CreateNewCompositionVersionCommand : IRequest<Guid>
{
    public Guid CompositionId { get; set; }
}

public sealed class ActivateCompositionVersionCommand : IRequest<bool>
{
    public Guid VersionId { get; set; }
}

public sealed record DeleteCompositionCommand(Guid Id) : IRequest<bool>;

public sealed record ChangeCompositionLifecycleCommand(Guid Id, string TargetState, string? Reason = null) : IRequest<bool>;
