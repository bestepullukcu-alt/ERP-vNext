using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;

internal sealed class PersonReferenceProjectionCreateRequestValidator<T> : AbstractValidator<T>
{
    public PersonReferenceProjectionCreateRequestValidator(Func<T, PersonReferenceProjectionCreateRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).ReferenceDisplayName).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).HrisSourceProfileId).NotEmpty();
        RuleFor(x => selector(x).ReferenceState).IsInEnum();
        RuleFor(x => selector(x).SourceContractVersion).NotEmpty().MaximumLength(64).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).CorrelationKey).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).ValidationFailureReason).MaximumLength(500).MustNotContainForbiddenPersonReferenceValue();
    }
}

internal sealed class PersonReferenceProjectionUpdateRequestValidator<T> : AbstractValidator<T>
{
    public PersonReferenceProjectionUpdateRequestValidator(Func<T, PersonReferenceProjectionUpdateRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).ReferenceDisplayName).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).HrisSourceProfileId).NotEmpty();
        RuleFor(x => selector(x).ReferenceState).IsInEnum();
        RuleFor(x => selector(x).SourceContractVersion).NotEmpty().MaximumLength(64).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).CorrelationKey).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).ValidationFailureReason).MaximumLength(500).MustNotContainForbiddenPersonReferenceValue();
    }
}

internal sealed class PersonReferenceExternalCorrelationRequestValidator<T> : AbstractValidator<T>
{
    public PersonReferenceExternalCorrelationRequestValidator(Func<T, PersonReferenceExternalCorrelationRequest> selector)
    {
        RuleFor(x => selector(x).ExternalObjectType)
            .IsInEnum()
            .Equal(PersonReferenceExternalObjectType.Employee)
            .WithMessage("ExternalObjectType must be Employee. Payroll, time-attendance, provider-specific, and TEP candidate object types are not allowed.");
        RuleFor(x => selector(x).ExternalObjectReference).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).CorrelationKey).NotEmpty().MaximumLength(160).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).CorrelationState).IsInEnum();
        RuleFor(x => selector(x).SourceContractVersion).NotEmpty().MaximumLength(64).MustNotContainForbiddenPersonReferenceValue();
    }
}

internal sealed class PersonReferenceDirectoryHealthRequestValidator<T> : AbstractValidator<T>
{
    public PersonReferenceDirectoryHealthRequestValidator(Func<T, PersonReferenceDirectoryHealthRequest> selector)
    {
        RuleFor(x => selector(x).SnapshotKey).NotEmpty().MaximumLength(120).MustNotContainForbiddenPersonReferenceValue();
        RuleFor(x => selector(x).ProjectionCount).GreaterThanOrEqualTo(0).When(x => selector(x).ProjectionCount.HasValue);
        RuleFor(x => selector(x).ValidatedCount).GreaterThanOrEqualTo(0).When(x => selector(x).ValidatedCount.HasValue);
        RuleFor(x => selector(x).ConflictCount).GreaterThanOrEqualTo(0).When(x => selector(x).ConflictCount.HasValue);
        RuleFor(x => selector(x).RedactedStatus).MaximumLength(500).MustNotContainForbiddenPersonReferenceValue();
    }
}

internal static class PersonReferenceValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> MustNotContainForbiddenPersonReferenceValue<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => !PersonReferenceSensitiveValueGuard.LooksForbidden(value))
            .WithMessage("Value must not contain raw HRIS payload, raw credentials/tokens/secrets, PII-heavy profile, payroll/bank/tax/payslip, biometric/geolocation, provider-adapter, or TEP candidate data.");
}
