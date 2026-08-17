using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;

public sealed class RecordPersonReferenceDirectoryHealthValidator : AbstractValidator<RecordPersonReferenceDirectoryHealthCommand>
{
    public RecordPersonReferenceDirectoryHealthValidator() => Include(new PersonReferenceDirectoryHealthRequestValidator<RecordPersonReferenceDirectoryHealthCommand>(x => x.Request));
}
