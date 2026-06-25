using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — FU01-owned sidecar record keyed by <see cref="CollectionInstanceId"/> that holds the
/// folder-level (Layer 2) document permissions for a grantee. It NEVER mutates the read-only MOD-0028
/// <c>CollectionInstance</c>; permission fields live here, not on the MOD-0028 structure object.
/// </summary>
public sealed class FolderDocumentAccessPolicy : TenantScopedEntity
{
    public required Guid CollectionInstanceId { get; set; }
    public required Guid CompanyId { get; set; }
    public AccessTargetType TargetType { get; set; }
    public required string TargetId { get; set; }
    public FolderPermissionSet FolderPermissions { get; set; } = new();
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class FolderPermissionSet
{
    public bool CanViewFolderDocuments { get; set; }
    public bool CanUploadDocument { get; set; }
    public bool CanEditFolderDocuments { get; set; }
    public bool CanUploadNewVersion { get; set; }
    public bool CanShareFolderDocuments { get; set; }
    public bool CanManageFolderDocumentAccess { get; set; }
}
