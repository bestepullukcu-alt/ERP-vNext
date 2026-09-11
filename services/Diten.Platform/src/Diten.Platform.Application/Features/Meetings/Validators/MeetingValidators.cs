using Diten.Platform.Application.Features.Meetings.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Meetings.Validators;

// MOD-0357 S2 — pure field-SHAPE checks only. Every rule that needs a stable MEETING_* reason code on the wire
// (EndAt > StartAt, cancelled/completed conflicts, duplicate name, …) lives in the HANDLER instead: a
// FluentValidation failure in this codebase always surfaces as a plain 400 with no reason code unless
// ValidationReasonCode.From can derive one from ErrorCode — see CreateTaskItemValidator's own note on why
// business-rule refusals are kept out of validators here.

public sealed class CreateMeetingValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingValidator()
    {
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(MeetingFieldLimits.MaxTitleLength);
        RuleFor(x => x.Request.MeetingTypeId).NotEmpty();
        RuleFor(x => x.Request.Description).MaximumLength(MeetingFieldLimits.MaxDescriptionLength);
    }
}

public sealed class UpdateMeetingValidator : AbstractValidator<UpdateMeetingCommand>
{
    public UpdateMeetingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(MeetingFieldLimits.MaxTitleLength);
        RuleFor(x => x.Request.MeetingTypeId).NotEmpty();
        RuleFor(x => x.Request.Description).MaximumLength(MeetingFieldLimits.MaxDescriptionLength);
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class CancelMeetingValidator : AbstractValidator<CancelMeetingCommand>
{
    public CancelMeetingValidator()
    {
        RuleFor(x => x.Request.Reason).NotEmpty().WithMessage("A cancellation reason is required.");
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ReassignMeetingOrganizerValidator : AbstractValidator<ReassignMeetingOrganizerCommand>
{
    public ReassignMeetingOrganizerValidator()
    {
        RuleFor(x => x.Request.NewOrganizerUserId).NotEmpty();
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class AddMeetingAttendeesValidator : AbstractValidator<AddMeetingAttendeesCommand>
{
    public AddMeetingAttendeesValidator()
    {
        RuleFor(x => x.Request.UserIds).NotEmpty().WithMessage("At least one attendee is required.");
    }
}

public sealed class AddAgendaItemValidator : AbstractValidator<AddAgendaItemCommand>
{
    public AddAgendaItemValidator()
    {
        RuleFor(x => x.Request.Text).NotEmpty().MaximumLength(MeetingFieldLimits.MaxAgendaItemTextLength);
    }
}

public sealed class UpdateAgendaItemValidator : AbstractValidator<UpdateAgendaItemCommand>
{
    public UpdateAgendaItemValidator()
    {
        RuleFor(x => x.Request.Text).NotEmpty().MaximumLength(MeetingFieldLimits.MaxAgendaItemTextLength);
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ReorderAgendaValidator : AbstractValidator<ReorderAgendaCommand>
{
    public ReorderAgendaValidator()
    {
        RuleFor(x => x.Request.OrderedAgendaItemIds).NotEmpty();
    }
}

public sealed class CreateMeetingTypeValidator : AbstractValidator<CreateMeetingTypeCommand>
{
    public CreateMeetingTypeValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(MeetingFieldLimits.MaxTypeNameLength);

        RuleFor(x => x.Request.AgendaTemplate!)
            .Must(lines => lines.Count <= MeetingFieldLimits.MaxAgendaTemplateLines)
            .When(x => x.Request.AgendaTemplate is not null)
            .WithMessage($"At most {MeetingFieldLimits.MaxAgendaTemplateLines} agenda template lines are allowed.");

        RuleForEach(x => x.Request.AgendaTemplate!)
            .MaximumLength(MeetingFieldLimits.MaxAgendaTemplateLineLength)
            .When(x => x.Request.AgendaTemplate is not null);
    }
}

public sealed class UpdateMeetingTypeValidator : AbstractValidator<UpdateMeetingTypeCommand>
{
    public UpdateMeetingTypeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(MeetingFieldLimits.MaxTypeNameLength);
        RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0);

        RuleFor(x => x.Request.AgendaTemplate!)
            .Must(lines => lines.Count <= MeetingFieldLimits.MaxAgendaTemplateLines)
            .When(x => x.Request.AgendaTemplate is not null)
            .WithMessage($"At most {MeetingFieldLimits.MaxAgendaTemplateLines} agenda template lines are allowed.");

        RuleForEach(x => x.Request.AgendaTemplate!)
            .MaximumLength(MeetingFieldLimits.MaxAgendaTemplateLineLength)
            .When(x => x.Request.AgendaTemplate is not null);
    }
}
