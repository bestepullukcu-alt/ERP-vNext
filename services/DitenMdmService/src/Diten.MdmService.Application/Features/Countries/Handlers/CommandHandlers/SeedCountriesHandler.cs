using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Application.Features.Countries.Commands;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Application.Features.Countries.Handlers.CommandHandlers;

public sealed class SeedCountriesHandler : IRequestHandler<SeedCountriesCommand, SeedCountriesResult>
{
    private readonly ICountryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SeedCountriesHandler> _logger;

    public SeedCountriesHandler(
        ICountryRepository repository,
        ITenantContext tenantContext,
        ILogger<SeedCountriesHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    private sealed record SeedCountryRow(
        string? Name,
        string? Iso2Code,
        string? Iso3Code,
        string? PhoneCode);

    public async Task<SeedCountriesResult> Handle(
        SeedCountriesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "countries.json");
        if (!File.Exists(seedPath))
        {
            _logger.LogError("Seed file not found at path: {Path}", seedPath);
            throw new FileNotFoundException("Seed file not found.", seedPath);
        }

        var json = await File.ReadAllTextAsync(seedPath, cancellationToken);
        var rows = JsonSerializer.Deserialize<List<SeedCountryRow>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var seedCountries = new List<Country>();

        foreach (var r in rows)
        {
            var name = (r.Name ?? string.Empty).Trim();
            var iso2 = (r.Iso2Code ?? string.Empty).Trim().ToUpperInvariant();
            var iso3 = (r.Iso3Code ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(name) || iso2.Length != 2 || iso3.Length != 3)
            {
                continue;
            }

            seedCountries.Add(new Country
            {
                Name = name,
                Iso2Code = iso2,
                Iso3Code = iso3,
                PhoneCode = string.IsNullOrWhiteSpace(r.PhoneCode) ? null : r.PhoneCode.Trim(),
                IsActive = true
            });
        }

        var (insertedCount, modifiedCount) =
            await _repository.UpsertSeedAsync(seedCountries, cancellationToken);

        _logger.LogInformation(
            "Seed countries completed. Inserted={Inserted} Updated={Updated} Total={Total} TenantId={TenantId}",
            insertedCount, modifiedCount, seedCountries.Count, _tenantContext.TenantId);

        return new SeedCountriesResult(insertedCount, modifiedCount, seedCountries.Count);
    }
}

