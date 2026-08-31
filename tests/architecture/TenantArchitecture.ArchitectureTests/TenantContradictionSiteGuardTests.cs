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
 *
 * ⚠ AND WHAT IT CANNOT SEE — THIS IS A TEXT CHECK, NOT A BEHAVIOUR CHECK. Every assertion below reads these
 * middleware files as STRINGS: `RefusesTheContradiction` matches the regex above and then looks for the literal
 * "Status400BadRequest" within 800 characters. No request is ever made, no middleware is ever executed. So this
 * guard measures the PRESENCE of the rule, never its EFFECT, and a rule switched off in place keeps every
 * character it wants.
 *
 * MEASURED 2026-08-30 — try it rather than believing it. Prefix the condition in Platform.Common with `false &&`:
 *     if (false && jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
 * The refusal is now completely dead — that site accepts a request naming two different tenants — and all 14
 * tests in this project STAY GREEN, because the regex still matches and the 400 is still spelled out below it.
 * Deleting or commenting the block would be caught; neutering it in place is not. Nothing in this file can be.
 *
 * ⚠ SO WHAT ACTUALLY MEASURES THE RULE — named by PATH, because "the guard says so" is not an answer. One
 * behaviour test file per site, each driving the real middleware over real requests:
 *     gateway/Diten.ApiGateway.Tests/TenantContradictionGuardTests.cs
 *     services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.DevEnablementService/tests/Diten.DevEnablementService.Api.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.HcmService/tests/Diten.HcmService.Application.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.Platform/tests/Diten.Platform.Application.Tests/Tenancy/TenantContradictionGuardTests.cs
 *     services/Diten.PpmService/tests/Diten.PpmService.Tests/TenantResolutionMiddlewareTests.cs
 * The same `false &&` experiment that left this project 14/14 green turned FIVE tests red in the Platform file —
 * those are the tests that noticed. If the rule matters to you, that is the list to read and to keep alive.
 *
 * ⚠ THIS DOES NOT CONTRADICT THE EMPTINESS ARGUMENT made for `DecisionPendingByDesign` below; it answers a
 * DIFFERENT question. That argument is about whether the SET of sites is measured, and it holds exactly as
 * written: an eighth site cannot appear, and a classification cannot go stale, without a test here failing. This
 * paragraph is about whether the RULE IS ALIVE at the eight — and to that question, this file is not evidence.
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
    /// MEASURED 2026-08-31: eight files in the repo resolve a tenant from the header and/or the token. The count is
    /// pinned as an exact floor AND ceiling on purpose — the failure this prevents is a ninth site appearing and
    /// inheriting neither the rule nor the decision, which is exactly how the five-site drift happened.
    /// </summary>
    private const int KnownTenantResolutionSites = 8;

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
        "services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs",
        // MOD-0117 PPM reads tenant identity from the authenticated JWT and refuses a contradicting header before
        // its application-level tenant context is consumed. Behaviour test: services/Diten.PpmService/tests/
        // Diten.PpmService.Tests/TenantResolutionMiddlewareTests.cs.
        "services/Diten.PpmService/src/Diten.PpmService.Api/Security/TenantResolutionMiddleware.cs"
    ];

    /// <summary>
    /// ⚠ THIS LIST IS EMPTY, AND THAT IS A MEASURED STATE — NOT AN ABSENCE OF MEASUREMENT. All eight
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
    public void SitesThatEnforceTheRule_AreExactlyTheMeasuredEight()
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
    /// <para>So: the list is empty because eight sites out of eight enforce the rule. If a NEW tenant-resolution
    /// site is ever added, <see cref="KnownTenantResolutionSites"/> stops matching the scan and
    /// <see cref="EveryTenantResolutionSite_IsClassified_AndTheSetsAreExact"/> fails, forcing the new site to be
    /// classified — at which point, if it is pending a decision, this test fails too and demands the reason be
    /// written down here.</para>
    /// </summary>
    [Fact]
    public void NoSiteIsPendingADecision_BecauseAllEightEnforceTheRule()
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
