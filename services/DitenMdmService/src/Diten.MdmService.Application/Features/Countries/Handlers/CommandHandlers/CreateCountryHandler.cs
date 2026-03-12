using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.MdmService.Application.Features.Countries.Commands;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class CreateCountryHandler : IRequestHandler<CreateCountryCommand, CreateCountryResult>
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

    public async Task<CreateCountryResult> Handle(
        CreateCountryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        if (await _repository.ExistsByIso2Async(iso2, cancellationToken))
        {
            throw new InvalidOperationException("Country.Error.Iso2AlreadyExists");
        }

        var entity = new Country
        {
            Name = name,
            Iso2Code = iso2,
            Iso3Code = iso3,
            PhoneCode = request.PhoneCode,
            TenantId = _tenantContext.TenantId,
            IsActive = true
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Country created. Id={Id} TenantId={TenantId} Iso2={Iso2}",
            created.Id, created.TenantId, created.Iso2Code);

        return new CreateCountryResult(
            created.Id,
            created.Name,
            created.Iso2Code,
            created.Iso3Code,
            created.TenantId,
            created.CreatedAt);
    }
}

