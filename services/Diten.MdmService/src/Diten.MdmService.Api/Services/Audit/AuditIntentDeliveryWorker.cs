using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Audit;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.Audit;

public sealed class AuditIntentDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditIntentDeliveryWorkerOptions _options;
    private readonly AuthTrustedSourceAuditServiceIdentityProviderOptions _identityOptions;
    private readonly TrustedSourceAuditIntentClientOptions _clientOptions;

    public AuditIntentDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditIntentDeliveryWorkerOptions> options,
        IOptions<AuthTrustedSourceAuditServiceIdentityProviderOptions> identityOptions,
        IOptions<TrustedSourceAuditIntentClientOptions> clientOptions)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _identityOptions = identityOptions.Value;
        _clientOptions = clientOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        ValidateEnabledConfiguration();
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        Guid? cursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuditIntentTenantPartitionPage page;
            await using (var discoveryScope = _scopeFactory.CreateAsyncScope())
            {
                page = await discoveryScope.ServiceProvider
                    .GetRequiredService<IAuditIntentTenantPartitionDiscovery>()
                    .DiscoverAsync(cursor, _options.TenantPageSize, cancellationToken);
            }

            foreach (var tenantId in page.TenantIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var tenantScope = _scopeFactory.CreateAsyncScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
                tenantContext.SetTenant(tenantId);
                await tenantScope.ServiceProvider.GetRequiredService<AuditIntentDeliveryProcessor>()
                    .ProcessTenantAsync(
                        tenantId,
                        _options.BatchSize,
                        _options.LeaseOwner,
                        TimeSpan.FromSeconds(_options.LeaseSeconds),
                        TimeSpan.FromSeconds(_options.RetryDelaySeconds),
                        _options.MaximumAttempts,
                        cancellationToken);
            }

            cursor = page.NextTenantId;
        } while (cursor.HasValue);
    }

    private void ValidateEnabledConfiguration()
    {
        if (_options.TenantPageSize is < 1 or > 100 || _options.BatchSize is < 1 or > 100
            || _options.LeaseSeconds is < 10 or > 900 || _options.RetryDelaySeconds is < 1 or > 3600
            || _options.MaximumAttempts is < 1 or > 20 || _options.PollIntervalSeconds is < 1 or > 3600
            || !IsBounded(_options.LeaseOwner, 128)
            )
        {
            throw new InvalidOperationException("AUDIT_INTENT_DELIVERY_WORKER_CONFIGURATION_INVALID");
        }

        try
        {
            AuthTrustedSourceAuditServiceIdentityProvider.EnsureValidConfiguration(_identityOptions);
            PlatformTrustedSourceAuditIntentClient.EnsureValidConfiguration(_clientOptions);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                          or TrustedSourceAuditServiceIdentityException)
        {
            throw new InvalidOperationException("AUDIT_INTENT_DELIVERY_WORKER_CONFIGURATION_INVALID", exception);
        }
    }

    private static bool IsBounded(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
}
