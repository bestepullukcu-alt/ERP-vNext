using Diten.MdmService.Application.Features.LegalEntity;

namespace Diten.MdmService.Application.Tests;

internal static class LegalEntityTestData
{
    public const string ValidAddressJson = """{"line1":"1 Market St","city":"Istanbul","country":"TR"}""";

    // A fully-valid write request (passes LegalEntityWriteRequestValidator). Override per test as needed.
    public static LegalEntityWriteRequest ValidRequest(
        string code = "LE-001",
        string legalName = "Contoso Legal Entity",
        string? organizationRoleCode = "LEGALENTITY",
        Guid? parentLegalEntityId = null,
        string? registeredAddressJson = ValidAddressJson,
        string? vatNumber = "VAT-EU-123",
        string? placeOfIncorporation = "Istanbul, TR",
        DateTimeOffset? incorporationDate = null,
        DateTimeOffset? dissolutionDate = null)
        => new(
            Code: code,
            LegalName: legalName,
            DisplayName: "Contoso",
            LegalFormCode: "CORPORATION",
            OrganizationRoleCode: organizationRoleCode,
            RegistrationNumber: "REG-123",
            TaxId: "TAX-123",
            VatNumber: vatNumber,
            PlaceOfIncorporation: placeOfIncorporation,
            IncorporationDate: incorporationDate,
            DissolutionDate: dissolutionDate,
            CountryCode: "TR",
            StatutoryStatus: "Registered",
            ParentLegalEntityId: parentLegalEntityId,
            OwnershipPercent: null,
            ControlTypeCode: null,
            FiscalYearVariant: null,
            AccountingStandardCode: "IFRS",
            TaxRegimeCode: "STANDARD",
            BaseCurrencyCode: "TRY",
            RegisteredAddressJson: registeredAddressJson,
            CorrespondenceAddressJson: null,
            OfficialEmail: "info@contoso.example",
            OfficialPhone: "+90 212 000 0000",
            Website: "https://contoso.example",
            ApprovalStatus: "Draft",
            ReviewDueUtc: null,
            SourceSystem: null,
            LegacyCode: null,
            EvidenceStatus: "NotStarted");
}
