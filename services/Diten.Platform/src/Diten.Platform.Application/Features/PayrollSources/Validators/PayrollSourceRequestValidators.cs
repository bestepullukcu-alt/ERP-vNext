using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public class PayrollExternalSystemProfileRequestValidator<T> : AbstractValidator<T>
{
    public PayrollExternalSystemProfileRequestValidator(Func<T, PayrollExternalSystemProfileRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).DisplayName).NotEmpty().MaximumLength(200).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).ProviderFamily).IsInEnum();
        RuleFor(x => selector(x).ExternalPayrollSystemId).NotEmpty().MaximumLength(128).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).LifecycleState).IsInEnum();
        RuleFor(x => selector(x).ConnectionProfileReference).MaximumLength(256).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).SupportOwner).MaximumLength(200).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).Notes).MaximumLength(1000).MustNotContainSensitivePayrollData();
    }
}

public class PayrollContractProfileRequestValidator<T> : AbstractValidator<T>
{
    private static readonly PayrollSupportedObjectType[] AllowedObjectTypes =
    [
        PayrollSupportedObjectType.Employee,
        PayrollSupportedObjectType.Org,
        PayrollSupportedObjectType.Job,
        PayrollSupportedObjectType.PayrollCycle,
        PayrollSupportedObjectType.PayrollResultReference
    ];

    public PayrollContractProfileRequestValidator(Func<T, PayrollContractProfileRequest> selector)
    {
        RuleFor(x => selector(x).ContractVersion).NotEmpty().MaximumLength(64).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).EffectiveFrom).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value > x.EffectiveFrom)
            .WithMessage("EffectiveTo must be greater than EffectiveFrom.");
        RuleFor(x => selector(x).SupportedObjectTypes)
            .NotEmpty()
            .Must(values => values.All(value => AllowedObjectTypes.Contains(value)))
            .WithMessage("SupportedObjectTypes must not include payroll/time-attendance ownership objects.");
        RuleForEach(x => selector(x).SupportedObjectTypes).IsInEnum();
        RuleForEach(x => selector(x).StatusVocabulary).NotEmpty().MaximumLength(64).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).ErrorVocabulary)
            .Must(values => values == null || values.All(value => value.Length <= 128))
            .WithMessage("ErrorVocabulary values must be 128 characters or fewer.")
            .Must(values => values == null || values.All(value => !PayrollSensitiveValueGuard.LooksLikeRawSecret(value) && !PayrollSensitiveValueGuard.LooksLikeRawPayrollPayload(value)))
            .WithMessage("ErrorVocabulary must not contain raw credentials, tokens, secrets, or raw payroll payload.");
        RuleFor(x => selector(x).CorrelationIdPattern).MaximumLength(128).MustNotContainSensitivePayrollData();
    }
}

public class PayrollEmployeeReferenceMapRequestValidator<T> : AbstractValidator<T>
{
    public PayrollEmployeeReferenceMapRequestValidator(Func<T, PayrollEmployeeReferenceMapRequest> selector)
    {
        RuleFor(x => selector(x).ExternalEmployeeReference).NotEmpty().MaximumLength(128).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).MappingState).IsInEnum();
        RuleFor(x => selector(x))
            .Must(x => x.MappingState != PayrollReferenceMappingState.Mapped
                       || x.PersonReferenceId.HasValue
                       || x.OrganizationUnitReferenceId.HasValue
                       || x.PositionReferenceId.HasValue)
            .WithMessage("Mapped payroll employee reference requires at least one same-tenant MOD-0288 reference.");
    }
}

public class PayrollCycleReferenceRequestValidator<T> : AbstractValidator<T>
{
    public PayrollCycleReferenceRequestValidator(Func<T, PayrollCycleReferenceRequest> selector)
    {
        RuleFor(x => selector(x).ExternalPayCycleId).NotEmpty().MaximumLength(128).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).CycleCode).NotEmpty().MaximumLength(64).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).PeriodStart).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => x.PeriodEnd >= x.PeriodStart)
            .WithMessage("PeriodEnd must be on or after PeriodStart.");
        RuleFor(x => selector(x).ProcessingState).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitivePayrollData();
    }
}

public class PayrollResultReferenceRequestValidator<T> : AbstractValidator<T>
{
    public PayrollResultReferenceRequestValidator(Func<T, PayrollResultReferenceRequest> selector)
    {
        RuleFor(x => selector(x).PayrollCycleReferenceId).NotEmpty();
        RuleFor(x => selector(x).ExternalPayrollResultId).NotEmpty().MaximumLength(128).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).ResultVersion).NotEmpty().MaximumLength(64).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).ResultState).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitivePayrollData();
    }
}

public class PayrollSourceHealthSnapshotRequestValidator<T> : AbstractValidator<T>
{
    public PayrollSourceHealthSnapshotRequestValidator(Func<T, PayrollSourceHealthSnapshotRequest> selector)
    {
        RuleFor(x => selector(x).HealthState).IsInEnum();
        RuleFor(x => selector(x).CheckedAt).NotEmpty();
        RuleFor(x => selector(x).RedactedMessage).MaximumLength(1000).MustNotContainSensitivePayrollData();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitivePayrollData();
    }
}

internal static class PayrollValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> MustNotContainSensitivePayrollData<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => !PayrollSensitiveValueGuard.LooksLikeRawSecret(value) && !PayrollSensitiveValueGuard.LooksLikeRawPayrollPayload(value))
            .WithMessage("Value must not contain raw credentials, tokens, secrets, raw payroll payload, payslip, bank, or tax data.");
}
