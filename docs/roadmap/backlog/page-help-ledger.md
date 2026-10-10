# Sayfa yardımı borç defteri

Kural: `.antigravity/rules/page-help-and-keyboard.md` (UXH-001) · Ölçen test: `frontend/Diten.Web/tests/page-help-ledger.test.js`

Bu defter, **"?" yardım panelinde kendi metni olmayan** sayfaları listeler. Metni olmayan sayfada panel yalnız
kısayolları ve "Bu sayfa için yardım metni hazırlanıyor" yazısını gösterir; bu bir hata değil, bir **borçtur**.

- Kiracı sayfası: sayfa kodu (`Nav.Page.{KOD}`, menü / Ctrl+K'nin kararlı kodu) ya `PageHelpResource` içinde
  `Help.{KOD}.Purpose` metnini **7 dilde** (en, tr, fr, es, zh, ar, ru) taşır ya da aşağıdaki tabloda adıyla durur.
- Platform yönetici sayfası: kod `PLATFORM` + `Views/Platform/` klasör adı; metin **2 dilde** (en, tr) ya da tabloda.

Bu liste **yalnız küçülür**. Metni yazılan sayfanın satırı çıkarılır (metni olan sayfa defterde kalırsa test kızarır);
satır eklemek yasaktır — yeni bir sayfa metniyle birlikte gelir. Satır sayısı testte tavan olarak sabittir.

Neden `roadmap/`: belge henüz yazılmamış metinleri anlatıyor (`docs-organization.md` §1, soru 3).

## Kiracı sayfaları (171)

| kod | menüdeki ad (en, `Nav.Page`) |
|---|---|
| WORKINGCALENDAROVERRIDES | Working Calendar Overrides |
| SECURITYSETTINGS | Security Settings |
| MENUSETTINGS | Menu Settings |
| RECORDS | Records |
| QMSBASELINES | QMS Baselines |
| INSTANCES | Document Instances |
| CONTROLLEDDOCUMENTS | Controlled Documents |
| TEMPLATEMASTERS | Corporate Template Masters |
| TEMPLATEVARIANTS | Template Variants |
| ACCESSMATRIX | Document Access Matrix |
| LEGALENTITIES | Legal Entities |
| RDSETS | Reference Data Sets |
| ORGANIZATIONUNITS | Organization Units |
| POSITIONS | Positions |
| POSITIONASSIGNMENTS | Position Assignments |
| DEFINITIONS | Workflow Definitions |
| WORKCENTER | Task Center |
| TASKCHECKLISTTEMPLATES | Checklist Templates |
| TASKTEMPLATES | Task Templates |
| TASKRECURRENCERULES | Recurring Task Rules |
| TASKS | Tasks |
| TASKCREATE | New task |
| TASKDETAIL | Task detail |
| TASKEDIT | Edit task |
| GLOBALPRODUCTS | Global Products |
| GSKUS | GSKUs |
| TASKTYPES | Task Types |
| TASKWORKREPORT | Work Report |
| TASKDOCUMENTLIST | Controlled Documents |
| TASKFIELDDEFINITIONS | Field Definitions |
| ACCOUNTS | Accounts |
| CONTACTS | Contacts |
| TERRITORYMANAGEMENT | Territory Management |
| SEGMENTS | Segments |
| STRATEGYTEMPLATES | Strategy Templates |
| CAMPAIGNS | Campaigns |
| CYCLEPERIODS | Cycle Periods |
| CYCLECAPACITIES | Cycle Capacity |
| CONSENTPREFERENCES | Consent &amp; Preferences |
| KNOWLEDGE | Knowledge |
| KNOWLEDGECONCEPTS | Concepts |
| CLAIMS | Claims |
| CONTENTSCOPES | Content Scopes |
| CONTENTSETS | Content Sets |
| VISITFREQUENCYPOLICIES | Visit Frequency Policies |
| ELIGIBILITYPOLICIES | Eligibility Policies |
| KNOWLEDGEPATHS | Knowledge Paths |
| CONTENTENGAGEMENTJOURNEYS | Content Engagement Journeys |
| WORKINGCALENDARS | Working Calendars |
| WORKINGCALENDARIMPORTS | Working Calendar Imports |
| MASTERREGISTER | Document Master Register |
| REPOSITORYASSESSMENT | Repository Assessments |
| BRANDS | Brands |
| PLANNEDVISITS | Planned Visits |
| PORTFOLIOS | Portfolios |
| INITIATIVES | Initiatives |
| PROGRAMS | Programs |
| PROJECTS | Projects |
| INVESTMENTCASES | Investment Cases |
| BENEFITCOMMITMENTS | Benefit Commitments |
| PRODUCTS | Products |
| ACCOUNT | Account |
| CAMPAIGN | Campaign |
| CAMPAIGNSNAPSHOT | Campaign Snapshot |
| CAMPAIGNTARGET | Campaign Target |
| CONSENT | Consent |
| CONTACT | Contact |
| CYCLECAPACITY | Cycle Capacity |
| CYCLEPERIOD | Cycle Period |
| KNOWLEDGECONCEPT | Knowledge Concept |
| KNOWLEDGECONTENTENGAGEMENTJOURNEY | Content Engagement Journey |
| KNOWLEDGEPATH | Knowledge Path |
| KNOWLEDGESUBJECT | Knowledge Subject |
| PLANNEDVISIT | Planned Visit |
| PREFERENCE | Preference |
| SEGMENT | Segment |
| STRATEGYTEMPLATE | Strategy Template |
| TERRITORY | Territory |
| TERRITORYMODEL | Territory Model |
| TERRITORYNODE | Territory Node |
| ATTRIBUTE | Attribute |
| HIERARCHY | Hierarchy |
| OVERVIEW | Overview |
| ACCOUNTCONTACT | Account Contact |
| ACCOUNTRELATIONSHIP | Account Relationship |
| MODEL | Model |
| NODE | Node |
| REPORTS | Reports |
| DATAQUALITY | Data Quality |
| EMPLOYEE | Employee |
| CUSTOMFIELDS | Custom Fields |
| REPORTINGLINE | Reporting Line |
| FINISHEDGOODS | Finished Goods |
| LSKUS | Local SKUs |
| PRODUCTABBREVIATIONS | Product Abbreviations |
| DOCUMENTLIST | Document List |
| FIELDDEFINITIONS | Field Definitions |
| RECURRENCERULES | Recurrence Rules |
| TENANTNAVIGATION | Menu Settings |
| TENANTSECURITY | Security Settings |
| INBOX | Inbox |
| OVERRIDE | Override |
| ORGANIZATIONFIELDDEFINITIONS | Field Definitions |
| MEETINGS | Meetings |
| MYTIMESHEET | My Timesheet |
| TIMEAPPROVALS | Timesheet Approvals |
| TIMECATEGORIES | Work Categories |
| TIMESETTINGS | Time Entry Settings |
| MEETINGTYPES | Meeting Types |
| MEETINGSERIES | Meeting Series |
| MEETINGREPORT | Meeting Report |
| APPLICANTINTAKE | Applicant Intake |
| CANDIDATEPIPELINE | Candidate Pipeline |
| COMPENSATIONBENEFITS | Compensation &amp; Benefits |
| COMPETENCYSKILLS | Competency &amp; Skills |
| DEVELOPMENTPLANS | Development Plans |
| EMPLOYEEONBOARDING | Employee Onboarding |
| EMPLOYEEPROJECTIONS | Employee Projections |
| EMPLOYMENTCHANGES | Employment Changes |
| HEADCOUNTBUDGET | Headcount Budget |
| HRCASEMANAGEMENT | HR Case Management |
| HRCOMPLIANCE | HR Compliance |
| HRDOCUMENTATION | HR Documentation |
| HRKPIANALYTICS | HR KPI Analytics |
| LEARNINGTRAINING | Learning &amp; Training |
| OFFBOARDINGCASES | Offboarding Cases |
| OFFERMANAGEMENT | Offer Management |
| PERFORMANCEREVIEWS | Performance Reviews |
| SELFSERVICE | Self Service |
| SENSITIVEACCESS | Sensitive Access |
| SUCCESSION | Succession |
| TIMEATTENDANCELEAVE | Time, Attendance &amp; Leave |
| WORKFORCEPLANNING | Workforce Planning |
| EMPLOYEEMASTER | Employee Master |
| ASSOCIATIONMEMBERSHIPS | Association Memberships |
| ASSOCIATIONOPERATIONS | Association Operations |
| CANDIDATECAREERPASSPORT | Candidate Career Passport |
| CANDIDATEDISPUTES | Candidate Disputes |
| CANDIDATEPROFILES | Candidate Profiles |
| CONSENTVISIBILITYPOLICIES | Consent Visibility Policies |
| EARLYWARNINGSIGNALS | Early Warning Signals |
| EXITREFERENCERECORDS | Exit Reference Records |
| HIRINGRISKINDICATORS | Hiring Risk Indicators |
| INDUSTRYKNOWLEDGENETWORK | Industry Knowledge Network |
| INDUSTRYSKILLPASSPORT | Industry Skill Passport |
| INDUSTRYSUCCESSIONPOOL | Industry Succession Pool |
| INDUSTRYTALENTPOOL | Industry Talent Pool |
| MENTORSHIPRECOMMENDATIONNETWORK | Mentorship Recommendation Network |
| PAYBENCHMARKING | Pay Benchmarking |
| PROFESSIONALREPUTATIONLEDGER | Professional Reputation Ledger |
| REFERENCEEXCHANGE | Reference Exchange |
| REHIRERECOMMENDATIONS | Rehire Recommendations |
| RESTRICTEDINTEGRITYREGISTRY | Restricted Integrity Registry |
| REVIEWBOARD | Review Board |
| SECTORMOBILITYINTELLIGENCE | Sector Mobility Intelligence |
| SECTORTALENTTRENDS | Sector Talent Trends |
| SKILLSGAPHEATMAP | Skills Gap Heatmap |
| TALENTDATAFOUNDATION | Talent Data Foundation |
| TALENTDEVELOPMENTNETWORK | Talent Development Network |
| TALENTSUPPLYDEMANDFORECASTING | Talent Supply Demand Forecasting |
| TRUSTLEVELS | Trust Levels |
| VERIFIEDCERTIFICATIONREGISTRY | Verified Certification Registry |
| VERIFIEDPARTICIPANTS | Verified Participants |
| WORKFORCEANALYTICS | Workforce Analytics |
| SUPPLIERS | Suppliers |
| SOURCING | Sourcing |
| REQUISITIONS | Requisitions |
| PURCHASEORDERS | Purchase Orders |
| INVOICEMATCH | Invoice Capture &amp; 3-Way Match |
| CONTRACTS | Contracting &amp; Clause Library |
| GRN | Receiving (GRN) |

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
