using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class CreateFirstGskuDraftFacadeValidator : AbstractValidator<CreateFirstGskuDraftFacadeCommand>
{
    private static readonly string[] TechnicalOrForbiddenFields =
    [
        "TenantId", "CanonicalCode", "RevisionIdentifier", "GskuReservationId", "ReservationId",
        "ExpectedReservationVersion", "CreationCommandId", "IdempotencyKey", "OperationId",
        "PackApplicabilityCode", "SetCode", "CatalogVersionId", "CatalogVersionNumber", "ResolutionMode",
        "ResolvedAtUtc", "SelectableForNew", "IsRetired", "ReferenceTenantId", "ProviderEvidence",
        "Composition", "CompositionId", "Gtin", "PackagingLevelCode"
    ];

    public CreateFirstGskuDraftFacadeValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.OperationId).NotEmpty().MaximumLength(128);
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductId).NotEmpty();
            RuleFor(x => x.Request.PackQuantity).GreaterThan(0);
            RuleFor(x => x.Request.PackUomCode).NotEmpty().MaximumLength(16);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !fields.Keys.Any(key =>
                    TechnicalOrForbiddenFields.Contains(key, StringComparer.OrdinalIgnoreCase)))
                .WithMessage("GSKU_TECHNICAL_FIELD_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }
}
