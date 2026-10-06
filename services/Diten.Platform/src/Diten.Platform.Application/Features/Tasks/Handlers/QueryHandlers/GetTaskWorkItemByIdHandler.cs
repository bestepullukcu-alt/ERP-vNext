using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// BL-414 — one task, as a Task Center work item, for ANY reader the task read rule admits.
///
/// <para><b>Who may read it: the rule, asked here, before anything is projected.</b>
/// <see cref="ITaskReadAccessPolicy"/> — the same rule <see cref="GetTaskItemByIdHandler"/> asks for the module's
/// record page, so the Task Center detail and the record page cannot disagree about who may open a task.</para>
///
/// <para><b>What is returned: the list's own projection.</b> <see cref="TaskWorkItemProvider.GetWorkItemAsync"/>
/// runs the batch the list runs, over a page of one — never a second mapping that could drift from the row the
/// reader would see on the board.</para>
///
/// <para><b>One 404.</b> A task that does not exist, one in another tenant (the repository's tenant filter hides
/// the row) and one that exists but is not readable by the caller all answer from the SAME factory —
/// byte-identical message, status and reason code (BL-349).</para>
/// </summary>
public sealed class GetTaskWorkItemByIdHandler
    : IRequestHandler<GetTaskWorkItemByIdQuery, Response<WorkItemProjectionDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskReadAccessPolicy _readAccess;
    private readonly ICurrentUserContext _currentUser;

    /// <summary>
    /// The bound providers, from which the tasks provider is taken. Not a new DI registration: the provider is
    /// bound once, as a work-item provider (DependencyInjection.cs), and read from that binding here.
    /// </summary>
    private readonly IEnumerable<IWorkItemProvider> _providers;

    public GetTaskWorkItemByIdHandler(
        ITaskItemRepository tasks,
        ITaskReadAccessPolicy readAccess,
        ICurrentUserContext currentUser,
        IEnumerable<IWorkItemProvider> providers)
    {
        _tasks = tasks;
        _readAccess = readAccess;
        _currentUser = currentUser;
        _providers = providers;
    }

    public async Task<Response<WorkItemProjectionDto>> Handle(GetTaskWorkItemByIdQuery request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct);
        if (task is null)
        {
            return NotFound(request.CorrelationId);
        }

        if (!await _readAccess.CanReadAsync(task, _currentUser.UserId, ct))
        {
            // A real task the caller has no readable relationship to must not be distinguishable from one that
            // does not exist at all — same factory, same bytes.
            return NotFound(request.CorrelationId);
        }

        // A missing tasks provider is a wiring fault, not an answer about this task, so it is not dressed as a 404.
        var provider = _providers.OfType<TaskWorkItemProvider>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "The tasks work-item provider is not bound, so no task can be projected by id.");

        var actor = new WorkItemActor(_currentUser.UserId, request.IsPlatformActor, request.GrantedPermissions);
        var item = await provider.GetWorkItemAsync(task, actor, ct);
        return Response<WorkItemProjectionDto>.Success(item, correlationId: request.CorrelationId);
    }

    private static Response<WorkItemProjectionDto> NotFound(string correlationId)
        => Response<WorkItemProjectionDto>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, correlationId);
}
