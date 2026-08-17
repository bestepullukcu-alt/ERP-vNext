using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class RecordTimeAttendanceProviderHealthSnapshotValidator : TimeAttendanceProviderHealthSnapshotRequestValidator<RecordTimeAttendanceProviderHealthSnapshotCommand>
{
    public RecordTimeAttendanceProviderHealthSnapshotValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.ProviderProfileId).NotEmpty();
    }
}
