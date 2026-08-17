using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public class TimeAttendanceProviderProfileRequestValidator<T> : AbstractValidator<T>
{
    public TimeAttendanceProviderProfileRequestValidator(Func<T, TimeAttendanceProviderProfileRequest> selector)
    {
        RuleFor(x => selector(x).Code).NotEmpty().MaximumLength(64);
        RuleFor(x => selector(x).DisplayName).NotEmpty().MaximumLength(200).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).ProviderFamily).IsInEnum();
        RuleFor(x => selector(x).ExternalProviderAccountId).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).LifecycleState).IsInEnum();
        RuleFor(x => selector(x).ConnectionProfileReference).MaximumLength(256).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).SupportOwner).MaximumLength(200).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).Notes).MaximumLength(1000).MustNotContainSensitiveTimeAttendanceData();
    }
}

public class TimeAttendanceContractProfileRequestValidator<T> : AbstractValidator<T>
{
    private static readonly TimeAttendanceSupportedObjectType[] AllowedObjectTypes =
    [
        TimeAttendanceSupportedObjectType.Employee,
        TimeAttendanceSupportedObjectType.TimeEntryReference,
        TimeAttendanceSupportedObjectType.AttendanceEventReference,
        TimeAttendanceSupportedObjectType.AttendanceSummaryReference
    ];

    public TimeAttendanceContractProfileRequestValidator(Func<T, TimeAttendanceContractProfileRequest> selector)
    {
        RuleFor(x => selector(x).ContractVersion).NotEmpty().MaximumLength(64).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).EffectiveFrom).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value > x.EffectiveFrom)
            .WithMessage("EffectiveTo must be greater than EffectiveFrom.");
        RuleFor(x => selector(x).SupportedObjectTypes)
            .NotEmpty()
            .Must(values => values.All(value => AllowedObjectTypes.Contains(value)))
            .WithMessage("SupportedObjectTypes must not include internal attendance, leave, roster, or payroll ownership objects.");
        RuleForEach(x => selector(x).SupportedObjectTypes).IsInEnum();
        RuleForEach(x => selector(x).StatusVocabulary).NotEmpty().MaximumLength(64).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).ErrorVocabulary)
            .Must(values => values == null || values.All(value => value.Length <= 128))
            .WithMessage("ErrorVocabulary values must be 128 characters or fewer.")
            .Must(values => values == null || values.All(value => !TimeAttendanceSensitiveValueGuard.LooksLikeRawSecret(value) && !TimeAttendanceSensitiveValueGuard.LooksLikeRawPayload(value)))
            .WithMessage("ErrorVocabulary must not contain raw credentials, tokens, secrets, or raw provider payload.");
        RuleFor(x => selector(x).CorrelationIdPattern).MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
    }
}

public class TimeAttendanceEmployeeReferenceMapRequestValidator<T> : AbstractValidator<T>
{
    public TimeAttendanceEmployeeReferenceMapRequestValidator(Func<T, TimeAttendanceEmployeeReferenceMapRequest> selector)
    {
        RuleFor(x => selector(x).ExternalEmployeeReference).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).MappingState).IsInEnum();
        RuleFor(x => selector(x))
            .Must(x => x.MappingState != TimeAttendanceReferenceMappingState.Mapped
                       || x.HrisReferenceId.HasValue
                       || x.PersonReferenceId.HasValue
                       || x.OrganizationUnitReferenceId.HasValue
                       || x.PositionReferenceId.HasValue)
            .WithMessage("Mapped time-attendance employee reference requires at least one same-tenant canonical reference.");
    }
}

public class TimeAttendanceEventReferenceRequestValidator<T> : AbstractValidator<T>
{
    public TimeAttendanceEventReferenceRequestValidator(Func<T, TimeAttendanceEventReferenceRequest> selector)
    {
        RuleFor(x => selector(x).ExternalEventId).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).EventType)
            .IsInEnum()
            .Must(x => x is TimeAttendanceEventType.TimeEntry
                or TimeAttendanceEventType.ClockIn
                or TimeAttendanceEventType.ClockOut
                or TimeAttendanceEventType.BreakStart
                or TimeAttendanceEventType.BreakEnd
                or TimeAttendanceEventType.AttendanceAdjustment)
            .WithMessage("EventType must not include internal leave, payroll, or roster optimization objects.");
        RuleFor(x => selector(x).ExternalEmployeeReference).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).ProviderTimestamp).NotEmpty();
        RuleFor(x => selector(x).ProviderTimeZoneId).MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).ProcessingState).IsInEnum();
        RuleFor(x => selector(x).IdempotencyKey).NotEmpty().MaximumLength(256).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
    }
}

public class AttendanceSummaryReferenceRequestValidator<T> : AbstractValidator<T>
{
    public AttendanceSummaryReferenceRequestValidator(Func<T, AttendanceSummaryReferenceRequest> selector)
    {
        RuleFor(x => selector(x).ExternalSummaryId).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).ExternalEmployeeReference).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).SummaryPeriodStart).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => x.SummaryPeriodEnd >= x.SummaryPeriodStart)
            .WithMessage("SummaryPeriodEnd must be on or after SummaryPeriodStart.");
        RuleFor(x => selector(x).SummaryState).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
    }
}

public class TimeAttendanceSyncCheckpointRequestValidator<T> : AbstractValidator<T>
{
    public TimeAttendanceSyncCheckpointRequestValidator(Func<T, TimeAttendanceSyncCheckpointRequest> selector)
    {
        RuleFor(x => selector(x).SyncRunId).NotEmpty().MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).SyncMode).IsInEnum();
        RuleFor(x => selector(x).StartedAt).NotEmpty();
        RuleFor(x => selector(x))
            .Must(x => !x.CompletedAt.HasValue || x.CompletedAt.Value >= x.StartedAt)
            .WithMessage("CompletedAt must be on or after StartedAt.");
        RuleFor(x => selector(x).CheckpointReference).MaximumLength(256).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).RecordsSeen).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x).RecordsAccepted).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x).RecordsRejected).GreaterThanOrEqualTo(0);
        RuleFor(x => selector(x))
            .Must(x => x.RecordsAccepted + x.RecordsRejected <= x.RecordsSeen)
            .WithMessage("Accepted and rejected records cannot exceed RecordsSeen.");
        RuleFor(x => selector(x).Status).IsInEnum();
        RuleFor(x => selector(x).ErrorSummary).MaximumLength(1000).MustNotContainSensitiveTimeAttendanceData();
    }
}

public class TimeAttendanceProviderHealthSnapshotRequestValidator<T> : AbstractValidator<T>
{
    public TimeAttendanceProviderHealthSnapshotRequestValidator(Func<T, TimeAttendanceProviderHealthSnapshotRequest> selector)
    {
        RuleFor(x => selector(x).HealthState).IsInEnum();
        RuleFor(x => selector(x).CheckedAt).NotEmpty();
        RuleFor(x => selector(x).RedactedMessage).MaximumLength(1000).MustNotContainSensitiveTimeAttendanceData();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustNotContainSensitiveTimeAttendanceData();
    }
}

internal static class TimeAttendanceValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> MustNotContainSensitiveTimeAttendanceData<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => !TimeAttendanceSensitiveValueGuard.LooksLikeRawSecret(value) && !TimeAttendanceSensitiveValueGuard.LooksLikeRawPayload(value))
            .WithMessage("Value must not contain raw credentials, tokens, secrets, raw time/attendance payload, biometric, geolocation, or payroll data.");
}
