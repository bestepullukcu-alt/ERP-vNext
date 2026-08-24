using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class UpdateGskuDraftValidator : AbstractValidator<UpdateGskuDraftCommand>
{
    public UpdateGskuDraftValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GskuId).NotEmpty();
            RuleFor(x => x.Request.ExpectedVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.PackQuantity).GreaterThan(0);
            RuleFor(x => x.Request.PackUomCode).Must(CreateFirstGskuDraftValidator.IsAllowedUom)
                .WithMessage("PACK_UOM_INVALID");
            RuleFor(x => x.Request).Must(x =>
                    CreateFirstGskuDraftValidator.HasValidPrecision(x.PackQuantity, x.PackUomCode))
                .WithMessage("PACK_QUANTITY_PRECISION_EXCEEDED");
            RuleFor(x => x.Request.UnmappedFields).Must(fields => fields is null || fields.Count == 0)
                .WithMessage("REFERENCE_CATALOG_EVIDENCE_CLIENT_OVERRIDE_FORBIDDEN");
        });
    }
}
