using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;

public sealed class UpsertPersonReferenceExternalCorrelationValidator : AbstractValidator<UpsertPersonReferenceExternalCorrelationCommand>
{
    public UpsertPersonReferenceExternalCorrelationValidator() => Include(new PersonReferenceExternalCorrelationRequestValidator<UpsertPersonReferenceExternalCorrelationCommand>(x => x.Request));
}
