using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class UpdateHrisMappingProfileValidator : AbstractValidator<UpdateHrisMappingProfileCommand>
{
    public UpdateHrisMappingProfileValidator()
    {
        RuleFor(x => x.SourceProfileId).NotEmpty();
        Include(new HrisMappingProfileRequestValidator<UpdateHrisMappingProfileCommand>(x => x.Request));
    }
}
