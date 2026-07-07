using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class EmployeeProfilePatchRequestValidator : AbstractValidator<EmployeeProfilePatchRequest>
{
    public EmployeeProfilePatchRequestValidator()
    {
        RuleFor(x => x.ETag)
            .NotEmpty()
            .WithMessage("ETag is required for employee profile patch contracts.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.LegalFirstName)
            .MaximumLength(100);

        RuleFor(x => x.LegalMiddleName)
            .MaximumLength(100);

        RuleFor(x => x.LegalLastName)
            .MaximumLength(100);

        RuleFor(x => x.PreferredName)
            .MaximumLength(100);

        RuleFor(x => x.WorkEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.WorkEmail));

        RuleFor(x => x.PersonalEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));

        RuleFor(x => x.SensitivityLevel)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.SensitivityLevels, value))
            .WithMessage("Sensitivity level must be a known reference-data code.");
    }
}

public sealed class EmploymentRecordPatchRequestValidator : AbstractValidator<EmploymentRecordPatchRequest>
{
    public EmploymentRecordPatchRequestValidator()
    {
        RuleFor(x => x.ETag)
            .NotEmpty()
            .WithMessage("ETag is required for employment record patch contracts.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ContractType)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.ContractTypes, value))
            .WithMessage("Contract type must be a known reference-data code.");

        RuleFor(x => x.EmploymentStatus)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.EmploymentStatuses, value))
            .WithMessage("Employment status must be a known reference-data code.");
    }
}

public sealed class EmployeeStatusCommandRequestValidator : AbstractValidator<EmployeeStatusCommandRequest>
{
    public EmployeeStatusCommandRequestValidator()
    {
        RuleFor(x => x.ETag)
            .NotEmpty()
            .WithMessage("ETag is required for employee status command contracts.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NewStatus)
            .Must(value => EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.EmployeeStatuses, value))
            .WithMessage("Employee status must be a known reference-data code.");

        RuleFor(x => x.ReasonCategory)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.TerminationReasonCategories, value))
            .WithMessage("Reason category must be a known reference-data code.");
    }
}

public sealed class EmployeeDocumentLinkRequestValidator : AbstractValidator<EmployeeDocumentLinkRequest>
{
    public EmployeeDocumentLinkRequestValidator()
    {
        RuleFor(x => x.ETag)
            .NotEmpty()
            .WithMessage("ETag is required for employee document link contracts.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.EvidenceId)
            .NotEmpty();

        RuleFor(x => x.RetentionPolicyId)
            .NotEmpty();

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.VisibilityLevel)
            .NotEmpty()
            .MaximumLength(80);
    }
}

public sealed class DataQualityCasePatchRequestValidator : AbstractValidator<DataQualityCasePatchRequest>
{
    public DataQualityCasePatchRequestValidator()
    {
        RuleFor(x => x.ETag)
            .NotEmpty()
            .WithMessage("ETag is required for data-quality case patch contracts.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Status)
            .Must(value => string.IsNullOrWhiteSpace(value)
                || EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.DataQualityCaseStatuses, value))
            .WithMessage("Data-quality case status must be a known reference-data code.");
    }
}
