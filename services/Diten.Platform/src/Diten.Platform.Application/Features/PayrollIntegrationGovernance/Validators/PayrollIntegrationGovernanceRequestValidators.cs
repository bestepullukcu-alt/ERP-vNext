using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Validators;

internal static class PayrollIntegrationGovernanceValidatorExtensions
{
    public static IRuleBuilderOptions<T, string?> MustBeRedactedGovernanceText<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => !PayrollIntegrationGovernanceSensitiveValueGuard.LooksUnsafe(value))
            .WithMessage("Value must not contain raw payload, credential, token, secret, bank, tax, payslip, biometric, geolocation, provider adapter, or payroll calculation data.");
}

public class PayrollIntegrationRunRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationRunRequestValidator(Func<T, PayrollIntegrationRunRequest> selector)
    {
        RuleFor(x => selector(x).RunCode).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).PayrollSourceProfileId).NotEmpty();
        RuleFor(x => selector(x).ContractVersion).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).RunType).IsInEnum();
        RuleFor(x => selector(x).Status).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).NotEmpty().MaximumLength(128).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).IdempotencyKey).NotEmpty().MaximumLength(256).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x))
            .Must(x => !x.CompletedAt.HasValue || !x.StartedAt.HasValue || x.CompletedAt.Value >= x.StartedAt.Value)
            .WithMessage("CompletedAt must be on or after StartedAt.");
        RuleFor(x => selector(x).Summary).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationRunStatusRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationRunStatusRequestValidator(Func<T, PayrollIntegrationRunStatusRequest> selector)
    {
        RuleFor(x => selector(x).Status).IsInEnum();
        RuleFor(x => selector(x))
            .Must(x => !x.CompletedAt.HasValue || !x.StartedAt.HasValue || x.CompletedAt.Value >= x.StartedAt.Value)
            .WithMessage("CompletedAt must be on or after StartedAt.");
        RuleFor(x => selector(x).Summary).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationSourceLinkRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationSourceLinkRequestValidator(Func<T, PayrollIntegrationSourceLinkRequest> selector)
    {
        RuleFor(x => selector(x).SourceType).IsInEnum();
        RuleFor(x => selector(x).SourceReferenceId).NotEmpty();
        RuleFor(x => selector(x).SourceContractVersion).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).LinkState).IsInEnum();
        RuleFor(x => selector(x).ValidationMessage).MaximumLength(500).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationMappingControlRequestValidator<T> : AbstractValidator<T>
{
    private static readonly PayrollIntegrationMappingScope[] AllowedScopes =
    [
        PayrollIntegrationMappingScope.Employee,
        PayrollIntegrationMappingScope.Org,
        PayrollIntegrationMappingScope.Job,
        PayrollIntegrationMappingScope.PayrollResult,
        PayrollIntegrationMappingScope.AttendanceSummary
    ];

    public PayrollIntegrationMappingControlRequestValidator(Func<T, PayrollIntegrationMappingControlRequest> selector)
    {
        RuleFor(x => selector(x).MappingScope).IsInEnum().Must(x => AllowedScopes.Contains(x)).WithMessage("MappingScope must not include payroll calculation fields.");
        RuleFor(x => selector(x).SourceReferenceId).NotEmpty();
        RuleFor(x => selector(x).ControlState).IsInEnum();
        RuleFor(x => selector(x).MismatchCode).MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).ResolutionNote).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollReconciliationControlRequestValidator<T> : AbstractValidator<T>
{
    public PayrollReconciliationControlRequestValidator(Func<T, PayrollReconciliationControlRequest> selector)
    {
        RuleFor(x => selector(x).ReconciliationType).IsInEnum();
        RuleFor(x => selector(x).ExpectedCount).GreaterThanOrEqualTo(0).When(x => selector(x).ExpectedCount.HasValue);
        RuleFor(x => selector(x).ObservedCount).GreaterThanOrEqualTo(0).When(x => selector(x).ObservedCount.HasValue);
        RuleFor(x => selector(x).ControlState).IsInEnum();
        RuleFor(x => selector(x).Notes).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationExceptionRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationExceptionRequestValidator(Func<T, PayrollIntegrationExceptionRequest> selector)
    {
        RuleFor(x => selector(x).ExceptionCode).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).Severity).IsInEnum();
        RuleFor(x => selector(x).ExceptionState).IsInEnum();
        RuleFor(x => selector(x).RedactedMessage).NotEmpty().MaximumLength(1000).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).ResolutionNote).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationExceptionResolutionRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationExceptionResolutionRequestValidator(Func<T, PayrollIntegrationExceptionResolutionRequest> selector)
    {
        RuleFor(x => selector(x).ExceptionState).IsInEnum();
        RuleFor(x => selector(x).ResolutionNote).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationRetryReplayRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationRetryReplayRequestValidator(Func<T, PayrollIntegrationRetryReplayRequestModel> selector)
    {
        RuleFor(x => selector(x).RequestType).IsInEnum();
        RuleFor(x => selector(x).PurposeCode).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).IdempotencyKey).NotEmpty().MaximumLength(256).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).RequestState).IsInEnum();
        RuleFor(x => selector(x).RedactedReason).NotEmpty().MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationEvidenceExportReferenceRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationEvidenceExportReferenceRequestValidator(Func<T, PayrollIntegrationEvidenceExportReferenceRequest> selector)
    {
        RuleFor(x => selector(x).ExportPurposeCode).NotEmpty().MaximumLength(64).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).ExportState).IsInEnum();
        RuleFor(x => selector(x).CorrelationId).NotEmpty().MaximumLength(128).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).RedactedNotes).MaximumLength(1000).MustBeRedactedGovernanceText();
    }
}

public class PayrollIntegrationHealthSnapshotRequestValidator<T> : AbstractValidator<T>
{
    public PayrollIntegrationHealthSnapshotRequestValidator(Func<T, PayrollIntegrationHealthSnapshotRequest> selector)
    {
        RuleFor(x => selector(x).HealthState).IsInEnum();
        RuleFor(x => selector(x).CheckedAt).NotEmpty();
        RuleFor(x => selector(x).RedactedMessage).MaximumLength(1000).MustBeRedactedGovernanceText();
        RuleFor(x => selector(x).CorrelationId).MaximumLength(128).MustBeRedactedGovernanceText();
    }
}
