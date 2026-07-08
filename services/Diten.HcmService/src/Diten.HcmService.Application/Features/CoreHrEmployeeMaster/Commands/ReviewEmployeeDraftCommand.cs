using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record ReviewEmployeeDraftCommand(
    Guid DraftSessionId,
    string? IfMatch,
    DraftReviewRequest Request) : IRequest<Response<DraftReviewResponse>>;
