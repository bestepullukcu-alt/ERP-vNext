using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.QueryHandlers;

public sealed class GetCompositionByIdQueryHandler : IRequestHandler<GetCompositionByIdQuery, CompositionDetailDto?>
{
    private readonly ICompositionRepository _repository;
    private readonly ICompositionVersionRepository _versionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetCompositionByIdQueryHandler(
        ICompositionRepository repository,
        ICompositionVersionRepository versionRepository,
        IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<CompositionDetailDto?> Handle(GetCompositionByIdQuery request, CancellationToken cancellationToken)
    {
        var composition = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (composition == null)
        {
            return null;
        }

        // Determine which version to fetch
        Guid? versionIdToFetch = request.VersionId ?? composition.CurrentVersionId;
        CompositionVersion? targetVersion = null;
        
        if (versionIdToFetch.HasValue)
        {
            targetVersion = await _versionRepository.GetByIdAsync(versionIdToFetch.Value, cancellationToken);
        }

        // Fetch history
        var history = await _versionRepository.GetByCompositionIdAsync(composition.Id, cancellationToken);

        // Prepare version DTO if found
        CompositionVersionDto? versionDto = null;
        if (targetVersion != null)
        {
            var dosageForm = await _lookupRepository.GetDosageFormByIdAsync(targetVersion.DosageFormId, cancellationToken);
            var strengthUnit = await _lookupRepository.GetUnitOfMeasureByIdAsync(targetVersion.StrengthUnitId, cancellationToken);
            var fillUnit = targetVersion.TechnicalFillUnitId.HasValue
                ? await _lookupRepository.GetUnitOfMeasureByIdAsync(targetVersion.TechnicalFillUnitId.Value, cancellationToken)
                : null;

            versionDto = CompositionMapping.ToVersionDto(
                targetVersion,
                dosageForm?.Name ?? "Unknown",
                strengthUnit?.Name ?? string.Empty,
                fillUnit?.Name ?? string.Empty);
        }

        return new CompositionDetailDto
        {
            Id = composition.Id,
            FormulationCode = composition.FormulationCode,
            Name = composition.Name,
            LifecycleState = composition.LifecycleState,
            CurrentVersionId = composition.CurrentVersionId,
            CurrentVersion = versionDto,
            VersionHistory = history.Select(v => new CompositionVersionSummaryDto
            {
                Id = v.Id,
                VersionNo = v.VersionNo,
                Status = v.Status,
                IsCurrent = v.IsCurrent,
                CreatedAt = v.CreatedAt
            }).ToList()
        };
    }
}
