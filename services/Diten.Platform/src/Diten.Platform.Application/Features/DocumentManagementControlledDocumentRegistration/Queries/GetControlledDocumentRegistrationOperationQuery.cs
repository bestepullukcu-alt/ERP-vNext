using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Queries;

public sealed record GetControlledDocumentRegistrationOperationQuery(Guid OperationId, string CorrelationId)
    : IRequest<Response<ControlledDocumentRegistrationOperationModel>>;
