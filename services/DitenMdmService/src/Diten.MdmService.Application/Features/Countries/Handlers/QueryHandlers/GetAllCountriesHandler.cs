using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.MdmService.Application.Features.Countries.Queries;

namespace Diten.MdmService.Application.Features.Countries.Handlers.QueryHandlers;

public sealed class GetAllCountriesHandler : IRequestHandler<GetAllCountriesQuery, IEnumerable<Country>>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAllCountriesHandler> _logger;

    public GetAllCountriesHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<GetAllCountriesHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IEnumerable<Country>> Handle(
        GetAllCountriesQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}

