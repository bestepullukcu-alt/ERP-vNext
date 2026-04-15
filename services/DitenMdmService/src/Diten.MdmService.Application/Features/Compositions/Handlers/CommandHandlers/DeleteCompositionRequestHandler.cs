using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Enums;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.CommandHandlers;

public sealed class DeleteCompositionRequestHandler : IRequestHandler<DeleteCompositionCommand, bool>
{
    private readonly ICompositionRepository _repository;
    private readonly IItemLookupRepository _lookupRepository;

    public DeleteCompositionRequestHandler(
        ICompositionRepository repository,
        IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _lookupRepository = lookupRepository;
    }

    public async Task<bool> Handle(DeleteCompositionCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        // Use enum instead of magic string — see code-style.md § Magic String Yasağı
        if (existing.LifecycleState != CompositionLifecycleState.Draft)
        {
            throw new Exception("Only DRAFT formulations can be deleted.");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);
        return true;
    }
}
