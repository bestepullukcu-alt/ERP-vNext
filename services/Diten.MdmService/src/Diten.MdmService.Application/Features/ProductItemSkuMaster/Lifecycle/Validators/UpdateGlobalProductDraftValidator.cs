using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Entities;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class UpdateGlobalProductDraftValidator : AbstractValidator<UpdateGlobalProductDraftCommand>
{
    public UpdateGlobalProductDraftValidator()
    {
        RuleFor(x => x.GlobalProductId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductName)
                .NotEmpty().WithMessage("GLOBAL_PRODUCT_NAME_REQUIRED")
                .Must(GlobalProductNameRules.HasValidLength)
                .WithMessage("GLOBAL_PRODUCT_NAME_LENGTH_INVALID");
            RuleFor(x => x.Request.ExpectedVersion)
                .NotNull()
                .GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }
}
