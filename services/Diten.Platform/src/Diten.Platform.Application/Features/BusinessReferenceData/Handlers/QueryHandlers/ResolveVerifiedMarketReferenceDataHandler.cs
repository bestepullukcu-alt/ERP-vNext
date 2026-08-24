using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class ResolveVerifiedMarketReferenceDataHandler
    : IRequestHandler<ResolveVerifiedMarketReferenceDataQuery, Response<BusinessReferenceDataVerifiedMarketResolveResult>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ResolveVerifiedMarketReferenceDataHandler(
        IBusinessReferenceDataStewardshipRepository repository,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<Response<BusinessReferenceDataVerifiedMarketResolveResult>> Handle(
        ResolveVerifiedMarketReferenceDataQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Fail("REFERENCE_FORBIDDEN", 403);
        }

        if (!VerifiedMarketCatalogContract.IsCanonicalCode(request.MarketCode))
        {
            return Fail("REFERENCE_MARKET_NOT_FOUND", 404);
        }

        Guid referenceTenantId;
        try
        {
            referenceTenantId = _repository.GetRequiredReferenceTenantId();
        }
        catch (InvalidOperationException exception) when (
            string.Equals(exception.Message, "REFERENCE_PROVIDER_CONFIGURATION_INVALID", StringComparison.Ordinal))
        {
            return Fail("REFERENCE_PROVIDER_UNAVAILABLE", 503);
        }

        using (TenantScope.Begin(_tenantContext, referenceTenantId))
        {
            var publication = await _repository.GetVerifiedPublicationAsync(
                VerifiedMarketCatalogContract.SetCode,
                cancellationToken);
            if (publication is null
                || !EnumerateVerifiedMarketsHandler.TryGetActiveMarkets(publication.Version, out var activeMarkets))
            {
                return Fail("REFERENCE_PROVIDER_UNAVAILABLE", 503);
            }

            var value = activeMarkets.SingleOrDefault(value =>
                string.Equals(value.ValueCode, request.MarketCode, StringComparison.Ordinal));
            return value is null
                ? Fail("REFERENCE_MARKET_NOT_FOUND", 404)
                : Response<BusinessReferenceDataVerifiedMarketResolveResult>.Success(
                    new BusinessReferenceDataVerifiedMarketResolveResult(
                        new(
                            VerifiedMarketCatalogContract.SetCode,
                            value.ValueCode,
                            publication.Version.BusinessReferenceDataVersionId,
                            publication.Version.VersionNumber,
                            VerifiedMarketCatalogContract.ResolutionMode,
                            _timeProvider.GetUtcNow())));
        }
    }

    private static Response<BusinessReferenceDataVerifiedMarketResolveResult> Fail(string code, int statusCode) =>
        Response<BusinessReferenceDataVerifiedMarketResolveResult>.Fail(code, statusCode, code);
}
