using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkflowDesigner.Models;
using MediatR;

namespace Diten.Platform.Application.Features.WorkflowDesigner.Queries;

public sealed record GetWorkflowDefinitionsQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Response<WorkflowDefinitionListModel>>;

public sealed record GetWorkflowDefinitionByIdQuery(Guid Id)
    : IRequest<Response<WorkflowDefinitionDetailModel>>;

public sealed record GetWorkflowInstancesQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Response<WorkflowInstanceListModel>>;

public sealed record GetWorkflowInstanceByIdQuery(Guid Id)
    : IRequest<Response<WorkflowInstanceDetailModel>>;

public sealed record GetApprovalTasksQuery(string? Status, string? AssigneeRole, string? AssigneeId, Guid? InstanceId, int Page, int PageSize)
    : IRequest<Response<ApprovalTaskListModel>>;

public sealed record GetApprovalTaskByIdQuery(Guid Id)
    : IRequest<Response<ApprovalTaskDetailModel>>;
