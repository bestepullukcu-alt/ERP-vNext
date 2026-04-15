using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.CommandHandlers;

public sealed class CreateCompositionRequestHandler : IRequestHandler<CreateCompositionCommand, Guid>
{
    private readonly ICompositionRepository _repository;
    private readonly ICompositionVersionRepository _versionRepository;

    public CreateCompositionRequestHandler(
        ICompositionRepository repository,
        ICompositionVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<Guid> Handle(CreateCompositionCommand request, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsByCodeAsync(request.FormulationCode, ct: cancellationToken))
        {
            throw new Exception($"Composition with code {request.FormulationCode} already exists.");
        }

        // 1. Create Composition Header
        var composition = new Composition
        {
            FormulationCode = request.FormulationCode,
            Name = request.Name,
            LifecycleState = CompositionLifecycleState.Draft
        };

        var createdComposition = await _repository.CreateAsync(composition, cancellationToken);

        // 2. Create First Version (v1, Draft)
        var version = new CompositionVersion
        {
            CompositionId = createdComposition.Id,
            VersionNo = 1,
            Status = CompositionVersionStatus.Draft,
            IsCurrent = true, // First version is current by default
            DosageFormId = request.DosageFormId,
            StrengthValue = request.StrengthValue,
            StrengthUnitId = request.StrengthUnitId,
            TechnicalFillAmount = request.TechnicalFillAmount,
            TechnicalFillUnitId = request.TechnicalFillUnitId,
            Components = request.Components.Select(c => new CompositionComponent
            {
                Sequence = c.Sequence,
                ComponentId = c.ComponentId,
                ComponentName = c.ComponentName,
                ComponentType = c.ComponentType,
                Quantity = c.Quantity,
                UnitId = c.UnitId
            }).ToList()
        };

        var createdVersion = await _versionRepository.CreateAsync(version, cancellationToken);

        // 3. Link Header to Version
        createdComposition.CurrentVersionId = createdVersion.Id;
        await _repository.UpdateAsync(createdComposition, cancellationToken);

        return createdComposition.Id;
    }
}
