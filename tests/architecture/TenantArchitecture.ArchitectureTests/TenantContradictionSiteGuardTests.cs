using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE REPO-WIDE GUARD — WHICH TENANT-RESOLUTION SITES REFUSE A CONTRADICTION (BL-324).
 *
 * THE RULE (DCP-004 §7.4, owner decision 2026-08-29, from BL-323): if `X-Tenant-Id` and the authenticated JWT
 * name DIFFERENT tenants, the request is refused 400. A malformed request, not an access decision.
 *
 * WHY THIS GUARD EXISTS AND WHY IT DID NOT BEFORE. BL-324 recorded that a repo-wide static guard was deliberately
 * NOT written in the BL-323 round: it would have been red at five sites, and the exception list needed to make it
 * green would have turned the deviation into something "accepted". That objection is answered here by SHRINKING
 * the list rather than by writing one: three of those five sites are fixed in this round, and the two that remain
 * are not a convenience list — they are a single named, owner-level decision (below) that this guard now keeps
 * VISIBLE instead of letting it be forgotten.
 *
 * ⚠ WHAT THIS GUARD IS FOR, precisely. It is a CLASSIFICATION guard, not a behaviour test: the behaviour is
 * asserted per service by the real middleware, over real requests, in each service's own
 * TenantContradictionGuardTests. This one answers a question those cannot: "did a NEW tenant-resolution site
 * appear, or did an existing one quietly lose the rule?" Both sets are pinned EXACTLY — a site cannot be added to
 * either without editing this file and saying why.
 */
public class TenantContradictionSiteGuardTests
{
    /// <summary>
    /// The refusal shape: both values present AND different. Detected structurally rather than by a comment, so a
    /// site cannot pass this guard by mentioning BL-324 in prose.
    /// </summary>
    private static readonly Regex ContradictionCondition = new(
        @"jwtTenant\.HasValue\s*&&\s*headerTenant\.HasValue\s*&&\s*jwtTenant\.Value\s*!=\s*headerTenant\.Value",
        RegexOptions.Compiled);

    /// <summary>
    /// MEASURED 2026-08-29: seven files in the repo resolve a tenant from the header and/or the token. The count is
    /// pinned as an exact floor AND ceiling on purpose — the failure this prevents is an eighth site appearing and
    /// inheriting neither the rule nor the decision, which is exactly how the five-site drift happened.
    /// </summary>
    private const int KnownTenantResolutionSites = 7;

    /// <summary>Sites that refuse the contradiction. Measured, not aspirational.</summary>
    private static readonly string[] EnforcesTheRule =
    [
        "services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
        "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
        "services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
        "services/Diten.HcmService/src/Diten.HcmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs"
    ];

    /// <summary>
    /// ⚠ NOT AN EXEMPTION LIST — AN OPEN DECISION, NAMED. These two are the only tenant-resolution sites that also
    /// arbitrate PLATFORM-ACTOR requests, and in both the contradiction check would have to be ordered against an
    /// existing actor_type boundary that already answers 403. Choosing which refusal wins is an owner decision
    /// about an access boundary, not a cleanup, so BL-324 leaves them measured and open rather than guessed.
    /// Turning either one green belongs to that decision's round, together with deleting its line here.
    /// </summary>
    private static readonly string[] DecisionPendingByDesign =
    [
        // Ordinary tenant path resolves the tenant BEFORE the actor_type 403 ("Tenant endpoints require
        // tenant_user tokens"), so refusing the contradiction first would answer 400 where a platform actor is
        // deliberately answered 403 today.
        "services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs",
        // Same ordering question, plus a THIRD tenant source the rule does not mention at all: the request's
        // subdomain. "Contradiction" is not yet defined for three-way disagreement.
        "gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs"
    ];

    [Fact]
    public void EveryTenantResolutionSite_IsClassified_AndTheSetsAreExact()
    {
        var repoRoot = FindRepoRoot();
        var sites = FindTenantResolutionSites(repoRoot);

        // A measured floor AND ceiling, never "> 0": an empty or half-discovered scan would otherwise pass every
        // assertion below by having nothing to check.
        Assert.Equal(KnownTenantResolutionSites, sites.Count);

        var classified = EnforcesTheRule.Concat(DecisionPendingByDesign).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(classified, sites.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void SitesThatEnforceTheRule_AreExactlyTheMeasuredFive()
    {
        var repoRoot = FindRepoRoot();

        var actuallyEnforcing = FindTenantResolutionSites(repoRoot)
            .Where(site => RefusesTheContradiction(Path.Combine(repoRoot, site)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(EnforcesTheRule.Length, actuallyEnforcing.Count);
        Assert.Equal(EnforcesTheRule.OrderBy(x => x, StringComparer.Ordinal), actuallyEnforcing);
    }

    [Fact]
    public void SitesPendingADecision_StillDoNotEnforceIt_SoTheListCannotGoStale()
    {
        var repoRoot = FindRepoRoot();

        // The mirror of the test above, and the reason the pending list cannot quietly outlive its decision: if
        // someone DOES make one of these refuse the contradiction, this fails and demands the line be deleted.
        foreach (var site in DecisionPendingByDesign)
        {
            var full = Path.Combine(repoRoot, site);
            Assert.True(File.Exists(full), $"Pending site no longer exists — update BL-324's list: {site}");
            Assert.False(
                RefusesTheContradiction(full),
                $"{site} now refuses the contradiction. That is the owner decision BL-324 reserved: remove it " +
                "from DecisionPendingByDesign, add it to EnforcesTheRule, and give it a behaviour test.");
        }
    }

    /// <summary>
    /// A site REFUSES only if the contradiction condition is followed by an actual 400. A condition that merely
    /// logs and carries on — which is exactly what AuthService, Platform.Common and the gateway all did — must not
    /// count as enforcement.
    /// </summary>
    private static bool RefusesTheContradiction(string fullPath)
    {
        var content = File.ReadAllText(fullPath);
        var match = ContradictionCondition.Match(content);
        if (!match.Success)
        {
            return false;
        }

        var tail = content[match.Index..];
        var window = tail.Length < 800 ? tail : tail[..800];
        return window.Contains("Status400BadRequest", StringComparison.Ordinal);
    }

    private static List<string> FindTenantResolutionSites(string repoRoot)
    {
        return Directory.GetFiles(repoRoot, "TenantResolutionMiddleware.cs", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => !path.Contains("/obj/") && !path.Contains("/bin/"))
            .Select(path => path[(repoRoot.Replace('\\', '/').TrimEnd('/').Length + 1)..])
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
    }
}
