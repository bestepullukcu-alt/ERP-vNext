using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class CreateCountryHandler : IRequestHandler<CreateCountryCommand, Country>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateCountryHandler> _logger;

    public CreateCountryHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<CreateCountryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Country> Handle(
        CreateCountryCommand request,
        CancellationToken cancellationToken)
    {
        var country = new Country
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NativeName = request.NativeName,
            Iso2Code = request.Iso2Code.ToUpperInvariant(),
            Iso3Code = request.Iso3Code.ToUpperInvariant(),
            NumericCode = request.NumericCode,
            PhoneCode = request.PhoneCode,
            CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
            CurrencyName = request.CurrencyName,
            CurrencySymbol = request.CurrencySymbol,
            Region = request.Region,
            SubRegion = request.SubRegion,
            Capital = request.Capital,
            FlagEmoji = request.FlagEmoji,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsActive = request.IsActive
        };

        return await _repository.CreateAsync(country, cancellationToken);
    }
}