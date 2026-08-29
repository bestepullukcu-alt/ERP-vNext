using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Validators;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class Fu37ScopeAwareRegistrationTests
{
    [Fact]
    public void Missing_scope_defaults_to_company_at_api_contract()
    {
        var request = new CreateControlledDocumentRegistrationApiRequest();
        Assert.Null(request.DocumentScope);
        Assert.DoesNotContain("TenantId", request.GetType().GetProperties().Select(x => x.Name));
    }

    [Fact]
    public void Company_requires_company_owners_and_rejects_corporate_owner()
    {
        var input = ValidInput() with { CompanyId = Guid.Empty, OwnerCompanyId = Guid.Empty };
        input = input with { CorporateOwnerId = Guid.NewGuid() };
        var result = Validate(input);

        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("CompanyId", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("OwnerCompanyId", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("CorporateOwnerId", StringComparison.Ordinal));
    }

    [Fact]
    public void Corporate_requires_corporate_owner_and_rejects_company_owners()
    {
        var input = ValidInput() with
        {
            DocumentScope = DocumentScope.Corporate,
            CorporateOwnerId = Guid.Empty,
            FolderId = Guid.NewGuid()
        };
        var result = Validate(input);
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("CorporateOwnerId", StringComparison.Ordinal));

        input = input with
        {
            CorporateOwnerId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            OwnerCompanyId = Guid.NewGuid()
        };
        result = Validate(input);
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("CompanyId", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("OwnerCompanyId", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_scope_and_missing_governed_values_are_rejected()
    {
        var input = ValidInput() with
        {
            DocumentScope = (DocumentScope)99,
            GoverningLanguageId = "",
            RetentionClassId = "",
            GoverningLanguage = "",
            RetentionClass = null
        };
        var result = Validate(input);
        Assert.Contains(result.Errors, x => x.PropertyName.EndsWith("DocumentScope", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("governed language", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("governed retention", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Operation_scope_snapshot_is_set_once_and_retarget_is_rejected()
    {
        var operation = new ControlledDocumentRegistrationOperation
        {
            TenantId = Guid.NewGuid(),
            IdempotencyKey = "idem",
            CorrelationId = "corr",
            CreatedBy = "test"
        };
        var owner = Guid.NewGuid();
        var folder = Guid.NewGuid();
        var instance = folder;
        var partition = $"tenant/{operation.TenantId:D}/corporate/{owner:D}/folder/{folder:D}";

        Assert.True(operation.CaptureScopeSnapshot(
            DocumentScope.Corporate, owner, Guid.Empty, Guid.Empty, owner, instance, folder, partition,
            Guid.NewGuid(), null, "en", "quality-record", "Quality", "Owner", null, "fp-1", "test"));
        Assert.False(operation.CaptureScopeSnapshot(
            DocumentScope.Company, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, instance, folder,
            partition, null, null, "en", "quality-record", null, null, null, "fp-2", "test"));
        Assert.Equal(DocumentScope.Corporate, operation.DocumentScope);
        Assert.Equal(Guid.Empty, operation.CompanyId);
        Assert.Equal(owner, operation.CorporateOwnerId);
    }

    [Fact]
    public void Controlled_document_typed_ownership_keeps_company_and_corporate_distinct()
    {
        var corporateOwner = Guid.NewGuid();
        var corporate = NewDocument();
        corporate.DocumentScope = DocumentScope.Corporate;
        corporate.ScopeOwnerId = corporateOwner;
        corporate.CorporateOwnerId = corporateOwner;
        corporate.CompanyId = Guid.Empty;
        corporate.OwnerCompanyId = Guid.Empty;
        Assert.Equal(Guid.Empty, corporate.CompanyId);
        Assert.Equal(corporateOwner, corporate.ScopeOwnerId);

        var company = Guid.NewGuid();
        var companyDocument = NewDocument();
        companyDocument.DocumentScope = DocumentScope.Company;
        companyDocument.ScopeOwnerId = company;
        companyDocument.CompanyId = company;
        companyDocument.OwnerCompanyId = company;
        Assert.Equal(company, companyDocument.CompanyId);
        Assert.Equal(Guid.Empty, companyDocument.CorporateOwnerId);
    }

    [Fact]
    public void Storage_partition_literals_are_scope_separated()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var folder = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var builder = new CorporateCollectionStoragePartitionBuilder(tenant);

        Assert.Equal($"tenant/{tenantId:D}/company/{owner:D}/folder/{folder:D}", builder.ForCompany(owner, folder));
        Assert.Equal($"tenant/{tenantId:D}/corporate/{owner:D}/folder/{folder:D}", builder.ForCorporate(owner, folder));
    }

    [Fact]
    public void Legacy_company_folder_uses_company_id_when_scope_owner_was_not_migrated()
    {
        var companyId = Guid.NewGuid();
        var legacyFolder = Folder(companyId, Guid.Empty, "COMPANY");

        Assert.True(ControlledDocumentRegistrationService.IsScopeOwnerAligned(
            legacyFolder,
            DocumentScope.Company,
            companyId,
            companyId));
    }

    [Fact]
    public void Corporate_folder_never_falls_back_to_company_id()
    {
        var corporateOwnerId = Guid.NewGuid();
        var folder = Folder(corporateOwnerId, Guid.Empty, "CORPORATE");

        Assert.False(ControlledDocumentRegistrationService.IsScopeOwnerAligned(
            folder,
            DocumentScope.Corporate,
            corporateOwnerId,
            corporateOwnerId));
    }

    private static FluentValidation.Results.ValidationResult Validate(CreateControlledDocumentRegistrationInput input) =>
        new CreateControlledDocumentRegistrationValidator().Validate(
            new CreateControlledDocumentRegistrationCommand(input, "corr"));

    private static CreateControlledDocumentRegistrationInput ValidInput() => new(
        "idem", "Title", "Policy", "Standard", "Policy", null, null, "en", "Quality",
        Guid.NewGuid(), "Owner", null, 12, "quality-record", Guid.NewGuid(), Guid.NewGuid(),
        new FileUploadInput("policy.pdf", "application/pdf", "YQ=="))
    {
        GoverningLanguageId = "en",
        RetentionClassId = "quality-record"
    };

    private static ControlledDocument NewDocument() => new()
    {
        TenantId = Guid.NewGuid(),
        DocumentKey = "doc",
        CollectionInstanceId = Guid.NewGuid(),
        FolderId = Guid.NewGuid(),
        CollectionPath = "QMS/Policies",
        Title = "Policy",
        CreatedBy = "test"
    };

    private static CollectionInstanceReferenceDto Folder(
        Guid companyId,
        Guid scopeOwnerId,
        string scopeType) => new(
        Guid.NewGuid(),
        companyId,
        Guid.NewGuid(),
        "folder",
        null,
        "Folder",
        "Folder",
        "ACTIVE",
        true,
        [],
        CollectionScopeType: scopeType,
        ScopeOwnerId: scopeOwnerId);
}
