using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Validators;

/*
 * BL-500 — every rule on the Modules tab's commands answers with a CURATED code from TenantModuleEntitlementRefusalCodes
 * (`.WithErrorCode`), never the code ValidationReasonCode would derive from the field. The screen says a refusal from
 * its code; a derived code (VALIDATION_REQUEST_REASON_NOT_EMPTY…) has no sentence there and reached the reader as the
 * general "an error occurred" with the field's message lost. Guarded by TenantModuleEntitlementValidationCodeTests.
 *
 * The route's ids are never empty from the screen; an empty one names no row, so it is answered as "not found".
 */
public sealed class AddTenantModuleEntitlementCommandValidator : AbstractValidator<AddTenantModuleEntitlementCommand>
{
    public AddTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.Request.ModuleCode).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.ModuleRequired)
            .MaximumLength(64).WithErrorCode(TenantModuleEntitlementRefusalCodes.ModuleNotFound);
        RuleFor(x => x.Request.Source).IsInEnum().WithErrorCode(TenantModuleEntitlementRefusalCodes.SourceInvalid);
        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.ReasonRequired)
            .When(x => x.Request.Source == EntitlementSource.ManualOverride || x.Request.IsEnabled == false);
        RuleFor(x => x.Request.Reason).MaximumLength(500).WithErrorCode(TenantModuleEntitlementRefusalCodes.ReasonTooLong);
        // FIX2 — an add with an expiry already in the past would store a row that is expired the moment it exists.
        RuleFor(x => x.Request.ExpiryDateUtc)
            .Must(expiry => expiry > DateTimeOffset.UtcNow).WithErrorCode(TenantModuleEntitlementRefusalCodes.ExpiryInPast)
            .When(x => x.Request.ExpiryDateUtc.HasValue);
    }
}

public sealed class DisableTenantModuleEntitlementCommandValidator : AbstractValidator<DisableTenantModuleEntitlementCommand>
{
    public DisableTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.Request.ModuleCode).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.ModuleRequired)
            .MaximumLength(64).WithErrorCode(TenantModuleEntitlementRefusalCodes.ModuleNotFound);
        RuleFor(x => x.Request.Reason).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.ReasonRequired)
            .MaximumLength(500).WithErrorCode(TenantModuleEntitlementRefusalCodes.ReasonTooLong);
        // A stored row is written only against the version the screen saw. The plan's own line has no row and so no
        // version: two screens suspending it at once are kept to ONE override row by the unique index instead.
        RuleFor(x => x.Request.RowVersion).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.RowVersionRequired)
            .When(x => x.Request.PhysicalEntitlementId.HasValue);
    }
}

public sealed class EnableTenantModuleEntitlementCommandValidator : AbstractValidator<EnableTenantModuleEntitlementCommand>
{
    public EnableTenantModuleEntitlementCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.EntitlementId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.RowVersion).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.RowVersionRequired);
    }
}

public sealed class UpdateTenantModuleEntitlementExpiryCommandValidator : AbstractValidator<UpdateTenantModuleEntitlementExpiryCommand>
{
    public UpdateTenantModuleEntitlementExpiryCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.EntitlementId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.Request.Reason).MaximumLength(500).WithErrorCode(TenantModuleEntitlementRefusalCodes.ReasonTooLong);
        RuleFor(x => x.Request.RowVersion).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.RowVersionRequired);
        // "Extend expiry" sets a NEW date. An empty date used to be sent as null and silently REMOVED the expiry while
        // the screen said "saved"; taking an expiry away is a different action, and not this one.
        RuleFor(x => x.Request.ExpiryDateUtc)
            .NotNull().WithErrorCode(TenantModuleEntitlementRefusalCodes.ExpiryRequired)
            .Must(expiry => expiry > DateTimeOffset.UtcNow).WithErrorCode(TenantModuleEntitlementRefusalCodes.ExpiryInPast)
            .When(x => x.Request.ExpiryDateUtc.HasValue, ApplyConditionTo.CurrentValidator);
    }
}

public sealed class RemoveTenantManualModuleOverrideCommandValidator : AbstractValidator<RemoveTenantManualModuleOverrideCommand>
{
    public RemoveTenantManualModuleOverrideCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.EntitlementId).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.NotFound);
        RuleFor(x => x.Request.RowVersion).NotEmpty().WithErrorCode(TenantModuleEntitlementRefusalCodes.RowVersionRequired);
    }
}

public sealed class GetTenantModuleEffectiveAccessQueryValidator : AbstractValidator<GetTenantModuleEffectiveAccessQuery>
{
    public GetTenantModuleEffectiveAccessQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ModuleCode).NotEmpty().MaximumLength(64);
    }
}
