using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class BusinessReferenceDataCatalogLoadWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BusinessReferenceDataCatalogLoadWorker> _logger;
    private readonly BusinessReferenceDataCatalogLoadOptions _options;

    public BusinessReferenceDataCatalogLoadWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BusinessReferenceDataCatalogLoadOptions> options,
        ILogger<BusinessReferenceDataCatalogLoadWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("BusinessReferenceData catalog load worker disabled.");
            return;
        }

        if (!Guid.TryParse(_options.TenantId, out var tenantId) || tenantId == Guid.Empty)
        {
            _logger.LogError("BusinessReferenceData catalog load failed: invalid tenant id '{TenantId}'.", _options.TenantId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.CatalogPath))
        {
            _logger.LogError("BusinessReferenceData catalog load failed: empty catalog path.");
            return;
        }

        if (_options.RequiredSetCodes is null || _options.RequiredSetCodes.Count == 0)
        {
            _logger.LogError("BusinessReferenceData catalog load failed: no required set codes configured; refusing to mutate without a required set definition.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var loader = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataCatalogLoaderService>();
            var summary = await loader.LoadFromFileAsync(
                _options.CatalogPath,
                tenantId,
                string.IsNullOrWhiteSpace(_options.ActorId) ? "business-reference-data-seed-loader" : _options.ActorId.Trim(),
                _options.RequiredSetCodes,
                stoppingToken);

            _logger.LogInformation(
                "BusinessReferenceData catalog load completed. sets_processed={SetsProcessed}, sets_inserted={SetsInserted}, sets_updated={SetsUpdated}, sets_loaded={SetsLoaded}, sets_already_loaded={SetsAlreadyLoaded}, values_inserted={ValuesInserted}, values_updated={ValuesUpdated}, values_unchanged={ValuesUnchanged}, blocked_conflicts={BlockedConflictsCount}",
                summary.SetsProcessed,
                summary.SetsInserted,
                summary.SetsUpdated,
                summary.SetsLoaded,
                summary.SetsAlreadyLoaded,
                summary.ValuesInserted,
                summary.ValuesUpdated,
                summary.ValuesUnchanged,
                summary.BlockedConflicts.Count);

            foreach (var lookup in summary.LookupResults)
            {
                _logger.LogInformation(
                    "BusinessReferenceData catalog lookup verification. set_code={SetCode}, sample_value_code={SampleValueCode}, set_found={SetFound}, value_found={ValueFound}, status={Status}",
                    lookup.SetCode,
                    lookup.SampleValueCode,
                    lookup.SetFound,
                    lookup.ValueFound,
                    lookup.Status);
            }

            if (summary.BlockedConflicts.Count > 0)
            {
                foreach (var conflict in summary.BlockedConflicts)
                {
                    _logger.LogWarning("BusinessReferenceData catalog load conflict: {Conflict}", conflict);
                }

                if (_options.FailOnBlockedConflicts)
                {
                    throw new InvalidOperationException("business_reference_data_catalog_load_blocked_conflicts");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("BusinessReferenceData catalog load worker cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BusinessReferenceData catalog load worker failed.");
        }
    }
}
