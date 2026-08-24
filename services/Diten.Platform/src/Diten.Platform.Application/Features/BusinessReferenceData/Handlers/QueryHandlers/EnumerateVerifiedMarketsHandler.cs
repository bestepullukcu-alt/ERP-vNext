using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class EnumerateVerifiedMarketsHandler
    : IRequestHandler<EnumerateVerifiedMarketsQuery, Response<BusinessReferenceDataVerifiedMarketsResult>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EnumerateVerifiedMarketsHandler(IBusinessReferenceDataStewardshipRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<BusinessReferenceDataVerifiedMarketsResult>> Handle(
        EnumerateVerifiedMarketsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Fail("REFERENCE_FORBIDDEN", 403);
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
            if (publication is null || !TryGetActiveMarkets(publication.Version, out var activeMarkets))
            {
                return Fail("REFERENCE_PROVIDER_UNAVAILABLE", 503);
            }

            var markets = activeMarkets
                .OrderBy(value => value.SortOrder)
                .ThenBy(value => value.ValueCode, StringComparer.Ordinal)
                .Select(value => new BusinessReferenceDataVerifiedMarketOption(
                    value.ValueCode,
                    value.DisplayName,
                    value.SortOrder))
                .ToList();
            return Response<BusinessReferenceDataVerifiedMarketsResult>.Success(
                new BusinessReferenceDataVerifiedMarketsResult(markets));
        }
    }

    internal static bool TryGetActiveMarkets(
        BusinessReferenceDataVersion version,
        out IReadOnlyList<BusinessReferenceDataValue> activeMarkets)
    {
        var values = version.Values
            .Where(value => !value.IsDeprecated)
            .ToList();

        var isValid = values.Count is > 0 and <= VerifiedMarketCatalogContract.MaximumActiveMarketCount
            && values.All(value =>
                VerifiedMarketCatalogContract.IsCanonicalCode(value.ValueCode)
                && !string.IsNullOrWhiteSpace(value.DisplayName)
                && value.SortOrder >= 0)
            && values.Select(value => value.ValueCode).Distinct(StringComparer.Ordinal).Count() == values.Count;

        activeMarkets = isValid ? values : [];
        return isValid;
    }

    private static Response<BusinessReferenceDataVerifiedMarketsResult> Fail(string code, int statusCode) =>
        Response<BusinessReferenceDataVerifiedMarketsResult>.Fail(code, statusCode, code);
}
