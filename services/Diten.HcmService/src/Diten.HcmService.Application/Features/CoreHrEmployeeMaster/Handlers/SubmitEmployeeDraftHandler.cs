using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class SubmitEmployeeDraftHandler : IRequestHandler<SubmitEmployeeDraftCommand, Response<DraftSubmitResponse>>
{
    public const string ScopeBlockedReason = "mod0251_lifecycle_activation_not_enabled";

    public Task<Response<DraftSubmitResponse>> Handle(SubmitEmployeeDraftCommand request, CancellationToken cancellationToken)
        => Task.FromResult(Response<DraftSubmitResponse>.Fail(ScopeBlockedReason, 409));
}
