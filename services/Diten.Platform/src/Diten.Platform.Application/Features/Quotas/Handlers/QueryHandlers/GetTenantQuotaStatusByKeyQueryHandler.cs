using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Queries;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.QueryHandlers;

public sealed class GetTenantQuotaStatusByKeyQueryHandler : IRequestHandler<GetTenantQuotaStatusByKeyQuery, Response<QuotaStatusDto>>
{
    private readonly IQuotaService _quotaService;

    public GetTenantQuotaStatusByKeyQueryHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<QuotaStatusDto>> Handle(GetTenantQuotaStatusByKeyQuery request, CancellationToken ct) =>
        _quotaService.GetStatusResponseAsync(request.TenantId, request.QuotaKey, ct);
}
