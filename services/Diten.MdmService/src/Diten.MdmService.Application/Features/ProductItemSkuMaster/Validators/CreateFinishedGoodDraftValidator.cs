using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class CreateFinishedGoodDraftValidator : AbstractValidator<CreateFinishedGoodDraftCommand>
{
    private static readonly string[] ForbiddenFields =
    [
        "TenantId", "CanonicalCode", "CodeReservationId", "StewardLabel", "LskuId",
        "MarketSupplyAssignmentId", "MarketCode", "LegalEntityId", "MarketTradeName",
        "Packaging", "PackagingLevelCode", "Site", "SiteId", "Manufacturer", "ManufacturerId",
        "MarketingAuthorization", "MarketingAuthorizationId", "MaId", "RegisteredPresentation",
        "RegisteredPresentationId", "Artwork", "ArtworkId", "Gtin", "Batch", "BatchId",
        "Composition", "CompositionId", "LifecycleStatus", "Version", "CreatedAt", "UpdatedAt",
        "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt", "AuditIntents", "AuditIntentReceipts"
    ];

    public CreateFinishedGoodDraftValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GskuId).NotEmpty().WithMessage("GSKU_ID_REQUIRED");
            RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !fields.Keys.Any(IsForbidden))
                .WithMessage("FINISHED_GOOD_FIELD_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }

    private static bool IsForbidden(string key)
        => ForbiddenFields.Any(field => string.Equals(field, key, StringComparison.OrdinalIgnoreCase));
}
