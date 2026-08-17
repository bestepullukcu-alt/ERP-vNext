using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class RecordAttendanceSummaryReferenceValidator : AttendanceSummaryReferenceRequestValidator<RecordAttendanceSummaryReferenceCommand>
{
    public RecordAttendanceSummaryReferenceValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.ProviderProfileId).NotEmpty();
    }
}
