using Diten.Web.Models.WorkCenter;

namespace Diten.Web.Services.WorkCenter;

public interface ITaskDetailService
{
    TaskDetailResponse? GetTaskDetail(string id, string? currentUserId);

    TaskActionResult Accept(string id, string? currentUserId);
    TaskActionResult Plan(string id, string? currentUserId, PlanTaskRequest request);
    TaskActionResult Start(string id, string? currentUserId);
    TaskActionResult LogTime(string id, string? currentUserId, LogTimeRequest request);
    TaskActionResult RequestInfo(string id, string? currentUserId, RequestInfoRequest request);
    TaskActionResult Resume(string id, string? currentUserId);
    TaskActionResult Complete(string id, string? currentUserId, CompleteTaskRequest request);
    TaskActionResult Reassign(string id, string? currentUserId, ReassignTaskRequest request);

    TaskActionResult ApproveApprovalGate(string id, string? currentUserId, GateDecisionRequest request);
    TaskActionResult RejectApprovalGate(string id, string? currentUserId, GateDecisionRequest request);
    TaskActionResult ApproveReviewGate(string id, string? currentUserId, GateDecisionRequest request);
    TaskActionResult RejectReviewGate(string id, string? currentUserId, GateDecisionRequest request);

    TaskActionResult AnswerInfoRequest(string id, string irId, string? currentUserId, AnswerInfoRequest request);
    TaskActionResult Cancel(string id, string? currentUserId, CancelTaskRequest request);
}

public sealed class TaskDetailService : ITaskDetailService
{
    private readonly object _sync = new();

    private readonly Dictionary<string, PersonData> _people = new(StringComparer.OrdinalIgnoreCase)
    {
        ["elif-kaya"] = new("elif-kaya", "Elif Kaya", "EK"),
        ["mert-demir"] = new("mert-demir", "Mert Demir", "MD"),
        ["zeynep-arslan"] = new("zeynep-arslan", "Zeynep Arslan", "ZA"),
        ["ahmet-yilmaz"] = new("ahmet-yilmaz", "Ahmet Yılmaz", "AY"),
        ["selin-korkmaz"] = new("selin-korkmaz", "Selin Korkmaz", "SK"),
        ["burak-sari"] = new("burak-sari", "Burak Sarı", "BS"),
        ["can-ozkan"] = new("can-ozkan", "Can Özkan", "CÖ"),
        ["system"] = new("system", "System", "SY")
    };

    private readonly Dictionary<string, TaskData> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public TaskDetailService()
    {
        var sample = BuildCanonicalTask("TASK-2847");
        _tasks[sample.Id] = sample;
    }

