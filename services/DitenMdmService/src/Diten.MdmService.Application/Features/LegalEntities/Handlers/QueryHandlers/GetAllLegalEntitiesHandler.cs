using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Queries;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.QueryHandlers;

public sealed class GetAllLegalEntitiesHandler : IRequestHandler<GetAllLegalEntitiesQuery, IEnumerable<LegalEntity>>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAllLegalEntitiesHandler> _logger;

    public GetAllLegalEntitiesHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<GetAllLegalEntitiesHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IEnumerable<LegalEntity>> Handle(
        GetAllLegalEntitiesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
