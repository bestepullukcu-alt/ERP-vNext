using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

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
    private const string CyclePeriodsRead = "crm.cycle-period.read";
    private const string CycleCapacityRead = "crm.cycle-capacity.read";
    private const string ConsentRead = "crm.consent.read";
    private const string KnowledgeRead = "crm.knowledge.read";
    private const string KnowledgeConceptRead = "crm.knowledge.concept.read";
    private const string KnowledgePathRead = "crm.knowledge.path.read";
    private const string ContentEngagementJourneyRead = "crm.knowledge.content-engagement-journey.read";

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
                // MOD-0155-FU01 Visit Planning / Planned Visit — the field team's planning atom. confirm is a SEPARATE
                // key from manage (author-vs-confirmer SoD); there is no delete/bulk-delete surface (cancel/archive).
                new ModuleManifestPage("PLANNED_VISITS", "Planned Visits", "/CRM/PlannedVisits", PlannedVisitsRead, null, true, "List", 65,
                [
                    new ModuleManifestAction("MANAGE", "New Planned Visit", "crm.planned-visit.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("CONFIRM", "Confirm", "crm.planned-visit.confirm", "RowAction", 20, false, false, true)
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
                ])
            ]);
}
