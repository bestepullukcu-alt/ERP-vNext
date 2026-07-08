using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record ValidateDraftReferencesCommand(
    Guid DraftSessionId,
    string? IfMatch,
    ReferenceValidationRequest Request) : IRequest<Response<ReferenceValidationResponse>>;
