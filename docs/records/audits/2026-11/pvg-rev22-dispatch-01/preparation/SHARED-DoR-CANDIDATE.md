@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-0230-DL7-CI-SHARED-01 · Prompt PVG-WP-0230-DL7-CI-SHARED-01-P v1.0
Repository: C:/Users/epenc/.codex/worktrees/pvg-ct-handoff/ERP-vNext-git · Branch: feature/pvg/mod-0230-r22-shared-ci · Expected HEAD: 7265acb09b61ac819f1d3d1e3c8fb4110ac31cbe · Worktree: C:/Users/epenc/.codex/worktrees/pvg-r22-shared/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: sözleşme, uyum, modüller-arası-yazma
Görev sınıfı: ürün · Paket: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
İzinli yollar: frontend/Diten.Web/Resources/PageHelpResource.en.resx, frontend/Diten.Web/Resources/SharedResource.en.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.en.resx, frontend/Diten.Web/Resources/PageHelpResource.tr.resx, frontend/Diten.Web/Resources/SharedResource.tr.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.tr.resx, frontend/Diten.Web/Resources/PageHelpResource.fr.resx, frontend/Diten.Web/Resources/SharedResource.fr.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.fr.resx, frontend/Diten.Web/Resources/PageHelpResource.es.resx, frontend/Diten.Web/Resources/SharedResource.es.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.es.resx, frontend/Diten.Web/Resources/PageHelpResource.zh.resx, frontend/Diten.Web/Resources/SharedResource.zh.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.zh.resx, frontend/Diten.Web/Resources/PageHelpResource.ar.resx, frontend/Diten.Web/Resources/SharedResource.ar.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.ar.resx, frontend/Diten.Web/Resources/PageHelpResource.ru.resx, frontend/Diten.Web/Resources/SharedResource.ru.resx, frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.ru.resx, frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml, frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js
Korunan yollar: .antigravity/**, AGENTS.md, execution/**, docs/**, services/**, gateway/**, tests/**, .github/**, frontend/Diten.Web.Tests/**, frontend/Diten.Web/tests/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/_ViewStart.cshtml, frontend/Diten.Web/*.csproj, frontend/Diten.Web/package*.json, frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**, frontend/Diten.Web/Resources/Views/Pharmacovigilance/**, frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/**
Kapsanan dilim: DL-7, DL-6, DL-11
ADAY DURUMU: PLANNED hazırlık metni. Formal onay/DoR/finalsource/promptreview/normalgate/GO henüz bu aday için verilmedi; çalıştırma emri değildir.
Model · Effort (istenen): inherited gpt-6.2-astra · high; gerçek model/effort ölçülemiyorsa verilemiyor yaz.
Golden-Flow Profile: A — dar UI readonly interaction; mutation0 / persistence unchanged; mevcut RBAC/tenant/audit gate değişmez.
Agent Lane ID: pvg_r22_shared_writer
Area / Module: Pharmacovigilance / CaseIntakeTriage; ortak tüketici Governance / RoleAssignments, mevcut MOD-0018-FU9 point-XMC.
Golden / Shell / Layout: PVG compact/25userfields/data_mode=server/_LayoutTenantShell; mevcut4page/form düzeni. RoleAssignments FU9 mevcut UI yapısı değişmez; yeni form/table/layout feature yok.
Kolon / filtre: PVG mevcut responsive-control/IntakeDraftId/Status/Actions; mevcut plain status select +Apply/Reset. Bu iş filtre/Select2/table refactor yapmaz; RoleAssignments role/module/search/grid/checkbox UI aynıdır.
L10n seti: shared154new+1French canonicalcontent; views hiçbir resourcekey yazmaz, yalnız dört normalizePageCode.
Actor / interaction: synthetic tenant_user fixture; PVG page own read/create/update claim veya RoleAssignments auth.roles.assign-permission readonly-render; gerçek rolegrant ve mutation yok.
Persistence: yeni katman/depo/veri/side effect yok. Own fixture state/mutation requests ölçülür, canlı DB sonucu varsayılmaz.
Authority pins: Pack LF d20813b2a4290bc299d29c4c72fbb7c64c095908294213b820533e6fafeafd0b; domain LF 0aa1706ebd4fd1d434b7b9fec78e8c8043c7340076f3d935544dd4853d5f2298; owner canonical Git/LF 0fa566c24df9a3d0910e1ebd1b4febc68711d92ca22e64dbc754535fe217e0d7; canonical scope c1238043f7bced900b9b59224ada56e5cc5c9776a2be9c3a5c46077038d477ed; canonical content 875b6a025a64bac11cb923092015858d5e6b7615b2d684a7172ffb2327b405e4. Owner-reviewed raw scope a386b1bf... /content5157d2ec... SOURCE-MANIFEST ile canonical LF değerlerine kayıpsız aynı-JSON bağlıdır; raw/canonical ayrıdır.
Formal approval evidence: docs/records/audits/2026-11/pvg-rev22-formal-approval-01/APPROVAL-CHECK.txt@7265acb09b61ac819f1d3d1e3c8fb4110ac31cbe; source-pack-sha256: son kaynak dondurulduktan sonra normal pack aracı ve ayrı kayıtla bağlanır.
Final Expected HEAD, exact pack/domain formal review+approval rows/records ve READY/DoR kaynaklarını İÇERMELİ; yalnız root current row/commit kaydı yeterli değildir.
Preparation tuple history: managedworktrees creationDETACHED6571; sonra CT yukarıdaki namedbranchleri6571clean hazırladı. Finalsourceadvance ve clean/HEAD ölçümü CT yapar; eski tuple finalkaynak sayılmaz.
Task Source Row (exact text): WP-0230-DL7-CI-SHARED-01	MOD-0230 DL-7, DL-6, DL-11 — rev22 exact23shared help/nav/RoleAssignments labels; one writer; no auth/operational change	ortak:MOD-0230,MOD-0018-FU9	PLANNED	PVG CT	WP-0230-DL11,WP-0230-DL12-FE	-	2026-11-23	docs/records/audits/2026-11/pvg-rev22-dispatch-01/STATE-JOURNAL.md	-
Task-row snapshot CURRENT PLANNED at3cb; final ordinary prompt gerçek READY satırıyla yeniden bağlanır. Başladı/DISPATCHED durumu uydurulmaz.
Kaynak/alinti hazırlıkH: 3cb5131f0d89a3a2219c1fe594f868e2758aa794. Ürün consumerları değişmedi; finalHeader/DoR/onay kaynakları tamamlanınca alıntılar finalExpectedH üzerinde tekrar bağlanır.

Özel ortam: DOTNET_ROOT=C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8; DOTNET_CLI_HOME=C:/Users/epenc/AppData/Local/Temp/pvg-r22-shared-env-9wiogggg/dotnet-home; NUGET_PACKAGES=C:/Users/epenc/AppData/Local/Temp/pvg-r22-shared-env-9wiogggg/nuget; TEMP/TMP=C:/Users/epenc/AppData/Local/Temp/pvg-r22-shared-env-9wiogggg/tmp; evidence=C:/Users/epenc/AppData/Local/Temp/pvg-r22-shared-env-9wiogggg/evidence. Node_modules CT tarafından manifest53d0e78f ile3481dosya/48437254bayt hash doğrulanıp bu worktree'ye yalnız localcopy olarak hazırlandı; kurulum/indirme/hook yok. Private NuGet cache başta boş; normal onaylı scoped restore yalnız hash-doğrulanmış offlinefeed'den doldurabilir.

Önce oku:
1. AGENTS.md
2. execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
3. execution/domains/pharmacovigilance/domain-config.md
4. docs/records/decisions/2026-11/pvg-mod0230-rev22-owner-approval-01.md
5. docs/records/reviews/MOD-0230/2026-11-23-rev22-scope-01/scope.json
6. docs/records/reviews/MOD-0230/2026-11-23-rev22-scope-01/localized-content.json
7. docs/records/reviews/MOD-0230/2026-11-23-rev22-scope-01/SOURCE-MANIFEST.json
8. execution/domains/platform-shared-services/module-packs/MOD-0018-FU9-platform-governance-rbac-admin-ui.md
9. execution/portfolio/delivery-capability-packs/DCP-001-access-governance.md
10. execution/registries/module-implementation-status.md
11. docs/roadmap/plans/pvg-mod0230-rev22-parallel-delivery-plan-2026-11-23.md
12. docs/records/audits/2026-11/pvg-rev22-discovery-01/DISCOVERY.txt
13. docs/records/audits/2026-11/pvg-rev22-discovery-01/DECISIONS.md
14. docs/records/audits/2026-11/pvg-rev22-dispatch-01/STATE-JOURNAL.md
15. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/MANAGED-WORKTREE-BASELINE.json
16. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/WORKTREE-DOR-CANDIDATE.txt
17. execution/approvals/approvals.tsv
18. execution/approvals/reviews.tsv
19. .antigravity/agents/frontend-ui-ux.md
20. .antigravity/rules/multi-tenancy.md
21. .antigravity/rules/security-jwt.md
22. .antigravity/rules/git-safety.md
23. .antigravity/rules/code-style.md
24. .antigravity/rules/docs-organization.md
25. .antigravity/rules/cross-module-change-consent.md
26. .antigravity/rules/frontend-standards.md
27. .antigravity/rules/views-organization.md
28. .antigravity/rules/unauthorized-surface-standard.md
29. .antigravity/rules/frontend-js-standard.md
30. .antigravity/rules/frontend-datatable-template.md
31. .antigravity/rules/page-help-and-keyboard.md
32. .antigravity/rules/localization-standard.md
33. .antigravity/rules/dynamic-localization-standard.md
34. .antigravity/rules/permission-key-standard.md
35. .antigravity/rules/status-reporting-and-evidence.md
36. .antigravity/rules/configuration-safety.md
37. .antigravity/rules/dev-runbook.md
38. frontend/Diten.Web/Services/Navigation/CurrentPage.cs
39. frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs
40. frontend/Diten.Web/Services/Help/PageHelpCatalog.cs
41. frontend/Diten.Web/Views/Shared/_PageHelpPanel.cshtml
42. frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml
43. services/Diten.PvgService/src/Diten.PvgService.Api/ModuleRegistration/PvgCaseIntakeTriageManifestProvider.cs
44. services/Diten.Platform/src/Diten.Platform.Application/Features/Navigation/Handlers/GetTenantNavigationMenuQueryHandler.cs
45. frontend/Diten.Web.Tests/Controllers/LayoutRenderer.cs
46. frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs
47. services/Diten.PvgService/src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs
48. frontend/Diten.Web/tests/page-help-ledger.test.js
49. frontend/Diten.Web/tests/page-help-rule-contract.test.js
50. frontend/Diten.Web/tests/role-assignments-label-ledger.test.js
51. frontend/Diten.Web/tests/role-assignments-perm-label.test.js
52. frontend/Diten.Web.Tests/Navigation/CurrentPageTests.cs
53. frontend/Diten.Web.Tests/Controllers/PageHelpShellTests.cs
54. frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs
55. frontend/Diten.Web.Tests/Services/PageHelpCatalogTests.cs
56. frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs
57. frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
58. frontend/Diten.Web/package.json
59. frontend/Diten.Web/package-lock.json
60. frontend/Diten.Web/Resources/PageHelpResource.en.resx
61. frontend/Diten.Web/Resources/SharedResource.en.resx
62. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.en.resx
63. frontend/Diten.Web/Resources/PageHelpResource.tr.resx
64. frontend/Diten.Web/Resources/SharedResource.tr.resx
65. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.tr.resx
66. frontend/Diten.Web/Resources/PageHelpResource.fr.resx
67. frontend/Diten.Web/Resources/SharedResource.fr.resx
68. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.fr.resx
69. frontend/Diten.Web/Resources/PageHelpResource.es.resx
70. frontend/Diten.Web/Resources/SharedResource.es.resx
71. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.es.resx
72. frontend/Diten.Web/Resources/PageHelpResource.zh.resx
73. frontend/Diten.Web/Resources/SharedResource.zh.resx
74. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.zh.resx
75. frontend/Diten.Web/Resources/PageHelpResource.ar.resx
76. frontend/Diten.Web/Resources/SharedResource.ar.resx
77. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.ar.resx
78. frontend/Diten.Web/Resources/PageHelpResource.ru.resx
79. frontend/Diten.Web/Resources/SharedResource.ru.resx
80. frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.ru.resx
81. frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml
82. frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js
83. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_DataTable.cshtml
84. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_Filter.cshtml
85. docs/records/audits/2026-11/pvg-rev22-formal-approval-01/APPROVAL-CHECK.txt
86. docs/records/audits/2026-11/pvg-rev22-dispatch-01/READY.md
87. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/DEPENDENCY-MATERIALIZATION.json
88. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/RENDER-FEASIBILITY.txt
89. docs/records/audits/2026-11/pvg-rev22-dispatch-01/NAV-TITLE-PARITY.json
90. docs/records/audits/2026-11/pvg-rev22-dispatch-01/TOOLS.json
91. docs/records/reviews/MOD-0230/2026-11-23-rev22-formal-dialogue-02.md
92. docs/records/reviews/MOD-0230/2026-11-23-rev22-formal-blind-02.md
93. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.en.resx
94. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.tr.resx
95. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.fr.resx
96. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.es.resx
97. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.zh.resx
98. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.ar.resx
99. frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.ru.resx

Sözleşme yüzeyleri:
- API: «endpoints.MapGet(RoutePrefix, ListDraftsAsync).WithName("PvgCaseIntakeTriageListDrafts").RequireAuthorization();» (services/Diten.PvgService/src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs:29)
- İzin: «PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);» (frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:250)
- Test: «expect(unsaidVerbs).toEqual([]);» (frontend/Diten.Web/tests/role-assignments-label-ledger.test.js:362)
- Sabotaj: «- A critical new protection must have independent negative evidence in an external private scratch copy only:» (execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:254)

Alıntı açıklamaları: mevcut API/page boundary; görev controller/endpoint davranışı yazmaz.; gerçek permission claim gate aynı; yeni grant/adapter yok.; unchanged producer/consumer/test kaynağı ölçülür.; yalnız ownscope privatecopy, gerçek unchangedguard/render assertionRED→restoreGREEN.

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/pvg-rev22-discovery-01/DISCOVERY.txt@6571d2cbbe8e6bd7db99ed420f05f381f9df51d9 · sha256 5e17d72f0696e918089225db0d565364a82f44b7abb302eb44ae4ff511e94fff

Keşif kararı 1: parent — Blueprint mevcut MOD-0230 Case Intake & Triage kimliğini verir; yeni MOD/FU/capability yok.
Keşif kararı 2: parent — Registry aynı kanonik kimliği doğrular; yeni kayıt yok.
Keşif kararı 3: parent — Mevcut tek pack kullanılır;479ref eski/kopya bloblar ayrı iş izni değildir. Rev22 exact formal/HIGH gate beklenir.
Keşif kararı 4: yeni — Yalnız ad benzerliği; HR5063 ayrı domain, bu PVG kapsamından dışlanır, yeni HR işi açılmaz.
Keşif kararı 5: parent — Mevcut PVG5011 route ailesi korunur; gateway/rota değişikliği yok.
Keşif kararı 6: parent — PVG-M1-01 mevcut INSPECTION_REQUIRED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 7: parent — PVG-M1-02 mevcut INSPECTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 8: parent — PVG-M1-03 mevcut INSPECTION_REQUIRED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Operasyonel kullanım ayrı kapalı karardır; rev22 onu açmaz.
Keşif kararı 9: child — WP-0230-DL2A mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 10: child — WP-0230-DL2B mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 11: child — WP-0230-DL3 mevcut SUPERSEDED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Eski red/superseded kaydı silinmez veya yeniden izin sayılmaz; mevcut ardıl teslim korunur.
Keşif kararı 12: child — WP-0230-DL8 mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 13: child — WP-0230-DL3A mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 14: child — WP-0230-DL3B mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 15: child — WP-0230-DL4A mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 16: child — WP-0230-DL3C mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 17: child — WP-0230-DL6 mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 18: child — WP-0230-DL3D mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 19: child — WP-0230-DL4B mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 20: child — WP-0230-DL7A mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 21: child — WP-0230-DL4D mevcut ACCEPTED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 22: child — WP-0230-DL7B mevcut REWORK kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Eski red/superseded kaydı silinmez veya yeniden izin sayılmaz; mevcut ardıl teslim korunur.
Keşif kararı 23: child — WP-0230-DL7B-R1 mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 24: child — WP-0230-DL7C mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 25: child — WP-0230-DL10 mevcut REWORK kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Eski red/superseded kaydı silinmez veya yeniden izin sayılmaz; mevcut ardıl teslim korunur.
Keşif kararı 26: child — WP-0230-DL10-R1 mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 27: child — WP-0230-DL11 mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 28: child — WP-0230-DL4E mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 29: parent — WP-0230-DL12 mevcut IN_PROGRESS kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Parent koordinasyon IN_PROGRESS; BE/L10N/FE entegre, son kaynak CI/insan merge/kabul farklı kapılardır.
Keşif kararı 30: child — WP-0230-DL12-PREP-WIN mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 31: child — WP-0230-DL12-PREP-WEB mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 32: child — WP-0230-DL12-BE mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 33: child — WP-0230-DL12-L10N mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.
Keşif kararı 34: child — WP-0230-DL12-FE mevcut INTEGRATED kaydıdır; mevcut baseline ve tarihî durum korunur, yeniden dağıtılmaz. Yeni iş yalnız onaylı23ortak+4PVG dosya rev22 eki; eski teslim kapsamı/kanıt/kabulü genişlemez.

NE: MOD-0230 rev22 AC46/47/48/49 shared eki; sadece23listedeki dosya,154yeni localized değer+bir mevcut French değer,2ActionVerbs map satırı+2existing family word. Rol pvg_r22_shared_writer; dört PVGview yazılmaz.

NEDEN: Gerçek H82 CI'da web-js-tests5565case/3assertionRED: dört PVGhelpcode ve route/triage sözcükleri eksik. UI CI14 tek yeniFrench domain-English-equal RED. Actual PermLabel iki fiilde family bulmuyor. Bağımsız rawCIRED önceki durum kanıtıdır, onu test kopyası/uydurma RED ile değiştirme. Hidden-view PageCode başka worker'ın sınırıdır.

NASIL:
- Bu metin ADAY iken ürün işi/restore/build/test/commit yok. Yalnız son prompt/sourcepack, yazar-dışı HIGH inceleme, gerçek normal dispatch0 ve CT'nin açık GO mesajı sonrası başla. İlk olarak final branch/HEAD/clean, pinler, sourcepack/prompt/review/gate footer ve tam allowlist'i ölç; uyumsuzsa yazmadan dur. Final alanlarını CT bağlar.
- Bu rolün tek yazıcısısın; alt ajan veya sohbet açma. İki scope ayrıdır, max2 aktif çocuk; root ürün kodu yazmaz. Diğer worker'ın dosyalarını/test sonucunu kendi teslimine katma. Overlap veya beklenmeyen dirty dosya varsa CT'ye inventory ver; başka worktree/WIP'ye cleanup/stash/archive/silme yok.
- Kendi benzersiz dış evidence kökünde raw log/JSON/TRX/özel scratch/harness sakla. Başlangıç/final HEAD/tree/branch/status, bütün izinli dosyaların raw-disk SHA ve canonical-Git blob/SHA'sı, değişen key/statement manifesti ayrı tutulur. CRLF/LF hash'leri karıştırılmaz; root queue/index/ref/pack/domain/owner/review kaynakları yazılmaz.
- Tek ortam preflight: CT'nin ölçtüğü portable SDK8.0.428 ve Node22 kullan; gerçek executable --version/paths kaydet. Süreç içi DOTNET_ROOT/PATH; kendi DOTNET_CLI_HOME/TEMP/TMP, NUGET_PACKAGES private cache ve ölçülmüş offline feed. Global config/feed/cert/SDK/lock/proje değişikliği, install/download/skip yok. Root'ta global.json yoksa ekleme. Mevcut lock-bound node_modules/jsdom/Vitest2.1.9 yoksa somut ortam engeliyle dur.
- Gerekirse yalnız WebTests için bir dar restore: & "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8/dotnet.exe" restore frontend/Diten.Web.Tests/Diten.Web.Tests.csproj --packages "$env:NUGET_PACKAGES" --source "C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/nuget" --disable-parallel. Sonraki test --no-restore. NuGet kaynağı/cache önceden ölçülmeden bu aday komut çalıştırılmaz; npm install/npm ci yok.
- Profile A zinciri: synthetic tenant/permission fixture → salt okunur UI/help/etiket interaction → mevcut permission gate → mutation/persistence yok → gerçek render/reload sonucu → kanıt. Create/save/triage/route/grant/DELETE/POST mutasyonu yok. Canlı5000/5001/5011/27017, gerçek hasta/kiracı verisi, dotnet run/run_all veya yeni operasyonel Web host yok. Mevcut in-memory test rendering seam/gerçek rebuilt consumer kullanılabilir; production auth/field/audit adapter/DI değişmez. Mutasyon istek sayısı0, varsa test-local persisted state değişmez; ölçülmeyen canlıDB snapshot sonucu uydurulmaz.
- Harici harness, işçinin incelenebilir EVIDENCE ÇIKTISIDIR; bu promptta önceden varmış dosya/argv iddiası yoktur. Gerçek production types/views/bridge/consumer'ı çalıştırır; kendi Resolve/permission/localization kural kopyası olmaz. Worker somut source/entrypoint/exact argv/env/SHA/compile/exit/result/cleanup oluşturmadan başarı diyemez. Repo test/harness/proje kaynağı değişmez. Routine evidence temsilini işçi seçer; yeni repo write/guard/CI/scope ihtiyacında durur.
- Canonical content.json'u birebir uygula:7PageHelpResource×16key=112 (dörtCODE için Purpose/Audience/Step1Title/Step1);7SharedResource×4Nav.Page=28;7RoleAssignmentsIndex×2ActionVerb=14. en/tr/fr/es/zh/ar/ru tam. 28Nav.Page mevcut marker RESX PageTitle/CreateTitle/EditTitle/DetailTitle değerlerini aynen reuse eder; o7PVGmarkerresx dosyasını YAZMA. TamXML karşılaştırmasıyla doğrula; title benzerliği varsayma.
- Yalnız SharedResource.fr.resx mevcut Nav.Domain.PHARMACOVIGILANCE Pharmacovigilance→Pharmacovigilance (surveillance des médicaments). Diğer mevcut değerler/diğer6dil domain/7moduleNav değişmez. Yeni key/değer/çeviri tercihi yok; XML entity güvenliği, boş/Englishcopy0. Native sertifika veya klinik öneri iddia edilmez.
- Mevcut RoleAssignments ActionVerbs map'ine yalnız triage→Localizer["ActionVerb_Triage"].Value ve route→Localizer["ActionVerb_Route"].Value. Existing bridge mekanizmasını dönüştürme. perm-label.js'de triage yalnız existing success/bx-check-circle (decide) row'una, route yalnız dark/bx-user-check (assign/delegate) row'una. Yeni family/iconexception/parser/fallback yok; checkbox/grant/key/RBAC/group davranışı değişmez. FU9/platform-team ownership sadece owner-record point-XMC istisnasıyla bağlı, devrolmaz.
- Actual21XML ilk/son eski key/value map'ini Git baseline ile ölç: tam154unique addition +tam1French replacement; duplicate/removed/empty/unapprovedchange0. Canonical content ile tüm değerler eşit. Report-only resx-check'in inheritedFAIL'ini gizleme veya selectorPASS yerine koyma.
- Gerçek rebuilt RoleAssignments bridge partial/payload +actual production PermLabel.split/normalizeAction/resolveFamily üzerinden7culture'de iki localized verb ve doğru adopted family ölçülür. Kendi dictionary/uydurma L10n JSON/regex kopyası başarı değildir. Harici harness bu gerçek producer/consumer yolunu kullanır; herculture source/argv/hash/receipt ve mutation0 gereklidir. Dört view'ın7langhelp success'i isolated shared teslimine yüklenmez.
- Dış private repo-shaped kopyada AYRI tek mutantlar: bir requiredHelp.Purpose değerini kaldır; route bridge map'i kaldır; triage family word'ü kaldır; route family word'ü kaldır. İlgili değişmemiş gerçekguard assertionRED verir; restore candidate aynı hedefteGREEN. Test/guard/assertion değişmez; syntax/build/infrastructurefailure RED değil. Herpatch/source/harness/argv/hash/actualcase/exit ve cleanup saklı; mutantcommit yok.
- Teslim öncesi yalnız kendi açık yollarını stage et; git diff --cached --name-only, TAM git diff --cached ve git diff --check incelemesini kaydet. git add . /-A, force/reset/clean/amend/checkpoint, branch değişimi/merge, push/PR/mainmerge/approval/queue yazımı yok. Sahibin izin verdiği ordinary worker-local commit yalnız bu dal ve dosyalardır; yeni onay sorulmaz. Gerçek commit/tree/clean + evidence ile CT'ye teslim edip DUR.
- CT ayrı exact-commit E2/scope incelemesi sonrası no-ff birleştirir. Tüm4viewlocalizedhelp7dil/noPVGmenu ve RoleAssignments consumer zinciri iki teslim birleşince INT yükümlülüğüdür. E2 INTEGRATED, E4/E5 ACCEPTED veya operasyon izni değildir. Tek PR#460; yeni finalH bütün requiredCI/Phase1/ui-compliance/fullJS ve AC45 pvg-api ubuntu22.04 samejob target/fullTRX/commit-tree-checkout-event-runattempt bağıyla CT'de kapanır. Eski H82 sonuçları veya aynı-source Phase1retryGREEN root-cause repair/sonH PASS sayılmaz.

YAPMA: Exact23path/key/statement dışı write yok; dörtPVGview, API/controller/auth/permissiongrant/catalog/menu/manifest/store/audit/schema/routes/data, CI/test/guard/ledger/baseline/knownfail, proje/lock/dependency, pack/domain/owner/queue/review/CTproducer korunur. Eski FE6 producer bu27scope'u ölçmez ve genişletilemez. Yeni runtime adapter/operasyonel host, skip/countdrop/waiver ve başka worker düzenlemesi yok.

DOĞRULA:
1. Normal0+GO/final tuple/pins/clean ve baş/son23path raw/canonicalmanifest; hazırlık6571snapshot finalExpectedH yerine kullanılamaz. XML154+1/nootherchange ve realbridge/PermLabel7culture E2 yukarıdaki gibi.
2. frontend/Diten.Web cwd, tek targeted normal: & "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/node22/node-v22.23.5-win-x64/node.exe" node_modules/vitest/vitest.mjs run tests/page-help-ledger.test.js tests/page-help-rule-contract.test.js tests/role-assignments-label-ledger.test.js tests/role-assignments-perm-label.test.js --reporter=json --outputFile="$evidence/shared-targeted.json" --no-cache --maxWorkers=1 --minWorkers=1. Gerçek0fail/0skip/caseIDs/outputhash; count tek başına davranış kanıtı değil.
3. Her özel mutantta yalnız ilgili mevcuttestfile aynı Node komutuyla: Help için page-help-ledger; map/family için role-assignments-label-ledger ve gerektiğinde role-assignments-perm-label. TamJS mutant başına tekrarlanmaz. Dört gerçek tek-mutant RED→restoreGREEN, prodguard/test source aynı.
4. Bütün scoped ölçümlerden sonra FULLJS writer finalinde BİR KEZ: aynıcwd & "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/node22/node-v22.23.5-win-x64/node.exe" node_modules/vitest/vitest.mjs run --reporter=json --outputFile="$evidence/shared-full-js.json" --no-cache --maxWorkers=1 --minWorkers=1. Completecase multiset/0fail0skip; H82CI5565farkı varsa açıkla, testcase silme yok. Ek fullrun yalnız yeni source/fail/somut unresolvedconcern ile gerekçeli. FinalrequiredCI CT'nin birleşik yeniH'sinde ayrı bir kapanış.
5. Tamdiff/stagedreview/localcommit ve sadece ownPID/testhost/port/temp/cache/scratchcleanup kanıtı. Evidence korunur; başka task/process, devport veya recursive uncheckedpath silme yok. CT ayrı bağımsızE2/noff/combinedINT/AC45 bekler.

Durma koşulları: ADAY/finalHEAD/formalrecord/normal0/GO eksik; tuplehash/clean/overlap;23scope/154+1dışı ihtiyaç; restore/runtime/compile veya yeni fail/skip/countdrop; copiedlabelmap/fakepayload; mutantGREEN/compilefailure; productionmutation/PII/cleanup/sourceboundevidence belirsiz. Somut kanıtla CT'ye dön; guard/scope genişletme. Adopted content/family için rutin yeniden onay döngüsü açma.

Rapor formatı: exactWP/role/modelrequested+actualverilemiyor; begin/finalH/tree/branch/worktree/clean;23pathfull/stageddiff/rawGitSHA;21XML154+1/nootherchange;7culture gerçekbridge/PermLabel/family receipts;targeted/fullJS argv/case/exit/hash;four separateprivate assertionRED/restoredGREEN;ProfileA mutation0/persist ölçüm sınırı;owncleanup/localcommit;excluded4views ve combined7langINT/finalCI/AC45/live kapıları. KendiE2'ni CTVERIFIED/INTEGRATED/ACCEPTED/mainmerge/operasyon sayma.
