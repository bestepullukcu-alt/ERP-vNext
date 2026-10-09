using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Validators;

public sealed class EndProductLegalEntityScopePolicyValidator
    : AbstractValidator<EndProductLegalEntityScopePolicyCommand>
{
    public EndProductLegalEntityScopePolicyValidator()
    {
        RuleFor(x => x.GlobalProductId).NotEmpty();
        RuleFor(x => x.CommandId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }
}
