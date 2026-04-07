using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemLookups;

public sealed class LookupDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record GetItemTypesQuery : IRequest<IReadOnlyList<LookupDto>>;
public sealed record GetTrackingPoliciesQuery : IRequest<IReadOnlyList<LookupDto>>;
public sealed record GetLifecycleStatesQuery : IRequest<IReadOnlyList<LookupDto>>;
public sealed record GetUnitOfMeasuresQuery : IRequest<IReadOnlyList<LookupDto>>;

public sealed class GetItemTypesQueryHandler : IRequestHandler<GetItemTypesQuery, IReadOnlyList<LookupDto>>
{
    private readonly IItemLookupRepository _lookupRepository;

    public GetItemTypesQueryHandler(IItemLookupRepository lookupRepository)
    {
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetItemTypesQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        return (await _lookupRepository.GetItemTypesAsync(cancellationToken)).Select(ItemLookupMapping.ToDto).ToList();
    }
}

public sealed class GetTrackingPoliciesQueryHandler : IRequestHandler<GetTrackingPoliciesQuery, IReadOnlyList<LookupDto>>
{
    private readonly IItemLookupRepository _lookupRepository;

    public GetTrackingPoliciesQueryHandler(IItemLookupRepository lookupRepository)
    {
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetTrackingPoliciesQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        return (await _lookupRepository.GetTrackingPoliciesAsync(cancellationToken)).Select(ItemLookupMapping.ToDto).ToList();
    }
}

public sealed class GetLifecycleStatesQueryHandler : IRequestHandler<GetLifecycleStatesQuery, IReadOnlyList<LookupDto>>
{
    private readonly IItemLookupRepository _lookupRepository;

    public GetLifecycleStatesQueryHandler(IItemLookupRepository lookupRepository)
    {
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetLifecycleStatesQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        return (await _lookupRepository.GetLifecycleStatesAsync(cancellationToken)).Select(ItemLookupMapping.ToDto).ToList();
    }
}

public sealed class GetUnitOfMeasuresQueryHandler : IRequestHandler<GetUnitOfMeasuresQuery, IReadOnlyList<LookupDto>>
{
    private readonly IItemLookupRepository _lookupRepository;

    public GetUnitOfMeasuresQueryHandler(IItemLookupRepository lookupRepository)
    {
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetUnitOfMeasuresQuery request, CancellationToken cancellationToken)
    {
        await _lookupRepository.EnsureSeedDataAsync(cancellationToken);
        return (await _lookupRepository.GetUnitOfMeasuresAsync(cancellationToken)).Select(ItemLookupMapping.ToDto).ToList();
    }
}

internal static class ItemLookupMapping
{
    public static LookupDto ToDto(LookupEntityBase entity)
    {
        return new LookupDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive
        };
    }
}
