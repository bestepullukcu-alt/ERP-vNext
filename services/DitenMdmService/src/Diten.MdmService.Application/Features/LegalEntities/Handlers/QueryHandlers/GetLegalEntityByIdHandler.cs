using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.LegalEntities.Queries;

namespace Diten.MdmService.Application.Features.LegalEntities.Handlers.QueryHandlers;

public sealed class GetLegalEntityByIdHandler : IRequestHandler<GetLegalEntityByIdQuery, LegalEntity?>
{
    private readonly ILegalEntityRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetLegalEntityByIdHandler> _logger;

    public GetLegalEntityByIdHandler(
        ILegalEntityRepository repository,
        ITenantContext tenantContext,
        ILogger<GetLegalEntityByIdHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<LegalEntity?> Handle(
        GetLegalEntityByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.Id, _tenantContext.TenantId, cancellationToken);
    }
}
