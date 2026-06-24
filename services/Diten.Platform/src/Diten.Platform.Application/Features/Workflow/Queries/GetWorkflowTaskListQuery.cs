using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

public sealed record GetWorkflowTaskListQuery(
    string CorrelationId) : IRequest<Response<IReadOnlyList<WorkflowTaskDto>>>;
