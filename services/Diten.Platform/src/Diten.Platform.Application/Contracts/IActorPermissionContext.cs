namespace Diten.Platform.Application.Contracts;

/// <summary>
/// "Does the caller hold this permission?" — asked by the Application layer, answered from the request's claims.
///
/// <para><b>This is a SEAM, not an engine.</b> MOD-0018 owns roles, grants and the permission catalogue; the API
/// tier already evaluates a principal's claims against a required key with
/// <c>PermissionClaimEvaluator</c> (canonical + legacy-alias dual read). The single implementation of this
/// interface calls exactly that. Nothing here decides who holds what, and nothing here caches an answer —
/// re-deriving it per question is what makes a definition change take effect on the next request rather than
/// whenever some cache happens to expire.</para>
///
/// <para><b>Why it exists at all.</b> The established way to get a resolved permission into a handler is
/// <c>WorkItemActor.GrantedPermissions</c>: the controller evaluates a FIXED, compile-time list of keys and hands
/// the set in as data. Field-level authorization cannot use that shape, because the keys are DATA — a tenant
/// administrator names them on a field definition, and the controller cannot know them without reading the
/// catalogue, which would put a repository call in the web tier. So the QUESTION travels instead of the answer.
/// The evaluation still happens where the claims live.</para>
///
/// <para><b>Fail-closed.</b> With no authenticated principal every question answers false. An unauthenticated
/// caller reaching a handler is already a bug; it must not also be a caller who can read everything.</para>
/// </summary>
public interface IActorPermissionContext
{
    /// <summary>
    /// A PLATFORM actor — operating above a single tenant. Passes every permission, mirroring the API
    /// enforcement filter and <c>WorkItemActor.Has</c>, so the two cannot disagree about what "platform" means.
    /// </summary>
    bool IsPlatformActor { get; }

    /// <summary>Whether the caller holds <paramref name="permissionKey"/>. Null/blank keys answer TRUE — an
    /// unrestricted thing is not a permission check, and treating "no key" as a denial would hide every field
    /// nobody restricted.</summary>
    bool Has(string? permissionKey);
}
