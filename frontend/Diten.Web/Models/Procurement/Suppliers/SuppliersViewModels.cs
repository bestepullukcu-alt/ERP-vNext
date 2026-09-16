using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Procurement.Suppliers;

// MOD-0140 Supplier — frontend view models.
// Fields bind ONLY the SUPPLIER contract (docs/analysis/contracts/supplier.openapi.yaml):
// Supplier / SupplierUpsert (master) + OnboardingSubmit (KYC / documents / approval).
public sealed class SuppliersEditViewModel
{
    // Server-assigned public code (Supplier.supplierId); empty on create.
    public string? SupplierId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    // SupplierStatus enum: Active | OnHold | Blocked | Inactive.
    [Required]
    public string Status { get; set; } = "Active";

    // lowercase country code (nullable).
    public string? Country { get; set; }
    public string? TaxId { get; set; }

    // Primary Contact (Contact schema): type in {primary,billing,quality,logistics}.
    public string? ContactType { get; set; } = "primary";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // SupplierUpsert additive (DEC-INV-19 external feed).
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    // Onboarding (OnboardingSubmit): KYC + document reference + submit-for-approval.
    public string? KycLegalName { get; set; }
    public string? KycRegistrationNo { get; set; }
    public string? DocumentType { get; set; }
    public bool SubmitForApproval { get; set; }

    // Onboarding outcomes (read-only, shown on Details; OnboardingCase surface).
    public string? OnboardingStatus { get; set; }
    public string? KycOutcome { get; set; }
    public string? SanctionsOutcome { get; set; }
}

// Deserialization of the SUPPLIER contract Supplier schema (inside the house Response<T>.Data).
public sealed class SupplierApiModel
{
    public string SupplierId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public List<SupplierContactApiModel> Contacts { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class SupplierContactApiModel
{
    public string? Type { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

// Deserialization of the OnboardingCase schema.
public sealed class SupplierOnboardingApiModel
{
    public string? OnboardingStatus { get; set; }
    public string? KycOutcome { get; set; }
    public string? SanctionsOutcome { get; set; }
}

// POST/PATCH payload — SupplierUpsert shape (+ status carried from the Supplier surface).
public sealed class SupplierSavePayload
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public List<SupplierContactPayload> Contacts { get; set; } = [];
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
}

public sealed class SupplierContactPayload
{
    public string? Type { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

// POST /api/suppliers/{supplierId}/onboarding — OnboardingSubmit shape.
public sealed class SupplierOnboardingPayload
{
    public SupplierKycPayload? Kyc { get; set; }
    public List<SupplierDocumentPayload> Documents { get; set; } = [];
    public bool SubmitForApproval { get; set; }
}

public sealed class SupplierKycPayload
{
    public string? LegalName { get; set; }
    public string? RegistrationNo { get; set; }
}

public sealed class SupplierDocumentPayload
{
    public string? Type { get; set; }
    public string? EvidenceRef { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
