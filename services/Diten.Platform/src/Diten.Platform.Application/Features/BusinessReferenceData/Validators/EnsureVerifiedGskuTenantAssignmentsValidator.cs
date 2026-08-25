using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class EnsureVerifiedGskuTenantAssignmentsValidator
    : AbstractValidator<EnsureVerifiedGskuTenantAssignmentsCommand>
{
    public EnsureVerifiedGskuTenantAssignmentsValidator()
    {
        RuleFor(x => x.ConsumerTenantId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.IdempotencyNamespace).NotEmpty();
    }
}
