using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Domain.Entities;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;

public sealed class ReserveCanonicalCodeValidator : AbstractValidator<ReserveCanonicalCodeCommand>
{
    public ReserveCanonicalCodeValidator()
    {
        RuleFor(x => x.Request).NotNull();
        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.GlobalProductName)
                .NotEmpty().WithMessage("GLOBAL_PRODUCT_NAME_REQUIRED")
                .Must(GlobalProductNameRules.HasValidLength)
                .WithMessage("GLOBAL_PRODUCT_NAME_LENGTH_INVALID");
            RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !Contains(fields, "TenantId"))
                .WithMessage("TENANT_ID_CLIENT_INPUT_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !Contains(fields, "CanonicalCode"))
                .WithMessage("CANONICAL_CODE_ASSIGNMENT_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !Contains(fields, "GlobalProductNameNormalized"))
                .WithMessage("NORMALIZED_NAME_CLIENT_INPUT_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || !Contains(fields, "EntityType"))
                .WithMessage("ENTITY_TYPE_CLIENT_INPUT_FORBIDDEN");
            RuleFor(x => x.Request.UnmappedFields)
                .Must(fields => fields is null || fields.Count == 0)
                .WithMessage("UNKNOWN_WRITE_FIELD_FORBIDDEN");
        });
    }

    private static bool Contains(IDictionary<string, System.Text.Json.JsonElement> fields, string key)
        => fields.Keys.Any(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
}
