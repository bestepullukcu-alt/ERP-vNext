using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ItemMaster.Validators;

// MOD-0290-FU04 §12 — the structural rules (400). What needs the lists or the store (unknown codes, the temperature
// rule, conversions, Stockable completeness, the name's uniqueness) is the handler's (422 / 409).
public sealed class ItemFieldsInputValidator : AbstractValidator<ItemModels.ItemFieldsInput>
{
    private static readonly string[] ForbiddenFields =
    [
        "Id", "TenantId", "CanonicalCode", "CodeReservationId", "CreationCommandId", "CreationFingerprint", "NameKey",
        "NameNormalized", "LifecycleStatus", "ActivationKind", "Version", "CreatedAt", "UpdatedAt", "CreatedBy",
        "UpdatedBy", "IsDeleted", "DeletedAt", "MaterialTypeSelection", "BaseUomSelection", "StorageConditionSelections",
        "DeactivationReason", "DeactivatedAt", "RetirementReason", "RetiredAt", "SerialControlled", "Gtin", "LegalEntityId"
    ];

    public ItemFieldsInputValidator()
    {
        RuleFor(x => x.Name).Must(GlobalProductNameRules.HasValidLength).WithMessage("ITEM_NAME_INVALID");
        RuleFor(x => x.Name).Must(name => name is null || !name.Any(char.IsControl)).WithMessage("ITEM_NAME_INVALID");
        RuleFor(x => x.Description).Must(text => text is null || text.Trim().Length <= ItemRules.DescriptionMaximumLength)
            .WithMessage("ITEM_DESCRIPTION_INVALID");
        RuleFor(x => x.MaterialType).NotEmpty().WithMessage("ITEM_MATERIAL_TYPE_REQUIRED");
        RuleFor(x => x.BaseUomCode).NotEmpty().WithMessage("ITEM_BASE_UOM_REQUIRED");
        RuleFor(x => x.LotControlled).NotNull().WithMessage("ITEM_LOT_CONTROLLED_REQUIRED");
        RuleFor(x => x.Stockable).NotNull().WithMessage("ITEM_STOCKABLE_REQUIRED");
        RuleFor(x => x.ShelfLifeDays).InclusiveBetween(1, ItemRules.DaysMaximum).When(x => x.ShelfLifeDays is not null)
            .WithMessage("ITEM_SHELF_LIFE_INVALID");
        RuleFor(x => x.RetestDays).InclusiveBetween(1, ItemRules.DaysMaximum).When(x => x.RetestDays is not null)
            .WithMessage("ITEM_RETEST_INVALID");
        RuleFor(x => x.MinRemainingShelfLifeDays).InclusiveBetween(0, ItemRules.DaysMaximum)
            .When(x => x.MinRemainingShelfLifeDays is not null).WithMessage("ITEM_MIN_REMAINING_SHELF_LIFE_INVALID");
        RuleFor(x => x).Must(x => x.MinRemainingShelfLifeDays is null || x.ShelfLifeDays is null
                                  || x.MinRemainingShelfLifeDays <= x.ShelfLifeDays)
            .WithMessage("ITEM_MIN_REMAINING_SHELF_LIFE_INVALID");
        RuleFor(x => x.UomConversions).Must(list => list is null || list.Count <= ItemRules.MaximumConversions && list.All(c => c is not null))
            .WithMessage(ItemErrors.UomConversionInvalid);
        RuleFor(x => x.StorageConditionCodes)
            .Must(list => list is null || list.Count <= ItemRules.MaximumStorageConditions && list.All(c => !string.IsNullOrWhiteSpace(c)))
            .WithMessage("ITEM_STORAGE_CONDITION_INVALID");
        RuleFor(x => x.UnmappedFields).Must(fields => fields is null || !fields.Keys.Any(IsForbidden))
            .WithMessage(ItemErrors.FieldForbidden);
        RuleFor(x => x.UnmappedFields).Must(fields => fields is null || fields.Count == 0)
            .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
    }

    internal static bool IsForbidden(string key) =>
        ForbiddenFields.Any(field => string.Equals(field, key, StringComparison.OrdinalIgnoreCase));
}

public sealed class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Request).NotNull().WithMessage("ITEM_REQUEST_REQUIRED");
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request).SetValidator(new ItemFieldsInputValidator());
            RuleFor(x => x.Request.IdempotencyKey)
                .Must(key => !string.IsNullOrWhiteSpace(key) && key.Trim().Length <= ItemRules.IdempotencyKeyMaximumLength
                             && !key.Any(char.IsControl))
                .WithMessage(ItemErrors.IdempotencyKeyInvalid);
        });
    }
}

public sealed class UpdateItemValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage(ItemErrors.NotFound);
        RuleFor(x => x.Request).NotNull().WithMessage("ITEM_REQUEST_REQUIRED");
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request).SetValidator(new ItemFieldsInputValidator());
            RuleFor(x => x.Request.ExpectedVersion).NotNull().GreaterThanOrEqualTo(0).WithMessage("EXPECTED_VERSION_REQUIRED");
        });
    }
}

public abstract class ItemLifecycleValidator<T> : AbstractValidator<T>
{
    protected ItemLifecycleValidator(Func<T, Guid> id, Func<T, ItemModels.ItemLifecycleRequest?> request, bool reasonRequired)
    {
        RuleFor(x => id(x)).NotEmpty().WithMessage(ItemErrors.NotFound);
        RuleFor(x => request(x)).NotNull().WithMessage("ITEM_REQUEST_REQUIRED");
        When(x => request(x) is not null, () =>
        {
            RuleFor(x => request(x)!.ExpectedVersion).NotNull().GreaterThanOrEqualTo(0).WithMessage("EXPECTED_VERSION_REQUIRED");
            RuleFor(x => request(x)!.Reason)
                .Must(reason => reason is null ? !reasonRequired
                    : reason.Trim().Length is >= 1 and <= 500 && !reason.Any(char.IsControl))
                .WithMessage("ITEM_REASON_REQUIRED");
            RuleFor(x => request(x)!.UnmappedFields).Must(fields => fields is null || fields.Count == 0)
                .WithMessage(ItemErrors.FieldForbidden);
        });
    }
}

public sealed class SubmitItemValidator() : ItemLifecycleValidator<SubmitItemCommand>(x => x.ItemId, x => x.Request, false);
public sealed class DeactivateItemValidator() : ItemLifecycleValidator<DeactivateItemCommand>(x => x.ItemId, x => x.Request, true);
public sealed class RequestItemReactivationValidator()
    : ItemLifecycleValidator<RequestItemReactivationCommand>(x => x.ItemId, x => x.Request, true);
public sealed class RetireItemValidator() : ItemLifecycleValidator<RetireItemCommand>(x => x.ItemId, x => x.Request, true);
