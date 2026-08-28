using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class Fu37CManualLinkScopeGuardrailTests
{
    private readonly DocumentLinkScopeCompatibilityValidator _validator = new();

    [Fact]
    public void Matching_company_target_is_compatible()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        Assert.True(_validator.Validate(entry, document).IsCompatible);
    }

    [Fact]
    public void Cross_scope_is_blocked()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        document.DocumentScope = DocumentScope.Corporate;
        Assert.Equal(MasterRegisterReasonCodes.ScopeMismatch, _validator.Validate(entry, document).ReasonCode);
    }

    [Fact]
    public void Cross_owner_is_blocked()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        document.ScopeOwnerId = Guid.NewGuid();
        Assert.Equal(MasterRegisterReasonCodes.ScopeOwnerMismatch, _validator.Validate(entry, document).ReasonCode);
    }

    [Fact]
    public void Cross_collection_is_blocked()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        document.CollectionInstanceId = Guid.NewGuid();
        Assert.Equal(MasterRegisterReasonCodes.CollectionInstanceMismatch, _validator.Validate(entry, document).ReasonCode);
    }

    [Fact]
    public void Cross_folder_is_blocked()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        document.FolderId = Guid.NewGuid();
        Assert.Equal(MasterRegisterReasonCodes.FolderScopeMismatch, _validator.Validate(entry, document).ReasonCode);
    }

    [Fact]
    public void Matching_corporate_target_is_compatible()
    {
        var (entry, document) = Matching(DocumentScope.Corporate);
        Assert.True(_validator.Validate(entry, document).IsCompatible);
    }

    [Fact]
    public void Legacy_missing_target_metadata_is_blocked()
    {
        var (entry, document) = Matching(DocumentScope.Company);
        entry.CollectionInstanceId = Guid.Empty;
        Assert.Equal(MasterRegisterReasonCodes.LegacyLinkReconciliationRequired, _validator.Validate(entry, document).ReasonCode);
    }

    [Theory]
    [InlineData(false, DocumentLinkScopeCompatibilityStatus.Unvalidated)]
    [InlineData(true, DocumentLinkScopeCompatibilityStatus.Unvalidated)]
    [InlineData(true, DocumentLinkScopeCompatibilityStatus.Invalid)]
    public void Governed_relation_fails_closed(bool linked, DocumentLinkScopeCompatibilityStatus status)
    {
        var entry = new DocumentMasterRegisterEntry
        {
            TenantId = Guid.NewGuid(),
            DocumentTitle = "Governance guard",
            IsControlledDocument = true,
            ControlledDocumentId = linked ? Guid.NewGuid() : null,
            LinkScopeCompatibilityStatus = status
        };
        Assert.False(DocumentLinkGovernanceGuard.IsGovernedRelationCompatible(entry));
    }

    private static (DocumentMasterRegisterEntry Entry, ControlledDocument Document) Matching(DocumentScope scope)
    {
        var tenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var collection = Guid.NewGuid();
        var folder = Guid.NewGuid();
        var company = scope == DocumentScope.Company ? owner : Guid.Empty;
        var corporate = scope == DocumentScope.Corporate ? owner : Guid.Empty;
        return (
            new DocumentMasterRegisterEntry
            {
                TenantId = tenant,
                DocumentTitle = "Scope match",
                IsControlledDocument = true,
                DocumentScope = scope,
                ScopeOwnerId = owner,
                OwnerCompanyId = scope == DocumentScope.Company ? company : null,
                CorporateOwnerId = corporate,
                CollectionInstanceId = collection,
                FolderId = folder
            },
            new ControlledDocument
            {
                TenantId = tenant,
                DocumentKey = Guid.NewGuid().ToString("D"),
                DocumentScope = scope,
                ScopeOwnerId = owner,
                CompanyId = company,
                OwnerCompanyId = company,
                CorporateOwnerId = corporate,
                CollectionInstanceId = collection,
                FolderId = folder,
                CollectionPath = "/scope-match",
                Title = "Scope match"
            });
    }
}
