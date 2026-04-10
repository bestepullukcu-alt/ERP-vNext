using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Compositions.Handlers.QueryHandlers;

public sealed class GetCompositionVersionHistoryQueryHandler : IRequestHandler<GetCompositionVersionHistoryQuery, IReadOnlyList<CompositionVersionSummaryDto>>
{
    private readonly ICompositionVersionRepository _versionRepository;

    public GetCompositionVersionHistoryQueryHandler(ICompositionVersionRepository versionRepository)
    {
        _versionRepository = versionRepository;
    }

    public async Task<IReadOnlyList<CompositionVersionSummaryDto>> Handle(GetCompositionVersionHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _versionRepository.GetByCompositionIdAsync(request.CompositionId, cancellationToken);
        
        return history.Select(v => new CompositionVersionSummaryDto
        {
            Id = v.Id,
            VersionNo = v.VersionNo,
            Status = v.Status,
            IsCurrent = v.IsCurrent,
            CreatedAt = v.CreatedAt
        }).ToList();
    }
}
