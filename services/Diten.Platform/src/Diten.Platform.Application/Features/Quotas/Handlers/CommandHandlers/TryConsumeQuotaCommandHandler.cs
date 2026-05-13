using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class TryConsumeQuotaCommandHandler : IRequestHandler<TryConsumeQuotaCommand, Response<QuotaMutationDto>>
{
    private readonly IQuotaService _quotaService;

    public TryConsumeQuotaCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<QuotaMutationDto>> Handle(TryConsumeQuotaCommand request, CancellationToken ct) =>
        _quotaService.TryConsumeAsync(request.Request, ct);
}
