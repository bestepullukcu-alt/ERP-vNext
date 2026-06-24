using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Domain.Enums.DocumentManagement;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Validators;

public sealed class DryRunInstantiationValidator : AbstractValidator<DryRunInstantiationCommand>
{
    public DryRunInstantiationValidator()
    {
        RuleFor(x => x.BaselineReleaseId).NotEmpty();
        RuleFor(x => x.Scope).NotNull();
        RuleFor(x => x.Scope.CompanyId).NotEmpty();
        RuleFor(x => x.Scope.InstanceToken).MaximumLength(64);
        RuleFor(x => x.Selection).NotNull();
        RuleFor(x => x.Selection.SelectionMode).IsInEnum();
        RuleFor(x => x.Selection.SelectedCanonicalIds)
            .Must(x => x is not null)
            .WithMessage("Selected canonical ids are required.");
        RuleFor(x => x.Selection.SelectedCanonicalIds)
            .Must(x => x.Count > 0)
            .When(x => x.Selection.SelectionMode == InstantiationSelectionMode.SelectedBranches)
            .WithMessage("At least one selected canonical id is required for selected-branch instantiation.");
    }
}
