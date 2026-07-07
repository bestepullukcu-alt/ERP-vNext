using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record PatchEmployeeDraftCommand(
    Guid DraftSessionId,
    string? IfMatch,
    EmployeeDraftPatchRequest Request) : IRequest<Response<EmployeeDraftResponse>>;
