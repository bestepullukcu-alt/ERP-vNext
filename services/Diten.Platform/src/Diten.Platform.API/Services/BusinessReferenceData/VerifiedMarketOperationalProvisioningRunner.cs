using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public sealed class VerifiedMarketOperationalProvisioningRunner
{
    private readonly IBusinessReferenceDataVerifiedMarketOperationalEligibility _eligibility;
    private readonly IVerifiedMarketOperationalPreflight _preflight;
    private readonly IBusinessReferenceDataCatalogLoaderService _loader;
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly VerifiedMarketOperationalGovernanceAuditAdapter _auditAdapter;

    public VerifiedMarketOperationalProvisioningRunner(
        IBusinessReferenceDataVerifiedMarketOperationalEligibility eligibility,
        IVerifiedMarketOperationalPreflight preflight,
        IBusinessReferenceDataCatalogLoaderService loader,
        IBusinessReferenceDataStewardshipRepository repository,
        ITenantContext tenant,
        VerifiedMarketOperationalGovernanceAuditAdapter auditAdapter)
    {
        _eligibility = eligibility;
        _preflight = preflight;
        _loader = loader;
        _repository = repository;
        _tenant = tenant;
        _auditAdapter = auditAdapter;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var decision = await _eligibility.EvaluateAsync(ct);
        if (!decision.IsEligible
            || decision.Facts is null
            || decision.Authorization is null
            || !_eligibility.IsAuthorized(decision.Authorization, decision.Facts))
        {
            throw new InvalidOperationException(decision.ReasonCode);
        }

        _auditAdapter.Bind(decision.Facts);

        var preflight = await _preflight.VerifyBeforeWriteAsync(decision.Facts, ct);
        if (preflight.Disposition == VerifiedMarketOperationalTargetDisposition.ManualReconciliationRequired)
        {
            throw new InvalidOperationException(preflight.ReasonCode);
        }

        if (preflight.Disposition == VerifiedMarketOperationalTargetDisposition.Fresh)
        {
            try
            {
                var summary = await _loader.LoadVerifiedMarketCatalogFromFileAsync(
                    decision.Facts.CatalogPath,
                    decision.Facts.ActorId,
                    decision.Facts.IdempotencyNamespace,
                    decision.Authorization,
                    decision.Facts,
                    ct);
                if (summary.BlockedConflicts.Count > 0)
                {
                    throw new InvalidOperationException("REFERENCE_CONTRACT_MISMATCH");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "VERIFIED_MARKET_OPERATIONAL_MANUAL_RECONCILIATION_REQUIRED",
                    exception);
            }
        }

        var completion = await _preflight.VerifyCompletionAsync(decision.Facts, ct);
        if (completion.Disposition != VerifiedMarketOperationalTargetDisposition.ExactReplay)
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_MANUAL_RECONCILIATION_REQUIRED");
        }

        using var tenantScope = TenantScope.Begin(_tenant, decision.Facts.ReferenceTenantId);
        var publication = await _repository.GetVerifiedPublicationAsync(
            VerifiedMarketCatalogContract.SetCode,
            decision.Facts.CatalogVersion,
            decision.Facts.CatalogFingerprint,
            ct);
        if (publication is null)
        {
            throw new InvalidOperationException("REFERENCE_PUBLICATION_NOT_VERIFIED");
        }
    }
}

public enum VerifiedMarketOperationalCommandClassification
{
    NotRequested,
    Exact,
    Invalid
}

public static class VerifiedMarketOperationalCommandLine
{
    public const string RunArgument = "--run-verified-market-provisioning";

    public static VerifiedMarketOperationalCommandClassification Classify(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var suspicious = arguments.Where(argument =>
                argument.StartsWith(RunArgument, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, RunArgument, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (suspicious.Count == 0)
        {
            return VerifiedMarketOperationalCommandClassification.NotRequested;
        }

        return arguments.Count == 1
               && suspicious.Count == 1
               && string.Equals(suspicious[0], RunArgument, StringComparison.Ordinal)
            ? VerifiedMarketOperationalCommandClassification.Exact
            : VerifiedMarketOperationalCommandClassification.Invalid;
    }

    public static bool IsRequested(IReadOnlyList<string> arguments) =>
        Classify(arguments) == VerifiedMarketOperationalCommandClassification.Exact;
}
