using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

internal sealed class HrisSourceProfileCreateRequestValidator<T> : AbstractValidator<T>
{
    public HrisSourceProfileCreateRequestValidator(Func<T, HrisSourceProfileCreateRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => selector(x).ProviderKind).IsInEnum();
        RuleFor(x => selector(x).ExternalTenantKey).MaximumLength(128).MustNotContainSensitiveOrPayload();
        RuleFor(x => selector(x).ConnectionProfileReference)
            .NotEmpty()
            .MaximumLength(256)
            .Must(BeReferenceOnly)
            .WithMessage("ConnectionProfileReference must be a secret/config reference key, not raw credentials, tokens, secrets, or payload.");
        RuleFor(x => selector(x).LifecycleState).IsInEnum();
        RuleFor(x => selector(x).SyncMode).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitiveOrPayload();
    }

    private static bool BeReferenceOnly(string value) =>
        !HrisSensitiveValueGuard.LooksLikeRawSecret(value) && !HrisSensitiveValueGuard.LooksLikeRawPayload(value);
}

internal sealed class HrisSourceProfileUpdateRequestValidator<T> : AbstractValidator<T>
{
    public HrisSourceProfileUpdateRequestValidator(Func<T, HrisSourceProfileUpdateRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => selector(x).ProviderKind).IsInEnum();
        RuleFor(x => selector(x).ExternalTenantKey).MaximumLength(128).MustNotContainSensitiveOrPayload();
        RuleFor(x => selector(x).ConnectionProfileReference)
            .NotEmpty()
            .MaximumLength(256)
            .Must(BeReferenceOnly)
            .WithMessage("ConnectionProfileReference must be a secret/config reference key, not raw credentials, tokens, secrets, or payload.");
        RuleFor(x => selector(x).LifecycleState).IsInEnum();
        RuleFor(x => selector(x).SyncMode).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitiveOrPayload();
    }

    private static bool BeReferenceOnly(string value) =>
        !HrisSensitiveValueGuard.LooksLikeRawSecret(value) && !HrisSensitiveValueGuard.LooksLikeRawPayload(value);
}

internal sealed class HrisMappingProfileRequestValidator<T> : AbstractValidator<T>
{
    public HrisMappingProfileRequestValidator(Func<T, HrisMappingProfileRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => selector(x).MappingProfileVersion).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).ExternalSchemaReference)
            .NotEmpty()
            .MaximumLength(256)
            .MustNotContainSensitiveOrPayload();
        RuleForEach(x => selector(x).IdentifierMaps).SetValidator(new HrisExternalIdentifierMapRequestValidator());
    }
}

internal sealed class HrisExternalIdentifierMapRequestValidator : AbstractValidator<HrisExternalIdentifierMapRequest>
{
    public HrisExternalIdentifierMapRequestValidator()
    {
        RuleFor(x => x.ExternalObjectType)
            .IsInEnum()
            .Must(x => x is HrisExternalObjectType.Employee or HrisExternalObjectType.Org or HrisExternalObjectType.Job)
            .WithMessage("ExternalObjectType must be Employee, Org, or Job. Payroll and time-attendance object types are not allowed.");
        RuleFor(x => x.ExternalObjectId).NotEmpty().MaximumLength(256).MustNotContainSensitiveOrPayload();
        RuleFor(x => x.InternalReferenceType).IsInEnum();
        RuleFor(x => x.MappingState).IsInEnum();
        RuleFor(x => x.ProvenanceHash).MaximumLength(256).MustNotContainSensitiveOrPayload();
    }
}

internal sealed class HrisSyncCheckpointRequestValidator<T> : AbstractValidator<T>
{
    public HrisSyncCheckpointRequestValidator(Func<T, HrisSyncCheckpointRequest> selector)
    {
        RuleFor(x => selector(x).SyncRunId).NotEmpty().MaximumLength(128).MustNotContainSensitiveOrPayload();
        RuleFor(x => selector(x).SyncMode).IsInEnum();
        RuleFor(x => selector(x).StartedAt).NotEmpty();
        RuleFor(x => selector(x).Status).IsInEnum();
        RuleFor(x => selector(x).CursorReference).MaximumLength(256).MustNotContainSensitiveOrPayload();
        RuleFor(x => selector(x).RecordsSeen).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x).RecordsAccepted).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x).RecordsRejected).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x))
            .Must(x => x.RecordsAccepted + x.RecordsRejected <= x.RecordsSeen)
            .WithMessage("Accepted and rejected records cannot exceed RecordsSeen.");
        RuleFor(x => selector(x).ErrorSummary).MaximumLength(1000).MustNotContainSensitiveOrPayload();
    }
}

internal static class HrisValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> MustNotContainSensitiveOrPayload<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => !HrisSensitiveValueGuard.LooksLikeRawSecret(value) && !HrisSensitiveValueGuard.LooksLikeRawPayload(value))
            .WithMessage("Value must not contain raw credentials, tokens, secrets, or raw HRIS payload.");

}
