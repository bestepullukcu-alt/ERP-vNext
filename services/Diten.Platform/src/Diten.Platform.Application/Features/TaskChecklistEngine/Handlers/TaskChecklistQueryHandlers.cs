using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TaskChecklistEngine.Models;
using Diten.Platform.Application.Features.TaskChecklistEngine.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TaskChecklistEngine.Handlers;

public sealed class GetWorkTasksQueryHandler : IRequestHandler<GetWorkTasksQuery, Response<WorkTaskListModel>>
{
    private readonly IWorkTaskRepository _repository;
    public GetWorkTasksQueryHandler(IWorkTaskRepository repository) => _repository = repository;

    public async Task<Response<WorkTaskListModel>> Handle(GetWorkTasksQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryAsync(
            new WorkTaskListQuery(request.Search, request.Status, request.AssigneeId, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<WorkTaskListModel>.Success(new WorkTaskListModel(
            items.Select(TaskChecklistMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetWorkTaskByIdQueryHandler : IRequestHandler<GetWorkTaskByIdQuery, Response<WorkTaskDetailModel>>
{
    private readonly IWorkTaskRepository _repository;
    public GetWorkTaskByIdQueryHandler(IWorkTaskRepository repository) => _repository = repository;

    public async Task<Response<WorkTaskDetailModel>> Handle(GetWorkTaskByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        return entity is null
            ? Response<WorkTaskDetailModel>.Fail("task_not_found", 404)
            : Response<WorkTaskDetailModel>.Success(TaskChecklistMappings.ToDetail(entity));
    }
}

public sealed class GetChecklistTemplatesQueryHandler : IRequestHandler<GetChecklistTemplatesQuery, Response<ChecklistTemplateListModel>>
{
    private readonly IChecklistRepository _repository;
    public GetChecklistTemplatesQueryHandler(IChecklistRepository repository) => _repository = repository;

    public async Task<Response<ChecklistTemplateListModel>> Handle(GetChecklistTemplatesQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryTemplatesAsync(
            new ChecklistTemplateListQuery(request.Search, request.Status, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<ChecklistTemplateListModel>.Success(new ChecklistTemplateListModel(
            items.Select(TaskChecklistMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetChecklistTemplateByIdQueryHandler : IRequestHandler<GetChecklistTemplateByIdQuery, Response<ChecklistTemplateDetailModel>>
{
    private readonly IChecklistRepository _repository;
    public GetChecklistTemplateByIdQueryHandler(IChecklistRepository repository) => _repository = repository;

    public async Task<Response<ChecklistTemplateDetailModel>> Handle(GetChecklistTemplateByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetTemplateByIdAsync(request.Id, ct);
        return entity is null
            ? Response<ChecklistTemplateDetailModel>.Fail("template_not_found", 404)
            : Response<ChecklistTemplateDetailModel>.Success(TaskChecklistMappings.ToDetail(entity));
    }
}

public sealed class GetChecklistRunsQueryHandler : IRequestHandler<GetChecklistRunsQuery, Response<ChecklistRunListModel>>
{
    private readonly IChecklistRepository _repository;
    public GetChecklistRunsQueryHandler(IChecklistRepository repository) => _repository = repository;

    public async Task<Response<ChecklistRunListModel>> Handle(GetChecklistRunsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryRunsAsync(
            new ChecklistRunListQuery(request.Search, request.Status, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<ChecklistRunListModel>.Success(new ChecklistRunListModel(
            items.Select(TaskChecklistMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetChecklistRunByIdQueryHandler : IRequestHandler<GetChecklistRunByIdQuery, Response<ChecklistRunDetailModel>>
{
    private readonly IChecklistRepository _repository;
    public GetChecklistRunByIdQueryHandler(IChecklistRepository repository) => _repository = repository;

    public async Task<Response<ChecklistRunDetailModel>> Handle(GetChecklistRunByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetRunByIdAsync(request.Id, ct);
        return entity is null
            ? Response<ChecklistRunDetailModel>.Fail("run_not_found", 404)
            : Response<ChecklistRunDetailModel>.Success(TaskChecklistMappings.ToDetail(entity));
    }
}
