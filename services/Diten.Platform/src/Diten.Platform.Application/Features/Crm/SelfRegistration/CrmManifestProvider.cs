using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentRepository;

namespace Diten.Platform.Application.Features.Crm.SelfRegistration;

/// <summary>
/// MOD-0149 Customer 360 / Account Hierarchy — Commercial Suite CRM module self-registration manifest.
/// <para>Reconciles the code-owned identity of the CRM catalog entry (ModuleCode <c>CRM</c>) and its page/permission
/// descriptors. An operator had already created the <c>CRM</c> catalog item manually (Origin=Manual,
/// IsTenantAssignable=true); pushing this manifest flips it to Origin=SelfRegistered while PRESERVING every SOFT
/// operator field (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) per the reconcile ownership rules — so
/// tenant-assignability and the operator's taxonomy choices are untouched.</para>
/// <para><b>Navigation (MOD-0285 data-driven, 2026-08-28):</b> the tenant shell no longer hand-writes CRM links —
/// origin/main adopted the <c>DynamicModuleMenu</c> and its <c>_LayoutTenantShell</c> carries NO static CRM
/// &lt;li&gt; entries. So every CRM page below is registered <c>IsNavigationVisible = true</c>: the descriptor is now
/// the ONLY source of the sidebar entry (double-render is impossible — there is nothing static left to double).
/// Each page's <c>Nav.Page.{code}</c> label is shipped in all seven tenant languages in SharedResource.*.resx
/// (NavManifestL10nGuardTests enforces the key exists), and its RequiredPermission is the verbatim key the CRM page
/// controller and CrmService <c>[HasPermission]</c> both enforce — the sidebar entry only renders for a tenant
/// entitled to CRM and granted that permission.</para>
/// <para>This provider lives in Platform.Application (like the Organization/Workflow cross-service providers); it does
/// NOT add a manifest push inside CrmService, and it declares no Account business capability.</para>
/// </summary>
public sealed class CrmManifestProvider : IModuleManifestProvider
{
    // Verbatim read-permission keys the Diten.Web CRM controllers and CrmService [HasPermission] both enforce.
    private const string AccountsRead = "crm.account.read";
    private const string ContactsRead = "crm.contact.read";
    private const string TerritoryRead = "crm.territory.read";
    private const string SegmentsRead = "crm.segment.read";
    private const string StrategyTemplatesRead = "crm.strategy-template.read";
    private const string CampaignsRead = "crm.campaign.read";
    private const string PlannedVisitsRead = "crm.planned-visit.read";
    private const string VisitPlanRead = "crm.visit-plan.read";
    // WP-VP-FIX-1 — the canonical key (not the crm.territory.read fallback the Web console also accepts): it is granted
    // to the 97c5 tenant Admin (manual-grant-mod0155, measured 2026-10-06), so the entry renders where the page opens.
    private const string VisitReportRead = "crm.visit-report.read";
    private const string CyclePeriodsRead = "crm.cycle-period.read";
    private const string CycleCapacityRead = "crm.cycle-capacity.read";
    private const string ConsentRead = "crm.consent.read";
    private const string KnowledgeRead = "crm.knowledge.read";
    private const string KnowledgeConceptRead = "crm.knowledge.concept.read";
    private const string KnowledgePathRead = "crm.knowledge.path.read";
    private const string ContentEngagementJourneyRead = "crm.knowledge.content-engagement-journey.read";
    private const string ClaimRead = "crm.claim.read";
    // WP-VW-W3 (W3-BE-b) — the objection library (approved answers to doctor objections; approve ≠ manage, SoD).
    private const string ObjectionResponseRead = "crm.objection-response.read";
    // WP-VW-W3 (W3-BE-c) — the visit evidence settings page (read without managing, K-C4).
    private const string VisitEvidencePolicyRead = "crm.visit-evidence-policy.read";
    private const string EligibilityRead = "crm.eligibility.read";
    private const string SafetyTextRead = "crm.safety-text.read";
    private const string CountryLegalProfileRead = "crm.country-legal-profile.read";

