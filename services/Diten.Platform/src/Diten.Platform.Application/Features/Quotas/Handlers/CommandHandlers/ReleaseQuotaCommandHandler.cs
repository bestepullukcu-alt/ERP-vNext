using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class ReleaseQuotaCommandHandler : IRequestHandler<ReleaseQuotaCommand, Response<QuotaMutationDto>>
{
    private readonly IQuotaService _quotaService;

    public ReleaseQuotaCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<QuotaMutationDto>> Handle(ReleaseQuotaCommand request, CancellationToken ct) =>
        _quotaService.ReleaseAsync(request.Request, ct);
}
