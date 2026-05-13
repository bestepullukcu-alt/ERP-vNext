using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class RecalculateQuotaUsageCommandHandler : IRequestHandler<RecalculateQuotaUsageCommand, Response<QuotaStatusDto>>
{
    private readonly IQuotaService _quotaService;

    public RecalculateQuotaUsageCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<QuotaStatusDto>> Handle(RecalculateQuotaUsageCommand request, CancellationToken ct) =>
        _quotaService.RecalculateAsync(request.Request, ct);
}