    public TaskDetailResponse? GetTaskDetail(string id, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_sync)
        {
            var task = EnsureTask(id);
            var userId = ResolveUserId(task, currentUserId);
            return BuildResponse(task, userId);
        }
    }

    public TaskActionResult Accept(string id, string? currentUserId)
    {
        return Mutate(id, currentUserId, "accept", (task, userId) =>
        {
            if (task.Status != TaskStatus.PendingAcceptance)
                return TaskActionResult.Conflict("Accept is only available in Pending Acceptance.");

            task.Status = TaskStatus.Open;
            Touch(task);
            AddActivity(task, "transition", userId, "Task accepted", statusBadge: TaskStatus.Open);
            return TaskActionResult.Ok("Task accepted.");
        });
    }

    public TaskActionResult Plan(string id, string? currentUserId, PlanTaskRequest request)
    {
        return Mutate(id, currentUserId, "plan", (task, userId) =>
        {
            if (request.PlanDate == default)
                return TaskActionResult.BadRequest("Plan date is required.");

            task.PlanDate = request.PlanDate;
            task.Status = TaskStatus.Planned;
            Touch(task);
            AddActivity(task, "transition", userId, $"Planned task for {request.PlanDate:dd.MM.yyyy}", statusBadge: TaskStatus.Planned);
            return TaskActionResult.Ok("Task planned.");
        });
    }

    public TaskActionResult Start(string id, string? currentUserId)
    {
        return Mutate(id, currentUserId, "start", (task, userId) =>
        {
            if (task.Status is not (TaskStatus.Open or TaskStatus.Planned))
                return TaskActionResult.Conflict("Start is only available in Open or Planned.");

            task.Status = TaskStatus.InProgress;
            Touch(task);
            AddActivity(task, "transition", userId, "Started work", statusBadge: TaskStatus.InProgress);
            return TaskActionResult.Ok("Work started.");
        });
    }

    public TaskActionResult LogTime(string id, string? currentUserId, LogTimeRequest request)
    {
        return Mutate(id, currentUserId, "logTime", (task, userId) =>
        {
            if (string.IsNullOrWhiteSpace(request.Description))
                return TaskActionResult.BadRequest("Description is required.");

            if (!TryParseDuration(request.Duration, out var minutes) || minutes <= 0)
                return TaskActionResult.BadRequest("Duration format is invalid.");

            var entry = new TimeEntryData
            {
                Date = request.Date == default ? DateOnly.FromDateTime(DateTime.Today) : request.Date,
                Description = request.Description.Trim(),
                DurationMinutes = minutes
            };

            task.TimeEntries.Add(entry);
            Touch(task);
            AddActivity(task, "timeLog", userId, $"Logged {FormatHours(minutes)} — {entry.Description}");
            return TaskActionResult.Ok("Time entry recorded.");
        });
    }

    public TaskActionResult RequestInfo(string id, string? currentUserId, RequestInfoRequest request)
    {
        return Mutate(id, currentUserId, "requestInfo", (task, userId) =>
        {
            if (string.IsNullOrWhiteSpace(request.RecipientId) || !_people.ContainsKey(request.RecipientId.Trim()))
                return TaskActionResult.BadRequest("Valid recipient is required.");

            if (string.IsNullOrWhiteSpace(request.Question))
                return TaskActionResult.BadRequest("Question is required.");

            var infoRequest = new InformationRequestData
            {
                Id = $"IR-{Guid.NewGuid():N}"[..10],
                FromId = userId,
                ToId = request.RecipientId.Trim(),
                Question = request.Question.Trim(),
                Status = "Waiting",
                CreatedAt = DateTime.Now
            };

            task.InformationRequests.Add(infoRequest);
            task.Status = TaskStatus.WaitingForInformation;
            task.PausedFromStatus = TaskStatus.InProgress;

            Touch(task);
            AddActivity(task, "infoRequest", userId, $"Requested info from {GetPerson(infoRequest.ToId).Name}: {infoRequest.Question}");
            AddActivity(task, "transition", userId, "Paused work → Waiting for information", statusBadge: TaskStatus.WaitingForInformation);
            return TaskActionResult.Ok("Information request created and task paused.");
        });
    }

    public TaskActionResult Resume(string id, string? currentUserId)
    {
        return Mutate(id, currentUserId, "resume", (task, userId) =>
        {
            task.Status = task.PausedFromStatus ?? TaskStatus.InProgress;
            task.PausedFromStatus = null;
            Touch(task);
            AddActivity(task, "transition", userId, "Resumed work", statusBadge: task.Status);
            return TaskActionResult.Ok("Task resumed.");
        }, bypassActionValidation: true);
    }

    public TaskActionResult Complete(string id, string? currentUserId, CompleteTaskRequest request)
    {
        return Mutate(id, currentUserId, "complete", (task, userId) =>
        {
            if (!string.IsNullOrWhiteSpace(request.ClosureSummary))
                task.ClosureSummary = request.ClosureSummary.Trim();

            if (task.Gates.Review.Required)
            {
                task.Status = TaskStatus.PendingReview;
                task.Gates.Review.Status = "pending";
                Touch(task);
                AddActivity(task, "transition", userId, "Completed work → Sent to review", statusBadge: TaskStatus.PendingReview);
                return TaskActionResult.Ok("Task sent to review.");
            }

            task.Status = TaskStatus.Closed;
            task.Gates.Review.Status = "not_required";
            Touch(task);
            AddActivity(task, "transition", userId, "Completed work → Closed", statusBadge: TaskStatus.Closed);
            return TaskActionResult.Ok("Task closed.");
        });
    }

    public TaskActionResult Reassign(string id, string? currentUserId, ReassignTaskRequest request)
    {
        return Mutate(id, currentUserId, "reassign", (task, userId) =>
        {
            if (string.IsNullOrWhiteSpace(request.AssigneeId))
                return TaskActionResult.BadRequest("Assignee is required.");

            var assigneeId = request.AssigneeId.Trim();

            if (!_people.ContainsKey(assigneeId))
                return TaskActionResult.BadRequest("Assignee not found in directory.");

            if (string.Equals(assigneeId, task.AssigneeId, StringComparison.OrdinalIgnoreCase))
                return TaskActionResult.BadRequest("Task is already assigned to this person.");

            var assignee = GetPerson(assigneeId);

            task.AssigneeId = assignee.Id;
            Touch(task);
            AddActivity(task, "transition", userId, $"Reassigned task to {assignee.Name}", statusBadge: task.Status);
            return TaskActionResult.Ok("Task reassigned.");
        });
    }

    public TaskActionResult AnswerInfoRequest(string id, string irId, string? currentUserId, AnswerInfoRequest request)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TaskActionResult.BadRequest("Task id is required.");

        if (string.IsNullOrWhiteSpace(irId))
            return TaskActionResult.BadRequest("Information request id is required.");

        if (string.IsNullOrWhiteSpace(request.Answer))
            return TaskActionResult.BadRequest("Answer is required.");

        lock (_sync)
        {
            var task = EnsureTask(id);
            var userId = ResolveUserId(task, currentUserId);

            var ir = task.InformationRequests.FirstOrDefault(x => string.Equals(x.Id, irId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ir is null)
                return TaskActionResult.NotFound("Information request not found.");

            if (ir.Status == "Closed")
                return TaskActionResult.Conflict("Information request is already answered.");

            if (!string.Equals(ir.ToId, userId, StringComparison.OrdinalIgnoreCase))
                return TaskActionResult.Forbidden("Only the request recipient can answer.");

            ir.Answer = request.Answer.Trim();
            ir.Status = "Closed";
            ir.AnsweredAt = DateTime.Now;

            Touch(task);
            AddActivity(task, "infoAnswer", userId, $"Answered information request: {ir.Answer}");
            return TaskActionResult.Ok("Information request answered.");
        }
    }

    public TaskActionResult Cancel(string id, string? currentUserId, CancelTaskRequest request)
    {
        return Mutate(id, currentUserId, "cancel", (task, userId) =>
        {
            task.CancelledFromStatus = task.Status;
            task.Status = TaskStatus.Cancelled;
            task.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Cancelled by assignee."
                : request.Reason.Trim();

            Touch(task);
            AddActivity(task, "cancellation", userId, $"Task cancelled: {task.CancellationReason}");
            return TaskActionResult.Ok("Task cancelled.");
        });
    }

    public TaskActionResult ApproveApprovalGate(string id, string? currentUserId, GateDecisionRequest request)
    {
        return Mutate(id, currentUserId, "approve", (task, userId) =>
        {
            if (!task.Gates.Approval.Required)
                return TaskActionResult.Conflict("Approval gate is not required.");

            task.Gates.Approval.Status = "approved";
            task.Gates.Approval.History.Add(new ApprovalHistoryData
            {
                Timestamp = DateTime.Now,
                Text = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Approved by {GetPerson(userId).Name}"
                    : $"Approved by {GetPerson(userId).Name}: {request.Note!.Trim()}"
            });

            task.Status = TaskStatus.PendingAcceptance;
            Touch(task);
            AddActivity(task, "transition", userId, "Approved task → Pending Acceptance", statusBadge: TaskStatus.PendingAcceptance);
            return TaskActionResult.Ok("Approval decision saved.");
        });
    }

    public TaskActionResult RejectApprovalGate(string id, string? currentUserId, GateDecisionRequest request)
    {
        return Mutate(id, currentUserId, "reject", (task, userId) =>
        {
            if (!task.Gates.Approval.Required)
                return TaskActionResult.Conflict("Approval gate is not required.");

            task.Gates.Approval.Status = "rejected";
            task.Gates.Approval.History.Add(new ApprovalHistoryData
            {
                Timestamp = DateTime.Now,
                Text = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Rejected by {GetPerson(userId).Name}"
                    : $"Rejected by {GetPerson(userId).Name}: {request.Note!.Trim()}"
            });

            task.CancelledFromStatus = TaskStatus.PendingApproval;
            task.Status = TaskStatus.Cancelled;
            task.CancellationReason = string.IsNullOrWhiteSpace(request.Note)
                ? "Rejected during approval gate."
                : request.Note.Trim();

            Touch(task);
            AddActivity(task, "cancellation", userId, $"Task cancelled: {task.CancellationReason}");
            return TaskActionResult.Ok("Task rejected and cancelled.");
        });
    }

    public TaskActionResult ApproveReviewGate(string id, string? currentUserId, GateDecisionRequest request)
    {
        return Mutate(id, currentUserId, "approve", (task, userId) =>
        {
            if (!task.Gates.Review.Required)
                return TaskActionResult.Conflict("Review gate is not required.");

            task.Gates.Review.Status = "approved";
            task.Gates.Review.History.Add(new ReviewHistoryData
            {
                Timestamp = DateTime.Now,
                Text = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Approved by {GetPerson(userId).Name}"
                    : $"Approved by {GetPerson(userId).Name}: {request.Note!.Trim()}"
            });
            task.Status = TaskStatus.Closed;
            Touch(task);

            if (!string.IsNullOrWhiteSpace(request.Note))
                AddActivity(task, "comment", userId, request.Note.Trim());

            AddActivity(task, "transition", userId, "Review approved → Closed", statusBadge: TaskStatus.Closed);
            return TaskActionResult.Ok("Review approved.");
        });
    }

    public TaskActionResult RejectReviewGate(string id, string? currentUserId, GateDecisionRequest request)
    {
        return Mutate(id, currentUserId, "reject", (task, userId) =>
        {
            if (!task.Gates.Review.Required)
                return TaskActionResult.Conflict("Review gate is not required.");

            task.Gates.Review.Status = "rejected";
            task.Gates.Review.History.Add(new ReviewHistoryData
            {
                Timestamp = DateTime.Now,
                Text = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Rejected by {GetPerson(userId).Name}"
                    : $"Rejected by {GetPerson(userId).Name}: {request.Note!.Trim()}"
            });
            task.Status = TaskStatus.InProgress;
            Touch(task);

            if (!string.IsNullOrWhiteSpace(request.Note))
                AddActivity(task, "comment", userId, request.Note.Trim());

            AddActivity(task, "transition", userId, "Review rejected → Back to In Progress", statusBadge: TaskStatus.InProgress);
            return TaskActionResult.Ok("Review rejected.");
        });
    }

    private TaskActionResult Mutate(
        string id,
        string? currentUserId,
        string actionId,
        Func<TaskData, string, TaskActionResult> operation,
        bool bypassActionValidation = false)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TaskActionResult.BadRequest("Task id is required.");

        lock (_sync)
        {
            var task = EnsureTask(id);
            var userId = ResolveUserId(task, currentUserId);

            if (!bypassActionValidation)
            {
                var validation = ValidateAction(task, userId, actionId);
                if (!validation.Success)
                    return validation;
            }
            else
            {
                var role = ResolveUserRole(task, userId);
                if (actionId == "resume" && task.Status != TaskStatus.WaitingForInformation)
                    return TaskActionResult.Conflict("Resume is only available while waiting for information.");

                if (actionId == "resume" && role != "assignee")
                    return TaskActionResult.Forbidden("Only assignee can resume work.");
            }

            return operation(task, userId);
        }
    }

    private TaskActionResult ValidateAction(TaskData task, string userId, string actionId)
    {
        var role = ResolveUserRole(task, userId);
        var blockers = BuildBlockers(task);
        var actions = BuildAvailableActions(task, role, blockers);

        var action = actions.FirstOrDefault(a => string.Equals(a.Id, actionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
            return TaskActionResult.Forbidden("Action is not available for current role and status.");

        if (action.IsBlocked)
            return TaskActionResult.Conflict(action.BlockReason ?? "Action is blocked.");

        return TaskActionResult.Ok();
    }

    private TaskData EnsureTask(string id)
    {
        if (_tasks.TryGetValue(id, out var task))
            return task;

        task = BuildVariantTask(id);
        _tasks[id] = task;
        return task;
    }

    private TaskDetailResponse BuildResponse(TaskData task, string userId)
    {
        var blockers = BuildBlockers(task);
        var role = ResolveUserRole(task, userId);
        var availableActions = BuildAvailableActions(task, role, blockers);
        var lifecycle = BuildLifecycle(task);

        var checklistCompleted = task.Checklist.Count(x => x.IsCompleted);

        return new TaskDetailResponse
        {
            Id = task.Id,
            Title = task.Title,
            Status = ToStatusCode(task.Status),
            Urgency = ComputeUrgency(task.DueDate, task.Status),
            DueDate = task.DueDate,
            PlanDate = task.PlanDate,

            Assignee = ToPerson(task.AssigneeId, isPlanned: task.Status == TaskStatus.PendingApproval),
            Creator = ToPerson(task.CreatorId),
            Reviewer = task.ReviewerId is null ? null : ToPerson(task.ReviewerId),
            Approver = task.Gates.Approval.Required && task.ApproverId is not null
                ? ToPerson(task.ApproverId)
                : null,

            UserRole = role,

            AvailableActions = availableActions,
            Blockers = blockers,
            Lifecycle = lifecycle,

            Description = task.Description,
            BusinessContext = task.BusinessContext,
            ClosureSummary = task.ClosureSummary,

            Subtasks = task.Subtasks.Select(ToSubtask).ToList(),
            Checklist = new TaskChecklist
            {
                Total = task.Checklist.Count,
                Completed = checklistCompleted,
                IsBlocking = false,
                Items = task.Checklist.Select(x => new TaskChecklistItem
                {
                    Id = x.Id,
                    Text = x.Text,
                    IsCompleted = x.IsCompleted
                }).ToList()
            },
            Dependencies = task.Dependencies.Select(ToDependency).ToList(),
            InformationRequests = task.InformationRequests
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToInformationRequest)
                .ToList(),

            Gates = new TaskGates
            {
                Approval = new TaskApprovalGate
                {
                    Required = task.Gates.Approval.Required,
                    Status = task.Gates.Approval.Required ? task.Gates.Approval.Status : "not_required",
                    AssignedTo = task.Gates.Approval.Required && task.ApproverId is not null
                        ? ToPerson(task.ApproverId)
                        : null,
                    History = task.Gates.Approval.History
                        .OrderBy(x => x.Timestamp)
                        .Select(x => new TaskApprovalHistoryItem { Timestamp = x.Timestamp, Text = x.Text })
                        .ToList()
                },
                Review = new TaskReviewGate
                {
                    Required = task.Gates.Review.Required,
                    Status = task.Gates.Review.Required ? task.Gates.Review.Status : "not_required",
                    AssignedTo = task.ReviewerId is null ? null : ToPerson(task.ReviewerId),
                    History = task.Gates.Review.History
                        .OrderBy(x => x.Timestamp)
                        .Select(x => new TaskReviewHistoryItem { Timestamp = x.Timestamp, Text = x.Text })
                        .ToList()
                }
            },
            TimeLogged = new TaskTimeLogged
            {
                TotalFormatted = FormatHours(task.TimeEntries.Sum(x => x.DurationMinutes)),
                IsPaused = task.Status == TaskStatus.WaitingForInformation,
                Entries = task.TimeEntries
                    .OrderBy(x => x.Date)
                    .Select(x => new TaskTimeEntry
                    {
                        Date = x.Date,
                        Description = x.Description,
                        DurationFormatted = FormatHours(x.DurationMinutes)
                    })
                    .ToList()
            },
            Activity = task.Activity
                .OrderByDescending(x => x.Timestamp)
                .Select(ToActivity)
                .ToList(),
            Metadata = new TaskMetadata
            {
                Domain = task.Metadata.Domain,
                Source = task.Metadata.Source,
                Relation = task.Metadata.Relation,
                PlanDate = task.PlanDate,
                CreatedAt = task.Metadata.CreatedAt,
                LastUpdatedAt = task.Metadata.LastUpdatedAt,
                Tags = task.Metadata.Tags,
                Watchers = task.Metadata.WatcherIds.Select(x => new TaskWatcher { Initials = GetPerson(x).Initials }).ToList()
            },
            Directory = _people.Values
                .Where(p => !string.Equals(p.Id, "system", StringComparison.OrdinalIgnoreCase))
                .Select(p => new TaskPerson
                {
                    Id = p.Id,
                    Name = p.Name,
                    Initials = p.Initials,
                    IsPlanned = false
                })
                .OrderBy(p => p.Name)
                .ToList(),
            CancellationReason = task.CancellationReason
        };
    }

    private IReadOnlyList<TaskAvailableAction> BuildAvailableActions(TaskData task, string role, TaskBlockers blockers)
    {
        var actions = new List<TaskAvailableAction>();

        void Add(string id, string label, IReadOnlyList<TaskBlockerItem>? blockerSource = null)
        {
            var blockedItems = blockerSource ?? Array.Empty<TaskBlockerItem>();
            actions.Add(new TaskAvailableAction
            {
                Id = id,
                Label = label,
                IsBlocked = blockedItems.Count > 0,
                BlockReason = BuildBlockReason(blockedItems)
            });
        }

        switch (task.Status)
        {
            case TaskStatus.PendingApproval:
                if (role == "approver")
                {
                    Add("approve", "Approve");
                    Add("reject", "Reject");
                }
                if (role == "assignee")
                {
                    Add("cancel", "Withdraw");
                }
                break;

            case TaskStatus.PendingAcceptance:
                if (role == "assignee")
                {
                    Add("accept", "Accept");
                    Add("reassign", "Reassign");
                }
                break;

            case TaskStatus.Open:
                if (role == "assignee")
                {
                    Add("plan", "Plan");
                    Add("start", "Start Work", blockers.StartingWork);
                    Add("reassign", "Reassign");
                }
                break;

            case TaskStatus.Planned:
                if (role == "assignee")
                {
                    Add("start", "Start Work", blockers.StartingWork);
                    Add("reassign", "Reassign");
                }
                break;

            case TaskStatus.InProgress:
                if (role == "assignee")
                {
                    Add("complete", "Complete", blockers.Completion);
                    Add("logTime", "Log Time");
                    Add("requestInfo", "Request Info");
                    Add("reassign", "Reassign");
                    Add("cancel", "Cancel Task");
                }
                break;

            case TaskStatus.WaitingForInformation:
                if (role == "assignee")
                {
                    Add("resume", "Resume Work");
                    Add("reassign", "Reassign");
                    Add("cancel", "Cancel Task");
                }
                break;

            case TaskStatus.PendingReview:
                if (role == "reviewer")
                {
                    Add("approve", "Approve");
                    Add("reject", "Reject");
                }
                break;

            case TaskStatus.Closed:
            case TaskStatus.Cancelled:
                break;
        }

        return actions;
    }

    private static string? BuildBlockReason(IReadOnlyList<TaskBlockerItem> blockers)
    {
        if (blockers.Count == 0)
            return null;

        if (blockers.Count == 1)
        {
            var b = blockers[0];
            if (string.Equals(b.Type, "dependency", StringComparison.OrdinalIgnoreCase))
                return "Blocked by 1 unresolved dependency — see details above";

            if (string.Equals(b.Type, "subtasks", StringComparison.OrdinalIgnoreCase))
                return $"Blocked by {b.Count ?? 1} open subtask(s) — complete them first";

            if (string.Equals(b.Type, "infoRequests", StringComparison.OrdinalIgnoreCase))
                return $"Blocked by {b.Count ?? 1} unanswered information request(s) — recipients must answer first";

            return "Blocked by 1 condition — see details above";
        }

        return $"Blocked by {blockers.Count} conditions — see details above";
    }

    private TaskBlockers BuildBlockers(TaskData task)
    {
        var startingWork = task.Dependencies
            .Where(x => x.Relation == DependencyRelation.BlockedBy && x.IsActive && x.Type is DependencyType.FS or DependencyType.SS)
            .Select(ToDependencyBlocker)
            .ToList();

        var completion = new List<TaskBlockerItem>();

        var openSubtasks = task.Subtasks.Where(x => !IsClosedLike(x.Status)).ToList();
        if (openSubtasks.Count > 0)
        {
            completion.Add(new TaskBlockerItem
            {
                Type = "subtasks",
                Count = openSubtasks.Count,
                Items = openSubtasks.Select(ToSubtask).ToList()
            });
        }

        completion.AddRange(task.Dependencies
            .Where(x => x.Relation == DependencyRelation.BlockedBy && x.IsActive && x.Type is DependencyType.FF or DependencyType.SF)
            .Select(ToDependencyBlocker));

        var openInfoRequests = task.InformationRequests.Where(x => x.Status == "Waiting").ToList();
        if (openInfoRequests.Count > 0)
        {
            completion.Add(new TaskBlockerItem
            {
                Type = "infoRequests",
                Count = openInfoRequests.Count
            });
        }

        return new TaskBlockers
        {
            StartingWork = startingWork,
            Completion = completion
        };
    }

    private TaskLifecycle BuildLifecycle(TaskData task)
    {
        var stepDefs = task.Gates.Approval.Required
            ? new[]
            {
                new StepDef("pendingApproval", "Pending Approval", TaskStatus.PendingApproval),
                new StepDef("pendingAcceptance", "Pending Acceptance", TaskStatus.PendingAcceptance),
                new StepDef("open", "Open", TaskStatus.Open),
                new StepDef("planned", "Planned", TaskStatus.Planned),
                new StepDef("inProgress", "In Progress", TaskStatus.InProgress),
                new StepDef("pendingReview", "Pending Review", TaskStatus.PendingReview),
                new StepDef("closed", "Closed", TaskStatus.Closed)
            }
            : new[]
            {
                new StepDef("pendingAcceptance", "Pending Acceptance", TaskStatus.PendingAcceptance),
                new StepDef("open", "Open", TaskStatus.Open),
                new StepDef("planned", "Planned", TaskStatus.Planned),
                new StepDef("inProgress", "In Progress", TaskStatus.InProgress),
                new StepDef("pendingReview", "Pending Review", TaskStatus.PendingReview),
                new StepDef("closed", "Closed", TaskStatus.Closed)
            };

        var anchorStatus = task.Status;
        if (task.Status == TaskStatus.WaitingForInformation)
            anchorStatus = task.PausedFromStatus ?? TaskStatus.InProgress;

        if (task.Status == TaskStatus.Cancelled)
            anchorStatus = task.CancelledFromStatus ?? TaskStatus.InProgress;

        var anchorIndex = Array.FindIndex(stepDefs, x => x.Status == anchorStatus);
        if (anchorIndex < 0)
            anchorIndex = 0;

        var steps = new List<TaskLifecycleStep>();
        for (var i = 0; i < stepDefs.Length; i++)
        {
            var state = "upcoming";

            if (task.Status == TaskStatus.Cancelled)
            {
                state = i <= anchorIndex ? "completed" : "upcoming";
            }
            else if (i < anchorIndex)
            {
                state = "completed";
            }
            else if (i == anchorIndex)
            {
                state = "active";
            }

            steps.Add(new TaskLifecycleStep
            {
                Id = stepDefs[i].Id,
                Label = stepDefs[i].Label,
                State = state
            });
        }

        return new TaskLifecycle
        {
            HasApprovalGate = task.Gates.Approval.Required,
            IsPaused = task.Status == TaskStatus.WaitingForInformation,
            IsCancelled = task.Status == TaskStatus.Cancelled,
            Steps = steps
        };
    }

    private string ResolveUserRole(TaskData task, string userId)
    {
        var isAssignee = string.Equals(userId, task.AssigneeId, StringComparison.OrdinalIgnoreCase);
        var isReviewer = !string.IsNullOrWhiteSpace(task.ReviewerId) && string.Equals(userId, task.ReviewerId, StringComparison.OrdinalIgnoreCase);
        var isApprover = !string.IsNullOrWhiteSpace(task.ApproverId) && string.Equals(userId, task.ApproverId, StringComparison.OrdinalIgnoreCase);

        // When status requires reviewer action, prioritize reviewer over assignee to prevent deadlock
        if (task.Status == TaskStatus.PendingReview && isReviewer)
            return "reviewer";

        // When status requires approver action, prioritize approver
        if (task.Status == TaskStatus.PendingApproval && isApprover)
            return "approver";

        if (isAssignee) return "assignee";
        if (isReviewer) return "reviewer";
        if (isApprover) return "approver";

        return "viewer";
    }

    private string ResolveUserId(TaskData task, string? currentUserId)
    {
        if (!string.IsNullOrWhiteSpace(currentUserId))
            return currentUserId.Trim();

        return task.AssigneeId;
    }

    private TaskBlockerItem ToDependencyBlocker(DependencyData dependency)
    {
        return new TaskBlockerItem
        {
            Type = "dependency",
            TaskId = dependency.Id,
            Title = dependency.Title,
            DependencyType = dependency.Type.ToString(),
            Status = ToStatusCode(dependency.Status)
        };
    }

    private TaskSubtask ToSubtask(SubtaskData subtask)
    {
        return new TaskSubtask
        {
            Id = subtask.Id,
            Title = subtask.Title,
            Status = ToStatusCode(subtask.Status),
            Assignee = ToPerson(subtask.AssigneeId)
        };
    }

    private TaskDependency ToDependency(DependencyData dependency)
    {
        return new TaskDependency
        {
            Id = dependency.Id,
            Title = dependency.Title,
            Relation = dependency.Relation == DependencyRelation.BlockedBy ? "BlockedBy" : "Blocks",
            Type = dependency.Type.ToString(),
            Status = ToStatusCode(dependency.Status),
            IsActive = dependency.IsActive
        };
    }

    private TaskInformationRequest ToInformationRequest(InformationRequestData request)
    {
        return new TaskInformationRequest
        {
            Id = request.Id,
            From = ToPerson(request.FromId),
            To = ToPerson(request.ToId),
            Question = request.Question,
            Answer = request.Answer,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            AnsweredAt = request.AnsweredAt
        };
    }

    private TaskActivityItem ToActivity(ActivityData activity)
    {
        return new TaskActivityItem
        {
            Type = activity.Type,
            Actor = ToPerson(activity.ActorId),
            Timestamp = activity.Timestamp,
            StatusBadge = activity.StatusBadge is null ? null : ToStatusCode(activity.StatusBadge.Value),
            Text = activity.Text
        };
    }

    private TaskPerson ToPerson(string id, bool isPlanned = false)
    {
        var person = GetPerson(id);
        return new TaskPerson
        {
            Id = person.Id,
            Name = person.Name,
            Initials = person.Initials,
            IsPlanned = isPlanned
        };
    }

    private PersonData GetPerson(string id)
    {
        return _people.TryGetValue(id, out var person) ? person : GetOrCreatePerson(id);
    }

    private PersonData GetOrCreatePerson(string id)
    {
        if (_people.TryGetValue(id, out var person))
            return person;

        var cleanId = id.Trim();
        var segments = cleanId.Split(['-', ' ', '_'], StringSplitOptions.RemoveEmptyEntries);
        var display = segments.Length == 0
            ? cleanId
            : string.Join(' ', segments.Select(s => char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()));

        var initials = string.Concat(display.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
        person = new PersonData(cleanId, display, string.IsNullOrWhiteSpace(initials) ? "NA" : initials);
        _people[cleanId] = person;
        return person;
    }

    private void AddActivity(TaskData task, string type, string actorId, string text, TaskStatus? statusBadge = null)
    {
        task.Activity.Add(new ActivityData
        {
            Type = type,
            ActorId = actorId,
            Timestamp = DateTime.Now,
            StatusBadge = statusBadge,
            Text = text
        });
    }

    private static bool TryParseDuration(string? raw, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim().ToLowerInvariant();

        if (value.Contains(':'))
        {
            var parts = value.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
            {
                minutes = (h * 60) + m;
                return true;
            }
        }

        if (value.EndsWith("h") && double.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hours))
        {
            minutes = (int)Math.Round(hours * 60);
            return true;
        }

        if (value.EndsWith("m") && int.TryParse(value[..^1], out var mins))
        {
            minutes = mins;
            return true;
        }

        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var decimalHours))
        {
            minutes = (int)Math.Round(decimalHours * 60);
            return true;
        }

        return false;
    }

    private static string FormatHours(int minutes)
    {
        if (minutes <= 0)
            return "0h";

        var hours = minutes / 60;
        var remaining = minutes % 60;

        if (remaining == 0)
            return $"{hours}h";

        if (remaining == 30)
            return $"{hours + 0.5:0.#}h";

        return $"{hours}h {remaining}m";
    }

    private static string? ComputeUrgency(DateOnly? dueDate, TaskStatus status)
    {
        if (!dueDate.HasValue || status is TaskStatus.Closed or TaskStatus.Cancelled)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var diffDays = dueDate.Value.DayNumber - today.DayNumber;

        if (diffDays > 3)
            return "OnTrack";

        if (diffDays >= 1 && diffDays <= 3)
            return "DueSoon";

        return "Overdue";
    }

    private static bool IsClosedLike(TaskStatus status) =>
        status is TaskStatus.Closed or TaskStatus.Cancelled;

    private static string ToStatusCode(TaskStatus status)
    {
        return status switch
        {
            TaskStatus.PendingApproval => "PendingApproval",
            TaskStatus.PendingAcceptance => "PendingAcceptance",
            TaskStatus.Open => "Open",
            TaskStatus.Planned => "Planned",
            TaskStatus.InProgress => "InProgress",
            TaskStatus.WaitingForInformation => "WaitingForInformation",
            TaskStatus.PendingReview => "PendingReview",
            TaskStatus.Closed => "Closed",
            TaskStatus.Cancelled => "Cancelled",
            _ => "Open"
        };
    }

    private static void Touch(TaskData task)
    {
        task.Metadata.LastUpdatedAt = DateTime.Now;
    }

    private TaskData BuildVariantTask(string id)
    {
        var sample = BuildCanonicalTask(id);
        if (string.Equals(id, "TASK-2847", StringComparison.OrdinalIgnoreCase))
            return sample;

        if (string.Equals(id, "TASK-AUDIT-NOREVIEW", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureInProgressCompletionReady(sample, reviewRequired: false);
            return sample;
        }

        if (string.Equals(id, "TASK-AUDIT-COMPLETE-OK", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureInProgressCompletionReady(sample, reviewRequired: true);
            return sample;
        }

        if (string.Equals(id, "TASK-AUDIT-SS-BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureOpenWithSsStartBlocker(sample);
            return sample;
        }

        var digit = new string(id.Where(char.IsDigit).ToArray());
        var numeric = int.TryParse(digit, out var parsed) ? parsed : id.Length;
        
        var variant = numeric % 9;
        
        if (numeric >= 4100 && numeric <= 4999)
        {
            variant = (numeric / 100) switch
            {
                41 => 0,
                42 => 1,
                43 => 2,
                44 => 3,
                45 => 4,
                46 => 5,
                47 => 6,
                48 => 7,
                49 => 8,
                _ => variant
            };
        }

        sample.Status = variant switch
        {
            0 => TaskStatus.PendingApproval,
            1 => TaskStatus.PendingAcceptance,
            2 => TaskStatus.Open,
            3 => TaskStatus.Planned,
            4 => TaskStatus.InProgress,
            5 => TaskStatus.WaitingForInformation,
            6 => TaskStatus.PendingReview,
            7 => TaskStatus.Closed,
            _ => TaskStatus.Cancelled
        };

        NormalizeVariantData(sample);

        return sample;
    }

    private static void NormalizeVariantData(TaskData task)
    {
        static List<SubtaskData> CreateExecutionSubtasks() =>
        [
            new SubtaskData { Id = "TASK-2848", Title = "Define rate limit policies per client tier", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" },
            new SubtaskData { Id = "TASK-2849", Title = "Implement Redis-based rate counter", Status = TaskStatus.InProgress, AssigneeId = "burak-sari" },
            new SubtaskData { Id = "TASK-2850", Title = "Add 429 response handler with retry-after", Status = TaskStatus.Open, AssigneeId = "elif-kaya" }
        ];

        static List<TimeEntryData> CreateExecutionTime() =>
        [
            new TimeEntryData { Date = new DateOnly(2026, 3, 25), Description = "Initial research and policy draft", DurationMinutes = 120 },
            new TimeEntryData { Date = new DateOnly(2026, 3, 26), Description = "Redis counter implementation", DurationMinutes = 210 },
            new TimeEntryData { Date = new DateOnly(2026, 3, 28), Description = "Testing and debugging", DurationMinutes = 90 }
        ];

        static List<DependencyData> CreateInProgressDependencies() =>
        [
            new DependencyData
            {
                Id = "TASK-2801",
                Title = "Redis cluster setup for production",
                Relation = DependencyRelation.BlockedBy,
                Type = DependencyType.FS,
                Status = TaskStatus.InProgress,
                IsActive = true
            },
            new DependencyData
            {
                Id = "TASK-2870",
                Title = "Performance baseline verification",
                Relation = DependencyRelation.BlockedBy,
                Type = DependencyType.FF,
                Status = TaskStatus.InProgress,
                IsActive = true
            },
            new DependencyData
            {
                Id = "TASK-2860",
                Title = "Client SDK retry logic update",
                Relation = DependencyRelation.Blocks,
                Type = DependencyType.FS,
                Status = TaskStatus.Open,
                IsActive = true
            }
        ];

        static List<ActivityData> CreatePendingApprovalActivity() =>
        [
            new ActivityData
            {
                Type = "transition",
                ActorId = "system",
                Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                StatusBadge = TaskStatus.PendingApproval,
                Text = "Task created — requires approval before assignment"
            },
            new ActivityData
            {
                Type = "transition",
                ActorId = "system",
                Timestamp = new DateTime(2026, 3, 20, 9, 0, 20),
                StatusBadge = TaskStatus.PendingApproval,
                Text = "Awaiting approval from Selin Korkmaz"
            }
        ];

        task.PausedFromStatus = null;
        task.CancelledFromStatus = null;
        task.CancellationReason = null;
        task.ClosureSummary = null;
        task.Gates.Approval.History = [];

        task.ApproverId = task.Status == TaskStatus.PendingApproval ? "selin-korkmaz" : null;
        task.Gates.Approval.Required = task.Status == TaskStatus.PendingApproval;
        task.Gates.Approval.Status = task.Status == TaskStatus.PendingApproval ? "pending" : "not_required";
        task.Gates.Review.Required = true;
        task.Gates.Review.Status = task.Status switch
        {
            TaskStatus.PendingReview => "pending",
            TaskStatus.Closed => "approved",
            _ => "pending"
        };

        switch (task.Status)
        {
            case TaskStatus.PendingApproval:
                task.PlanDate = null;
                task.TimeEntries = [];
                task.InformationRequests = [];
                task.Subtasks = [];
                task.Dependencies = [];
                task.Activity = CreatePendingApprovalActivity();
                task.Gates.Approval.History =
                [
                    new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 0), Text = "Task created — requires approval before assignment" },
                    new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 20), Text = "Awaiting approval from Selin Korkmaz" }
                ];
                break;

            case TaskStatus.PendingAcceptance:
                task.PlanDate = null;
                task.TimeEntries = [];
                task.InformationRequests = [];
                task.Subtasks = [];
                task.Dependencies = [];
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 15),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Waiting for assignee acceptance"
                    }
                ];
                break;

            case TaskStatus.Open:
                task.PlanDate = null;
                task.TimeEntries = [];
                task.InformationRequests = [];
                task.Subtasks = [];
                task.Dependencies =
                [
                    new DependencyData
                    {
                        Id = "TASK-2801",
                        Title = "Redis cluster setup for production",
                        Relation = DependencyRelation.BlockedBy,
                        Type = DependencyType.FS,
                        Status = TaskStatus.InProgress,
                        IsActive = true
                    },
                    new DependencyData
                    {
                        Id = "TASK-2860",
                        Title = "Client SDK retry logic update",
                        Relation = DependencyRelation.Blocks,
                        Type = DependencyType.FS,
                        Status = TaskStatus.Open,
                        IsActive = true
                    }
                ];
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    }
                ];
                break;

            case TaskStatus.Planned:
                task.TimeEntries = [];
                task.InformationRequests = [];
                task.Subtasks = [];
                task.Dependencies =
                [
                    new DependencyData
                    {
                        Id = "TASK-2801",
                        Title = "Redis cluster setup for production",
                        Relation = DependencyRelation.BlockedBy,
                        Type = DependencyType.FS,
                        Status = TaskStatus.InProgress,
                        IsActive = true
                    }
                ];
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 25, 8, 45, 0),
                        StatusBadge = TaskStatus.Planned,
                        Text = "Planned task for 25.03.2026"
                    }
                ];
                break;

            case TaskStatus.InProgress:
                task.TimeEntries = CreateExecutionTime();
                task.InformationRequests = [];
                task.Subtasks = CreateExecutionSubtasks();
                task.Dependencies = CreateInProgressDependencies();
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 25, 9, 0, 0),
                        StatusBadge = TaskStatus.InProgress,
                        Text = "Started work"
                    },
                    new ActivityData
                    {
                        Type = "timeLog",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 28, 17, 30, 0),
                        Text = "Logged 1.5h — Testing and debugging"
                    }
                ];
                break;

            case TaskStatus.WaitingForInformation:
                task.PausedFromStatus = TaskStatus.InProgress;
                task.TimeEntries = CreateExecutionTime();
                task.Subtasks = CreateExecutionSubtasks();
                task.Dependencies = CreateInProgressDependencies();
                task.InformationRequests =
                [
                    new InformationRequestData
                    {
                        Id = "IR-2001",
                        FromId = "elif-kaya",
                        ToId = "mert-demir",
                        Question = "What should be the burst limit for enterprise tier clients?",
                        Status = "Waiting",
                        CreatedAt = new DateTime(2026, 3, 28, 10, 15, 0)
                    },
                    new InformationRequestData
                    {
                        Id = "IR-2002",
                        FromId = "elif-kaya",
                        ToId = "zeynep-arslan",
                        Question = "Need the finalized client tier classification document.",
                        Status = "Waiting",
                        CreatedAt = new DateTime(2026, 3, 29, 9, 40, 0)
                    }
                ];
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 25, 9, 0, 0),
                        StatusBadge = TaskStatus.InProgress,
                        Text = "Started work"
                    },
                    new ActivityData
                    {
                        Type = "infoRequest",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 28, 10, 15, 0),
                        Text = "Requested info from Mert Demir: Burst limit confirmation"
                    }
                ];
                break;

            case TaskStatus.PendingReview:
                task.ClosureSummary = "Implementation completed and awaiting reviewer decision.";
                task.TimeEntries = CreateExecutionTime();
                task.InformationRequests = [];
                task.Subtasks =
                [
                    new SubtaskData { Id = "TASK-2848", Title = "Define rate limit policies per client tier", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" },
                    new SubtaskData { Id = "TASK-2849", Title = "Implement Redis-based rate counter", Status = TaskStatus.Closed, AssigneeId = "burak-sari" },
                    new SubtaskData { Id = "TASK-2850", Title = "Add 429 response handler with retry-after", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" }
                ];
                task.Dependencies =
                [
                    new DependencyData
                    {
                        Id = "TASK-2860",
                        Title = "Client SDK retry logic update",
                        Relation = DependencyRelation.Blocks,
                        Type = DependencyType.FS,
                        Status = TaskStatus.Open,
                        IsActive = true
                    }
                ];
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 25, 9, 0, 0),
                        StatusBadge = TaskStatus.InProgress,
                        Text = "Started work"
                    },
                    new ActivityData
                    {
                        Type = "timeLog",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 28, 17, 30, 0),
                        Text = "Logged 1.5h — Testing and debugging"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 30, 10, 0, 0),
                        StatusBadge = TaskStatus.PendingReview,
                        Text = "Completed work → Sent to review"
                    }
                ];
                break;

            case TaskStatus.Closed:
                task.ClosureSummary = "Implementation completed and accepted by reviewer.";
                task.TimeEntries = CreateExecutionTime();
                task.InformationRequests = [];
                task.Subtasks =
                [
                    new SubtaskData { Id = "TASK-2848", Title = "Define rate limit policies per client tier", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" },
                    new SubtaskData { Id = "TASK-2849", Title = "Implement Redis-based rate counter", Status = TaskStatus.Closed, AssigneeId = "burak-sari" },
                    new SubtaskData { Id = "TASK-2850", Title = "Add 429 response handler with retry-after", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" }
                ];
                task.Dependencies =
                [
                    new DependencyData
                    {
                        Id = "TASK-2860",
                        Title = "Client SDK retry logic update",
                        Relation = DependencyRelation.Blocks,
                        Type = DependencyType.FS,
                        Status = TaskStatus.Open,
                        IsActive = true
                    }
                ];
                task.Gates.Review.Status = "approved";
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingAcceptance,
                        Text = "Task created"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                        StatusBadge = TaskStatus.Open,
                        Text = "Task accepted"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 25, 9, 0, 0),
                        StatusBadge = TaskStatus.InProgress,
                        Text = "Started work"
                    },
                    new ActivityData
                    {
                        Type = "timeLog",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 28, 17, 30, 0),
                        Text = "Logged 1.5h — Testing and debugging"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "elif-kaya",
                        Timestamp = new DateTime(2026, 3, 30, 10, 0, 0),
                        StatusBadge = TaskStatus.PendingReview,
                        Text = "Completed work → Sent to review"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "ahmet-yilmaz",
                        Timestamp = new DateTime(2026, 3, 30, 14, 0, 0),
                        StatusBadge = TaskStatus.Closed,
                        Text = "Review approved → Closed"
                    }
                ];
                break;

            case TaskStatus.Cancelled:
                task.PlanDate = null;
                task.ApproverId = "selin-korkmaz";
                task.ReviewerId = null;
                task.Gates.Approval.Required = true;
                task.Gates.Approval.Status = "rejected";
                task.Gates.Approval.History =
                [
                    new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 0), Text = "Task created — requires approval before assignment" },
                    new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 20), Text = "Awaiting approval from Selin Korkmaz" },
                    new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 15, 0), Text = "Rejected by Selin Korkmaz: Scope is not approved for this cycle." }
                ];
                task.Gates.Review.Required = false;
                task.Gates.Review.Status = "not_required";

                task.ClosureSummary = null;
                task.TimeEntries = [];
                task.InformationRequests = [];
                task.Subtasks = [];
                task.Dependencies = [];
                task.CancelledFromStatus = TaskStatus.PendingApproval;
                task.CancellationReason = "Rejected during approval gate.";
                task.Activity =
                [
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                        StatusBadge = TaskStatus.PendingApproval,
                        Text = "Task created — requires approval before assignment"
                    },
                    new ActivityData
                    {
                        Type = "transition",
                        ActorId = "system",
                        Timestamp = new DateTime(2026, 3, 20, 9, 0, 20),
                        StatusBadge = TaskStatus.PendingApproval,
                        Text = "Awaiting approval from Selin Korkmaz"
                    },
                    new ActivityData
                    {
                        Type = "cancellation",
                        ActorId = "selin-korkmaz",
                        Timestamp = new DateTime(2026, 3, 20, 9, 15, 0),
                        StatusBadge = TaskStatus.Cancelled,
                        Text = "Task cancelled: Rejected during approval gate."
                    }
                ];
                break;
        }
    }

    private static void ConfigureInProgressCompletionReady(TaskData sample, bool reviewRequired)
    {
        sample.Status = TaskStatus.InProgress;
        sample.Gates.Approval.Required = false;
        sample.Gates.Approval.Status = "not_required";
        sample.ApproverId = null;

        sample.Subtasks.ForEach(s => s.Status = TaskStatus.Closed);
        sample.Dependencies = sample.Dependencies
            .Where(d => d.Relation == DependencyRelation.Blocks)
            .ToList();

        sample.Gates.Review.Required = reviewRequired;
        sample.Gates.Review.Status = reviewRequired ? "pending" : "not_required";

        if (!reviewRequired)
            sample.ReviewerId = null;
    }

    private static void ConfigureOpenWithSsStartBlocker(TaskData sample)
    {
        sample.Status = TaskStatus.Open;
        sample.Gates.Approval.Required = false;
        sample.Gates.Approval.Status = "not_required";
        sample.ApproverId = null;

        sample.Dependencies =
        [
            new DependencyData
            {
                Id = "TASK-SS-1001",
                Title = "Shared setup kickoff alignment",
                Relation = DependencyRelation.BlockedBy,
                Type = DependencyType.SS,
                Status = TaskStatus.InProgress,
                IsActive = true
            },
            new DependencyData
            {
                Id = "TASK-2860",
                Title = "Client SDK retry logic update",
                Relation = DependencyRelation.Blocks,
                Type = DependencyType.FS,
                Status = TaskStatus.Open,
                IsActive = true
            }
        ];
    }

    private TaskData BuildCanonicalTask(string id)
    {
        var createdAt = new DateOnly(2026, 3, 20);

        return new TaskData
        {
            Id = id,
            Title = "Finalize API rate-limiting rollout",
            Status = TaskStatus.InProgress,
            DueDate = new DateOnly(2026, 4, 3),
            PlanDate = new DateOnly(2026, 3, 25),
            AssigneeId = "elif-kaya",
            CreatorId = "elif-kaya",
            ReviewerId = "ahmet-yilmaz",
            ApproverId = "selin-korkmaz",
            Description = "Introduce deterministic per-tier rate limiting, complete regression coverage and prepare rollout notes for client teams.",
            BusinessContext = "Enterprise and standard client tiers must receive predictable throttling behavior. This work protects platform reliability during peak traffic windows while keeping SLA commitments transparent.",
            ClosureSummary = null,
            CancellationReason = null,
            Subtasks =
            [
                new SubtaskData { Id = "TASK-2848", Title = "Define rate limit policies per client tier", Status = TaskStatus.Closed, AssigneeId = "elif-kaya" },
                new SubtaskData { Id = "TASK-2849", Title = "Implement Redis-based rate counter", Status = TaskStatus.InProgress, AssigneeId = "burak-sari" },
                new SubtaskData { Id = "TASK-2850", Title = "Add 429 response handler with retry-after", Status = TaskStatus.Open, AssigneeId = "elif-kaya" }
            ],
            Checklist =
            [
                new ChecklistItemData { Id = "chk-1", Text = "Review existing rate limit documentation", IsCompleted = true },
                new ChecklistItemData { Id = "chk-2", Text = "Benchmark current request volumes", IsCompleted = true },
                new ChecklistItemData { Id = "chk-3", Text = "Test with load generator", IsCompleted = false },
                new ChecklistItemData { Id = "chk-4", Text = "Update API documentation", IsCompleted = false },
                new ChecklistItemData { Id = "chk-5", Text = "Notify client teams about changes", IsCompleted = false }
            ],
            Dependencies =
            [
                new DependencyData
                {
                    Id = "TASK-2801",
                    Title = "Redis cluster setup for production",
                    Relation = DependencyRelation.BlockedBy,
                    Type = DependencyType.FS,
                    Status = TaskStatus.InProgress,
                    IsActive = true
                },
                new DependencyData
                {
                    Id = "TASK-2870",
                    Title = "Performance baseline verification",
                    Relation = DependencyRelation.BlockedBy,
                    Type = DependencyType.FF,
                    Status = TaskStatus.InProgress,
                    IsActive = true
                },
                new DependencyData
                {
                    Id = "TASK-2860",
                    Title = "Client SDK retry logic update",
                    Relation = DependencyRelation.Blocks,
                    Type = DependencyType.FS,
                    Status = TaskStatus.Open,
                    IsActive = true
                }
            ],
            InformationRequests =
            [
                new InformationRequestData
                {
                    Id = "IR-2001",
                    FromId = "elif-kaya",
                    ToId = "mert-demir",
                    Question = "What should be the burst limit for enterprise tier clients?",
                    Status = "Waiting",
                    CreatedAt = new DateTime(2026, 3, 28, 10, 15, 0)
                },
                new InformationRequestData
                {
                    Id = "IR-2002",
                    FromId = "elif-kaya",
                    ToId = "zeynep-arslan",
                    Question = "Need the finalized client tier classification document.",
                    Status = "Waiting",
                    CreatedAt = new DateTime(2026, 3, 29, 9, 40, 0)
                },
                new InformationRequestData
                {
                    Id = "IR-1999",
                    FromId = "elif-kaya",
                    ToId = "mert-demir",
                    Question = "What should be the burst limit for enterprise tier clients?",
                    Answer = "Go with 500 req/min for enterprise, 100 req/min for standard.",
                    Status = "Closed",
                    CreatedAt = new DateTime(2026, 3, 27, 11, 0, 0),
                    AnsweredAt = new DateTime(2026, 3, 28, 14, 20, 0)
                }
            ],
            Gates = new GateData
            {
                Approval = new ApprovalGateData
                {
                    Required = false,
                    Status = "not_required",
                    History =
                    [
                        new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 0), Text = "Task created — requires approval before assignment" },
                        new ApprovalHistoryData { Timestamp = new DateTime(2026, 3, 20, 9, 0, 20), Text = "Awaiting approval from Selin Korkmaz" }
                    ]
                },
                Review = new ReviewGateData
                {
                    Required = true,
                    Status = "pending"
                }
            },
            TimeEntries =
            [
                new TimeEntryData { Date = new DateOnly(2026, 3, 25), Description = "Initial research and policy draft", DurationMinutes = 120 },
                new TimeEntryData { Date = new DateOnly(2026, 3, 26), Description = "Redis counter implementation", DurationMinutes = 210 },
                new TimeEntryData { Date = new DateOnly(2026, 3, 28), Description = "Testing and debugging", DurationMinutes = 90 }
            ],
            Activity =
            [
                new ActivityData
                {
                    Type = "transition",
                    ActorId = "elif-kaya",
                    Timestamp = new DateTime(2026, 3, 30, 10, 0, 0),
                    StatusBadge = TaskStatus.PendingReview,
                    Text = "Completed work → Sent to review"
                },
                new ActivityData
                {
                    Type = "timeLog",
                    ActorId = "elif-kaya",
                    Timestamp = new DateTime(2026, 3, 28, 17, 30, 0),
                    Text = "Logged 1.5h — Testing and debugging"
                },
                new ActivityData
                {
                    Type = "comment",
                    ActorId = "mert-demir",
                    Timestamp = new DateTime(2026, 3, 27, 11, 0, 0),
                    Text = "Make sure we handle the 429 gracefully on mobile clients too."
                },
                new ActivityData
                {
                    Type = "infoRequest",
                    ActorId = "elif-kaya",
                    Timestamp = new DateTime(2026, 3, 28, 10, 15, 0),
                    Text = "Requested info from Mert Demir: Burst limit confirmation"
                },
                new ActivityData
                {
                    Type = "transition",
                    ActorId = "elif-kaya",
                    Timestamp = new DateTime(2026, 3, 25, 9, 0, 0),
                    StatusBadge = TaskStatus.InProgress,
                    Text = "Started work"
                },
                new ActivityData
                {
                    Type = "transition",
                    ActorId = "elif-kaya",
                    Timestamp = new DateTime(2026, 3, 21, 8, 30, 0),
                    StatusBadge = TaskStatus.Open,
                    Text = "Task accepted"
                },
                new ActivityData
                {
                    Type = "transition",
                    ActorId = "system",
                    Timestamp = new DateTime(2026, 3, 20, 9, 0, 0),
                    StatusBadge = TaskStatus.PendingAcceptance,
                    Text = "Task created"
                }
            ],
            Metadata = new MetadataData
            {
                Domain = "Platform Engineering",
                Source = "Sprint 42",
                Relation = "Sprint Item",
                CreatedAt = createdAt,
                LastUpdatedAt = new DateTime(2026, 3, 30, 14, 32, 0),
                Tags = ["infrastructure", "security", "high-priority"],
                WatcherIds = ["zeynep-arslan", "can-ozkan"]
            },
            PausedFromStatus = TaskStatus.InProgress,
            CancelledFromStatus = null
        };
    }

    private readonly record struct StepDef(string Id, string Label, TaskStatus Status);

    private sealed class TaskData
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public DateOnly? DueDate { get; set; }
        public DateOnly? PlanDate { get; set; }
        public string AssigneeId { get; set; } = string.Empty;
        public string CreatorId { get; set; } = string.Empty;
        public string? ReviewerId { get; set; }
        public string? ApproverId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string BusinessContext { get; set; } = string.Empty;
        public string? ClosureSummary { get; set; }
        public string? CancellationReason { get; set; }

        public List<SubtaskData> Subtasks { get; set; } = [];
        public List<ChecklistItemData> Checklist { get; set; } = [];
        public List<DependencyData> Dependencies { get; set; } = [];
        public List<InformationRequestData> InformationRequests { get; set; } = [];

        public GateData Gates { get; set; } = new();
        public List<TimeEntryData> TimeEntries { get; set; } = [];
        public List<ActivityData> Activity { get; set; } = [];

        public MetadataData Metadata { get; set; } = new();
        public TaskStatus? PausedFromStatus { get; set; }
        public TaskStatus? CancelledFromStatus { get; set; }
    }

    private sealed class SubtaskData
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public string AssigneeId { get; set; } = string.Empty;
    }

    private sealed class ChecklistItemData
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }

    private sealed class DependencyData
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DependencyRelation Relation { get; set; }
        public DependencyType Type { get; set; }
        public TaskStatus Status { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class InformationRequestData
    {
        public string Id { get; set; } = string.Empty;
        public string FromId { get; set; } = string.Empty;
        public string ToId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string? Answer { get; set; }
        public string Status { get; set; } = "Waiting";
        public DateTime CreatedAt { get; set; }
        public DateTime? AnsweredAt { get; set; }
    }

    private sealed class GateData
    {
        public ApprovalGateData Approval { get; set; } = new();
        public ReviewGateData Review { get; set; } = new();
    }

    private sealed class ApprovalGateData
    {
        public bool Required { get; set; }
        public string Status { get; set; } = "not_required";
        public List<ApprovalHistoryData> History { get; set; } = [];
    }

    private sealed class ReviewGateData
    {
        public bool Required { get; set; }
        public string Status { get; set; } = "not_required";
        public List<ReviewHistoryData> History { get; set; } = [];
    }

    private sealed class ReviewHistoryData
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed class ApprovalHistoryData
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed class TimeEntryData
    {
        public DateOnly Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
    }

    private sealed class ActivityData
    {
        public string Type { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TaskStatus? StatusBadge { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed class MetadataData
    {
        public string Domain { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public DateOnly CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public List<string> Tags { get; set; } = [];
        public List<string> WatcherIds { get; set; } = [];
    }

    private sealed record PersonData(string Id, string Name, string Initials);

    private enum TaskStatus
    {
        PendingApproval,
        PendingAcceptance,
        Open,
        Planned,
        InProgress,
        WaitingForInformation,
        PendingReview,
        Closed,
        Cancelled
    }

    private enum DependencyRelation
    {
        BlockedBy,
        Blocks
    }

    private enum DependencyType
    {
        FS,
        SS,
        FF,
        SF
    }
}
