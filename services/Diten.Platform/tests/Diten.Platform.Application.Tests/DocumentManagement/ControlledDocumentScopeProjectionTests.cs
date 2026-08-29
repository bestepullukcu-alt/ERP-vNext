using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class ControlledDocumentScopeProjectionTests
{
    [Fact]
    public void Legacy_document_without_scope_snapshot_projects_as_legacy_without_throwing()
    {
        var companyId = Guid.NewGuid();
        var model = ControlledDocumentMapping.ToListItem(Document(companyId));

        Assert.Equal("LEGACY", model.DocumentScope);
        Assert.Equal(companyId, model.CompanyId);
        Assert.Null(model.ScopeOwnerId);
        Assert.Null(model.CorporateOwnerId);
        Assert.Null(model.FolderId);
    }

    [Fact]
    public void Corporate_document_projects_no_dummy_company()
    {
        var document = Document(Guid.Empty);
        document.DocumentScope = DocumentScope.Corporate;
        document.CorporateOwnerId = Guid.NewGuid();
        document.ScopeOwnerId = document.CorporateOwnerId;
        document.FolderId = document.CollectionInstanceId;

        var model = ControlledDocumentMapping.ToListItem(document);

        Assert.Equal("CORPORATE", model.DocumentScope);
        Assert.Null(model.CompanyId);
        Assert.Equal(document.CorporateOwnerId, model.CorporateOwnerId);
        Assert.Equal(document.FolderId, model.FolderId);
    }

    private static ControlledDocument Document(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DocumentKey = Guid.NewGuid().ToString("D"),
        CompanyId = companyId,
        OwnerCompanyId = companyId,
        CollectionInstanceId = Guid.NewGuid(),
        CollectionPath = "/quality",
        Title = "Runtime compatibility",
        DocumentType = DocumentType.Other,
        CreatedBy = "test"
    };
}
