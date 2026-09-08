using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TaskChecklistEngine.Models;
using MediatR;

namespace Diten.Platform.Application.Features.TaskChecklistEngine.Queries;

public sealed record GetWorkTasksQuery(string? Search, string? Status, string? AssigneeId, int Page, int PageSize)
    : IRequest<Response<WorkTaskListModel>>;

public sealed record GetWorkTaskByIdQuery(Guid Id)
    : IRequest<Response<WorkTaskDetailModel>>;

public sealed record GetChecklistTemplatesQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Response<ChecklistTemplateListModel>>;

public sealed record GetChecklistTemplateByIdQuery(Guid Id)
    : IRequest<Response<ChecklistTemplateDetailModel>>;

public sealed record GetChecklistRunsQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Response<ChecklistRunListModel>>;

public sealed record GetChecklistRunByIdQuery(Guid Id)
    : IRequest<Response<ChecklistRunDetailModel>>;
