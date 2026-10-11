using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

/// <summary>
/// MOD-0290-FU04 S5b (BL-600): the values of the asked ITEM-UNIVERSAL sets, in their order, with their attributes and the
/// set's catalog version — read from the same code-owned catalog the resolve reads (whatever version this branch holds).
/// Only the three item sets; any other, a repeat, an empty or a malformed ask is 400 by name. Like the resolve, a
/// resolved tenant is required (the trust executor's tenant) although the lists are the same for every tenant.
/// </summary>
public sealed class EnumerateItemUniversalReferenceDataHandler
    : IRequestHandler<EnumerateItemUniversalReferenceDataQuery, Response<ItemUniversalEnumerationResult>>
{
    private readonly ITenantContext _tenantContext;

    public EnumerateItemUniversalReferenceDataHandler(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public Task<Response<ItemUniversalEnumerationResult>> Handle(EnumerateItemUniversalReferenceDataQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
            return Task.FromResult(Fail(ResolveItemUniversalReferenceDataHandler.Forbidden, 403));
        var setCodes = request.SetCodes;
        if (setCodes is null || setCodes.Count is < 1 or > 3
            || setCodes.Any(code => string.IsNullOrEmpty(code) || code.Length > ResolveItemUniversalReferenceDataHandler.MaximumCodeLength)
            || setCodes.Distinct(StringComparer.Ordinal).Count() != setCodes.Count)
            return Task.FromResult(Fail(ResolveItemUniversalReferenceDataHandler.ContractInvalid, 400));
        if (setCodes.Any(code => !ItemUniversalCatalog.IsSet(code)))
            return Task.FromResult(Fail(ResolveItemUniversalReferenceDataHandler.SetNotSupported, 400));

        var sets = setCodes.Select(code => new ItemUniversalEnumeratedSet(
                code!, ItemUniversalCatalog.CatalogVersion, ItemUniversalCatalog.GetVersionId(code!),
                ItemUniversalCatalog.CatalogVersionNumber,
                ItemUniversalCatalog.Values.Where(value => value.SetCode == code)
                    .OrderBy(value => value.SortOrder).ThenBy(value => value.ValueCode, StringComparer.Ordinal)
                    .Select(value => new ItemUniversalEnumeratedValue(value.ValueCode, value.DisplayName, value.SortOrder,
                        value.Attributes))
                    .ToList()))
            .ToList();
        return Task.FromResult(Response<ItemUniversalEnumerationResult>.Success(new ItemUniversalEnumerationResult(sets)));
    }

    private static Response<ItemUniversalEnumerationResult> Fail(string code, int statusCode) =>
        Response<ItemUniversalEnumerationResult>.Fail(code, statusCode, code);
}
