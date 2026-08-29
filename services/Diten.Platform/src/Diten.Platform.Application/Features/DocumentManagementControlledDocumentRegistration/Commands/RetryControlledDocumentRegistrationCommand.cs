using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;

public sealed record RetryControlledDocumentRegistrationCommand(Guid OperationId, string CorrelationId)
    : IRequest<Response<RetryControlledDocumentRegistrationResultModel>>;
