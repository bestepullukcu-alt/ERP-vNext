using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;

public sealed record DryRunInstantiationCommand(
    Guid BaselineReleaseId,
    InstantiationScopeRequest Scope,
    InstantiationSelectionRequest Selection,
    string CorrelationId)
    : IRequest<Response<InstantiationResultModel>>;
