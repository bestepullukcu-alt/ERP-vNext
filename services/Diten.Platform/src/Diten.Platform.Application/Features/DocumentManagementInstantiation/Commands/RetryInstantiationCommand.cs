using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;

public sealed record RetryInstantiationCommand(
    Guid OperationId,
    IReadOnlyList<string> NodeKeys,
    string CorrelationId)
    : IRequest<Response<InstantiationResultModel>>;
