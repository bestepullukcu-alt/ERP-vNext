using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Queries;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.QueryHandlers;

public sealed class GetTenantQuotaStatusQueryHandler : IRequestHandler<GetTenantQuotaStatusQuery, Response<IReadOnlyList<QuotaStatusDto>>>
{
    private readonly IQuotaService _quotaService;

    public GetTenantQuotaStatusQueryHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<IReadOnlyList<QuotaStatusDto>>> Handle(GetTenantQuotaStatusQuery request, CancellationToken ct) =>
        _quotaService.GetStatusesAsync(request.TenantId, ct);
}
