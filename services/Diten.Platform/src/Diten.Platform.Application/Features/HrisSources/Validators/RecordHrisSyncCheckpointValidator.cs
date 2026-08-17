using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class RecordHrisSyncCheckpointValidator : AbstractValidator<RecordHrisSyncCheckpointCommand>
{
    public RecordHrisSyncCheckpointValidator()
    {
        RuleFor(x => x.SourceProfileId).NotEmpty();
        Include(new HrisSyncCheckpointRequestValidator<RecordHrisSyncCheckpointCommand>(x => x.Request));
    }
}
