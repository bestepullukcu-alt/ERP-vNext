using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class CreateHrisSourceProfileValidator : AbstractValidator<CreateHrisSourceProfileCommand>
{
    public CreateHrisSourceProfileValidator() => Include(new HrisSourceProfileCreateRequestValidator<CreateHrisSourceProfileCommand>(x => x.Request));
}
