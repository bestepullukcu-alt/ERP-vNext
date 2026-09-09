using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkflowDesigner.Models;
using Diten.Platform.Application.Features.WorkflowDesigner.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkflowDesigner.Handlers;

public sealed class GetWorkflowDefinitionsQueryHandler : IRequestHandler<GetWorkflowDefinitionsQuery, Response<WorkflowDefinitionListModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    public GetWorkflowDefinitionsQueryHandler(IWorkflowDefinitionRepository repository) => _repository = repository;

    public async Task<Response<WorkflowDefinitionListModel>> Handle(GetWorkflowDefinitionsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryAsync(new WorkflowDefinitionListQuery(request.Search, request.Status, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<WorkflowDefinitionListModel>.Success(new WorkflowDefinitionListModel(
            items.Select(WorkflowMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetWorkflowDefinitionByIdQueryHandler : IRequestHandler<GetWorkflowDefinitionByIdQuery, Response<WorkflowDefinitionDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    public GetWorkflowDefinitionByIdQueryHandler(IWorkflowDefinitionRepository repository) => _repository = repository;

    public async Task<Response<WorkflowDefinitionDetailModel>> Handle(GetWorkflowDefinitionByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        return entity is null
            ? Response<WorkflowDefinitionDetailModel>.Fail("definition_not_found", 404)
            : Response<WorkflowDefinitionDetailModel>.Success(WorkflowMappings.ToDetail(entity));
    }
}

public sealed class GetWorkflowInstancesQueryHandler : IRequestHandler<GetWorkflowInstancesQuery, Response<WorkflowInstanceListModel>>
{
    private readonly IWorkflowRuntimeRepository _repository;
    public GetWorkflowInstancesQueryHandler(IWorkflowRuntimeRepository repository) => _repository = repository;

    public async Task<Response<WorkflowInstanceListModel>> Handle(GetWorkflowInstancesQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryInstancesAsync(new WorkflowInstanceListQuery(request.Search, request.Status, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<WorkflowInstanceListModel>.Success(new WorkflowInstanceListModel(
            items.Select(WorkflowMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetWorkflowInstanceByIdQueryHandler : IRequestHandler<GetWorkflowInstanceByIdQuery, Response<WorkflowInstanceDetailModel>>
{
    private readonly IWorkflowRuntimeRepository _repository;
    public GetWorkflowInstanceByIdQueryHandler(IWorkflowRuntimeRepository repository) => _repository = repository;

    public async Task<Response<WorkflowInstanceDetailModel>> Handle(GetWorkflowInstanceByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetInstanceByIdAsync(request.Id, ct);
        return entity is null
            ? Response<WorkflowInstanceDetailModel>.Fail("instance_not_found", 404)
            : Response<WorkflowInstanceDetailModel>.Success(WorkflowMappings.ToDetail(entity));
    }
}

public sealed class GetApprovalTasksQueryHandler : IRequestHandler<GetApprovalTasksQuery, Response<ApprovalTaskListModel>>
{
    private readonly IWorkflowRuntimeRepository _repository;
    public GetApprovalTasksQueryHandler(IWorkflowRuntimeRepository repository) => _repository = repository;

    public async Task<Response<ApprovalTaskListModel>> Handle(GetApprovalTasksQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;
        var (items, total) = await _repository.QueryTasksAsync(
            new ApprovalTaskListQuery(request.Status, request.AssigneeRole, request.AssigneeId, request.InstanceId, page, pageSize), ct);
        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Response<ApprovalTaskListModel>.Success(new ApprovalTaskListModel(
            items.Select(WorkflowMappings.ToListItem).ToList(), page, pageSize, total, totalPages));
    }
}

public sealed class GetApprovalTaskByIdQueryHandler : IRequestHandler<GetApprovalTaskByIdQuery, Response<ApprovalTaskDetailModel>>
{
    private readonly IWorkflowRuntimeRepository _repository;
    public GetApprovalTaskByIdQueryHandler(IWorkflowRuntimeRepository repository) => _repository = repository;

    public async Task<Response<ApprovalTaskDetailModel>> Handle(GetApprovalTaskByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetTaskByIdAsync(request.Id, ct);
        return entity is null
            ? Response<ApprovalTaskDetailModel>.Fail("task_not_found", 404)
            : Response<ApprovalTaskDetailModel>.Success(WorkflowMappings.ToDetail(entity));
    }
}
