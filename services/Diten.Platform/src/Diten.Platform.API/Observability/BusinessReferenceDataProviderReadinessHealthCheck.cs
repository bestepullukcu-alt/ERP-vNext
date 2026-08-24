using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Observability;

public sealed class BusinessReferenceDataProviderReadinessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<VerifiedGskuOperationalProvisioningOptions> _options;

    public BusinessReferenceDataProviderReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        IOptions<VerifiedGskuOperationalProvisioningOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataStewardshipRepository>();
            var referenceTenantId = repository.GetRequiredReferenceTenantId();
            var options = _options.Value;
            VerifiedStateFacts? stateFacts = null;
            if (options.Enabled)
            {
                var eligibility = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
                var decision = await eligibility.EvaluateAsync(cancellationToken);
                if (!decision.IsEligible || decision.Facts is null || decision.Authorization is null)
                {
                    return HealthCheckResult.Unhealthy("Verified GSKU operational pilot is not eligible.");
                }

                stateFacts = new VerifiedStateFacts(
                    decision.Facts.CatalogVersion,
                    decision.Facts.CatalogFingerprint,
                    decision.Facts.ReferenceTenantId,
                    decision.Facts.ConsumerTenantId,
                    decision.Facts.RequiredSetCodes);
            }

            if (options.EnumerationEnabled)
            {
                var eligibility = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataVerifiedGskuOperationalEligibility>();
                var decision = await eligibility.EvaluateEnumerationAsync(cancellationToken);
                if (!decision.IsEligible || decision.Facts is null)
                {
                    return HealthCheckResult.Unhealthy("Verified GSKU enumeration pilot is not eligible.");
                }

                var enumerationFacts = new VerifiedStateFacts(
                    decision.Facts.CatalogVersion,
                    decision.Facts.CatalogFingerprint,
                    decision.Facts.ReferenceTenantId,
                    decision.Facts.ConsumerTenantId,
                    decision.Facts.RequiredSetCodes);
                if (stateFacts is not null && !Matches(stateFacts, enumerationFacts))
                {
                    return HealthCheckResult.Unhealthy("Verified GSKU operational configuration is inconsistent.");
                }

                stateFacts = enumerationFacts;
            }

            // The one-shot provisioning runner is intentionally default-disabled. Its disabled state
            // is not evidence that an already verified publication is unavailable; read that durable
            // state instead, while retaining the same locked version/fingerprint and tenant checks.
            var facts = stateFacts ?? CreateDisabledProvisioningFacts(options, referenceTenantId);
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            using (TenantScope.Begin(tenantContext, referenceTenantId))
            {
                foreach (var setCode in facts.RequiredSetCodes)
                {
                    if (await repository.GetVerifiedPublicationAsync(
                            setCode,
                            facts.CatalogVersion,
                            facts.CatalogFingerprint,
                            cancellationToken) is null
                        || await repository.GetActiveTenantAssignmentAsync(
                            facts.ConsumerTenantId,
                            setCode,
                            cancellationToken) is null)
                    {
                        return HealthCheckResult.Unhealthy("Verified GSKU operational state is incomplete.");
                    }
                }
            }

            return HealthCheckResult.Healthy("Verified GSKU operational state is ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Business reference data provider configuration or state is invalid.");
        }
    }

    private sealed record VerifiedStateFacts(
        string CatalogVersion,
        string CatalogFingerprint,
        Guid ReferenceTenantId,
        Guid ConsumerTenantId,
        IReadOnlyList<string> RequiredSetCodes);

    private static VerifiedStateFacts CreateDisabledProvisioningFacts(
        VerifiedGskuOperationalProvisioningOptions options,
        Guid referenceTenantId)
    {
        if (referenceTenantId == Guid.Empty
            || !options.ConsumerTenantId.HasValue
            || options.ConsumerTenantId.Value == Guid.Empty
            || options.ConsumerTenantId.Value == referenceTenantId
            || !string.Equals(
                options.ExpectedCatalogVersion,
                VerifiedGskuOperationalProvisioningOptions.LockedCatalogVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                options.ExpectedCatalogFingerprint,
                VerifiedGskuOperationalProvisioningOptions.LockedCatalogFingerprint,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("VERIFIED_GSKU_DISABLED_PROVISIONING_CONFIGURATION_INVALID");
        }

        return new VerifiedStateFacts(
            options.ExpectedCatalogVersion,
            options.ExpectedCatalogFingerprint,
            referenceTenantId,
            options.ConsumerTenantId.Value,
            ["pack-applicability", "uom"]);
    }

    private static bool Matches(VerifiedStateFacts left, VerifiedStateFacts right) =>
        string.Equals(left.CatalogVersion, right.CatalogVersion, StringComparison.Ordinal)
        && string.Equals(left.CatalogFingerprint, right.CatalogFingerprint, StringComparison.OrdinalIgnoreCase)
        && left.ReferenceTenantId == right.ReferenceTenantId
        && left.ConsumerTenantId == right.ConsumerTenantId
        && left.RequiredSetCodes.SequenceEqual(right.RequiredSetCodes, StringComparer.Ordinal);
}
