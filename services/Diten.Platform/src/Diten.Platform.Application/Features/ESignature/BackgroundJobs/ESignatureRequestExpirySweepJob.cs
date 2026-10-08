using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ESignature.BackgroundJobs;

public sealed record ESignatureRequestExpirySweepJobArgs(int MaxItemsPerTenant);

/// <summary>
/// MOD-0022 — recurring sweep: Waiting signature requests past their due date become Expired (pack §8 "Signature requests";
/// FU23 never automated this — audit gap 8). Expiry is tenant-scoped and a recurring job runs without a tenant, so it
/// iterates the active tenants and sends <see cref="ExpireSignatureRequestsCommand"/> inside each tenant's scope — the
/// <c>WorkflowEscalationSweepJob</c> pattern. A failure in one tenant is logged and does not stop the others.
/// </summary>
public sealed class ESignatureRequestExpirySweepJob : IBackgroundJobHandler<ESignatureRequestExpirySweepJobArgs>
{
    private const int DefaultMaxItemsPerTenant = 200;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ESignatureRequestExpirySweepJob> _logger;

    public ESignatureRequestExpirySweepJob(ITenantRegistryRepository tenantRegistry, ITenantContext tenantContext, IMediator mediator,
        ILogger<ESignatureRequestExpirySweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(ESignatureRequestExpirySweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);
        var maxItems = args.MaxItemsPerTenant <= 0 ? DefaultMaxItemsPerTenant : args.MaxItemsPerTenant;
        var expired = 0;
        var failedTenants = 0;

        foreach (var tenant in await _tenantRegistry.GetActiveTenantsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var result = await _mediator.Send(new ExpireSignatureRequestsCommand(maxItems), cancellationToken);
                    expired += result.IsSuccessful ? result.Data : 0;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedTenants++;
                _logger.LogError(ex, "E-signature request expiry failed for tenant {TenantId}.", tenant.Id);
            }
        }

        _logger.LogInformation("E-signature request expiry sweep: {Expired} expired, {FailedTenants} tenant(s) failed.", expired, failedTenants);
    }
}
