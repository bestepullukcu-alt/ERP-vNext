using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;

public sealed record CreateControlledDocumentRegistrationCommand(
    CreateControlledDocumentRegistrationInput Input,
    string CorrelationId)
    : IRequest<Response<ControlledDocumentRegistrationResultModel>>;
