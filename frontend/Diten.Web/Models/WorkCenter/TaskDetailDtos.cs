namespace Diten.Web.Models.WorkCenter;

public sealed class TaskDetailResponse
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Urgency { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateOnly? PlanDate { get; init; }

    public TaskPerson Assignee { get; init; } = new();
    public TaskPerson Creator { get; init; } = new();
    public TaskPerson? Reviewer { get; init; }
    public TaskPerson? Approver { get; init; }

    public string UserRole { get; init; } = "viewer";

    public IReadOnlyList<TaskAvailableAction> AvailableActions { get; init; } = [];
    public TaskBlockers Blockers { get; init; } = new();
    public TaskLifecycle Lifecycle { get; init; } = new();

    public string Description { get; init; } = string.Empty;
    public string BusinessContext { get; init; } = string.Empty;
    public string? ClosureSummary { get; init; }

    public IReadOnlyList<TaskSubtask> Subtasks { get; init; } = [];
    public TaskChecklist Checklist { get; init; } = new();
    public IReadOnlyList<TaskDependency> Dependencies { get; init; } = [];
    public IReadOnlyList<TaskInformationRequest> InformationRequests { get; init; } = [];

    public TaskGates Gates { get; init; } = new();
    public TaskTimeLogged TimeLogged { get; init; } = new();
    public IReadOnlyList<TaskActivityItem> Activity { get; init; } = [];
    public TaskMetadata Metadata { get; init; } = new();

    public IReadOnlyList<TaskPerson> Directory { get; init; } = [];
    public string? CancellationReason { get; init; }
}

public sealed class TaskPerson
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public bool IsPlanned { get; init; }
}

public sealed class TaskAvailableAction
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsBlocked { get; init; }
    public string? BlockReason { get; init; }
}

public sealed class TaskBlockers
{
    public IReadOnlyList<TaskBlockerItem> StartingWork { get; init; } = [];
    public IReadOnlyList<TaskBlockerItem> Completion { get; init; } = [];
}

public sealed class TaskBlockerItem
{
    public string Type { get; init; } = string.Empty;

    public string? TaskId { get; init; }
    public string? Title { get; init; }
    public string? DependencyType { get; init; }
    public string? Status { get; init; }

    public int? Count { get; init; }
    public IReadOnlyList<TaskSubtask>? Items { get; init; }
}

public sealed class TaskLifecycle
{
    public bool HasApprovalGate { get; init; }
    public bool IsPaused { get; init; }
    public bool IsCancelled { get; init; }
    public IReadOnlyList<TaskLifecycleStep> Steps { get; init; } = [];
}

public sealed class TaskLifecycleStep
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}

public sealed class TaskSubtask
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public TaskPerson Assignee { get; init; } = new();
}

public sealed class TaskChecklist
{
    public int Total { get; init; }
    public int Completed { get; init; }
    public bool IsBlocking { get; init; }
    public IReadOnlyList<TaskChecklistItem> Items { get; init; } = [];
}

public sealed class TaskChecklistItem
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
}

public sealed class TaskDependency
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Relation { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class TaskInformationRequest
{
    public string Id { get; init; } = string.Empty;
    public TaskPerson From { get; init; } = new();
    public TaskPerson To { get; init; } = new();
    public string Question { get; init; } = string.Empty;
    public string? Answer { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
}

public sealed class TaskGates
{
    public TaskApprovalGate Approval { get; init; } = new();
    public TaskReviewGate Review { get; init; } = new();
}

public sealed class TaskApprovalGate
{
    public bool Required { get; init; }
    public string Status { get; init; } = "not_required";
    public TaskPerson? AssignedTo { get; init; }
    public IReadOnlyList<TaskApprovalHistoryItem> History { get; init; } = [];
}

public sealed class TaskApprovalHistoryItem
{
    public DateTime Timestamp { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class TaskReviewGate
{
    public bool Required { get; init; }
    public string Status { get; init; } = "not_required";
    public TaskPerson? AssignedTo { get; init; }
    public IReadOnlyList<TaskReviewHistoryItem> History { get; init; } = [];
}

public sealed class TaskReviewHistoryItem
{
    public DateTime Timestamp { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class TaskTimeLogged
{
    public string TotalFormatted { get; init; } = "0h";
    public bool IsPaused { get; init; }
    public IReadOnlyList<TaskTimeEntry> Entries { get; init; } = [];
}

public sealed class TaskTimeEntry
{
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public string DurationFormatted { get; init; } = string.Empty;
}

public sealed class TaskActivityItem
{
    public string Type { get; init; } = string.Empty;
    public TaskPerson Actor { get; init; } = new();
    public DateTime Timestamp { get; init; }
    public string? StatusBadge { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class TaskMetadata
{
    public string Domain { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Relation { get; init; } = string.Empty;
    public DateOnly? PlanDate { get; init; }
    public DateOnly CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<TaskWatcher> Watchers { get; init; } = [];
}

public sealed class TaskWatcher
{
    public string Initials { get; init; } = string.Empty;
}

public sealed class PlanTaskRequest
{
    public DateOnly PlanDate { get; init; }
}

public sealed class LogTimeRequest
{
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
}

public sealed class RequestInfoRequest
{
    public string RecipientId { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
}

public sealed class CompleteTaskRequest
{
    public string? ClosureSummary { get; init; }
}

public sealed class ReassignTaskRequest
{
    public string AssigneeId { get; init; } = string.Empty;
}

public sealed class GateDecisionRequest
{
    public string? Note { get; init; }
}

public sealed class AnswerInfoRequest
{
    public string Answer { get; init; } = string.Empty;
}

public sealed class CancelTaskRequest
{
    public string? Reason { get; init; }
}

public sealed class TaskActionResult
{
    public bool Success { get; private init; }
    public int HttpStatusCode { get; private init; }
    public string Message { get; private init; } = string.Empty;

    public static TaskActionResult Ok(string message = "OK") =>
        new() { Success = true, HttpStatusCode = StatusCodes.Status200OK, Message = message };

    public static TaskActionResult NotFound(string message) =>
        new() { Success = false, HttpStatusCode = StatusCodes.Status404NotFound, Message = message };

    public static TaskActionResult Forbidden(string message) =>
        new() { Success = false, HttpStatusCode = StatusCodes.Status403Forbidden, Message = message };

    public static TaskActionResult BadRequest(string message) =>
        new() { Success = false, HttpStatusCode = StatusCodes.Status400BadRequest, Message = message };

    public static TaskActionResult Conflict(string message) =>
        new() { Success = false, HttpStatusCode = StatusCodes.Status409Conflict, Message = message };
}

public sealed class TaskActionResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
