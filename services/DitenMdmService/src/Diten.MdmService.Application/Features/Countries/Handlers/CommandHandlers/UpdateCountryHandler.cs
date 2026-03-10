using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class UpdateCountryHandler : IRequestHandler<UpdateCountryCommand, bool>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateCountryHandler> _logger;

    public UpdateCountryHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<UpdateCountryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<bool> Handle(
        UpdateCountryCommand request,
        CancellationToken cancellationToken)
    {
        var existingCountry = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existingCountry == null)
            return false;

        existingCountry.Name = request.Name;
        existingCountry.NativeName = request.NativeName;
        existingCountry.Iso2Code = request.Iso2Code.ToUpperInvariant();
        existingCountry.Iso3Code = request.Iso3Code.ToUpperInvariant();
        existingCountry.NumericCode = request.NumericCode;
        existingCountry.PhoneCode = request.PhoneCode;
        existingCountry.CurrencyCode = request.CurrencyCode?.ToUpperInvariant();
        existingCountry.CurrencyName = request.CurrencyName;
        existingCountry.CurrencySymbol = request.CurrencySymbol;
        existingCountry.Region = request.Region;
        existingCountry.SubRegion = request.SubRegion;
        existingCountry.Capital = request.Capital;
        existingCountry.FlagEmoji = request.FlagEmoji;
        existingCountry.Latitude = request.Latitude;
        existingCountry.Longitude = request.Longitude;
        existingCountry.IsActive = request.IsActive;

        return await _repository.UpdateAsync(existingCountry, cancellationToken);
    }
}