# Sayfa yardımı borç defteri

Kural: `.antigravity/rules/page-help-and-keyboard.md` (UXH-001) · Ölçen test: `frontend/Diten.Web/tests/page-help-ledger.test.js`

**Tavan (yalnız azalır): kiracı 246 · platform 18 · frozen 99**

- Tavan artışı (CT kararı): kiracı 236 → 246 — WP-LANE-UX-GUARDS-01, bir kerelik: main-sync (2026-10-12) ile gelen 11 Tedarik Zinciri / CRM sayfası (sahipleri satırlarında, metin sahiplerinin işi) eklendi; CRM'in kaldırdığı CONTENTSCOPES (WP-SB-1R) çıktı. Kural gevşemedi: bundan sonraki her yeni satır yine kırmızı.

Tavan bu satırdadır, testte değil. UX-COMMON-FIX2: test satır sayısının bu sayılara **EŞİT** olduğunu ölçer; bir satır
eklemek ya da silmek bu başlığı da düzenletir, böylece her değişiklik PR farkında başlık satırıyla görünür. Sayının bir
önceki sürümden büyük olmadığını (git geçmişi) test ölçemez — o, ana dala karşı ayrı bir CI denetimidir (CT backlog'u).

Bu defter, **"?" yardım panelinde kendi metni olmayan** sayfaları listeler. Metni olmayan sayfada panel yalnız
kısayolları ve "Bu sayfa için yardım metni hazırlanıyor" yazısını gösterir; bu bir hata değil, bir **borçtur**.

- **Kiracı sayfası:** üretimden türetilir — menünün / Ctrl+K'nin kararlı kodları (`Nav.Page.{KOD}`) ile modül
  manifestlerinin (`services/**/*ManifestProvider.cs`) sayfa kodlarının birleşimi; menüde görünmeyen alt sayfalar da
  dahil. Ya `PageHelpResource` içinde `Help.{KOD}.Purpose` metnini **7 dilde** taşır ya da aşağıdaki tabloda durur.
- **Platform yönetici sayfası:** kod `PLATFORM` + `Views/Platform/` klasör adı; metin **2 dilde** (en, tr) ya da tabloda.
- **FROZEN `_Layout` görünümleri:** `Layout =` taşımayan ve `Views/_ViewStart.cshtml` ile dondurulmuş `_Layout`'a düşen
  görünümler. O kabukta yardım düğmesi, Enter kuralı ve kısayol katmanı **yok** — yardım gösterilemez. Tenant shell'e
  taşınan görünüm bu listeden çıkar (ve kiracı sayfası olarak yukarıdaki kurala girer).

Bu liste **yalnız küçülür**. Metni yazılan sayfanın satırı çıkarılır (metni olan sayfa defterde kalırsa test kızarır);
satır eklemek yasaktır — yeni bir sayfa metniyle birlikte gelir.

Neden `roadmap/`: belge henüz yazılmamış metinleri anlatıyor (`docs-organization.md` §1, soru 3).

## Kiracı sayfaları (246)

| kod | menüdeki ad (en, `Nav.Page`) ya da manifest |
|---|---|
| ACCESSMATRIX | Document Access Matrix |
| ACCESSMATRIXCREATE | (menüde yok) DocumentManagementManifestProvider.cs |
| ACCESSMATRIXDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| ACCESSMATRIXEDIT | (menüde yok) DocumentManagementManifestProvider.cs |
| ACCOUNT | Account |
| ACCOUNTCONTACT | Account Contact |
| ACCOUNTRELATIONSHIP | Account Relationship |
| ACCOUNTS | Accounts |
| APPLICANTINTAKE | Applicant Intake |
| ASSIGNMENTCREATE | (menüde yok) OrganizationManifestProvider.cs |
| ASSIGNMENTDETAILS | (menüde yok) OrganizationManifestProvider.cs |
| ASSIGNMENTEDIT | (menüde yok) OrganizationManifestProvider.cs |
| ASSOCIATIONMEMBERSHIPS | Association Memberships |
| ASSOCIATIONOPERATIONS | Association Operations |
| ATTRIBUTE | Attribute |
| BENEFITCOMMITMENTS | Benefit Commitments |
| BRANDS | Brands |
| CAMPAIGN | Campaign |
| CAMPAIGNS | Campaigns |
| CAMPAIGNSNAPSHOT | Campaign Snapshot |
| CAMPAIGNTARGET | Campaign Target |
| CANDIDATECAREERPASSPORT | Candidate Career Passport |
| CANDIDATEDISPUTES | Candidate Disputes |
| CANDIDATEPIPELINE | Candidate Pipeline |
| CANDIDATEPROFILES | Candidate Profiles |
| CARRIERS | Carriers — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| CLAIMS | Claims |
| COMPENSATIONBENEFITS | Compensation &amp; Benefits |
| COMPETENCYSKILLS | Competency &amp; Skills |
| CONSENT | Consent |
| CONSENTPREFERENCES | Consent &amp; Preferences |
| CONSENTVISIBILITYPOLICIES | Consent Visibility Policies |
| CONTACT | Contact |
| CONTACTS | Contacts |
| CONTENTENGAGEMENTJOURNEYS | Content Engagement Journeys |
| CONTENTSETS | Content Sets |
| CONTRACTS | Contracting &amp; Clause Library |
| CONTROLLEDDOCUMENTS | Controlled Documents |
| CONTROLLEDDOCUMENTSCREATE | (menüde yok) DocumentManagementManifestProvider.cs |
| CONTROLLEDDOCUMENTSDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| CONTROLLEDDOCUMENTSSHARE | (menüde yok) DocumentManagementManifestProvider.cs |
| CONTROLLEDDOCUMENTSVERSIONS | (menüde yok) DocumentManagementManifestProvider.cs |
| CREATE | (menüde yok) GoldenCompactManifestProvider.cs |
| CUSTOMFIELDS | Custom Fields |
| CYCLECAPACITIES | Cycle Capacity |
| CYCLECAPACITY | Cycle Capacity |
| CYCLEPERIOD | Cycle Period |
| CYCLEPERIODS | Cycle Periods |
| DATAQUALITY | Data Quality |
| DEFINITIONS | Workflow Definitions |
| DESIGNER | (menüde yok) WorkflowManifestProvider.cs |
| DETAILS | (menüde yok) GoldenCompactManifestProvider.cs |
| DEVELOPMENTPLANS | Development Plans |
| DOCUMENTLIST | Document List |
| EARLYWARNINGSIGNALS | Early Warning Signals |
| EDIT | (menüde yok) GoldenCompactManifestProvider.cs |
| ELIGIBILITYPOLICIES | Eligibility Policies |
| EMPLOYEE | Employee |
| EMPLOYEEMASTER | Employee Master |
| EMPLOYEEONBOARDING | Employee Onboarding |
| EMPLOYEEPROJECTIONS | Employee Projections |
| EMPLOYMENTCHANGES | Employment Changes |
| ESCALATIONS | (menüde yok) WorkflowManifestProvider.cs |
| EXITREFERENCERECORDS | Exit Reference Records |
| FIELDDEFINITIONS | Field Definitions |
| FINISHEDGOODS | Finished Goods |
| GLOBALPRODUCTS | Global Products |
| GRN | Receiving (GRN) |
| GSKUS | GSKUs |
| HEADCOUNTBUDGET | Headcount Budget |
| HIERARCHY | Hierarchy |
| HIRINGRISKINDICATORS | Hiring Risk Indicators |
| HRCASEMANAGEMENT | HR Case Management |
| HRCOMPLIANCE | HR Compliance |
| HRDOCUMENTATION | HR Documentation |
| HRKPIANALYTICS | HR KPI Analytics |
| INBOX | Inbox |
| INDUSTRYKNOWLEDGENETWORK | Industry Knowledge Network |
| INDUSTRYSKILLPASSPORT | Industry Skill Passport |
| INDUSTRYSUCCESSIONPOOL | Industry Succession Pool |
| INDUSTRYTALENTPOOL | Industry Talent Pool |
| INITIATIVES | Initiatives |
| INSTANCEDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| INSTANCES | Document Instances |
| INVESTMENTCASES | Investment Cases |
| INVOICEMATCH | Invoice Capture &amp; 3-Way Match |
| KNOWLEDGE | Knowledge |
| KNOWLEDGECONCEPT | Knowledge Concept |
| KNOWLEDGECONCEPTS | Concepts |
| KNOWLEDGECONTENTENGAGEMENTJOURNEY | Content Engagement Journey |
| KNOWLEDGEPATH | Knowledge Path |
| KNOWLEDGEPATHS | Knowledge Paths |
| KNOWLEDGESUBJECT | Knowledge Subject |
| LEARNINGTRAINING | Learning &amp; Training |
| LEGALENTITIES | Legal Entities |
| LEGALPROFILES | Legal Profiles — main-sync (2026-10-12) ile geldi; sahibi CRM; yardım metni yok, main'den geldi (sahibine iletilecek) |
| LOADS | Loads — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| LSKUS | Local SKUs |
| MASTERREGISTER | Document Master Register |
| MASTERREGISTERIMPORT | (menüde yok) DocumentManagementManifestProvider.cs |
| MEETINGCREATE | (menüde yok) MeetingManifestProvider.cs |
| MEETINGDETAIL | (menüde yok) MeetingManifestProvider.cs |
| MEETINGEDIT | (menüde yok) MeetingManifestProvider.cs |
| MEETINGREPORT | Meeting Report |
| MEETINGS | Meetings |
| MEETINGSERIES | Meeting Series |
| MEETINGTYPES | Meeting Types |
| MENTORSHIPRECOMMENDATIONNETWORK | Mentorship Recommendation Network |
| MENUSETTINGS | Menu Settings |
| MODEL | Model |
| MYTIMESHEET | My Timesheet |
| NODE | Node |
| OFFBOARDINGCASES | Offboarding Cases |
| OFFERMANAGEMENT | Offer Management |
| ORGANIZATIONFIELDDEFINITIONS | Field Definitions |
| ORGANIZATIONUNITS | Organization Units |
| ORGFIELDDEFINITIONCREATE | (menüde yok) OrganizationManifestProvider.cs |
| ORGFIELDDEFINITIONDETAILS | (menüde yok) OrganizationManifestProvider.cs |
| ORGFIELDDEFINITIONEDIT | (menüde yok) OrganizationManifestProvider.cs |
| ORGUNITCREATE | (menüde yok) OrganizationManifestProvider.cs |
| ORGUNITDETAILS | (menüde yok) OrganizationManifestProvider.cs |
| ORGUNITEDIT | (menüde yok) OrganizationManifestProvider.cs |
| OVERRIDE | Override |
| OVERVIEW | Overview |
| PAYBENCHMARKING | Pay Benchmarking |
| PERFORMANCEREVIEWS | Performance Reviews |
| PERMISSIONS | (menüde yok) AccessGovernanceManifestProvider.cs · Permissions |
| PLANNEDVISIT | Planned Visit |
| PLANNEDVISITS | Planned Visits |
| PORTFOLIOS | Portfolios |
| POSITIONASSIGNMENTS | Position Assignments |
| POSITIONCREATE | (menüde yok) OrganizationManifestProvider.cs |
| POSITIONDETAILS | (menüde yok) OrganizationManifestProvider.cs |
| POSITIONEDIT | (menüde yok) OrganizationManifestProvider.cs |
| POSITIONS | Positions |
| PREFERENCE | Preference |
| PRODUCTABBREVIATIONS | Product Abbreviations |
| PRODUCTLEGALENTITYSCOPES | (menüde yok) ProductItemSkuMasterManifestProvider.cs |
| PRODUCTS | Products |
| PROFESSIONALREPUTATIONLEDGER | Professional Reputation Ledger |
| PROGRAMS | Programs |
| PROJECTS | Projects |
| PURCHASEORDERS | Purchase Orders |
| QMSBASELINES | QMS Baselines |
| QMSCREATEMANUAL | (menüde yok) DocumentManagementManifestProvider.cs |
| QMSDESIGNER | (menüde yok) DocumentManagementManifestProvider.cs |
| QMSDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| QMSIMPORT | (menüde yok) DocumentManagementManifestProvider.cs |
| RDATTRIBUTES | (menüde yok) ReferenceDataManifestProvider.cs |
| RDDRAFTWIZARD | (menüde yok) ReferenceDataManifestProvider.cs |
| RDHIERARCHY | (menüde yok) ReferenceDataManifestProvider.cs |
| RDIMPORTPREVIEW | (menüde yok) ReferenceDataManifestProvider.cs |
| RDMAPPINGS | (menüde yok) ReferenceDataManifestProvider.cs |
| RDPUBLISHREVIEW | (menüde yok) ReferenceDataManifestProvider.cs |
| RDSETDETAIL | (menüde yok) ReferenceDataManifestProvider.cs |
| RDSETS | Reference Data Sets |
| RDUSAGE | (menüde yok) ReferenceDataManifestProvider.cs |
| RDUSAGECREATE | (menüde yok) ReferenceDataManifestProvider.cs |
| RDUSAGEDETAILS | (menüde yok) ReferenceDataManifestProvider.cs |
| RDUSAGEEDIT | (menüde yok) ReferenceDataManifestProvider.cs |
| RDVERSIONDETAIL | (menüde yok) ReferenceDataManifestProvider.cs |
| RECORDS | Records |
| RECURRENCERULES | Recurrence Rules |
| REFERENCEEXCHANGE | Reference Exchange |
| REHIRERECOMMENDATIONS | Rehire Recommendations |
| REPORTINGLINE | Reporting Line |
| REPORTS | Reports |
| REPOSITORYASSESSMENT | Repository Assessments |
| REQUISITIONS | Requisitions |
| RESTRICTEDINTEGRITYREGISTRY | Restricted Integrity Registry |
| RETURNS | Returns — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| REVIEWBOARD | Review Board |
| SAFETYTEXTS | Safety Texts — main-sync (2026-10-12) ile geldi; sahibi CRM; yardım metni yok, main'den geldi (sahibine iletilecek) |
| SECTORMOBILITYINTELLIGENCE | Sector Mobility Intelligence |
| SECTORTALENTTRENDS | Sector Talent Trends |
| SECURITYSETTINGS | Security Settings |
| SEGMENT | Segment |
| SEGMENTS | Segments |
| SELFSERVICE | Self Service |
| SENSITIVEACCESS | Sensitive Access |
| SHIPMENTCLAIMS | Shipment Claims — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| SHIPMENTCREATE | (menüde yok) manifest — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| SHIPMENTDETAILS | (menüde yok) manifest — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| SHIPMENTS | Shipments — main-sync (2026-10-12) ile geldi; sahibi SCE / MVP-6 (gmg-10); yardım metni yok, main'den geldi (sahibine iletilecek) |
| SKILLSGAPHEATMAP | Skills Gap Heatmap |
| SLARULES | (menüde yok) WorkflowManifestProvider.cs |
| SOURCING | Sourcing |
| STRATEGYTEMPLATE | Strategy Template |
| STRATEGYTEMPLATES | Strategy Templates |
| SUCCESSION | Succession |
| SUPPLIERS | Suppliers |
| TALENTDATAFOUNDATION | Talent Data Foundation |
| TALENTDEVELOPMENTNETWORK | Talent Development Network |
| TALENTSUPPLYDEMANDFORECASTING | Talent Supply Demand Forecasting |
| TASKCHECKLISTTEMPLATES | Checklist Templates |
| TASKCREATE | New task |
| TASKDETAIL | Task detail |
| TASKDOCUMENTLIST | Controlled Documents |
| TASKEDIT | Edit task |
| TASKFIELDDEFINITIONS | Field Definitions |
| TASKRECURRENCERULES | Recurring Task Rules |
| TASKS | Tasks |
| TASKTEMPLATES | Task Templates |
| TASKTYPES | Task Types |
| TASKWORKREPORT | Work Report |
| TEMPLATEMASTERS | Corporate Template Masters |
| TEMPLATEMASTERSCREATE | (menüde yok) DocumentManagementManifestProvider.cs |
| TEMPLATEMASTERSDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| TEMPLATEVARIANTS | Template Variants |
| TEMPLATEVARIANTSCREATE | (menüde yok) DocumentManagementManifestProvider.cs |
| TEMPLATEVARIANTSDETAILS | (menüde yok) DocumentManagementManifestProvider.cs |
| TENANTNAVIGATION | Menu Settings |
| TENANTSECURITY | Security Settings |
| TERRITORY | Territory |
| TERRITORYMANAGEMENT | Territory Management |
| TERRITORYMODEL | Territory Model |
| TERRITORYNODE | Territory Node |
| TIMEAPPROVALDETAIL | (menüde yok) TimeEntryManifestProvider.cs |
| TIMEAPPROVALS | Timesheet Approvals |
| TIMEATTENDANCELEAVE | Time, Attendance &amp; Leave |
| TIMECATEGORIES | Work Categories |
| TIMESETTINGS | Time Entry Settings |
| TRANSITIONGATE | (menüde yok) WorkflowManifestProvider.cs |
| TRUSTLEVELS | Trust Levels |
| VERIFIEDCERTIFICATIONREGISTRY | Verified Certification Registry |
| VERIFIEDPARTICIPANTS | Verified Participants |
| VERSIONS | (menüde yok) WorkflowManifestProvider.cs |
| VISITEXECUTION | Visit Execution — main-sync (2026-10-12) ile geldi; sahibi CRM; yardım metni yok, main'den geldi (sahibine iletilecek) |
| VISITFREQUENCYPOLICIES | Visit Frequency Policies |
| VISITPLANNING | Visit Planning — main-sync (2026-10-12) ile geldi; sahibi CRM; yardım metni yok, main'den geldi (sahibine iletilecek) |
| VISUALDESIGNER | (menüde yok) WorkflowManifestProvider.cs |
| WIZARDCREATE | (menüde yok) LegalEntityManifestProvider.cs |
| WIZARDEDIT | (menüde yok) LegalEntityManifestProvider.cs |
| WORKCENTER | Task Center |
| WORKFORCEANALYTICS | Workforce Analytics |
| WORKFORCEPLANNING | Workforce Planning |
| WORKINGCALENDARCREATE | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDARDETAILS | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDAREDIT | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDARIMPORTREVIEW | (menüde yok) WorkingCalendarImportManifestProvider.cs · Review Working Calendar Import |
| WORKINGCALENDARIMPORTS | Working Calendar Imports |
| WORKINGCALENDAROVERRIDECREATE | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDAROVERRIDEDETAILS | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDAROVERRIDEEDIT | (menüde yok) WorkingCalendarManifestProvider.cs |
| WORKINGCALENDAROVERRIDES | Working Calendar Overrides |
| WORKINGCALENDARS | Working Calendars |

## Platform yönetici sayfaları (18)

| kod | klasör |
|---|---|
| PLATFORMACCOUNT | Views/Platform/Account |
| PLATFORMADMINISTRATORS | Views/Platform/Administrators |
| PLATFORMAUDITLOG | Views/Platform/AuditLog |
| PLATFORMAUDITRETENTION | Views/Platform/AuditRetention |
| PLATFORMDOMAINMANAGEMENT | Views/Platform/DomainManagement |
| PLATFORMINTERFACEREGISTRY | Views/Platform/InterfaceRegistry |
| PLATFORMMODULECATALOG | Views/Platform/ModuleCatalog |
| PLATFORMNOTIFICATIONDISPATCHES | Views/Platform/NotificationDispatches |
| PLATFORMNOTIFICATIONEVENTS | Views/Platform/NotificationEvents |
| PLATFORMNOTIFICATIONSETTINGS | Views/Platform/NotificationSettings |
| PLATFORMNOTIFICATIONTEMPLATES | Views/Platform/NotificationTemplates |
| PLATFORMSELFACCESS | Views/Platform/SelfAccess |
| PLATFORMSERVICEMANAGEMENT | Views/Platform/ServiceManagement |
| PLATFORMSUBSCRIPTIONFEATURES | Views/Platform/SubscriptionFeatures |
| PLATFORMSUBSCRIPTIONPLANS | Views/Platform/SubscriptionPlans |
| PLATFORMTENANTS | Views/Platform/Tenants |
| PLATFORMWORKINGCALENDARIMPORTS | Views/Platform/WorkingCalendarImports |
| PLATFORMWORKINGCALENDARS | Views/Platform/WorkingCalendars |

## FROZEN `_Layout` görünümleri (99)

| görünüm | kabuk |
|---|---|
| `DecompositionTreeBuilder/Index.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/DeliveryMap.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/InitiativeCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/InitiativeDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/Initiatives.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/Overview.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/Programs.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DeliveryExecutionManagement/ProjectCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DemandIdeas/Capture.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DemandIdeas/Dashboard.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DemandIdeas/Detail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DemandIdeas/Index.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `DemandIdeas/ModulePlaceholder.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/CascadeBuilder.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Connections.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ConnectionsGap.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ConnectionsGraph.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ConnectionsLineage.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ConsolidationView.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/DecisionsActions.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/GoalCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/GoalCreateStepper.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/GoalDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/GoalHierarchy.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Goals.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/InitiativeCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/InitiativeDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Initiatives.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiCatalog.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiEditor.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiGovernance.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiLibraryCatalog.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiOwnership.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiScorecard.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiScorecardPackDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiScorecardPacks.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiTemplateDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/KpiThresholdModels.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ObjectiveAlignment.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ObjectiveCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ObjectiveDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Objectives.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Overview.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Planning.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/PlanningCycleCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/PlanningCycleDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/PlanningCycles.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ProjectDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/Projects.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ReviewCalendar.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ReviewHistory.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/ReviewPack.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryBlueprintDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryCatalog.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryGovernance.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryImportWorkbench.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryInstantiateWizard.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryProjects.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyLibraryUsage.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyPeriodCreate.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyPeriodDetail.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/StrategyPeriods.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/TargetAllocation.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `EnterpriseStrategyBusinessPerformance/VarianceAnalysis.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `InventoryGovernance/Index.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Cadence.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/ExecutiveCockpit.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Favorites.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Index.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/ModulePlaceholder.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/RecentActivity.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Reports.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Search.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Subdomain.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/SubdomainWorkspace.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/Subdomains.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `ManagementGovernance/WorkQueue.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `Shared/Components/DynamicModuleMenu/Default.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `Shared/Components/TenantBrand/Default.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `Shared/Components/TenantFavicon/Default.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/AllWorkScopeBar.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/AllWorkTab.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/InboxList.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/InboxRow.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/InboxTab.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskActionsPanel.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskActivity.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskDependencies.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskDescription.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskHeader.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskMetadataSummary.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskSecondaryMeta.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskStepBar.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskSubtasks.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/TaskTimeEntry.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/WorkCenterHeader.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/WorkCenterPage.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
| `WorkCenter/WorkCenterTabs.cshtml` | `_Layout` (FROZEN) — yardım gösterilemez |
