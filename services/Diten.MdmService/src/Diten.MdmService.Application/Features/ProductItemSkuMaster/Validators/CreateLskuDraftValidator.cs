using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class CreateLskuDraftValidator : AbstractValidator<CreateLskuDraftCommand>
{
    private static readonly string[] ForbiddenFields =
    [
        "TenantId", "CanonicalCode", "CodeReservationId", "ReferenceTenantId", "Credential",
        "CredentialIdentifier", "CredentialSecret", "Publication", "PublicationEvidence", "SetCode",
        "ValueCode", "CatalogVersionId", "CatalogVersionNumber", "ResolutionMode", "ResolvedAtUtc",
        "MarketSelection", "LegalEntityId", "MarketTradeName", "FinishedGoodId", "MarketSupplyAssignmentId",
        "MarketingAuthorization", "MarketingAuthorizationId", "MaId", "RegisteredPresentation",
        "RegisteredPresentationId", "Artwork", "ArtworkId", "Packaging", "PackagingLevelCode", "Manufacturer",
        "ManufacturerId", "Site", "SiteId", "Gtin", "Composition", "CompositionId", "LifecycleStatus",
        "Version", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt",
        "AuditIntents", "AuditIntentReceipts"
    ];

    public CreateLskuDraftValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GskuId).NotEmpty().WithMessage("GSKU_ID_REQUIRED");
            RuleFor(x => x.Request.MarketCode)
                .NotNull()
                .Must(IsExactIsoAlpha2)
                .WithMessage("MARKET_CODE_INVALID");
            RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !fields.Keys.Any(IsForbidden))
                .WithMessage("LSKU_FIELD_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }

    private static bool IsExactIsoAlpha2(string? value) =>
        value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z'
        && value[1] is >= 'A' and <= 'Z';

    private static bool IsForbidden(string key) =>
        ForbiddenFields.Any(field => string.Equals(field, key, StringComparison.OrdinalIgnoreCase));
}
