using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class VerifiedGskuOperationalProvisioningRunner : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VerifiedGskuOperationalProvisioningRunner> _logger;
    private int _started;

    public VerifiedGskuOperationalProvisioningRunner(
        IServiceScopeFactory scopeFactory,
        ILogger<VerifiedGskuOperationalProvisioningRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var eligibility = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
        var decision = await eligibility.EvaluateAsync(cancellationToken);
        if (!decision.IsEligible || decision.Facts is null || decision.Authorization is null)
        {
            _logger.LogInformation("Verified GSKU operational reconciliation skipped with outcome {Outcome}.", "disabled_or_ineligible");
            return;
        }

        try
        {
            var facts = decision.Facts;
            var loader = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataCatalogLoaderService>();
            var summary = await loader.LoadVerifiedGskuCatalogFromFileAsync(
                facts.CatalogPath,
                facts.ActorId,
                facts.IdempotencyNamespace,
                facts.RequiredSetCodes,
                decision.Authorization,
                facts,
                cancellationToken);
            if (summary.BlockedConflicts.Count > 0)
            {
                throw new InvalidOperationException("REFERENCE_CONTRACT_MISMATCH");
            }

            var repository = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataStewardshipRepository>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            using (TenantScope.Begin(tenantContext, facts.ReferenceTenantId))
            {
                foreach (var setCode in facts.RequiredSetCodes)
                {
                    if (await repository.GetVerifiedPublicationAsync(
                            setCode,
                            facts.CatalogVersion,
                            facts.CatalogFingerprint,
                            cancellationToken) is null)
                    {
                        throw new InvalidOperationException("REFERENCE_PUBLICATION_NOT_VERIFIED");
                    }
                }
            }

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var assignmentResponse = await mediator.Send(
                new EnsureVerifiedGskuTenantAssignmentsCommand(
                    facts.ConsumerTenantId,
                    facts.ActorId,
                    facts.IdempotencyNamespace),
                cancellationToken);
            if (!assignmentResponse.IsSuccessful)
            {
                throw new InvalidOperationException(assignmentResponse.ReasonCode ?? "REFERENCE_ASSIGNMENT_CONFLICT");
            }

            using (TenantScope.Begin(tenantContext, facts.ReferenceTenantId))
            {
                foreach (var setCode in facts.RequiredSetCodes)
                {
                    if (await repository.GetActiveTenantAssignmentAsync(
                            facts.ConsumerTenantId,
                            setCode,
                            cancellationToken) is null)
                    {
                        throw new InvalidOperationException("REFERENCE_ASSIGNMENT_CONFLICT");
                    }
                }
            }

            _logger.LogInformation("Verified GSKU operational reconciliation completed with outcome {Outcome}.", "verified");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Verified GSKU operational reconciliation failed with classification {Classification}.",
                exception is InvalidOperationException ? "contract_or_state" : "provider_unavailable");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
