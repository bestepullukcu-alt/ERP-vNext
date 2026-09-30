using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

/// <summary>
/// WP-CL-BE-3a — reads <see cref="WorkflowTransitionLog"/> rows of one instance in <c>SequenceNo</c> order. Read only.
/// <list type="bullet">
/// <item>From/To stage+step: a <c>start</c> row enters the step of the task it created (From empty). Any other row
/// leaves the step of the task it acted on; it enters the next step when the engine's following row opened one,
/// stays on the same step while the instance is still active (delegate / request-info / escalate), and has no To on a
/// terminal transition.</item>
/// <item>Comment: the actor's comment / rejection reason. A <c>start</c> row carries none — the engine's internal
/// next-step marker is not user text and is not returned.</item>
/// <item>ActorDisplay: filled only through the existing <see cref="IUserDisplayNameResolver"/> (batch, best effort);
/// absent when not resolvable — an id is never shown as a name.</item>
/// </list>
/// </summary>
public sealed class GetWorkflowInstanceHistoryHandler
    : IRequestHandler<GetWorkflowInstanceHistoryQuery, Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTemplateVersionRepository _versions;
    private readonly IUserDisplayNameResolver? _displayNames;

    public GetWorkflowInstanceHistoryHandler(
        IWorkflowInstanceRepository instances,
        IWorkflowTransitionLogRepository logs,
        IApprovalTaskRepository tasks,
        IWorkflowTemplateVersionRepository versions,
        IUserDisplayNameResolver? displayNames = null)
    {
        _instances = instances;
        _logs = logs;
        _tasks = tasks;
        _versions = versions;
        _displayNames = displayNames;
    }

    public async Task<Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>> Handle(
        GetWorkflowInstanceHistoryQuery request, CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.Id, ct);
        if (instance is null)
        {
            return Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>.Fail(
                "Workflow instance not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        var logs = (await _logs.ListByInstanceIdAsync(instance.Id, ct))
            .Where(l => l.WorkflowInstanceId == instance.Id)
            .OrderBy(l => l.SequenceNo)
            .ToList();
        var tasks = (await _tasks.ListByInstanceIdAsync(instance.Id, ct)).ToDictionary(t => t.Id);
        var steps = instance.TemplateVersionId is { } versionId
            ? WorkflowDefinitionRuntimePlan.FromVersion(await _versions.GetByIdAsync(versionId, ct))
            : [];
        var names = await ResolveNamesAsync(logs, ct);

        var rows = new List<WorkflowInstanceHistoryEntryDto>(logs.Count);
        for (var i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            var acted = TaskOf(log, tasks);
            string? fromStage = null, fromStep = null, toStage = null, toStep = null;
            if (log.Action == WorkflowTransitionAction.Start)
            {
                (toStage, toStep) = (acted?.StageCode, acted?.StepCode);
            }
            else
            {
                (fromStage, fromStep) = (acted?.StageCode, acted?.StepCode);
                var next = i + 1 < logs.Count ? logs[i + 1] : null;
                if (next is { Action: WorkflowTransitionAction.Start } && TaskOf(next, tasks) is { } opened
                    && opened.Id != acted?.Id)
                {
                    (toStage, toStep) = (opened.StageCode, opened.StepCode);
                }
                else if (string.Equals(log.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal))
                {
                    (toStage, toStep) = (fromStage, fromStep);
                }
            }

            var stageForName = fromStage ?? toStage;
            var stepForName = fromStep ?? toStep;
            var stepName = steps.FirstOrDefault(s =>
                string.Equals(s.StageCode, stageForName, StringComparison.Ordinal)
                && string.Equals(s.StepCode, stepForName, StringComparison.Ordinal))?.StepName;

            rows.Add(new WorkflowInstanceHistoryEntryDto(
                log.SequenceNo,
                ActionName(log.Action),
                log.ActorId,
                Guid.TryParse(log.ActorId, out var actorGuid) && names.TryGetValue(actorGuid, out var display)
                    ? display
                    : null,
                fromStage,
                fromStep,
                toStage,
                toStep,
                stepName,
                log.Action == WorkflowTransitionAction.Start ? null : log.Comment,
                log.ReasonCode,
                log.CreatedAt));
        }

        return Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>.Success(rows, correlationId: request.CorrelationId);
    }

    private static ApprovalTask? TaskOf(WorkflowTransitionLog log, IReadOnlyDictionary<Guid, ApprovalTask> tasks)
        => log.ApprovalTaskId is { } id && tasks.TryGetValue(id, out var task) ? task : null;

    public static string ActionName(WorkflowTransitionAction action) => action switch
    {
        WorkflowTransitionAction.Start => "start",
        WorkflowTransitionAction.Approve => "approve",
        WorkflowTransitionAction.Reject => "reject",
        WorkflowTransitionAction.Delegate => "delegate",
        WorkflowTransitionAction.Escalate => "escalate",
        WorkflowTransitionAction.Timeout => "timeout",
        WorkflowTransitionAction.RequestInfo => "request-info",
        WorkflowTransitionAction.Cancel => "cancel",
        _ => action.ToString().ToLowerInvariant()
    };

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(
        IReadOnlyList<WorkflowTransitionLog> logs, CancellationToken ct)
    {
        var ids = logs
            .Select(l => Guid.TryParse(l.ActorId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (_displayNames is null || ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await _displayNames.ResolveAsync(ids, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Best effort (resolver contract): a name is decoration — rows still return without it.
            return new Dictionary<Guid, string>();
        }
    }
}
