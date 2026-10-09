using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

/// <summary>
/// WP-PLT-ITEM-LISTS-01: exact LATEST resolution against the current item catalog deployment (ITEM-UNIVERSAL-V2), many selections in one call (≤ 200, the
/// item create asks every code it stores). Code-owned: no repository, no tenant assignment — a resolved tenant is still
/// required (the trust executor's tenant). Refusals are named: a malformed request or a set outside the three is 400; a
/// value the list does not hold is 404 REFERENCE_VALUE_NOT_FOUND with the missing pairs as data.
/// </summary>
public sealed class ResolveItemUniversalReferenceDataHandler
    : IRequestHandler<ResolveItemUniversalReferenceDataQuery, Response<ItemUniversalResolveResult>>
{
    public const string Forbidden = "REFERENCE_FORBIDDEN";
    public const string BatchLimitExceeded = "REFERENCE_BATCH_LIMIT_EXCEEDED";
    public const string ContractInvalid = "REFERENCE_RESOLUTION_CONTRACT_INVALID";
    public const string SetNotSupported = "REFERENCE_SET_NOT_SUPPORTED";
    public const string ValueNotFound = "REFERENCE_VALUE_NOT_FOUND";
    public const int MaximumCodeLength = 64;

    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ResolveItemUniversalReferenceDataHandler(ITenantContext tenantContext, TimeProvider timeProvider)
    {
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public Task<Response<ItemUniversalResolveResult>> Handle(ResolveItemUniversalReferenceDataQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
            return Task.FromResult(Fail(Forbidden, 403));
        var selections = request.Selections;
        if (selections is null || selections.Count is < 1 or > ItemUniversalCatalog.MaximumSelections)
            return Task.FromResult(Fail(BatchLimitExceeded, 400));
        var seen = new HashSet<(string, string)>();
        foreach (var selection in selections)
        {
            // FIX1 M1: a code is short (≤ 64); a longer one is a malformed request, never echoed back as "not found".
            if (selection is null || string.IsNullOrEmpty(selection.SetCode) || string.IsNullOrEmpty(selection.ValueCode)
                || selection.SetCode.Length > MaximumCodeLength || selection.ValueCode.Length > MaximumCodeLength
                || !string.Equals(selection.ResolutionMode, ItemUniversalCatalog.ResolutionMode, StringComparison.Ordinal)
                || !seen.Add((selection.SetCode, selection.ValueCode)))
                return Task.FromResult(Fail(ContractInvalid, 400));
            if (!ItemUniversalCatalog.IsSet(selection.SetCode))
                return Task.FromResult(Fail(SetNotSupported, 400));
        }

        var notFound = selections
            .Where(selection => ItemUniversalCatalog.Find(selection!.SetCode, selection.ValueCode) is null)
            .Select(selection => new ItemUniversalNotFound(selection!.SetCode, selection.ValueCode))
            .ToList();
        if (notFound.Count > 0)
            return Task.FromResult(Response<ItemUniversalResolveResult>.FailWithData(
                ValueNotFound, 404, ValueNotFound, new ItemUniversalResolveResult([], notFound)));

        var now = _timeProvider.GetUtcNow();
        var resolved = selections.Select(selection =>
        {
            var value = ItemUniversalCatalog.Find(selection!.SetCode, selection.ValueCode)!;
            return new ItemUniversalResolveSelection(value.SetCode, value.ValueCode, ItemUniversalCatalog.CatalogVersion,
                ItemUniversalCatalog.GetVersionId(value.SetCode), ItemUniversalCatalog.GetVersionNumber(value.SetCode),
                ItemUniversalCatalog.ResolutionMode, now, IsRetired: false, SelectableForNew: true, value.Attributes);
        }).ToList();
        return Task.FromResult(Response<ItemUniversalResolveResult>.Success(new ItemUniversalResolveResult(resolved, [])));
    }

    private static Response<ItemUniversalResolveResult> Fail(string code, int statusCode) =>
        Response<ItemUniversalResolveResult>.Fail(code, statusCode, code);
}
