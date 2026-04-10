using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.QueryHandlers;

public sealed class GetAllCompositionsQueryHandler : IRequestHandler<GetAllCompositionsQuery, IReadOnlyList<CompositionListItemDto>>
{
    private readonly ICompositionRepository _repository;
    private readonly ICompositionVersionRepository _versionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllCompositionsQueryHandler(
        ICompositionRepository repository,
        ICompositionVersionRepository versionRepository,
        IItemLookupRepository lookupRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<CompositionListItemDto>> Handle(GetAllCompositionsQuery request, CancellationToken cancellationToken)
    {
        var compositions = await _repository.GetAllAsync(cancellationToken);
        if (compositions.Count == 0)
        {
            return [];
        }

        var dosageForms = await _lookupRepository.GetDosageFormsAsync(cancellationToken);
        var units = await _lookupRepository.GetUnitOfMeasuresAsync(cancellationToken);

        var dosageFormMap = dosageForms.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
        var unitMap = units.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);

        var resultList = new List<CompositionListItemDto>();

        foreach (var composition in compositions)
        {
            CompositionVersion? currentVersion = null;
            if (composition.CurrentVersionId.HasValue)
            {
                currentVersion = await _versionRepository.GetByIdAsync(composition.CurrentVersionId.Value, cancellationToken);
            }

            resultList.Add(CompositionMapping.ToListDto(
                composition,
                currentVersion,
                currentVersion != null ? dosageFormMap.GetValueOrDefault(currentVersion.DosageFormId, "Unknown") : "-",
                currentVersion != null ? unitMap.GetValueOrDefault(currentVersion.StrengthUnitId, string.Empty) : string.Empty
            ));
        }

        return resultList;
    }
}
