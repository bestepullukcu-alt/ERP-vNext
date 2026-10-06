namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>
/// SCMM-15 (CAND-CAP-0011) ContentSetRevision permission keys (PKS-001: lowercase-dotted, ≥3 segments). Definition only
/// — the AuthService catalog + 97c5 grant are the RBAC seed. Read gates list/get. Submit reuses the existing
/// <c>crm.content-set.manage</c> authoring capability (freezing is part of managing the set). Review is a SEPARATE key so
/// separation-of-duties can be enforced by role, not only by the runtime reviewer≠author guard.
/// </summary>
public static class ContentSetRevisionPermissions
{
    public const string Read = "crm.content-set.read";
    public const string Submit = "crm.content-set.manage";
    public const string Review = "crm.content-set.review";

    /// <summary>SCMM-16B — render an approved revision to a PDF artifact. A SEPARATE capability from authoring/review:
    /// producing the released output is its own operation. Needs an AuthService catalog entry + 97c5 grant to seed.</summary>
    public const string Render = "crm.content-set.render";

    /// <summary>SCMM-17 — release a rendered revision's artifact. A SEPARATE capability so the releaser can be held to a
    /// third role (author → reviewer → releaser); the runtime SoD guard also blocks the reviewer from releasing.</summary>
    public const string Release = "crm.content-set.release";

    /// <summary>SCMM-17 — managed withdrawal of a released revision (a state change, never a deletion).</summary>
    public const string Withdraw = "crm.content-set.withdraw";

    /// <summary>The keys these WPs introduce (Read/Submit already existed under ContentSet; Review = SCMM-15;
    /// Render = SCMM-16B; Release/Withdraw = SCMM-17).</summary>
    public static readonly IReadOnlyList<string> New = new[] { Review, Render, Release, Withdraw };
}
