using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class VerifiedMarketOperationalProvisioningRunner
{
    private readonly IBusinessReferenceDataVerifiedMarketOperationalEligibility _eligibility; private readonly IBusinessReferenceDataCatalogLoaderService _loader; private readonly IBusinessReferenceDataStewardshipRepository _repository; private readonly ITenantContext _tenant;
    public VerifiedMarketOperationalProvisioningRunner(IBusinessReferenceDataVerifiedMarketOperationalEligibility eligibility, IBusinessReferenceDataCatalogLoaderService loader, IBusinessReferenceDataStewardshipRepository repository, ITenantContext tenant) => (_eligibility, _loader, _repository, _tenant) = (eligibility, loader, repository, tenant);
    public async Task RunAsync(CancellationToken ct = default)
    {
        var decision = await _eligibility.EvaluateAsync(ct); if (!decision.IsEligible || decision.Facts is null || decision.Authorization is null || !_eligibility.IsAuthorized(decision.Authorization, decision.Facts)) throw new InvalidOperationException(decision.ReasonCode);
        var summary = await _loader.LoadVerifiedMarketCatalogFromFileAsync(decision.Facts.CatalogPath, decision.Facts.ActorId, decision.Facts.IdempotencyNamespace, decision.Authorization, decision.Facts, ct); if (summary.BlockedConflicts.Count > 0) throw new InvalidOperationException("REFERENCE_CONTRACT_MISMATCH");
        using (TenantScope.Begin(_tenant, decision.Facts.ReferenceTenantId)) if (await _repository.GetVerifiedPublicationAsync(VerifiedMarketCatalogContract.SetCode, decision.Facts.CatalogVersion, decision.Facts.CatalogFingerprint, ct) is null) throw new InvalidOperationException("REFERENCE_PUBLICATION_NOT_VERIFIED");
    }
}

public static class VerifiedMarketOperationalCommandLine
{
    public const string RunArgument = "--run-verified-market-provisioning";

    public static bool IsRequested(IEnumerable<string> arguments) =>
        arguments.Any(argument => string.Equals(argument, RunArgument, StringComparison.Ordinal));

    public static void EnsureDevelopment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED");
        }
    }
}
