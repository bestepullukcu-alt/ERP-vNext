using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.ReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 (CT decision A): outside Development, a start without the item-list provider's configuration
/// says so ONCE — every item create / update will answer 503 until it is configured. It never stops the start (the
/// GSKU / market clients' rule: fail-closed at the call). Development stays quiet.
/// </summary>
public sealed class ItemReferenceProviderStartupWarning(
    IHostEnvironment environment,
    IOptions<VerifiedGskuResolverOptions> options,
    ILogger<ItemReferenceProviderStartupWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !PlatformItemReferenceResolverClient.IsConfigured(options.Value))
            logger.LogWarning(ItemReferenceProvider.NotConfiguredWarning);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
