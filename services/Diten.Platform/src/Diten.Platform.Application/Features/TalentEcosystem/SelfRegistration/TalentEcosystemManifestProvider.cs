using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.TalentEcosystem.SelfRegistration;

/// <summary>
/// TALENT-ECOSYSTEM (TEP) — Talent Ecosystem module self-registration manifest (HR nav-wiring gap #4, WP-HR-nav-B).
/// <para>Mirror of <see cref="Crm.SelfRegistration.CrmManifestProvider"/>: reconciles the code-owned identity of the
/// TEP catalog entry (ModuleCode <c>TALENT-ECOSYSTEM</c>) and its page/permission descriptors so the MOD-0285
/// data-driven tenant sidebar (<c>GET /api/platform/navigation/menu</c>) can surface the 31 ported TEP pages. Before
/// this manifest existed the module was invisible everywhere — <c>platform_module_catalog</c>, page/action descriptors
/// and <c>tenant_module_entitlements</c> all carried zero TEP rows; the pages were URL-reachable only.</para>
/// <para><b>Navigation (MOD-0285 data-driven):</b> every page below is registered <c>IsNavigationVisible = true</c> —
/// the descriptor is the ONLY source of the sidebar entry (the tenant shell hand-writes no TEP links). Each page's
/// RequiredPermission is the verbatim key the Diten.Web TEP page controller AND the Diten.TalentEcosystemService
/// controller <c>[HasPermission]</c> both enforce, resolved from the backend permission constants (Guard/Permissions
/// classes) — zero drift, nothing invented. The sidebar entry only renders for a tenant entitled to TALENT-ECOSYSTEM
/// and granted that page permission (entitlement + RBAC grant are WP-C, out of this WP's scope).</para>
/// <para><b>Route source:</b> <c>frontend/Diten.Web/Controllers/*Controller.cs</c> — each carries
/// <c>[Route("TalentEcosystem/&lt;Name&gt;")]</c> with a single <c>Index</c> GET (the pages carry no permission of their
/// own; the gate lives on the backend service, hence the readPerm below is the backend read key).</para>
/// <para><b>Two deliberate non-page controllers / divergences (documented, not invented):</b></para>
/// <list type="bullet">
///   <item><description><c>TepShellMetadata</c> is a shell/metadata endpoint (backend key <c>tep.shell.read</c>), NOT a
///   navigable list page — it is intentionally EXCLUDED from the manifest (30 nav pages, not 31), per WP-HR-nav-B.</description></item>
///   <item><description><c>PayBenchmarking</c>'s Web route is <c>/TalentEcosystem/PayBenchmarking</c> but its backend
///   owner key/permissions are <c>tep.salary-benchmarking.*</c> (NOT <c>pay-benchmarking</c>). The verbatim backend key
///   is used — the route name is never the source of the permission.</description></item>
/// </list>
/// <para>This provider lives in Platform.Application (like Crm/Organization/Workflow cross-service providers); it does
/// NOT add a manifest push inside the TEP service and declares no business capability.</para>
/// <para><b>KNOWN GAP — reported per this WP's stop-condition ("yeni resx gereği" / new-resx requirement), NOT silently
/// resolved (mirrors <see cref="Meetings.SelfRegistration.MeetingManifestProvider"/>'s own reported conflict).</b>
/// The mere existence of this <c>*ManifestProvider.cs</c> under <c>services/</c> makes
/// <c>frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests</c> require, in all SEVEN tenant languages:
/// <c>Nav.Module.TALENTECOSYSTEM</c>, <c>Nav.Domain.TALENTECOSYSTEM</c>, and one <c>Nav.Page.*</c> key per nav-visible
/// page below (30 of them, e.g. <c>Nav.Page.ASSOCIATIONMEMBERSHIPS</c>). Those resx rows do not exist yet, so that
/// frontend guard turns red at <c>dotnet test</c> time (NOT at build). This is EXPECTED and PLAN-SEQUENCED: WP-HR-nav-B
/// explicitly forbids touching <c>frontend/**</c> and defers the 7-language Nav.* fill to gap #5 / WP-D, which the plan
/// (PLAN-HR-nav-wiring-module-catalog.md §Sıra) orders AFTER this WP (WP-A+WP-B → WP-C entitlement/grant → menu visible
/// → WP-D L10n). nav-visible=true is REQUIRED here — surfacing these pages in the MOD-0285 sidebar is the whole
/// deliverable, so deferring registration (Meetings' alternative) would defeat the WP. The frontend guard failures are
/// a tracked WP-D obligation, not a Platform-test regression (this WP's E2 scope is the Platform test suite).</para>
/// </summary>
public sealed class TalentEcosystemManifestProvider : IModuleManifestProvider
{
    // Verbatim read-permission keys the Diten.TalentEcosystemService controllers [HasPermission] enforce (backend
    // Guard/Permissions constants — the Diten.Web TEP page controllers gate on the same backend key). NOTE the
    // salary-benchmarking divergence: the /PayBenchmarking route's backend key family is tep.salary-benchmarking.*.
    private const string AssociationMembershipsRead = "tep.association-memberships.read";
    private const string AssociationOperationsRead = "tep.association-operations.read";
    private const string CandidateCareerPassportRead = "tep.candidate-career-passport.read";
    private const string CandidateDisputesRead = "tep.candidate-disputes.read";
    private const string CandidateProfilesRead = "tep.candidate-profiles.read";
    private const string ConsentVisibilityPoliciesRead = "tep.consent-visibility-policies.read";
    private const string EarlyWarningSignalsRead = "tep.early-warning-signals.read";
    private const string ExitReferenceRecordsRead = "tep.exit-reference-records.read";
    private const string HiringRiskIndicatorsRead = "tep.hiring-risk-indicators.read";
    private const string IndustryKnowledgeNetworkRead = "tep.industry-knowledge-network.read";
    private const string IndustrySkillPassportRead = "tep.industry-skill-passport.read";
    private const string IndustrySuccessionPoolRead = "tep.industry-succession-pool.read";
    private const string IndustryTalentPoolRead = "tep.industry-talent-pool.read";
    private const string MentorshipRecommendationNetworkRead = "tep.mentorship-recommendation-network.read";
    private const string PayBenchmarkingRead = "tep.salary-benchmarking.read"; // divergence: route=PayBenchmarking, key=salary-benchmarking
    private const string ProfessionalReputationLedgerRead = "tep.professional-reputation-ledger.read";
    private const string ReferenceExchangeRead = "tep.reference-exchange.read";
    private const string RehireRecommendationsRead = "tep.rehire-recommendations.read";
    private const string RestrictedIntegrityRegistryRead = "tep.restricted-integrity-registry.read";
    private const string ReviewBoardRead = "tep.review-board.read";
    private const string SectorMobilityIntelligenceRead = "tep.sector-mobility-intelligence.read";
    private const string SectorTalentTrendsRead = "tep.sector-talent-trends.read";
    private const string SkillsGapHeatmapRead = "tep.skills-gap-heatmap.read";
    private const string TalentDataFoundationRead = "tep.talent-data-foundation.read";
    private const string TalentDevelopmentNetworkRead = "tep.talent-development-network.read";
    private const string TalentSupplyDemandForecastingRead = "tep.talent-supply-demand-forecasting.read";
    private const string TrustLevelsRead = "tep.trust-levels.read";
    private const string VerifiedCertificationRegistryRead = "tep.verified-certification-registry.read";
    private const string VerifiedParticipantsRead = "tep.verified-participants.read";
    private const string WorkforceAnalyticsRead = "tep.workforce-analytics.read";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "TALENT-ECOSYSTEM",
            ModuleName: "Talent Ecosystem",
            DisplayName: "Talent Ecosystem",
            Domain: "Talent Ecosystem", // SOFT: only seeds on first-register; the operator Domain is preserved thereafter.
            Service: "DitenTalentEcosystemService", // SOFT: preserved for the existing item.
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true, // SOFT.
            SortOrder: 130,
            Icon: "bx-network",
            IsBaseline: false, // HARD: TEP is a licensed module — tenant entitlement required (never entitlement-free).
            Pages:
            [
                // Each page's Actions declare the granular write operations (verbatim backend permission) so the module
                // catalogue carries page-action descriptors an RBAC admin can grant — not just the read gate. AuditRead
                // is a secondary read gate on the backend, not a nav/write action, so it is intentionally not surfaced.
                new ModuleManifestPage("ASSOCIATION_MEMBERSHIPS", "Association Memberships", "/TalentEcosystem/AssociationMemberships", AssociationMembershipsRead, null, true, "List", 10,
                [
                    new ModuleManifestAction("MANAGE", "New Association Membership", "tep.association-memberships.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MEMBER_COMPANY_MANAGE", "Manage Member Company", "tep.association-memberships.member-company.manage", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.association-memberships.evaluate", "RowAction", 30, false, false, true),
                    new ModuleManifestAction("ARCHIVE", "Archive", "tep.association-memberships.archive", "RowAction", 40, false, false, true)
                ]),
                new ModuleManifestPage("ASSOCIATION_OPERATIONS", "Association Operations", "/TalentEcosystem/AssociationOperations", AssociationOperationsRead, null, true, "List", 20,
                [
                    new ModuleManifestAction("MANAGE", "New Association Operations Readiness", "tep.association-operations.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.association-operations.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CANDIDATE_CAREER_PASSPORT", "Candidate Career Passport", "/TalentEcosystem/CandidateCareerPassport", CandidateCareerPassportRead, null, true, "List", 30,
                [
                    new ModuleManifestAction("MANAGE", "New Candidate Career Passport", "tep.candidate-career-passport.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.candidate-career-passport.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CANDIDATE_DISPUTES", "Candidate Disputes", "/TalentEcosystem/CandidateDisputes", CandidateDisputesRead, null, true, "List", 40,
                [
                    new ModuleManifestAction("MANAGE", "New Candidate Dispute", "tep.candidate-disputes.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.candidate-disputes.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CANDIDATE_PROFILES", "Candidate Profiles", "/TalentEcosystem/CandidateProfiles", CandidateProfilesRead, null, true, "List", 50,
                [
                    new ModuleManifestAction("MANAGE", "New Candidate Profile", "tep.candidate-profiles.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.candidate-profiles.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CONSENT_VISIBILITY_POLICIES", "Consent Visibility Policies", "/TalentEcosystem/ConsentVisibilityPolicies", ConsentVisibilityPoliciesRead, null, true, "List", 60,
                [
                    new ModuleManifestAction("MANAGE", "New Consent Visibility Policy", "tep.consent-visibility-policies.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.consent-visibility-policies.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("EARLY_WARNING_SIGNALS", "Early Warning Signals", "/TalentEcosystem/EarlyWarningSignals", EarlyWarningSignalsRead, null, true, "List", 70,
                [
                    new ModuleManifestAction("MANAGE", "New Early Warning Signal", "tep.early-warning-signals.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.early-warning-signals.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("EXIT_REFERENCE_RECORDS", "Exit Reference Records", "/TalentEcosystem/ExitReferenceRecords", ExitReferenceRecordsRead, null, true, "List", 80,
                [
                    new ModuleManifestAction("MANAGE", "New Exit Reference Record", "tep.exit-reference-records.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.exit-reference-records.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HIRING_RISK_INDICATORS", "Hiring Risk Indicators", "/TalentEcosystem/HiringRiskIndicators", HiringRiskIndicatorsRead, null, true, "List", 90,
                [
                    new ModuleManifestAction("MANAGE", "New Hiring Risk Indicator", "tep.hiring-risk-indicators.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.hiring-risk-indicators.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("INDUSTRY_KNOWLEDGE_NETWORK", "Industry Knowledge Network", "/TalentEcosystem/IndustryKnowledgeNetwork", IndustryKnowledgeNetworkRead, null, true, "List", 100,
                [
                    new ModuleManifestAction("MANAGE", "New Industry Knowledge Network", "tep.industry-knowledge-network.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.industry-knowledge-network.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("INDUSTRY_SKILL_PASSPORT", "Industry Skill Passport", "/TalentEcosystem/IndustrySkillPassport", IndustrySkillPassportRead, null, true, "List", 110,
                [
                    new ModuleManifestAction("MANAGE", "New Industry Skill Passport", "tep.industry-skill-passport.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.industry-skill-passport.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("INDUSTRY_SUCCESSION_POOL", "Industry Succession Pool", "/TalentEcosystem/IndustrySuccessionPool", IndustrySuccessionPoolRead, null, true, "List", 120,
                [
                    new ModuleManifestAction("MANAGE", "New Industry Succession Pool", "tep.industry-succession-pool.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.industry-succession-pool.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("INDUSTRY_TALENT_POOL", "Industry Talent Pool", "/TalentEcosystem/IndustryTalentPool", IndustryTalentPoolRead, null, true, "List", 130,
                [
                    new ModuleManifestAction("MANAGE", "New Industry Talent Pool", "tep.industry-talent-pool.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.industry-talent-pool.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("MENTORSHIP_RECOMMENDATION_NETWORK", "Mentorship Recommendation Network", "/TalentEcosystem/MentorshipRecommendationNetwork", MentorshipRecommendationNetworkRead, null, true, "List", 140,
                [
                    new ModuleManifestAction("MANAGE", "New Mentorship Recommendation Network", "tep.mentorship-recommendation-network.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.mentorship-recommendation-network.evaluate", "RowAction", 20, false, false, true)
                ]),
                // DIVERGENCE: Web route /TalentEcosystem/PayBenchmarking, backend permission family tep.salary-benchmarking.*.
                new ModuleManifestPage("PAY_BENCHMARKING", "Pay Benchmarking", "/TalentEcosystem/PayBenchmarking", PayBenchmarkingRead, null, true, "List", 150,
                [
                    new ModuleManifestAction("MANAGE", "New Pay Benchmarking", "tep.salary-benchmarking.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.salary-benchmarking.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("PROFESSIONAL_REPUTATION_LEDGER", "Professional Reputation Ledger", "/TalentEcosystem/ProfessionalReputationLedger", ProfessionalReputationLedgerRead, null, true, "List", 160,
                [
                    new ModuleManifestAction("MANAGE", "New Professional Reputation Ledger", "tep.professional-reputation-ledger.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.professional-reputation-ledger.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("REFERENCE_EXCHANGE", "Reference Exchange", "/TalentEcosystem/ReferenceExchange", ReferenceExchangeRead, null, true, "List", 170,
                [
                    new ModuleManifestAction("MANAGE", "New Reference Exchange", "tep.reference-exchange.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.reference-exchange.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("REHIRE_RECOMMENDATIONS", "Rehire Recommendations", "/TalentEcosystem/RehireRecommendations", RehireRecommendationsRead, null, true, "List", 180,
                [
                    new ModuleManifestAction("MANAGE", "New Rehire Recommendation", "tep.rehire-recommendations.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.rehire-recommendations.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("RESTRICTED_INTEGRITY_REGISTRY", "Restricted Integrity Registry", "/TalentEcosystem/RestrictedIntegrityRegistry", RestrictedIntegrityRegistryRead, null, true, "List", 190,
                [
                    new ModuleManifestAction("MANAGE", "New Restricted Integrity Registry", "tep.restricted-integrity-registry.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.restricted-integrity-registry.evaluate", "RowAction", 20, false, false, true)
                ]),
                // ReviewBoard's write surface is Manage + a SEPARATE Review key (SoD), no generic Evaluate.
                new ModuleManifestPage("REVIEW_BOARD", "Review Board", "/TalentEcosystem/ReviewBoard", ReviewBoardRead, null, true, "List", 200,
                [
                    new ModuleManifestAction("MANAGE", "New Review Board", "tep.review-board.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("REVIEW", "Review", "tep.review-board.review", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("SECTOR_MOBILITY_INTELLIGENCE", "Sector Mobility Intelligence", "/TalentEcosystem/SectorMobilityIntelligence", SectorMobilityIntelligenceRead, null, true, "List", 210,
                [
                    new ModuleManifestAction("MANAGE", "New Sector Mobility Intelligence", "tep.sector-mobility-intelligence.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.sector-mobility-intelligence.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("SECTOR_TALENT_TRENDS", "Sector Talent Trends", "/TalentEcosystem/SectorTalentTrends", SectorTalentTrendsRead, null, true, "List", 220,
                [
                    new ModuleManifestAction("MANAGE", "New Sector Talent Trend", "tep.sector-talent-trends.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.sector-talent-trends.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("SKILLS_GAP_HEATMAP", "Skills Gap Heatmap", "/TalentEcosystem/SkillsGapHeatmap", SkillsGapHeatmapRead, null, true, "List", 230,
                [
                    new ModuleManifestAction("MANAGE", "New Skills Gap Heatmap", "tep.skills-gap-heatmap.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.skills-gap-heatmap.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("TALENT_DATA_FOUNDATION", "Talent Data Foundation", "/TalentEcosystem/TalentDataFoundation", TalentDataFoundationRead, null, true, "List", 240,
                [
                    new ModuleManifestAction("MANAGE", "New Talent Data Foundation", "tep.talent-data-foundation.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.talent-data-foundation.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("TALENT_DEVELOPMENT_NETWORK", "Talent Development Network", "/TalentEcosystem/TalentDevelopmentNetwork", TalentDevelopmentNetworkRead, null, true, "List", 250,
                [
                    new ModuleManifestAction("MANAGE", "New Talent Development Network", "tep.talent-development-network.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.talent-development-network.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("TALENT_SUPPLY_DEMAND_FORECASTING", "Talent Supply Demand Forecasting", "/TalentEcosystem/TalentSupplyDemandForecasting", TalentSupplyDemandForecastingRead, null, true, "List", 260,
                [
                    new ModuleManifestAction("MANAGE", "New Talent Supply Demand Forecasting", "tep.talent-supply-demand-forecasting.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.talent-supply-demand-forecasting.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("TRUST_LEVELS", "Trust Levels", "/TalentEcosystem/TrustLevels", TrustLevelsRead, null, true, "List", 270,
                [
                    new ModuleManifestAction("MANAGE", "New Trust Level", "tep.trust-levels.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.trust-levels.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("VERIFIED_CERTIFICATION_REGISTRY", "Verified Certification Registry", "/TalentEcosystem/VerifiedCertificationRegistry", VerifiedCertificationRegistryRead, null, true, "List", 280,
                [
                    new ModuleManifestAction("MANAGE", "New Verified Certification Registry", "tep.verified-certification-registry.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.verified-certification-registry.evaluate", "RowAction", 20, false, false, true)
                ]),
                // VerifiedParticipants' write surface is Manage + a SEPARATE Verify key (SoD) + Evaluate.
                new ModuleManifestPage("VERIFIED_PARTICIPANTS", "Verified Participants", "/TalentEcosystem/VerifiedParticipants", VerifiedParticipantsRead, null, true, "List", 290,
                [
                    new ModuleManifestAction("MANAGE", "New Verified Participant", "tep.verified-participants.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("VERIFY", "Verify", "tep.verified-participants.verify", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.verified-participants.evaluate", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("WORKFORCE_ANALYTICS", "Workforce Analytics", "/TalentEcosystem/WorkforceAnalytics", WorkforceAnalyticsRead, null, true, "List", 300,
                [
                    new ModuleManifestAction("MANAGE", "New Workforce Analytics", "tep.workforce-analytics.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "tep.workforce-analytics.evaluate", "RowAction", 20, false, false, true)
                ])
            ]);
}
