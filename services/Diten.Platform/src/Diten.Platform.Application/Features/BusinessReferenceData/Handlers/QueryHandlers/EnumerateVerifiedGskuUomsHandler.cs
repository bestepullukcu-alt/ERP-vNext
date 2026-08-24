using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class EnumerateVerifiedGskuUomsHandler
    : IRequestHandler<EnumerateVerifiedGskuUomsQuery, Response<BusinessReferenceDataVerifiedUomResult>>
{
    private readonly ITenantContext _tenantContext;

    public EnumerateVerifiedGskuUomsHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Task<Response<BusinessReferenceDataVerifiedUomResult>> Handle(
        EnumerateVerifiedGskuUomsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return Task.FromResult(Fail("REFERENCE_FORBIDDEN", 403));
        }

        return Task.FromResult(Response<BusinessReferenceDataVerifiedUomResult>.Success(
            new BusinessReferenceDataVerifiedUomResult(VerifiedGskuUniversalCatalog.Uoms)));
    }

    private static Response<BusinessReferenceDataVerifiedUomResult> Fail(string code, int statusCode) =>
        Response<BusinessReferenceDataVerifiedUomResult>.Fail(code, statusCode, code);
}
