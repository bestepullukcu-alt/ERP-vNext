using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.CommandHandlers;

public sealed class ChangeCompositionLifecycleRequestHandler : IRequestHandler<ChangeCompositionLifecycleCommand, bool>
{
    private readonly ICompositionRepository _repository;

    public ChangeCompositionLifecycleRequestHandler(ICompositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ChangeCompositionLifecycleCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        // Add lifecycle validation logic if needed, but for now we just update
        existing.LifecycleStateId = request.TargetStateId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
