using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Queries;

/// <summary>WP-CL-BE-3a — the transition history of one workflow instance (tenant scoped; another tenant's id is a
/// non-leaking 404).</summary>
public sealed record GetWorkflowInstanceHistoryQuery(
    Guid Id,
    string CorrelationId) : IRequest<Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>>;
