using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.CommandHandlers;

public sealed class ActivateCompositionVersionRequestHandler : IRequestHandler<ActivateCompositionVersionCommand, bool>
{
    private readonly ICompositionRepository _repository;
    private readonly ICompositionVersionRepository _versionRepository;

    public ActivateCompositionVersionRequestHandler(
        ICompositionRepository repository,
        ICompositionVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<bool> Handle(ActivateCompositionVersionCommand request, CancellationToken cancellationToken)
    {
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken);
        if (version == null || version.Status != CompositionVersionStatus.Draft)
        {
            return false;
        }

        var composition = await _repository.GetByIdAsync(version.CompositionId, cancellationToken);
        if (composition == null)
        {
            return false;
        }

        // 1. Mark other Active versions as Superseded
        await _versionRepository.MarkOtherVersionsAsSupersededAsync(composition.Id, version.Id, cancellationToken);

        // 2. Activate this version
        version.Status = CompositionVersionStatus.Active;
        version.IsCurrent = true;
        version.EffectiveFrom = DateTimeOffset.UtcNow;
        await _versionRepository.UpdateAsync(version, cancellationToken);

        // 3. Update Composition Header
        composition.CurrentVersionId = version.Id;
        composition.LifecycleState = CompositionLifecycleState.Active;
        await _repository.UpdateAsync(composition, cancellationToken);

        return true;
    }
}
