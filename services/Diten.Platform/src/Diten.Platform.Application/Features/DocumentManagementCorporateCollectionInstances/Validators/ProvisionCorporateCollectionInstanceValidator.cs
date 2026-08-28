using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Validators;

public sealed class ProvisionCorporateCollectionInstanceValidator : AbstractValidator<ProvisionCorporateCollectionInstanceCommand>
{
    public ProvisionCorporateCollectionInstanceValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
        RuleFor(x => x.CorporateOwnerId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DisplayName).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(200);
    }
}
