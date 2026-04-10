using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.CommandHandlers;

public sealed class UpdateCompositionRequestHandler : IRequestHandler<UpdateCompositionCommand, bool>
{
    private readonly ICompositionRepository _repository;
    private readonly ICompositionVersionRepository _versionRepository;

    public UpdateCompositionRequestHandler(
        ICompositionRepository repository, 
        ICompositionVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<bool> Handle(UpdateCompositionCommand request, CancellationToken cancellationToken)
    {
        var composition = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (composition == null)
        {
            return false;
        }

        // Get current version from header link
        CompositionVersion? currentVersion = null;
        if (composition.CurrentVersionId.HasValue)
        {
            currentVersion = await _versionRepository.GetByIdAsync(composition.CurrentVersionId.Value, cancellationToken);
        }

        // If no version found (should not happen), or current version is Active/Superseded/Obsolete -> Create NEW version
        if (currentVersion == null || currentVersion.Status != CompositionVersionStatus.Draft)
        {
            var nextVersionNo = await _versionRepository.GetNextVersionNoAsync(composition.Id, cancellationToken);
            var newVersion = new CompositionVersion
            {
                CompositionId = composition.Id,
                VersionNo = nextVersionNo,
                Status = CompositionVersionStatus.Draft,
                IsCurrent = false, // Stays false until activated if an active version exists, or becomes current if it's the only one
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
                }).OrderBy(c => c.Sequence).ToList()
            };

            // If there's no current version at all, this becomes current
            if (composition.CurrentVersionId == null)
            {
                newVersion.IsCurrent = true;
            }

            var createdVersion = await _versionRepository.CreateAsync(newVersion, cancellationToken);
            
            // If it became current, update header
            if (newVersion.IsCurrent)
            {
                composition.CurrentVersionId = createdVersion.Id;
            }
        }
        else
        {
            // Current version is Draft, update it directly
            currentVersion.DosageFormId = request.DosageFormId;
            currentVersion.StrengthValue = request.StrengthValue;
            currentVersion.StrengthUnitId = request.StrengthUnitId;
            currentVersion.TechnicalFillAmount = request.TechnicalFillAmount;
            currentVersion.TechnicalFillUnitId = request.TechnicalFillUnitId;
            currentVersion.Components = request.Components.Select(c => new CompositionComponent
            {
                Sequence = c.Sequence,
                ComponentId = c.ComponentId,
                ComponentName = c.ComponentName,
                ComponentType = c.ComponentType,
                Quantity = c.Quantity,
                UnitId = c.UnitId
            }).OrderBy(c => c.Sequence).ToList();

            await _versionRepository.UpdateAsync(currentVersion, cancellationToken);
        }

        // Update Header Name/Code if changed
        composition.FormulationCode = request.FormulationCode;
        composition.Name = request.Name;
        await _repository.UpdateAsync(composition, cancellationToken);

        return true;
    }
}
