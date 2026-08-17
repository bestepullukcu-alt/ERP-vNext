using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;

public sealed class CreatePersonReferenceProjectionValidator : AbstractValidator<CreatePersonReferenceProjectionCommand>
{
    public CreatePersonReferenceProjectionValidator() => Include(new PersonReferenceProjectionCreateRequestValidator<CreatePersonReferenceProjectionCommand>(x => x.Request));
}
