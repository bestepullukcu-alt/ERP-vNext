using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.Contact;

/// <summary>
/// Validates Contact reference fields against MOD-0048 / PSS-012 published sets. Mirrors AccountReferenceValidation:
/// no CRM local seed / hardcoded fallback — a missing required set surfaces as a controlled error (blocks create/update).
/// </summary>
public static class ContactReferenceValidation
{
    public const string ContactTypeSet = "contact-type";
    public const string ContactStatusSet = "contact-status";

    // MOD-0150 Contact Location Hardening — optional location sets (shared with MOD-0149 Account). No CRM local seed;
    // SetMissing is tolerated for optional fields (value simply not validated until published), InvalidValue → 400.
    public const string CountrySet = "country";
    public const string CitySet = "city";
    public const string DistrictSet = "district";

    // MOD-0150 pack §10 — optional professional reference sets (title/specialty/department). Same optional contract.
    public const string ProfessionalTitleSet = "professional-title";
    public const string MedicalSpecialtySet = "medical-specialty";
    public const string DepartmentTypeSet = "department-type";
    public const string GenderSet = "gender";

    public static async Task<IReadOnlyList<string>> ValidateAsync(
        IReferenceDataValidator validator, string contactType, string status, CancellationToken cancellationToken)
        => await ValidateAsync(validator, contactType, status, null, null, null, null, null, null, null, cancellationToken);

    public static async Task<IReadOnlyList<string>> ValidateAsync(
        IReferenceDataValidator validator, string contactType, string status,
        string? countryRef, string? cityRef, string? districtRef,
        string? professionalTitle, string? specialty, string? department, string? gender, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        await CheckRequiredAsync(validator, ContactTypeSet, contactType, errors, cancellationToken);
        await CheckRequiredAsync(validator, ContactStatusSet, status, errors, cancellationToken);
        await CheckOptionalAsync(validator, CountrySet, countryRef, errors, cancellationToken);
        await CheckOptionalAsync(validator, CitySet, cityRef, errors, cancellationToken);
        await CheckOptionalAsync(validator, DistrictSet, districtRef, errors, cancellationToken);
        await CheckOptionalAsync(validator, ProfessionalTitleSet, professionalTitle, errors, cancellationToken);
        await CheckOptionalAsync(validator, MedicalSpecialtySet, specialty, errors, cancellationToken);
        await CheckOptionalAsync(validator, DepartmentTypeSet, department, errors, cancellationToken);
        await CheckOptionalAsync(validator, GenderSet, gender, errors, cancellationToken);
        return errors;
    }

    /// <summary>Optional location field: unpublished set is tolerated; only an unknown published value blocks (→ 400).</summary>
    private static async Task CheckOptionalAsync(
        IReferenceDataValidator validator, string setCode, string? value, List<string> errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var result = await validator.ValidateAsync(setCode, value, ct);
        if (result.Status == ReferenceValidationStatus.InvalidValue)
        {
            errors.Add($"'{value}' is not a valid published value of reference set '{setCode}'.");
        }
    }

    private static async Task CheckRequiredAsync(
        IReferenceDataValidator validator, string setCode, string value, List<string> errors, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"'{setCode}' is required.");
            return;
        }

        var result = await validator.ValidateAsync(setCode, value, ct);
        switch (result.Status)
        {
            case ReferenceValidationStatus.InvalidValue:
                errors.Add($"'{value}' is not a valid published value of reference set '{setCode}'.");
                break;
            case ReferenceValidationStatus.SetMissing:
                errors.Add($"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).");
                break;
        }
    }
}
