using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetTrustedWorkflowCancellationPreflightQuery(
    Guid ServiceClientId,
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    Guid ExpectedMakerSubjectId,
    string CorrelationId) : IRequest<Response<TrustedWorkflowCancellationPreflight>>;
