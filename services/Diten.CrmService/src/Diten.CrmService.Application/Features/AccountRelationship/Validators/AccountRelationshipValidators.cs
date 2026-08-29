using Diten.CrmService.Application.Features.AccountRelationship.Commands;
using FluentValidation;

namespace Diten.CrmService.Application.Features.AccountRelationship.Validators;

public sealed class CreateAccountRelationshipValidator : AbstractValidator<CreateAccountRelationshipCommand>
{
    public CreateAccountRelationshipValidator()
    {
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.TargetAccountId).NotEmpty();
        RuleFor(x => x.RelationshipType).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.ValidFrom is null || x.ValidTo is null || x.ValidFrom <= x.ValidTo)
            .WithMessage("ValidFrom must be on or before ValidTo.")
            .WithName("Validity");
    }
}

public sealed class UpdateAccountRelationshipValidator : AbstractValidator<UpdateAccountRelationshipCommand>
{
    public UpdateAccountRelationshipValidator()
    {
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.RelationshipId).NotEmpty();
        RuleFor(x => x.RelationshipType).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Notes!).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => x.ValidFrom is null || x.ValidTo is null || x.ValidFrom <= x.ValidTo)
            .WithMessage("ValidFrom must be on or before ValidTo.")
            .WithName("Validity");
    }
}
