# PLAN — HR (HCM/TEP) Module Catalog nav + entitlement wiring (gap #4)

> **Control Tower kaydı (SoR).** Branch: `fix/hr-integration-gaps` (origin/main @ 01035898). HR merge (PR #109 HCM UI 23 + #110 TEP UI 31) additive port'landı; **nav wiring kasıtlı yapılmadı** → 54+1 sayfa menüde YOK (yalnız URL). Kaynak: `docs/records/audits/2026-09/auth-legal-entities-claim-port-deferred-2026-09-14.md` §Frontend(a). Owner: Ali; bu iş = o gap'i kapatma.

## Mekanizma (MOD-0285 data-driven nav) — 4 kapı
`GET /api/platform/navigation/menu` → `GetTenantNavigationMenuQueryHandler`:
1. **Catalog** — modül `platform_module_catalog`'ta (assignable).
2. **Entitlement** — `_accessService.HasAccessAsync(tenant, moduleCode)` → `tenant_module_entitlements`.
3. **Page descriptors** — `platform_module_page_descriptors` (route+label+izin); descriptor'ı olmayan modül menüden düşer.
4. **Permission** — kullanıcı rolü sayfa iznini taşır (RBAC).
**Dolum:** her modülün `IModuleManifestProvider`'ı (kod) → açılışta `PlatformModuleSelfRegistrationWorker` `GetManifest()` → `RegisterModuleManifestCommand` (platform-scope Guid.Empty) → catalog+page+action descriptor; izinler `PlatformPermissionAutoRegistrationWorker`. 17 provider var (CrmManifestProvider…), **HR YOK**.

## Ölçülen durum (HR = her yerde 0)
platform_module_catalog HR=0 · page_descriptors HR=0 · action_descriptors HR=0 · tenant_module_entitlements 97c5 HR=0. Sayfalar Diten.Web'de VAR (URL-erişilir).

## Manifest imzası (mirror: CrmManifestProvider)
`ModuleManifestDocument(ModuleCode, ModuleName, DisplayName, Domain, Service, ModuleVersion, IsTenantAssignable, SortOrder, Icon, IsBaseline, Pages:[ ModuleManifestPage(code, label, route, readPerm, null, true, "List", order, [ModuleManifestAction(...)]) ])`. DI: `services.AddSingleton<IModuleManifestProvider, XxxProvider>()` (Diten.Platform.Application/DependencyInjection.cs ~426-439).

## 55 HR modülü — route enumerasyonu (kaynak: web controller Route'ları)
**HumanCapital/ (23):** ApplicantIntake, CandidatePipeline, CompensationBenefits, CompetencySkills, DevelopmentPlans, EmployeeOnboarding, EmployeeProjections, EmploymentChanges, HeadcountBudget, HrCaseManagement, HrCompliance, HrDocumentation, HrKpiAnalytics, PositionAssignments (HumanCapitalPositionAssignmentsController), LearningTraining, OffboardingCases, OfferManagement, PerformanceReviews, SelfService, SensitiveAccess, Succession, TimeAttendanceLeave, WorkforcePlanning.
**TalentEcosystem/ (31):** AssociationMemberships, AssociationOperations, CandidateCareerPassport, CandidateDisputes, CandidateProfiles, ConsentVisibilityPolicies, EarlyWarningSignals, ExitReferenceRecords, HiringRiskIndicators, IndustryKnowledgeNetwork, IndustrySkillPassport, IndustrySuccessionPool, IndustryTalentPool, MentorshipRecommendationNetwork, PayBenchmarking, ProfessionalReputationLedger, ReferenceExchange, RehireRecommendations, RestrictedIntegrityRegistry, ReviewBoard, SectorMobilityIntelligence, SectorTalentTrends, SkillsGapHeatmap, TalentDataFoundation, TalentDevelopmentNetwork, TalentSupplyDemandForecasting, TepShellMetadata, TrustLevels, VerifiedCertificationRegistry, VerifiedParticipants, WorkforceAnalytics.
**HCM/ (1):** Employees (employee master, MOD-0251; perm mod0251.employee.view — HcmService 5060).
> Her sayfanın **readPerm**'i web controller'ın izin sabiti + backend servis `[HasPermission]`'dan türetilir (ör. hcm.employee-projections.read, hcm.sensitive-access.read, mod0251.employee.view). Yazma aksiyonları → ModuleManifestAction.

## WP kırılımı
- **WP-A (backend, Platform):** `HumanCapitalManifestProvider` (23 + HCM/Employees) — CrmManifestProvider aynası; her route bir ModuleManifestPage; readPerm controller/backend'den; DI kaydı. Self-registration → catalog+page+action+permission.
- **WP-B (backend, Platform):** `TalentEcosystemManifestProvider` (31) — aynı desen.
- **WP-C (ops/CT):** 97c5 tenant'ı HR modüllerine entitle (tenant_module_entitlements) + HR izinlerini tenant Admin rolüne grant (RBAC).
- **WP-D (frontend, follow-up):** Nav.Page.* 7-dil (HR yalnız en+tr → +5) — gap #5.
- Doğrulama (her WP): Platform build + restart → `GET /api/platform/navigation/menu` HR modülleri görünür (entitlement+grant sonrası) + izole worktree Platform test baseline-diff.

## Sıra
WP-A + WP-B (paralel olabilir, farklı dosyalar + DI'de 2 satır) → WP-C (entitlement+grant) → menüde görünür → WP-D (L10n). PR → main (merge=Ali). Sonra SCMM'e sync (ayrı iş).
