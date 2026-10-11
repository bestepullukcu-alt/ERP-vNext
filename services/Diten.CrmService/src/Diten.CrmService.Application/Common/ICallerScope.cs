namespace Diten.CrmService.Application.Common;

/// <summary>
/// WP-VP-2 (B-1) — who is calling, as the visit features need it: the caller's RESOURCE id (the interim
/// user-as-resource rule of <c>resources/me</c>: the token's <c>sub</c>) and whether the caller holds a given permission
/// key. Resolved server-side from the principal — never from a route, query or body.
/// <para>Ownership of planned visits, planning sessions and visit reports is decided against this seam (see
/// <see cref="VisitOwnership"/>): without the feature's <c>read-all</c> key a caller only ever sees and touches records
/// whose resource is themselves.</para>
/// </summary>
public interface ICallerScope
{
    /// <summary>The caller's resource id (user id), or null when the principal carries no stable identity.</summary>
    string? CallerResourceId { get; }

    bool HasPermission(string permissionKey);
}

/// <summary>WP-VP-2 (B-1) — the one ownership rule, so every handler asks the same question the same way.</summary>
public static class VisitOwnership
{
    /// <summary>Machine code: a caller without read-all asked to act for another resource.</summary>
    public const string ResourceNotCaller = "resource_not_caller";

    /// <summary>May the caller see / act on a record owned by <paramref name="resourceId"/>? The read-all key holder may
    /// (tenant-wide); otherwise only when the record is the caller's own. No identity and no read-all ⇒ nothing (fail-closed).</summary>
    public static bool MayAccess(this ICallerScope caller, string readAllKey, string? resourceId)
        => caller.HasPermission(readAllKey)
           || (caller.CallerResourceId is { } me
               && !string.IsNullOrWhiteSpace(resourceId)
               && string.Equals(me, resourceId.Trim(), StringComparison.Ordinal));

    /// <summary>The resource a write is made for. Without read-all the caller IS the resource: an empty request value
    /// becomes the caller, a different one is refused (<c>Allowed = false</c> → 403 <see cref="ResourceNotCaller"/>).
    /// With read-all the request's value is kept (empty → the caller).</summary>
    public static (bool Allowed, string? ResourceId) ResolveWriteResource(
        this ICallerScope caller, string readAllKey, string? requestedResourceId)
    {
        var requested = string.IsNullOrWhiteSpace(requestedResourceId) ? null : requestedResourceId.Trim();
        if (caller.HasPermission(readAllKey))
        {
            return (true, requested ?? caller.CallerResourceId);
        }

        if (caller.CallerResourceId is not { } me)
        {
            return (false, null);
        }

        return requested is null || string.Equals(requested, me, StringComparison.Ordinal)
            ? (true, me)
            : (false, null);
    }
}
