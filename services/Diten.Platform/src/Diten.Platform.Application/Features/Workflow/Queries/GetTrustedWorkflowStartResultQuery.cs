using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetTrustedWorkflowStartResultQuery(
    string IdempotencyKey,
    Guid ServiceClientId,
    Guid ExpectedMakerSubjectId,
    string ExpectedObjectType,
    string ExpectedObjectId,
    string CorrelationId) : IRequest<Response<TrustedWorkflowStartResult>>;
