using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record SubmitEmployeeDraftCommand(
    Guid DraftSessionId,
    string? IfMatch,
    DraftSubmitRequest Request) : IRequest<Response<DraftSubmitResponse>>;
