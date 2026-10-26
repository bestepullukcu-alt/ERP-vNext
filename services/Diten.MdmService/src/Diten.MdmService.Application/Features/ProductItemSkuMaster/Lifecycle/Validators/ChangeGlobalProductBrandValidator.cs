using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

public sealed class ChangeGlobalProductBrandValidator : AbstractValidator<ChangeGlobalProductBrandCommand>
{
    public ChangeGlobalProductBrandValidator()
    {
        RuleFor(x => x.GlobalProductId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ExpectedVersion).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.BrandIdSpecified)
                .Equal(true)
                .WithMessage("GLOBAL_PRODUCT_BRAND_ID_REQUIRED");
            RuleFor(x => x.Request.BrandId)
                .Must(brandId => brandId != Guid.Empty)
                .When(x => x.Request.BrandId.HasValue)
                .WithMessage("GLOBAL_PRODUCT_BRAND_ID_INVALID");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }
}
