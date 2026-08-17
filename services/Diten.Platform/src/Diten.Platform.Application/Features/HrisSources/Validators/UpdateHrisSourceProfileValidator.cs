using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class UpdateHrisSourceProfileValidator : AbstractValidator<UpdateHrisSourceProfileCommand>
{
    public UpdateHrisSourceProfileValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new HrisSourceProfileUpdateRequestValidator<UpdateHrisSourceProfileCommand>(x => x.Request));
    }
}
