using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class ResetQuotaPeriodCommandHandler : IRequestHandler<ResetQuotaPeriodCommand, Response<QuotaStatusDto>>
{
    private readonly IQuotaService _quotaService;

    public ResetQuotaPeriodCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<QuotaStatusDto>> Handle(ResetQuotaPeriodCommand request, CancellationToken ct) =>
        _quotaService.ResetPeriodAsync(request.Request, ct);
}
