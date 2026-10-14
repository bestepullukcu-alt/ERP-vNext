using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Domain.Entities.DocumentRepository;

namespace Diten.Platform.Application.Features.DocumentRepository.Services;

/// <summary>
/// WP-VW-W3-PLT — who may write, read and compensate what, for callers that reach the repository over its HTTP
/// surface. Pure decisions over the caller's permissions, the object's scope and its creator; the
/// <see cref="DocumentRepositoryService"/> asks them at the service boundary (AD-5).
/// <list type="bullet">
/// <item><b>Write:</b> general <c>manage</c>, or <c>visit-evidence.write</c> AND the target scope is
/// <see cref="ContentStorageScope.CrmVisitEvidence"/>.</item>
/// <item><b>Read:</b> general <c>read</c>, or the object's scope is CrmVisitEvidence AND (<c>visit-evidence.read-all</c>
/// OR (<c>visit-evidence.read</c> AND the caller created it)).</item>
/// <item><b>Compensate:</b> general <c>manage</c>, or <c>visit-evidence.write</c> AND scope CrmVisitEvidence AND the
/// caller created it.</item>
/// </list>
/// The general keys keep exactly their FU01 meaning. A scope-bound key never opens another scope.
/// </summary>
public static class DocumentRepositoryAccessPolicy
{
    private static readonly string VisitEvidenceScope = nameof(ContentStorageScope.CrmVisitEvidence);

    public static bool CanWrite(IActorPermissionContext? permissions, ContentStorageScope scope) =>
        Has(permissions, DocumentRepositoryPermissions.Manage)
        || (Has(permissions, DocumentRepositoryPermissions.VisitEvidenceWrite)
            && scope == ContentStorageScope.CrmVisitEvidence);

    public static bool CanRead(IActorPermissionContext? permissions, RepositoryObject row, string? caller) =>
        Has(permissions, DocumentRepositoryPermissions.Read)
        || (IsVisitEvidence(row)
            && (Has(permissions, DocumentRepositoryPermissions.VisitEvidenceReadAll)
                || (Has(permissions, DocumentRepositoryPermissions.VisitEvidenceRead) && IsCreator(row, caller))));

    public static bool CanCompensate(IActorPermissionContext? permissions, RepositoryObject row, string? caller) =>
        Has(permissions, DocumentRepositoryPermissions.Manage)
        || (Has(permissions, DocumentRepositoryPermissions.VisitEvidenceWrite)
            && IsVisitEvidence(row)
            && IsCreator(row, caller));

    private static bool IsVisitEvidence(RepositoryObject row) =>
        string.Equals(row.Scope, VisitEvidenceScope, StringComparison.Ordinal);

    /// <summary>
    /// <c>CreatedBy</c> is the store-time <c>ICurrentUserContext.ActorName</c>, so "the caller created it" compares
    /// the same value. The anonymous fallback actor ("system") never owns anything: two unauthenticated contexts
    /// must not match each other.
    /// </summary>
    private static bool IsCreator(RepositoryObject row, string? caller) =>
        !string.IsNullOrWhiteSpace(caller)
        && !string.Equals(caller, "system", StringComparison.OrdinalIgnoreCase)
        && string.Equals(row.CreatedBy, caller, StringComparison.OrdinalIgnoreCase);

    // Fail-closed: without a permission context nothing is granted.
    private static bool Has(IActorPermissionContext? permissions, string key) => permissions?.Has(key) == true;
}
