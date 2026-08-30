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
 * VISIBLE instead of letting it be forgotten. UPDATE 2026-08-30: the guard did its job — BOTH reserved decisions
 * were made on the same day (the gateway, then Platform.Common), both sites moved into EnforcesTheRule, and the
 * pending list is now EMPTY. Read `DecisionPendingByDesign` below before assuming that emptiness is vacuous: it is
 * asserted directly, because a loop over an empty list measures nothing.
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
        "services/Diten.HcmService/src/Diten.HcmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
        // MOVED HERE 2026-08-30 from DecisionPendingByDesign. The ordering question that reserved it was answered
        // by the owner: the contradiction is refused BEFORE the actor_type boundary, because a request naming two
        // tenants cannot be evaluated for access at all. The third source it named — the subdomain — is covered by
        // the SAME rule rather than a second one: if the token names a tenant, EVERY other signal must agree.
        // Behaviour test: gateway/Diten.ApiGateway.Tests/TenantContradictionGuardTests.cs.
        "gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs",
        // MOVED HERE 2026-08-30 from DecisionPendingByDesign — THE LAST SITE. The ordering question that reserved
        // it (contradiction 400 vs. the actor_type 403 that IsTenantScopedOrgPath deliberately answers for
        // platform actors) was answered by the owner with the SAME answer as the gateway: the contradiction is
        // refused FIRST, because a request naming two tenants cannot be evaluated for access at all. The designed
        // 403 survives — with no X-Tenant-Id there is no contradiction.
        // Behaviour test: services/Diten.Platform/tests/Diten.Platform.Application.Tests/Tenancy/TenantContradictionGuardTests.cs.
        "services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs"
    ];

    /// <summary>
    /// ⚠ THIS LIST IS EMPTY, AND THAT IS A MEASURED STATE — NOT AN ABSENCE OF MEASUREMENT. All seven
    /// tenant-resolution sites now refuse the contradiction; the last one (Platform.Common) left this list on
    /// 2026-08-30 when the owner answered its ordering question, and the gateway left it earlier the same day.
    ///
    /// <para>It is kept rather than deleted because it is where a FUTURE site would be classified. Emptiness does
    /// not make the guard vacuous: <see cref="EveryTenantResolutionSite_IsClassified_AndTheSetsAreExact"/> pins
    /// <see cref="KnownTenantResolutionSites"/> as an exact floor and ceiling, so an eighth site cannot appear
    /// without failing that test and forcing whoever added it to classify it here or in
    /// <see cref="EnforcesTheRule"/>.</para>
    /// </summary>
    private static readonly string[] DecisionPendingByDesign = [];

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
    public void SitesThatEnforceTheRule_AreExactlyTheMeasuredSeven()
    {
        var repoRoot = FindRepoRoot();

        var actuallyEnforcing = FindTenantResolutionSites(repoRoot)
            .Where(site => RefusesTheContradiction(Path.Combine(repoRoot, site)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(EnforcesTheRule.Length, actuallyEnforcing.Count);
        Assert.Equal(EnforcesTheRule.OrderBy(x => x, StringComparer.Ordinal), actuallyEnforcing);
    }

    /// <summary>
    /// ⚠ THIS TEST REPLACED A `foreach` OVER <see cref="DecisionPendingByDesign"/> (previously
    /// <c>SitesPendingADecision_StillDoNotEnforceIt_SoTheListCannotGoStale</c>). That test asserted, for each
    /// pending site, that it did NOT yet refuse the contradiction — a mirror that demanded the line be deleted
    /// once the decision was made. On 2026-08-30 the last decision WAS made and the list went empty, and a
    /// `foreach` over an empty list ASSERTS NOTHING: it would have stayed green forever while measuring nothing.
    /// Deleting it and asserting the emptiness DIRECTLY is the only way the fact stays measured.
    ///
    /// <para>So: the list is empty because seven sites out of seven enforce the rule. If a NEW tenant-resolution
    /// site is ever added, <see cref="KnownTenantResolutionSites"/> stops matching the scan and
    /// <see cref="EveryTenantResolutionSite_IsClassified_AndTheSetsAreExact"/> fails, forcing the new site to be
    /// classified — at which point, if it is pending a decision, this test fails too and demands the reason be
    /// written down here.</para>
    /// </summary>
    [Fact]
    public void NoSiteIsPendingADecision_BecauseAllSevenEnforceTheRule()
    {
        Assert.Empty(DecisionPendingByDesign);
        Assert.Equal(KnownTenantResolutionSites, EnforcesTheRule.Length);
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
