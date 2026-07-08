namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public static class EmployeeMasterValidationContracts
{
    public static readonly IReadOnlyList<string> ActivationRequiredFields =
    [
        "legal_first_name",
        "legal_last_name",
        "hire_date",
        "person_id",
        "legal_entity_id",
        "worker_type",
        "employment_type",
        "employee_number"
    ];

    public static IReadOnlyList<string> ValidateActivationReadiness(EmployeeActivationReadinessContract contract)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(contract.LegalFirstName))
        {
            errors.Add("legal_first_name_required_before_activation");
        }

        if (string.IsNullOrWhiteSpace(contract.LegalLastName))
        {
            errors.Add("legal_last_name_required_before_activation");
        }

        if (contract.HireDate is null)
        {
            errors.Add("hire_date_required_before_activation");
        }

        if (contract.PersonId == Guid.Empty || !contract.PersonReferenceIsSameTenant)
        {
            errors.Add("person_reference_must_resolve_same_tenant");
        }

        if (contract.LegalEntityId == Guid.Empty || !contract.LegalEntityReferenceIsSameTenant)
        {
            errors.Add("legal_entity_reference_must_resolve_same_tenant");
        }

        if (!EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.WorkerTypes, contract.WorkerType))
        {
            errors.Add("worker_type_must_be_reference_data_code");
        }

        if (!EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.EmploymentTypes, contract.EmploymentType))
        {
            errors.Add("employment_type_must_be_reference_data_code");
        }

        if (!contract.EmployeeNumberIsUniqueForTenant)
        {
            errors.Add("employee_number_must_be_unique_for_tenant");
        }

        return errors;
    }

    public static bool IsPatchConcurrencyTokenPresent(string? etag)
        => !string.IsNullOrWhiteSpace(etag);

    public static bool IsIdempotencyKeyPresent(string? idempotencyKey)
        => !string.IsNullOrWhiteSpace(idempotencyKey);
}

public sealed record EmployeeActivationReadinessContract(
    string? LegalFirstName,
    string? LegalLastName,
    DateOnly? HireDate,
    Guid PersonId,
    bool PersonReferenceIsSameTenant,
    Guid LegalEntityId,
    bool LegalEntityReferenceIsSameTenant,
    string? WorkerType,
    string? EmploymentType,
    bool EmployeeNumberIsUniqueForTenant);
