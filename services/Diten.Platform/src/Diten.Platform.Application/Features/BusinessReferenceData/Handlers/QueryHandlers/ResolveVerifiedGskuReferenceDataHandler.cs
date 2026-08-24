using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class ResolveVerifiedGskuReferenceDataHandler
    : IRequestHandler<ResolveVerifiedGskuReferenceDataQuery, Response<BusinessReferenceDataVerifiedResolveResult>>
{
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ResolveVerifiedGskuReferenceDataHandler(
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public Task<Response<BusinessReferenceDataVerifiedResolveResult>> Handle(
        ResolveVerifiedGskuReferenceDataQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Task.FromResult(Fail("REFERENCE_FORBIDDEN", 403));
        }

        if (!IsValidRequest(request.Selections))
        {
            return Task.FromResult(Fail("REFERENCE_RESOLUTION_CONTRACT_INVALID", 409));
        }

        var now = _timeProvider.GetUtcNow();
        var resolved = new List<BusinessReferenceDataVerifiedResolveSelection>(request.Selections.Count);
        foreach (var requested in request.Selections)
        {
            resolved.Add(new BusinessReferenceDataVerifiedResolveSelection(
                requested.SetCode,
                requested.ValueCode,
                VerifiedGskuUniversalCatalog.GetVersionId(requested.SetCode),
                VerifiedGskuUniversalCatalog.CatalogVersionNumber,
                VerifiedGskuUniversalCatalog.ResolutionMode,
                now,
                IsRetired: false,
                SelectableForNew: true));
        }

        return Task.FromResult(Response<BusinessReferenceDataVerifiedResolveResult>.Success(
            new BusinessReferenceDataVerifiedResolveResult(resolved)));
    }

    private static bool IsValidRequest(IReadOnlyList<BusinessReferenceDataVerifiedResolveSelectionInput>? selections)
    {
        if (selections is null || selections.Count is < 1 or > 2)
        {
            return false;
        }

        var seenSets = new HashSet<string>(StringComparer.Ordinal);
        return selections.All(selection =>
            selection is not null
            && VerifiedGskuUniversalCatalog.IsSupported(selection.SetCode, selection.ValueCode)
            && string.Equals(
                selection.ResolutionMode,
                VerifiedGskuUniversalCatalog.ResolutionMode,
                StringComparison.Ordinal)
            && seenSets.Add(selection.SetCode));
    }

    private static Response<BusinessReferenceDataVerifiedResolveResult> Fail(string code, int statusCode) =>
        Response<BusinessReferenceDataVerifiedResolveResult>.Fail(code, statusCode, code);
}
