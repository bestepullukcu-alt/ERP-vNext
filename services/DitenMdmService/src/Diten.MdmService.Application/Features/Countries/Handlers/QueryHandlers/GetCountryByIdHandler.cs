using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.MdmService.Application.Features.Countries.Queries;

namespace Diten.MdmService.Application.Features.Countries.Handlers.QueryHandlers;

public sealed class GetCountryByIdHandler : IRequestHandler<GetCountryByIdQuery, Country?>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetCountryByIdHandler> _logger;

    public GetCountryByIdHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<GetCountryByIdHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Country?> Handle(
        GetCountryByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.Id, cancellationToken);
    }
}

