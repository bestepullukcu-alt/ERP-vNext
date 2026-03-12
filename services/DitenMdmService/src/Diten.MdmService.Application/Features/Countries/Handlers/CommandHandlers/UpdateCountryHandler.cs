using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
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
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        var name = (request.Name ?? string.Empty).Trim();
        var iso2 = (request.Iso2Code ?? string.Empty).Trim().ToUpperInvariant();
        var iso3 = (request.Iso3Code ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Country.Validation.NameRequired", nameof(request.Name));
        }
        if (iso2.Length != 2)
        {
            throw new ArgumentException("Country.Validation.Iso2Invalid", nameof(request.Iso2Code));
        }
        if (iso3.Length != 3)
        {
            throw new ArgumentException("Country.Validation.Iso3Invalid", nameof(request.Iso3Code));
        }

        existing.Name = name;
        existing.Iso2Code = iso2;
        existing.Iso3Code = iso3;
        existing.PhoneCode = request.PhoneCode;
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);
        _logger.LogInformation(
            "Country updated. Id={Id} TenantId={TenantId} Updated={Updated}",
            existing.Id, _tenantContext.TenantId, updated);

        return updated;
    }
}

