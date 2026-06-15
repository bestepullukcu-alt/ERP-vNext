using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class DeactivateBusinessReferenceDataUsageRegistrationsBulkCommandValidator : AbstractValidator<DeactivateBusinessReferenceDataUsageRegistrationsBulkCommand>
{
    public DeactivateBusinessReferenceDataUsageRegistrationsBulkCommandValidator()
    {
        RuleFor(x => x.UsageRegistrationIds)
            .NotNull()
            .NotEmpty()
            .Must(ids => ids != null && ids.Count <= 500)
            .WithMessage("At most 500 usage registrations can be deactivated at once.");
        RuleForEach(x => x.UsageRegistrationIds).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
    }
}
