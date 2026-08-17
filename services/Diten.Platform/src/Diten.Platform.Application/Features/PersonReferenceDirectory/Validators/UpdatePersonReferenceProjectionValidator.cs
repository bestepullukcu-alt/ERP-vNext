using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;

public sealed class UpdatePersonReferenceProjectionValidator : AbstractValidator<UpdatePersonReferenceProjectionCommand>
{
    public UpdatePersonReferenceProjectionValidator() => Include(new PersonReferenceProjectionUpdateRequestValidator<UpdatePersonReferenceProjectionCommand>(x => x.Request));
}
