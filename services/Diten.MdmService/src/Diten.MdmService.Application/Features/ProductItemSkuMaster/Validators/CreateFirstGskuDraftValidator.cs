using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class CreateFirstGskuDraftValidator : AbstractValidator<CreateFirstGskuDraftCommand>
{
    private static readonly string[] AllowedUoms = ["C62", "GRM", "KGM", "MLT", "LTR"];
    private static readonly string[] ForbiddenFields =
    [
        "TenantId", "CanonicalCode", "RevisionIdentifier", "Composition", "CompositionId",
        "SetCode", "CatalogVersionId", "CatalogVersionNumber", "ResolutionMode", "ResolvedAtUtc",
        "PackApplicabilityCode", "ProductType", "ProductTypeCode", "DosageForm", "DosageFormCode",
        "RouteOfAdministration", "RouteOfAdministrationCodes", "Strength", "StrengthValue", "StrengthUomCode",
        "EffectiveDate", "EffectiveFrom", "EffectiveTo", "IsCurrent", "Gtin", "PackagingLevelCode"
    ];

    public CreateFirstGskuDraftValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.GskuReservationId).NotEmpty();
            RuleFor(x => x.Request.ExpectedReservationVersion).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Request.CreationCommandId).NotEmpty().MaximumLength(128);
            RuleFor(x => x.Request.PackQuantity).GreaterThan(0);
            RuleFor(x => x.Request.PackUomCode).Must(IsAllowedUom).WithMessage("PACK_UOM_INVALID");
            RuleFor(x => x.Request).Must(x => HasValidPrecision(x.PackQuantity, x.PackUomCode))
                .WithMessage("PACK_QUANTITY_PRECISION_EXCEEDED");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !fields.Keys.Any(key => ForbiddenFields.Contains(key, StringComparer.OrdinalIgnoreCase)))
                .WithMessage("REFERENCE_CATALOG_EVIDENCE_CLIENT_OVERRIDE_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields).Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }

    internal static bool HasValidPrecision(decimal quantity, string uom)
    {
        var scale = (decimal.GetBits(quantity)[3] >> 16) & 0x7F;
        return string.Equals(uom, "C62", StringComparison.Ordinal) ? scale == 0 : scale <= 3;
    }

    internal static bool IsAllowedUom(string value) => AllowedUoms.Contains(value, StringComparer.Ordinal);
}
