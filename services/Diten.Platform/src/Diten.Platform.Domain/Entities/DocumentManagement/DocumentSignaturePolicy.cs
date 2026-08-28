using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU23 — the rules that apply when a given kind of record is signed with a given meaning
/// (GMG-QMS-SOP-0001 §11.2).
///
/// WHY A POLICY AT ALL: without one, "signed" means whatever the caller felt like sending. The policy states, per
/// subject type and meaning, whether a meaning statement is mandatory, whether the object must be fingerprinted,
/// whether a repository assessment must exist, and which repository categories may host the signature. When no
/// policy matches, the service applies the SAFE DEFAULT (everything required, no compliance claim) rather than
/// falling through permissively.
///
/// <see cref="RequiresSecondFactor"/> DELIBERATELY CANNOT BE SATISFIED in FU23. There is no second-factor
/// authentication context in the platform, and accepting a client-asserted "I did 2FA" boolean would be worse than
/// having no control at all — it would create false evidence. A policy demanding it therefore BLOCKS signing with
/// SECOND_FACTOR_NOT_AVAILABLE until a real authentication context exists.
/// </summary>
public sealed class DocumentSignaturePolicy : TenantScopedEntity
{
    public required string PolicyKey { get; set; }
    public required string PolicyName { get; set; }
    public SignaturePolicyStatus PolicyStatus { get; set; } = SignaturePolicyStatus.Draft;

    public SignableSubjectType SignableSubjectType { get; set; } = SignableSubjectType.Other;
    public SignatureMeaning SignatureMeaning { get; set; } = SignatureMeaning.Other;

    /// <summary>Only satisfiable by an <c>AuthenticationContextReference</c> — never by a client-asserted boolean.</summary>
    public bool RequiresReAuthentication { get; set; }

    /// <summary>Not implementable in FU23; a policy that demands it blocks signing rather than faking it.</summary>
    public bool RequiresSecondFactor { get; set; }

    public bool RequiresMeaningStatement { get; set; } = true;
    public bool RequiresRepositoryAssessment { get; set; }
    public bool RequiresObjectFingerprint { get; set; } = true;
    public bool RequiresManifestation { get; set; } = true;

    /// <summary>
    /// Which SOP §11 repository categories may host a signature under this policy. An empty list means "unconstrained
    /// by the policy" — the boundary evaluator still applies its own floor, and an unapproved repository is still
    /// blocked regardless of what any policy permits.
    /// </summary>
    public List<RepositoryType> AllowedRepositoryTypes { get; set; } = [];

    public bool AllowInterimRepositorySignature { get; set; } = true;

    /// <summary>Tenant-authored wording appended to the generated boundary statement. Never replaces it.</summary>
    public string? InterimRepositoryBoundaryStatement { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Fewer permissions granted ⇒ more restrictive. Used to pick a winner when several policies match.</summary>
    public int RestrictivenessScore() =>
        (RequiresReAuthentication ? 1 : 0)
        + (RequiresSecondFactor ? 1 : 0)
        + (RequiresMeaningStatement ? 1 : 0)
        + (RequiresRepositoryAssessment ? 1 : 0)
        + (RequiresObjectFingerprint ? 1 : 0)
        + (RequiresManifestation ? 1 : 0)
        + (AllowInterimRepositorySignature ? 0 : 1);
}
