@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-0230-DL7-CI-VIEWS-01 · Prompt PVG-WP-0230-DL7-CI-VIEWS-01-P v1.1
Repository: C:/Users/epenc/.codex/worktrees/pvg-ct-handoff/ERP-vNext-git · Branch: feature/pvg/mod-0230-r22-page-help · Expected HEAD: b0dcca109997320e55659d7f41c489396338e15c · Worktree: C:/Users/epenc/.codex/worktrees/pvg-r22-views/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: sözleşme, kiracı, uyum
Görev sınıfı: ürün · Paket: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
İzinli yollar: frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml, frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml, frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml, frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml
Korunan yollar: .antigravity/**, AGENTS.md, execution/**, docs/**, services/**, gateway/**, tests/**, .github/**, frontend/Diten.Web.Tests/**, frontend/Diten.Web/tests/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/_ViewStart.cshtml, frontend/Diten.Web/*.csproj, frontend/Diten.Web/package*.json, frontend/Diten.Web/Resources/**, frontend/Diten.Web/Views/Governance/**, frontend/Diten.Web/wwwroot/assets/js/**
Kapsanan dilim: DL-7
Dağıtım sınırı: READY kayıtlı bu kesin prompt, kendi ayrı HIGH inceleme ve gerçek normalgate0 + CT GO olmadan ürün/build/test başlatmaz; READY başladı demek değildir.
Model · Effort (istenen): inherited gpt-6.2-astra · high; gerçek model/effort ölçülemiyorsa verilemiyor yaz.
Golden-Flow Profile: A — dar UI readonly interaction; mutation0 / persistence unchanged; mevcut RBAC/tenant/audit gate değişmez.
Agent Lane ID: pvg_r22_views_writer
Area / Module: Pharmacovigilance / CaseIntakeTriage; ortak tüketici Governance / RoleAssignments, mevcut MOD-0018-FU9 point-XMC.
Golden / Shell / Layout: PVG compact/25userfields/data_mode=server/_LayoutTenantShell; mevcut4page/form düzeni. RoleAssignments FU9 mevcut UI yapısı değişmez; yeni form/table/layout feature yok.
Kolon / filtre: PVG mevcut responsive-control/IntakeDraftId/Status/Actions; mevcut plain status select +Apply/Reset. Bu iş filtre/Select2/table refactor yapmaz; RoleAssignments role/module/search/grid/checkbox UI aynıdır.
L10n seti: shared154new+1French canonicalcontent; views hiçbir resourcekey yazmaz, yalnız dört normalizePageCode.
Actor / interaction: synthetic tenant_user fixture; PVG page own read/create/update claim veya RoleAssignments auth.roles.assign-permission readonly-render; gerçek rolegrant ve mutation yok.
Persistence: yeni katman/depo/veri/side effect yok. Own fixture state/mutation requests ölçülür, canlı DB sonucu varsayılmaz.
Authority pins: Pack LF d20813b2a4290bc299d29c4c72fbb7c64c095908294213b820533e6fafeafd0b; domain LF 0aa1706ebd4fd1d434b7b9fec78e8c8043c7340076f3d935544dd4853d5f2298; owner canonical Git/LF 0fa566c24df9a3d0910e1ebd1b4febc68711d92ca22e64dbc754535fe217e0d7; canonical scope c1238043f7bced900b9b59224ada56e5cc5c9776a2be9c3a5c46077038d477ed; canonical content 875b6a025a64bac11cb923092015858d5e6b7615b2d684a7172ffb2327b405e4. Owner-reviewed raw scope a386b1bf... /content5157d2ec... SOURCE-MANIFEST ile canonical LF değerlerine kayıpsız aynı-JSON bağlıdır; raw/canonical ayrıdır.
Formal approval evidence: docs/records/audits/2026-11/pvg-rev22-formal-approval-01/APPROVAL-CHECK.txt@7265acb09b61ac819f1d3d1e3c8fb4110ac31cbe; source-pack-sha256: 879866cd87e960cdf84f05c64d2767e5872d563f745e669adc36894f30db08cd; aynı exact kaynak için normal pack receipt ayrıca saklanır; normal0 öncesi GO yok.
Final Expected HEAD, exact pack/domain formal review+approval rows/records ve READY/DoR kaynaklarını İÇERMELİ; yalnız root current row/commit kaydı yeterli değildir.
Preparation tuple history: managedworktrees creationDETACHED6571; sonra CT yukarıdaki namedbranchleri6571clean hazırladı. Finalsourceadvance ve clean/HEAD ölçümü CT yapar; eski tuple finalkaynak sayılmaz.
Task Source Row (exact text): WP-0230-DL7-CI-VIEWS-01	MOD-0230 DL-7 — rev22 four existing PVG views only their PageCode assignment; no layout/table/behavior change	MOD-0230	READY	PVG CT	WP-0230-DL11,WP-0230-DL12-FE	-	2026-11-23	docs/records/audits/2026-11/pvg-rev22-dispatch-01/READY.md	-
Task-row snapshot: Bu tam READY satırı final ExpectedHEAD kaynağındadır; DISPATCHED/IN_PROGRESS yalnız gerçek GO/başlama olaylarında ayrı journal ile kaydedilir.
Alıntı kaynak bağlamı: b0dcca109997320e55659d7f41c489396338e15c. Bütün dört alıntı bu gerçek ExpectedHEAD Git blobları üzerinde birebir yeniden denetlendi; ürün kaynakları H82 ile aynıdır.

Özel ortam: DOTNET_ROOT=C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8; DOTNET_CLI_HOME=C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/dotnet-home; NUGET_PACKAGES=C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/nuget; TEMP/TMP=C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/tmp; evidence=C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/evidence. Node_modules CT tarafından manifest53d0e78f ile3481dosya/48437254bayt hash doğrulanıp bu worktree'ye yalnız localcopy olarak hazırlandı; kurulum/indirme/hook yok. Private NuGet cache başta boş; normal onaylı scoped restore yalnız hash-doğrulanmış offlinefeed'den doldurabilir.

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
12. docs/records/audits/2026-11/pvg-rev22-discovery-02/DISCOVERY.txt
13. docs/records/audits/2026-11/pvg-rev22-discovery-02/DECISIONS.md
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
60. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml
61. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml
62. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml
63. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml
64. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_DataTable.cshtml
65. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_Filter.cshtml
66. docs/records/audits/2026-11/pvg-rev22-formal-approval-01/APPROVAL-CHECK.txt
67. docs/records/audits/2026-11/pvg-rev22-dispatch-01/READY.md
68. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/DEPENDENCY-MATERIALIZATION.json
69. docs/records/audits/2026-11/pvg-rev22-dispatch-01/preparation/RENDER-FEASIBILITY.txt
70. docs/records/audits/2026-11/pvg-rev22-dispatch-01/NAV-TITLE-PARITY.json
71. docs/records/audits/2026-11/pvg-rev22-dispatch-01/TOOLS.json
72. docs/records/reviews/MOD-0230/2026-11-23-rev22-formal-dialogue-02.md
73. docs/records/reviews/MOD-0230/2026-11-23-rev22-formal-blind-02.md
74. .antigravity/rules/premium-modal-standard.md
75. .antigravity/rules/logging-observability.md
76. docs/records/audits/2026-11/pvg-rev22-discovery-02/OLD-RED-NEW-GREEN-PRODUCER-BINDING.json
77. docs/records/audits/2026-11/pvg-rev22-dispatch-01/NORMAL-01-CORRECTION.md

Sözleşme yüzeyleri:
- API: «RequirePage(ReadPermission) ?? View("~/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml");» (frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:60)
- İzin: «PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);» (frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:250)
- Test: «Assert.Equal("TIMEAPPROVALS", CurrentPage.Resolve(context, viewData));» (frontend/Diten.Web.Tests/Navigation/CurrentPageTests.cs:69)
- Sabotaj: «- A critical new protection must have independent negative evidence in an external private scratch copy only:» (execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:254)

Alıntı açıklamaları: mevcut API/page boundary; görev controller/endpoint davranışı yazmaz.; gerçek permission claim gate aynı; yeni grant/adapter yok.; unchanged producer/consumer/test kaynağı ölçülür.; yalnız ownscope privatecopy, gerçek unchangedguard/render assertionRED→restoreGREEN.

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/pvg-rev22-discovery-02/DISCOVERY.txt@b0dcca109997320e55659d7f41c489396338e15c · sha256 0f52bf703e10a143af4d81ce895819c5de690e344e8a1a6e4d6ec558929463bb

Keşif kararı 1: parent — Blueprint mevcut MOD-0230 Case Intake & Triage kimliğini verir; yeni MOD/FU/capability yok.
Keşif kararı 2: parent — Registry aynı kanonik kimliği doğrular; yeni kayıt yok.
Keşif kararı 3: parent — Mevcut tek pack kullanılır;481ref eski/kopya bloblar ayrı iş izni değildir. Rev22 exact formal/HIGH gate beklenir.
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
Keşif kararı 35: child — WP-0230-DL7-CI-VIEWS-01 mevcut READY rev22 dört-görünüm işidir; bu prompt aynı işi dağıtım için düzeltir, mükerrer WP veya yeni dilim açmaz. Normalv1.0 keşif producer-byte bağı nedeniyle1 kaldı; actual0 ve CTGO olmadan ürün yok.



NE: MOD-0230 rev22 AC46/49 dört PVGviewPageCode eki; yalnız Index/Create/Edit/Details mevcut setup bloğuna kendi ViewData["PageCode"] ataması. Rol pvg_r22_views_writer;23shared/metin/bridge/family yazımı yok.

NEDEN: Hidden PVGmanifest sayfaları gerçek tenantmenu tarafından elenir; CurrentPage menümatch'i bu dört direktadreste kod bulamaz. Sadece sharedRESX metni yardım panelini bağlamaz. Dört view'ın metadata ataması gerçek production CurrentPage.Resolve tarafından okunmalı; izin/layout/form/rota/persistence değişmez.

NASIL:
- Normal0+GO sonrası yalnız bu süreç için aşağıdaki ortamı ayarla; değerleri global profile/config'e yazma. SDK/Node sürümünü ve kendi evidence kökünü tekrar ölç.
$env:DOTNET_ROOT = "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8"
$env:DOTNET_CLI_HOME = "C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/dotnet-home"
$env:NUGET_PACKAGES = "C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/nuget"
$env:TEMP = "C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/tmp"
$env:TMP = $env:TEMP
$evidence = "C:/Users/epenc/AppData/Local/Temp/pvg-r22-views-env-34r_fel1/evidence"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:PATH = "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/node22/node-v22.23.5-win-x64;C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8;" + $env:PATH
- Normalgate0 ve CT GO yoksa ürün işi/restore/build/test/commit yok. Yalnız son prompt/sourcepack, yazar-dışı HIGH inceleme, gerçek normal dispatch0 ve CT'nin açık GO mesajı sonrası başla. İlk olarak final branch/HEAD/clean, pinler, sourcepack/prompt/review/gate footer ve tam allowlist'i ölç; uyumsuzsa yazmadan dur. Bu son tuple değişirse dur; kendin sourcepack/prompt kapsamını değiştirme.
- Bu rolün tek yazıcısısın; alt ajan veya sohbet açma. İki scope ayrıdır, max2 aktif çocuk; root ürün kodu yazmaz. Diğer worker'ın dosyalarını/test sonucunu kendi teslimine katma. Overlap veya beklenmeyen dirty dosya varsa CT'ye inventory ver; başka worktree/WIP'ye cleanup/stash/archive/silme yok.
- Kendi benzersiz dış evidence kökünde raw log/JSON/TRX/özel scratch/harness sakla. Başlangıç/final HEAD/tree/branch/status, bütün izinli dosyaların raw-disk SHA ve canonical-Git blob/SHA'sı, değişen key/statement manifesti ayrı tutulur. CRLF/LF hash'leri karıştırılmaz; root queue/index/ref/pack/domain/owner/review kaynakları yazılmaz.
- Tek ortam preflight: CT'nin ölçtüğü portable SDK8.0.428 ve Node22 kullan; gerçek executable --version/paths kaydet. Süreç içi DOTNET_ROOT/PATH; kendi DOTNET_CLI_HOME/TEMP/TMP, NUGET_PACKAGES private cache ve ölçülmüş offline feed. Global config/feed/cert/SDK/lock/proje değişikliği, install/download/skip yok. Root'ta global.json yoksa ekleme. Mevcut lock-bound node_modules/jsdom/Vitest2.1.9 yoksa somut ortam engeliyle dur.
- Gerekirse yalnız WebTests için bir dar restore: & "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8/dotnet.exe" restore frontend/Diten.Web.Tests/Diten.Web.Tests.csproj --packages "$env:NUGET_PACKAGES" --source "C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/WP-0230-DL12-PREP-WEB-1795176414809/nuget" --disable-parallel. Sonraki test --no-restore. NuGet kaynağı/cache önceden ölçülmeden bu scoped komut çalıştırılmaz; npm install/npm ci yok.
- Profile A zinciri: synthetic tenant/permission fixture → salt okunur UI/help/etiket interaction → mevcut permission gate → mutation/persistence yok → gerçek render/reload sonucu → kanıt. Create/save/triage/route/grant/DELETE/POST mutasyonu yok. Canlı5000/5001/5011/27017, gerçek hasta/kiracı verisi, dotnet run/run_all veya yeni operasyonel Web host yok. Mevcut in-memory test rendering seam/gerçek rebuilt consumer kullanılabilir; production auth/field/audit adapter/DI değişmez. Mutasyon istek sayısı0, varsa test-local persisted state değişmez; ölçülmeyen canlıDB snapshot sonucu uydurulmaz.
- Harici harness, işçinin incelenebilir EVIDENCE ÇIKTISIDIR; bu promptta önceden varmış dosya/argv iddiası yoktur. Gerçek production types/views/bridge/consumer'ı çalıştırır; kendi Resolve/permission/localization kural kopyası olmaz. Worker somut source/entrypoint/exact argv/env/SHA/compile/exit/result/cleanup oluşturmadan başarı diyemez. Repo test/harness/proje kaynağı değişmez. Routine evidence temsilini işçi seçer; yeni repo write/guard/CI/scope ihtiyacında durur.
- Tam bir satır her mevcut Razor setup bloğuna: Index→ViewData["PageCode"] = "CASEINTAKETRIAGE"; Create→"CASEINTAKETRIAGECREATE"; Edit→"CASEINTAKETRIAGEEDIT"; Details→"CASEINTAKETRIAGEDETAILS". Title/FormMode/IntakeDraftId/layout/scripts/partial/markup/antiforgery aynı; dört atama dışı delta0. Bu machine metadata localizedstring değildir. CurrentPage/NavNameLocalizer/menu veya shared helper davranışı değiştirilmez.
- Dört ACTUAL Razor view existing testviewengine/layout yolunda in-memory synthetic reader'a render edilir. Caller ViewData'ya PageCode veya Items['Diten.PageCode'] ENJEKTE ETMEZ, sahte eşleşen PVGmenu sağlamaz. Mevcut controller permissiongate'inin seçtiği gerçek view, gereken typed model/safe syntheticid bağlamı kullanılır. Production CurrentPage.Resolve view'ın kendi set ettiği kodu layout'ta tüketir; cached/hardcodedHTML, Razorcopy, ownResolve algoritması veya yalnızgrep gerçekE2 değildir.
- İzole E2 doğru actual4view/data-page-code/resolver/noinjectedoverride/mutation0 içindir. Sharedresources henüz yoksa yardımın preparing yüzü bunu dürüstçe gösterir. Tüm4viewlocalizedtitle/purpose/audience/titledstep7language/noPVGmenu tam zinciri ikiwriterCTINTsonrası yükümlülük; kendi23sharededitini yapma, missingmetni testinput'la doldurma ve scopedcodePASS'i falsefullhelpPASS diye sunma.
- Existing LayoutRenderer internal ve CurrentCulture/CurrentUICulture=en hardcoded; o helper'ın çıktısından7langPASS çıkarılamaz. Harici harness gerçek IRazorViewEngine/compiledview/layout/CurrentPage yolunu gözlenebilir kılar. Kaynak/entrypoint/argv/SHA/compile/actualoutput worker'ın evidence çıktısıdır; öncedenvarmış probe iddiası yok. Repo test/harness/proje kaynağı değiştirilmez.
- Her dörtviewoverride'ını AYRI dışprivatekopyada çıkar. Aynı no-menu/no-caller-code actualrender/resolver gözlemi yalnız oviewiçin genuineassertionRED vermeli; diğerüçsatır/consumer/assertion sabit. Restore candidate aynı ölçümGREEN. Tekpatch/source/harness/argv/hash/compile0/actualmissingwrongcoderesult/cleanup saklı. Syntax/compilefailure/regexmissingline/cachedHTML RED değil; mutantcommit yok.
- Index touch gerçek baseline6571verifier exit1/12knowncategorygaps/5forbiddengap0'dır. OD-PVG10/AC14 mevcutownerexclusion bulk/export/import/selectall/clearselection/ilgiliwiring. Aynı kategoriler için rutin yeniden defer/onay yok. Kategori dışı yeni gap çıkarsa DUR; table/Select2/export/bulk/layout refactor bu4pathscope'da değil.
- Teslim öncesi yalnız kendi açık yollarını stage et; git diff --cached --name-only, TAM git diff --cached ve git diff --check incelemesini kaydet. git add . /-A, force/reset/clean/amend/checkpoint, branch değişimi/merge, push/PR/mainmerge/approval/queue yazımı yok. Sahibin izin verdiği ordinary worker-local commit yalnız bu dal ve dosyalardır; yeni onay sorulmaz. Gerçek commit/tree/clean + evidence ile CT'ye teslim edip DUR.
- CT ayrı exact-commit E2/scope incelemesi sonrası no-ff birleştirir. Tüm4viewlocalizedhelp7dil/noPVGmenu ve RoleAssignments consumer zinciri iki teslim birleşince INT yükümlülüğüdür. E2 INTEGRATED, E4/E5 ACCEPTED veya operasyon izni değildir. Tek PR#460; yeni finalH bütün requiredCI/Phase1/ui-compliance/fullJS ve AC45 pvg-api ubuntu22.04 samejob target/fullTRX/commit-tree-checkout-event-runattempt bağıyla CT'de kapanır. Eski H82 sonuçları veya aynı-source Phase1retryGREEN root-cause repair/sonH PASS sayılmaz.

Token/JWT/signing-key/cookie değerlerini ham log veya response raporuna yazma; yalnız gerekli sentetik kimlik/claim türü ve güvenli hash/kaynak bağını kaydet. Sandbox bu yeni managed path'te yetkili işlemi engellerse yalnız exact scoped komut için require_escalated kullan; otomatik inceleme reddi çözülmezse somut neden ile CT'ye dön, scope veya korumayı atlatma.

YAPMA: Dört assignment dışı repo write yok. Resources/RoleAssignmentsbridge/perm-label/JS/Controllers/sharedshell/CurrentPage/menu/API/data/store/audit/auth/route/field/formmode/_Form/CI/tests/guards/ledger/proje/lock/dependency/pack/domain/queue/approval kaynakları korunur. Mutasyon endpointi/unsafeadapter/yenioperasyonelhost yok. EskiFE6 producer4pathkanıtı değildir. Dışharness output olabilir ama başka repo write hakkı vermez; diğerworker metinlerini kendi ölçümüne enjekte etme.

DOĞRULA:
1. Normal0+GO/finaltuple/pins/clean ve baş/son4pathrawGitmanifest. İş öncesi/sonrası mevcutverifier: & "C:/Users/epenc/AppData/Local/Programs/Python/Python313/python.exe" -B .antigravity/scripts/verify_datatable_page.py . --area Pharmacovigilance --module CaseIntakeTriage --reference compact --api-profile proxy --format gaps. Tamargv/stdout/hash/exit tutulur. Beklenen12knownOD10: serverexport;BulkDelete/BulkDeleteConfirm/AreYouSure/Export/Importbridge;selectall;bulkconfig/selection/endpoint/trigger;clearselection. Data-mode/_TableSkeleton/_Filter/_IndexL10n/formmodel beşforbiddenkategori0. Exit1 genericPASS değildir; kategori dışı gapta dur ve CT'ye bildir.
2. Darrestore sonrası & "C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8/dotnet.exe" test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~Diten.Web.Tests.Navigation.CurrentPageTests|FullyQualifiedName~Diten.Web.Tests.Controllers.PageHelpShellTests|FullyQualifiedName~Diten.Web.Tests.Navigation.NavManifestL10nGuardTests|FullyQualifiedName~Diten.Web.Tests.Services.PageHelpCatalogTests|FullyQualifiedName~Without_the_key_a_page_action_neither_redirects_nor_writes_view_data|FullyQualifiedName~Without_the_read_key_the_case_intake_page_is_the_shared_403_without_its_skeleton" --logger "trx;LogFileName=r22-views-targeted.trx" --results-directory "$evidence/normal" --disable-build-servers -nodeReuse:false -p:UseSharedCompilation=false -p:UseRazorBuildServer=false. Namespace/testadları mevcut kaynaklarda var; actualtestcaseIDs/0failed0skip/TRX/source-DLL-hash kaydı. Bu genericCurrentPage/help/403 regresyonları actualfourPVGview-specific kanıtın yerine geçmez.
3. Actual4Razor→productionCurrentPage/noinjectedoverride ve dört separate omission-negative outsideharness çıktısıyla yukarıdaki gibi ölçülür. Worker somut harnesssource/argv üretmeden başarılı sayım yok. Mutantta yalnız ilgili view dar ölçümü yeterli; tamWebsuite/fullJS her mutantta tekrarlanmaz.
4. İki DataTable kategori gözleminde knownset aynı/forbidden5gap0. FULLJS bu4pathworker'da koşulmaz;23sharedwriterfinalinde bir kez ve CTfinalrequiredCI'da yeniH bağlanır. Ek test yalnız yeni delta/fail/somut unresolvedconcern ile gerekçeli.
5. Tam4path/satır/stageddiff+ordinarylocalcommit ve ownedprocess/testhost/temp/scratchcleanup kanıtıyla teslim et. Evidence saklı; başka worktree/process/devport veya uncheckedrecursivepath silme yok. ProfileA mutation0/persistence ölçüm sınırı açık. CT ayrıE2/noff/combined7langINT/finalCI/AC45 bekler.

Durma koşulları: normalgate/GO veya finalHEAD/formalrecord/normal0/GO yok; tuple/branch/hash/clean/overlap belirsiz;4PageCode dışı helper/resource/test/CI/API/auth ihtiyacı; genuineviewRED yerinegrep/syntax/compile/cache/fakeinjectedcode; yeniDataTablecategorydışıgap; skip/fail/countdrop/unsafehost; gerçek harness/mutation0/cleanup/source-DLL-TRX bağı kanıtsız. Somutkanıtla CT'ye dön;23shared veya Create/edit business logic sessizce düzeltilmez.

Rapor formatı: exactWP/role/requested+actualmodelverilemiyor;begin/finalH/tree/branch/managedpath/clean;4pathfull/stageddiff/rawcanonicalhash;actual4Razor→CurrentPage hiddenmenu/noinjectedcode receipts;four omissions RED→restoreGREEN/source/harness/argv/compile/output;existingtargetedC#TRX/0skip/assemblybinding;DataTable12knowncategory/5forbiddengap0ikiölçüm;ProfileA mutation/persist ölçüm sınırı;owncleanup/localcommit;23sharedexcluded ve combined4view7langINT/finalCI/AC45/live bekleyenleri. KendiisolatedE2'ni combinedhelpPASS/CTACCEPTED/mainmerge/operasyon sonucu ilan etme.

Revizyon v1.1 farkı: yalnız kesin kaynak/ExpectedHEAD ve producer-bağı düzeltilmiş discovery02/35karar. Önceki v1.0 normal1 ham kayıtları korunur; ilk prompt UYGUN ve integrity0 gerçek dispatch0 değildir. Kapsam/yetki, test veya product bytes değişmedi; yeni exact v1.1 HIGH inceleme ve actual normal0 gerekir.
