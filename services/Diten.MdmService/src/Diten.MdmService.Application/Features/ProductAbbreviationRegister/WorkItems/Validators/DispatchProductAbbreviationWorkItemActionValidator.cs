using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Validators;

public sealed class DispatchProductAbbreviationWorkItemActionValidator
    : AbstractValidator<DispatchProductAbbreviationWorkItemActionCommand>
{
    public DispatchProductAbbreviationWorkItemActionValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.ActionCode).Must(ProductAbbreviationWorkItemContract.ActionCodes.Contains);
        RuleFor(x => x.ProviderCode).Equal(ProductAbbreviationWorkItemContract.ProviderCode);
        RuleFor(x => x.ExpectedVersion).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.HasUnmappedFields).Equal(false);
        RuleFor(x => x).Must(x => x.ActionCode != "reject" || !string.IsNullOrWhiteSpace(x.Reason ?? x.Note));
    }
}