    // MOD-0165-FU03 (WP-FREQ-A). The canonical crm.visit-frequency-policy.* keys are not seeded yet, so — exactly like
    // the CrmService [HasPermission] guards and the Diten.Web console — the page's read gate runs on the documented
    // fallback (crm.territory.read). Using the fallback here means the sidebar entry actually renders for a CRM user
    // today; it flips to the canonical key when MOD-0165-FU-RBAC lands. The action descriptors below carry the
    // canonical write/resolve keys so an RBAC admin can grant them ahead of that follow-up.
    private const string VisitFrequencyPolicyReadFallback = TerritoryRead;

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "CRM",
            ModuleName: "CRM",
            DisplayName: "CRM",
            Domain: "Sales", // SOFT: only seeds on first-register; the existing item's operator Domain is preserved.
            Service: "DitenCrmService", // SOFT: preserved for the existing item (operator may correct the legacy value).
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true, // SOFT: preserved for the existing item (already true).
            SortOrder: 60,
            Icon: "bx-buildings",
            IsBaseline: false, // HARD: CRM is a licensed module — tenant entitlement required (never entitlement-free).
            Pages:
            [
                // Each page's Actions declare the granular write operations (with the verbatim permission the CRM
                // controller + CrmService [HasPermission] enforce) so the module catalogue carries page-action
                // descriptors an RBAC admin can grant — not just the read gate on the page itself.
                new ModuleManifestPage("ACCOUNTS", "Accounts", "/CRM/Accounts", AccountsRead, null, true, "List", 10,
                [
                    new ModuleManifestAction("CREATE", "New Account", "crm.account.create", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EDIT", "Edit Account", "crm.account.update", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CONTACTS", "Contacts", "/CRM/Contacts", ContactsRead, null, true, "List", 20,
                [
                    new ModuleManifestAction("CREATE", "New Contact", "crm.contact.create", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("IMPORT", "Import Contacts", "crm.contact.import", "Toolbar", 20, false, true, false),
                    new ModuleManifestAction("EXPORT", "Export Contacts", "crm.contact.export", "Toolbar", 30, false, true, false),
                    new ModuleManifestAction("EDIT", "Edit Contact", "crm.contact.update", "RowAction", 40, false, false, true)
                ]),
                new ModuleManifestPage("TERRITORY_MANAGEMENT", "Territory Management", "/CRM/TerritoryManagement", TerritoryRead, null, true, "List", 30,
                [
                    new ModuleManifestAction("MANAGE_MODEL", "Manage Model", "crm.territory.model.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_NODE", "Manage Node", "crm.territory.node.manage", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("SEGMENTS", "Segments", "/CRM/Segments", SegmentsRead, null, true, "List", 40,
                [
                    new ModuleManifestAction("MANAGE", "New Segment", "crm.segment.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("ACTIVATE", "Activate", "crm.segment.activate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("STRATEGY_TEMPLATES", "Strategy Templates", "/CRM/StrategyTemplates", StrategyTemplatesRead, null, true, "List", 50,
                [
                    new ModuleManifestAction("MANAGE", "New Strategy Template", "crm.strategy-template.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("ACTIVATE", "Activate", "crm.strategy-template.activate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CAMPAIGNS", "Campaigns", "/CRM/Campaigns", CampaignsRead, null, true, "List", 60,
                [
                    new ModuleManifestAction("MANAGE", "New Campaign", "crm.campaign.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_TARGETS", "Manage Targets", "crm.campaign.target.manage", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("SNAPSHOT", "Take Snapshot", "crm.campaign.snapshot.create", "RowAction", 30, false, false, true)
                ]),
                // WP-VP-FIX-1 (D7) — MOD-0155 FU05 Visit Planning console (the rep's week plan → apply writes FU01 atoms)
                // and FU02 Visit Execution calendar (record / amend the immutable Visit Report). Both pages existed with
                // no sidebar entry. Planning sits right before Planned Visits (65), Execution right after it.
                // WP-VW-W2 (CT, WEB-a) — the Visit Workspace: one calendar to plan, run and report visits (MOD-0155 VW).
                // Above Visit Planning; the older pages stay until W9. The page itself also needs crm.visit-plan.read
                // (its own UAS-001 notice otherwise).
                // WP-VW-W3 (BE-a) — the visit execution keys (start / capture / finish / signature, the reads) and the tenant's
                // evidence policy (K-W2); declared here so a role can receive them from the role-permission screen (no seed).
                new ModuleManifestPage("VISIT_WORKSPACE", "Visit Workspace", "/CRM/VisitWorkspace", VisitReportRead, null, true, "List", 63,
                [
                    new ModuleManifestAction("EXECUTION_READ", "Read Visit Execution", "crm.visit-execution.read", "RowAction", 10, false, false, true),
                    new ModuleManifestAction("EXECUTION_RECORD", "Start and Record Visit", "crm.visit-execution.record", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("EVIDENCE_POLICY", "Manage Visit Evidence Policy", "crm.visit-evidence-policy.manage", "Toolbar", 30, false, true, false),
                    // WP-VW-W3-AUTH — the document repository's scope-bound visit-evidence keys (W3-PLT), declared on this
                    // tenant route so the manifest sync carries Scope=Tenant for them. Without a page home they reach Auth
                    // only through the scopeless A1 auto-registration, which stamps every new platform.* key PlatformAdmin,
                    // and no tenant role can then be granted them. They open ONLY the CrmVisitEvidence scope:
                    //   write    — stores visit evidence (photo, signature); compensation-deletes only evidence the caller created.
                    //   read     — reads only visit evidence the caller created.
                    //   read-all — reads every visit evidence object in the tenant (manager / reviewer).
                    // The general platform.document-repository.read / .manage are deliberately NOT declared here.
                    new ModuleManifestAction("EVIDENCE_WRITE", "Store Own Visit Evidence (Photo, Signature)", DocumentRepositoryPermissions.VisitEvidenceWrite, "RowAction", 40, false, false, true),
                    new ModuleManifestAction("EVIDENCE_READ", "Read Own Visit Evidence", DocumentRepositoryPermissions.VisitEvidenceRead, "RowAction", 50, false, false, true),
                    new ModuleManifestAction("EVIDENCE_READ_ALL", "Read All Visit Evidence (Tenant-Wide)", DocumentRepositoryPermissions.VisitEvidenceReadAll, "Toolbar", 60, false, true, false)
                ]),
                // WP-VW-W3 (W3-BE-c) — the visit evidence settings (tenant rules, effective rules of a rep, change history,
                // time-boxed exceptions approved in the Work Center — template CRM-VISIT-EVIDENCE-EXC). Read without
                // managing; the exception keys are separate (the approval itself is the workflow task's).
                new ModuleManifestPage("VISIT_EVIDENCE_SETTINGS", "Visit Evidence Settings", "/CRM/VisitEvidenceSettings", VisitEvidencePolicyRead, null, true, "List", 67,
                [
                    new ModuleManifestAction("MANAGE_POLICY", "Manage Visit Evidence Policy", "crm.visit-evidence-policy.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EXCEPTION_READ", "Read Time-Boxed Exceptions", "crm.visit-evidence-exception.read", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("EXCEPTION_MANAGE", "Manage Time-Boxed Exceptions", "crm.visit-evidence-exception.manage", "Toolbar", 30, false, true, false)
                ]),
                // WP-VW-W4 (W4-BE-c) — the per-product potential thresholds (Low / Medium / High, marketing). The doctor
                // insights keys live on this page so a role can receive them from the role-permission screen: the report
                // and the doctor card read with .read (own + team doctors), .read-all is the whole tenant, .read-aggregate
                // the marketing counts without a doctor's identity, .manage a hand edit of the doctor profile.
                new ModuleManifestPage("CRM_DOCTOR_INSIGHT_THRESHOLDS", "Potential Thresholds", "/CRM/PotentialThresholds", "crm.doctor-insights.thresholds.manage", null, true, "List", 68,
                [
                    new ModuleManifestAction("INSIGHTS_READ", "Read Doctor Insights", "crm.doctor-insights.read", "RowAction", 10, false, false, true),
                    new ModuleManifestAction("INSIGHTS_MANAGE", "Edit Doctor Profiles", "crm.doctor-insights.manage", "Toolbar", 20, false, true, false),
                    new ModuleManifestAction("INSIGHTS_READ_ALL", "Read All Doctors' Insights", "crm.doctor-insights.read-all", "RowAction", 30, false, false, true),
                    new ModuleManifestAction("INSIGHTS_READ_AGGREGATE", "Read Aggregated Doctor Insights", "crm.doctor-insights.read-aggregate", "RowAction", 40, false, false, true)
                ]),
                // WP-VW-W3E (W3E-BE-a, §2.7) — the institution QR codes (bulk page: state, issue, print). Read = the QR state
                // and history (never a payload); manage = issue / regenerate / revoke / print.
                new ModuleManifestPage("CRM_VISIT_QR_CODES", "Visit QR Codes", "/CRM/VisitQrCodes", "crm.visit-qr.read", null, true, "List", 69,
                [
                    new ModuleManifestAction("QR_MANAGE", "Issue, Revoke and Print Institution QR Codes", "crm.visit-qr.manage", "Toolbar", 10, false, true, false)
                ]),
                new ModuleManifestPage("VISIT_PLANNING", "Visit Planning", "/CRM/VisitPlanning", VisitPlanRead, null, true, "List", 64,
                [
                    new ModuleManifestAction("GENERATE", "Generate Plan", "crm.visit-plan.generate", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("APPLY", "Apply Plan", "crm.visit-plan.apply", "RowAction", 20, false, false, true),
                    // WP-VP-2 (B-1) — manifest home of the explicit-grant-only tenant-wide read (Tier-3), the
                    // platform.tasks.read-all precedent: a declared AUTHORITY so an authorized person can grant it to a
                    // tenant role; the entitlement sync and the full-catalog path never grant it (ExplicitGrantOnly).
                    new ModuleManifestAction("READ_ALL", "Read All Visit Plans (Tenant-Wide)", "crm.visit-plan.read-all", "Toolbar", 30, false, true, false)
                ]),
                new ModuleManifestPage("VISIT_EXECUTION", "Visit Execution", "/CRM/VisitExecution", VisitReportRead, null, true, "List", 66,
                [
                    new ModuleManifestAction("RECORD", "Record Visit", "crm.visit-report.record", "RowAction", 10, false, false, true),
                    new ModuleManifestAction("AMEND", "Amend Report", "crm.visit-report.amend", "RowAction", 20, false, false, true),
                    // WP-VW-W4 (W4-BE-a, G2) — competitor usage counts from the reports (no doctor identity, cells under 5
                    // hidden); the MDM competitor screens read it.
                    new ModuleManifestAction("READ_AGGREGATE", "Read Report Usage Counts", "crm.visit-report.read-aggregate", "RowAction", 30, false, false, true)
                ]),
                // MOD-0155-FU01 Visit Planning / Planned Visit — the field team's planning atom. confirm is a SEPARATE
                // key from manage (author-vs-confirmer SoD); there is no delete/bulk-delete surface (cancel/archive).
                new ModuleManifestPage("PLANNED_VISITS", "Planned Visits", "/CRM/PlannedVisits", PlannedVisitsRead, null, true, "List", 65,
                [
                    new ModuleManifestAction("MANAGE", "New Planned Visit", "crm.planned-visit.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("CONFIRM", "Confirm", "crm.planned-visit.confirm", "RowAction", 20, false, false, true),
                    // WP-VP-2 (B-1) — explicit-grant-only tenant-wide read of planned visits + visit reports (see above).
                    new ModuleManifestAction("READ_ALL", "Read All Planned Visits (Tenant-Wide)", "crm.planned-visit.read-all", "Toolbar", 30, false, true, false)
                ]),
                new ModuleManifestPage("CYCLE_PERIODS", "Cycle Periods", "/CRM/CyclePeriods", CyclePeriodsRead, null, true, "List", 70,
                [
                    new ModuleManifestAction("MANAGE", "New Cycle Period", "crm.cycle-period.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("ACTIVATE", "Activate", "crm.cycle-period.activate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CYCLE_CAPACITIES", "Cycle Capacity", "/CRM/CycleCapacities", CycleCapacityRead, null, true, "List", 80,
                [
                    new ModuleManifestAction("MANAGE", "New Cycle Capacity", "crm.cycle-capacity.manage", "Toolbar", 10, false, true, false)
                ]),
                new ModuleManifestPage("CONSENT_PREFERENCES", "Consent & Preferences", "/CRM/ConsentPreferences", ConsentRead, null, true, "List", 90,
                [
                    new ModuleManifestAction("MANAGE_CONSENT", "Manage Consent", "crm.consent.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_PREFERENCE", "Manage Preferences", "crm.preference.manage", "Toolbar", 20, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "crm.consent.evaluate", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("KNOWLEDGE", "Knowledge", "/CRM/Knowledge", KnowledgeRead, null, true, "List", 100,
                [
                    new ModuleManifestAction("MANAGE", "New Content", "crm.knowledge.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_SUBJECT", "Manage Subjects", "crm.knowledge.subject.manage", "Toolbar", 20, false, true, false)
                ]),
                new ModuleManifestPage("KNOWLEDGE_CONCEPTS", "Concepts", "/CRM/KnowledgeConcepts", KnowledgeConceptRead, null, true, "List", 110,
                [
                    new ModuleManifestAction("MANAGE", "New Concept", "crm.knowledge.concept.manage", "Toolbar", 10, false, true, false)
                ]),
                new ModuleManifestPage("KNOWLEDGE_PATHS", "Knowledge Paths", "/CRM/KnowledgePaths", KnowledgePathRead, null, true, "List", 120,
                [
                    new ModuleManifestAction("MANAGE", "New Knowledge Path", "crm.knowledge.path.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("PUBLISH", "Publish", "crm.knowledge.path.publish", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CONTENT_ENGAGEMENT_JOURNEYS", "Content Engagement Journeys", "/CRM/ContentEngagementJourneys", ContentEngagementJourneyRead, null, true, "List", 130,
                [
                    new ModuleManifestAction("MANAGE", "New Journey", "crm.knowledge.content-engagement-journey.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("PUBLISH", "Publish", "crm.knowledge.content-engagement-journey.publish", "RowAction", 20, false, false, true)
                ]),
                // SCMM-12 (CAND-CAP-0011) Claim authoring console. approve is a SEPARATE key from manage (author-vs-
                // approver SoD); there is no delete surface (closing a claim is Archive).
                new ModuleManifestPage("CLAIMS", "Claims", "/CRM/Claims", ClaimRead, null, true, "List", 140,
                [
                    new ModuleManifestAction("MANAGE", "New Claim", "crm.claim.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("APPROVE", "Approve", "crm.claim.approve", "RowAction", 20, false, false, true)
                ]),
                // WP-VW-W3 (W3-BE-b) — the objection library's keys.
                // WP-VW-W3 (W3-BE-c) — now IN the navigation (the W3-WEB-c screen, Content Studio group next to Claims).
                // The approval is the MOD-0023 task in the Work Center (template CRM-OBJ-RESP, MED-DIR): SUBMIT sends a
                // draft to it (and withdraws it); the direct approve key is no longer declared (its endpoint answers 409).
                new ModuleManifestPage("OBJECTION_RESPONSES", "Objection Responses", "/CRM/ObjectionResponses", ObjectionResponseRead, null, true, "List", 145,
                [
                    new ModuleManifestAction("MANAGE", "New Objection Response", "crm.objection-response.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("SUBMIT", "Submit for Approval", "crm.objection-response.submit", "RowAction", 20, false, false, true)
                ]),
                // WP-KP-5a-UI — the Regulatory-approved master data the page designer's locked blocks will read
                // (DESIGN-KP-STUDIO §2.4): safety texts (product × country × language) and country legal profiles
                // (country × language). The approval itself is the MOD-0023 Regulatory task (K1); SUBMIT sends a draft
                // to it. No delete surface (Archive).
                new ModuleManifestPage("SAFETY_TEXTS", "Safety Texts", "/CRM/SafetyTexts", SafetyTextRead, null, true, "List", 150,
                [
                    new ModuleManifestAction("MANAGE", "New Safety Text", "crm.safety-text.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("SUBMIT", "Submit for Approval", "crm.safety-text.submit", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("LEGAL_PROFILES", "Legal Profiles", "/CRM/LegalProfiles", CountryLegalProfileRead, null, true, "List", 160,
                [
                    new ModuleManifestAction("MANAGE", "New Legal Profile", "crm.country-legal-profile.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("SUBMIT", "Submit for Approval", "crm.country-legal-profile.submit", "RowAction", 20, false, false, true)
                ]),
                // WP-SB-1R retired the Content Scopes page (CONTENT_SCOPES) and WP-KP-4 the Content Sets page
                // (CONTENT_SETS): the Knowledge Path Studio (KNOWLEDGE_PATHS) took the content set's job. A re-registration
                // of this manifest prunes both pages from the live menu.
                // SCMM-11-UI (CAND-CAP-0011) eligibility policy authoring + evaluate. evaluate is a SEPARATE key from
                // manage (author-vs-evaluator SoD); no delete surface (Archive).
                new ModuleManifestPage("ELIGIBILITY_POLICIES", "Eligibility Policies", "/CRM/EligibilityPolicies", EligibilityRead, null, true, "List", 170,
                [
                    new ModuleManifestAction("MANAGE", "New Policy", "crm.eligibility.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "crm.eligibility.evaluate", "Toolbar", 20, false, true, false)
                ]),
                // MOD-0165-FU03 (WP-FREQ-A) Visit Frequency / Call-Cycle Policy console. Archive closes a policy as
                // readable history; Delete is the WP-FREQ-A soft-delete that removes it from the working set (both soft,
                // no hard delete). resolve is a SEPARATE read op from manage. Read gate = territory.read fallback until
                // MOD-0165-FU-RBAC seeds the canonical keys (see VisitFrequencyPolicyReadFallback).
                new ModuleManifestPage("VISIT_FREQUENCY_POLICIES", "Visit Frequency Policies", "/CRM/VisitFrequencyPolicies", VisitFrequencyPolicyReadFallback, null, true, "List", 180,
                [
                    new ModuleManifestAction("MANAGE", "New Policy", "crm.visit-frequency-policy.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("RESOLVE", "Resolve", "crm.visit-frequency-policy.resolve", "Toolbar", 20, false, true, false),
                    new ModuleManifestAction("DELETE", "Delete", "crm.visit-frequency-policy.manage", "RowAction", 30, false, false, true)
                ]),
                // WP-VW-W3E (BE-b, §2.7, K-W3E-11) — the manager's visit evidence flags (read only): .read = the caller's
                // team (+ the caller), .read-all = the tenant.
                new ModuleManifestPage("CRM_VISIT_EVIDENCE_FLAGS", "Visit Evidence Flags", "/CRM/VisitEvidenceFlags", "crm.visit-evidence-flags.read", null, true, "List", 69,
                [
                    new ModuleManifestAction("FLAGS_READ_ALL", "Read All Visit Evidence Flags (Tenant-Wide)", "crm.visit-evidence-flags.read-all", "Toolbar", 10, false, true, false)
                ])
            ]);
}
