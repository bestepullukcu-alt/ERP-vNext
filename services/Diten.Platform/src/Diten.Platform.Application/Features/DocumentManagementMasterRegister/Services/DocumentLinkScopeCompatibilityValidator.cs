using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

public sealed record DocumentLinkScopeCompatibilityResult(bool IsCompatible, string? ReasonCode, string? Message)
{
    public static DocumentLinkScopeCompatibilityResult Compatible() => new(true, null, null);
    public static DocumentLinkScopeCompatibilityResult Blocked(string reasonCode, string message) =>
        new(false, reasonCode, message);
}

/// <summary>
/// FU37C non-waivable relation invariant. Permissions can authorize an attempt but can never waive scope, owner,
/// collection or folder compatibility.
/// </summary>
public sealed class DocumentLinkScopeCompatibilityValidator
{
    public DocumentLinkScopeCompatibilityResult Validate(
        DocumentMasterRegisterEntry entry,
        ControlledDocument document)
    {
        if (entry.DocumentScope != document.DocumentScope)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.ScopeMismatch,
                "The register entry and controlled document scopes are incompatible.");
        }

        if (entry.ScopeOwnerId == Guid.Empty || document.ScopeOwnerId == Guid.Empty)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.LegacyLinkReconciliationRequired,
                "Scope ownership metadata must be reconciled before linking.");
        }

        if (entry.ScopeOwnerId != document.ScopeOwnerId)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.ScopeOwnerMismatch,
                "The register entry and controlled document scope owners are incompatible.");
        }

        if (entry.DocumentScope == DocumentScope.Company)
        {
            if (document.CompanyId == Guid.Empty
                || entry.OwnerCompanyId is null
                || entry.OwnerCompanyId == Guid.Empty
                || entry.OwnerCompanyId != document.CompanyId
                || document.OwnerCompanyId == Guid.Empty
                || document.OwnerCompanyId != entry.OwnerCompanyId)
            {
                return DocumentLinkScopeCompatibilityResult.Blocked(
                    MasterRegisterReasonCodes.ScopeOwnerMismatch,
                    "Company ownership metadata is incompatible.");
            }
        }
        else
        {
            if (entry.CorporateOwnerId == Guid.Empty
                || document.CorporateOwnerId == Guid.Empty
                || entry.CorporateOwnerId != document.CorporateOwnerId)
            {
                return DocumentLinkScopeCompatibilityResult.Blocked(
                    MasterRegisterReasonCodes.ScopeOwnerMismatch,
                    "Corporate ownership metadata is incompatible.");
            }
        }

        if (entry.CollectionInstanceId == Guid.Empty || document.CollectionInstanceId == Guid.Empty)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.LegacyLinkReconciliationRequired,
                "Collection metadata must be reconciled before linking.");
        }

        if (entry.CollectionInstanceId != document.CollectionInstanceId)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.CollectionInstanceMismatch,
                "The register entry and controlled document collection instances are incompatible.");
        }

        if (entry.FolderId == Guid.Empty || document.FolderId == Guid.Empty)
        {
            return DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.LegacyLinkReconciliationRequired,
                "Folder metadata must be reconciled before linking.");
        }

        return entry.FolderId != document.FolderId
            ? DocumentLinkScopeCompatibilityResult.Blocked(
                MasterRegisterReasonCodes.FolderScopeMismatch,
                "The register entry and controlled document folders are incompatible.")
            : DocumentLinkScopeCompatibilityResult.Compatible();
    }
}

public static class DocumentLinkGovernanceGuard
{
    public const string BlockingReason =
        "Controlled-document relation is missing or has not passed scope compatibility validation.";

    public static bool IsGovernedRelationCompatible(DocumentMasterRegisterEntry entry) =>
        !entry.IsControlledDocument
        || (entry.ControlledDocumentId is not null
            && entry.LinkScopeCompatibilityStatus == DocumentLinkScopeCompatibilityStatus.Compatible);
}
