using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Domain.Enums.Tasks;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tasks.Validators;

/// <summary>
/// MOD-0024 — create-time field validation. Deliberately absent: <c>Lifecycle</c> (system-set — pack §12 Y2) and
/// <c>SpentHours</c> (always 0 on a new task — pack §12 Y1) are not on the request at all, so they cannot be
/// smuggled in.
/// </summary>
public sealed class CreateTaskItemValidator : AbstractValidator<CreateTaskItemCommand>
{
    public CreateTaskItemValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(TaskFieldLimits.MaxTitleLength);

        RuleFor(x => x.Request.Description)
            .MaximumLength(TaskFieldLimits.MaxDescriptionLength);

        RuleFor(x => x.Request.Priority).IsInEnum();
        RuleFor(x => x.Request.AssignmentTarget).IsInEnum();

        // OD-3 — a due date is required for ALL THREE targets, pool included.
        RuleFor(x => x.Request.DueAt)
            .NotNull().WithMessage("A due date is required.");

        // Assignment intent must be internally coherent (pack §12 K5).
        RuleFor(x => x.Request.AssigneeUserId)
            .NotEmpty()
            .When(x => x.Request.AssignmentTarget == TaskAssignmentTarget.Person)
            .WithMessage("An assignee is required when assigning to a person.");

        RuleFor(x => x.Request.PoolPositionId)
            .NotEmpty()
            .When(x => x.Request.AssignmentTarget == TaskAssignmentTarget.PositionPool)
            .WithMessage("A position is required when pooling a task.");

        // A pool task has NO assignee — sending one means the caller misunderstood the target.
        RuleFor(x => x.Request.AssigneeUserId)
            .Must(id => id is null || id == Guid.Empty)
            .When(x => x.Request.AssignmentTarget == TaskAssignmentTarget.PositionPool)
            .WithMessage("A pooled task must not carry an assignee; it is claimed later.");

        RuleFor(x => x.Request.ApprovalManagerUserId)
            .NotEmpty()
            .When(x => x.Request.ApprovalRequired)
            .WithMessage("An approval manager is required when approval is requested.");

        RuleFor(x => x.Request.EstimateHours)
            .GreaterThanOrEqualTo(0).When(x => x.Request.EstimateHours is not null);

        RuleFor(x => x.Request.Tags!)
            .Must(tags => tags.Count <= TaskFieldLimits.MaxTags)
            .When(x => x.Request.Tags is not null)
            .WithMessage($"At most {TaskFieldLimits.MaxTags} tags are allowed.");

        RuleForEach(x => x.Request.Tags!)
            .MaximumLength(TaskFieldLimits.MaxTagLength)
            .When(x => x.Request.Tags is not null);
    }
}

public sealed class UpdateTaskItemValidator : AbstractValidator<UpdateTaskItemCommand>
{
    public UpdateTaskItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(TaskFieldLimits.MaxTitleLength);

        RuleFor(x => x.Request.Description).MaximumLength(TaskFieldLimits.MaxDescriptionLength);
        RuleFor(x => x.Request.Priority).IsInEnum();
        RuleFor(x => x.Request.DueAt).NotNull().WithMessage("A due date is required.");

        RuleFor(x => x.Request.EstimateHours)
            .GreaterThanOrEqualTo(0).When(x => x.Request.EstimateHours is not null);

        // Optimistic concurrency is mandatory on every edit (pack §13).
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}
