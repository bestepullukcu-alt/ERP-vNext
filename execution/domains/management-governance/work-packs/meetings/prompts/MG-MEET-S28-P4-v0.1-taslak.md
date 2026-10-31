@[.antigravity/agents/orchestrator.md]
WP: MG-MEET-S28 · Prompt P4 v0.1
Repository: C:\dev\ERP-vNext · Branch: chore/ct/mg-ct-01 · Expected HEAD: eb561752267ae5f7f5f573a43e451bdd0c9073bc · Worktree: C:/dev/ERP-vNext/.claude/worktrees/mg-ct-01
Risk: HIGH · §9.1 sinyalleri: yetki, kiracı, sözleşme
Görev sınıfı: ürün · Paket: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/QueryHandlers/MeetingReportQueryHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingReportModels.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportSerializer.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportPlaceholders.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/RecordLinks/MeetingRelatedRecordResolver.cs, frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.js, frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.l10n.js, frontend/Diten.Web/Views/Meetings/Report/_IndexL10n.cshtml, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.en.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.tr.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.fr.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.es.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.zh.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.ar.resx, frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.ru.resx, frontend/Diten.Web/tests/meeting-report-*.test.js, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.API/Controllers/MeetingsController.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs
Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Resources/SharedResource*.resx, frontend/Diten.Web/wwwroot/assets/js/shared/**, frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/**, frontend/Diten.Web/wwwroot/assets/css/**, frontend/Diten.Web/Program.cs, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv
Kapsanan dilim: DL-28

> **TASLAK v0.1 — DAĞITIMA HAZIR DEĞİL. Bu metin kod, commit ya da dağıtım izni değildir.**
> - Kuyruk satırı `PLANNED`, `READY` değil. Kayıt satırı `origin/main`'de değil. HIGH inceleme kaydı yok.
> - **Başlangıç noktası P3'ün son commit'idir; bugün bilinmez.** Repository, Branch, Expected HEAD ve Worktree, kapının iskeletinden gelir (CT dalının bugünkü ucu). Dağıtımda CT bunları yeniden yazar. Kod alıntıları ve satır numaraları eb561752'de ölçüldü; P1–P3 sonrası kayar, CT yeniden ölçer.
> - P1–P3'ün ürettiği `MeetingWriteAccess.cs`, `MeetingSystemOrigin.cs`, `MeetingAccessHttpMongoHost.cs`, `MeetingWriteAccessGuardSourceTests.cs` bugünkü Expected HEAD'de yoktur; bu yüzden "Önce oku" listesinde değildir. İşçi hepsini açar.
> - Tek dal, tek birleştirme isteği (DL-28). PR, CT'nin ayrı adımıdır ve sahibin ayrı onayını ister.
>
> **CT okumaları (paket yetkisi altında; paket değişmez):**
> - **K-0 — "draft" sözcüğü.** Paket §24 üst notu ve §24.31 satır 3892 "draft / not approved" der. Paket düzenlenmez: özet değişir, onay düşer. Aynı not bağlayıcı olanın onay satırı olduğunu söyler (satır 1147–1148). İşe başlarken `py -3 -I -B -X utf8 scripts/pack_approval.py check execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` koşulur; çıkış 0 değilse kod yazılmaz, DUR.
> - **K-16 — iki yol sınıfı.** İzinli yolların ilk grubu kalıcı değişiklik içindir (rapor, çözümleyici, ekran, testler). İkinci grup (`MeetingCommandHandlers.cs`, `MeetingFollowUpCommandHandlers.cs`, `GenerateDueMeetingSeriesHandler.cs`, `MeetingWriteAccess.cs`, `MeetingCommands.cs`, `MeetingModels.cs`, `MeetingsController.cs`, `PlatformSchemaManifest.Meetings.cs`) **yalnız geçici sabotaj içindir**: iş sonunda bu dosyaların P3 commit'ine göre farkı boştur (DOĞRULA 8).
> - **K-17 — sabotaj sayısı.** Paket "Tests" listesi (satır 4650–4704) ölçüldü: **34 madde**. 32. madde iki varyant taşır, koşu sayısı 35'tir.
> - **K-18 — satır yerleşimi.** Parça planı matrisi ve bayt eşitliğini bu prompt'a verir, öteki S28-8 satırlarını yerleştirmez. Yerleşim: oluşturma satırları P2, takip satırları P3, burada matris, durum satırları, gövde eşitliği, Respond, seri ve rapor satırları. Önceki satırlar yeniden yazılmaz.
> - **K-19 — yeni resx adları.** Paket adlarını vermez. Bu işte sabittir: `ActionTaskNotAccessible`, `ActionCurrentMeetingNotAccessible`. Sabit `MEETING_NOT_ACCESSIBLE` `MeetingReportModels.cs` içindedir, `MeetingReasonCodes` içinde değildir (paket satır 4153–4154); köprü testi etkilenmez.
> - **K-20 — ana daldaki kırmızı.** `Currency_minor_units_covers_every_code_the_currencies_lookup_returns` aynı test projesindedir (`BusinessReferenceData/TreasuryReferenceCatalogTests.cs`) ve `tests/known-failures.tsv`'de kayıtlı değildir. Geniş koşuda `known_failures.py` bunu YENİ der. Bu tek ad çıkarsa işçi dokunmaz, koşuyu sayar ve raporlar; başka her YENİ DUR'dur (XMC-001). Kayıt ya da düzeltme CT ve sahibin kararıdır.

Önce oku (sırayla):
1. execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md (§24.31 tamamı; S28-5 satır 4143; S28-6 satır 4146–4171; S28-7 satır 4172–4175; S28-8 satır 4176–4196, 4239–4253; S28-9 satır 4313; "Tests" satır 4646–4706; §26 DL-28 satır 4865)
2. AGENTS.md
3. execution/domains/management-governance/domain-config.md
4. docs/records/audits/2026-10/mg-meetings-discovery-01/RECORD.md
5. docs/records/audits/2026-10/mg-s28-inspection-01/INSPECTION-NOTE.md
6. .antigravity/agents/backend-architect.md
7. .antigravity/agents/l10n-agent.md
8. .antigravity/agents/frontend-ui-ux.md
9. .antigravity/agents/testing-agent.md
10. .antigravity/rules/multi-tenancy.md
11. .antigravity/rules/security-jwt.md
12. .antigravity/rules/permission-key-standard.md
13. .antigravity/rules/business-module-enforcement-standard.md
14. .antigravity/rules/data-scope-enforcement.md
15. .antigravity/rules/handler-design.md
16. .antigravity/rules/response-envelope.md
17. .antigravity/rules/audit-trail-standard.md
18. .antigravity/rules/localization-standard.md
19. .antigravity/rules/dynamic-localization-standard.md
20. .antigravity/rules/frontend-standards.md
21. .antigravity/rules/frontend-js-standard.md
22. .antigravity/rules/unauthorized-surface-standard.md
23. .antigravity/rules/workcenter-bridge-standard.md
24. .antigravity/rules/status-reporting-and-evidence.md
25. .antigravity/rules/code-style.md
26. .antigravity/rules/git-safety.md
27. .antigravity/rules/docs-organization.md
28. .antigravity/rules/cross-module-change-consent.md
29. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/QueryHandlers/MeetingReportQueryHandlers.cs
30. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingReportModels.cs
31. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportSerializer.cs
32. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportDisclaimer.cs
33. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/RecordLinks/MeetingRelatedRecordResolver.cs
34. services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskLinks.cs (yalnız okunur, :36)
35. services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskReadAccessPolicy.cs (yalnız okunur, :44–69)
36. services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs (yalnız okunur, :1916)
37. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Providers/MeetingWorkItemProvider.cs (yalnız okunur, :214)
38. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/BackgroundJobs/MeetingSeriesSweepJob.cs (yalnız okunur, :81)
39. services/Diten.Platform/src/Diten.Platform.API/Controllers/MeetingsController.cs (rotalar ve anahtarlar; yalnız sabotaj)
40. frontend/Diten.Web/wwwroot/assets/js/Meetings/Report/index.js
41. frontend/Diten.Web/Views/Meetings/Report/_IndexL10n.cshtml
42. frontend/Diten.Web/Resources/Views/Meetings/Report/MeetingReportIndex.en.resx
43. frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.en.resx (yalnız :74, `LinkedTaskNotAccessible`)
44. frontend/Diten.Web/tests/meeting-report-export-disclaimer-consistency.test.js (iki servis arası metin eşitliği emsali)
45. frontend/Diten.Web/tests/meeting-report-l10n-parity.test.js
46. frontend/Diten.Web/tests/meeting-report-screen.test.js
47. frontend/Diten.Web/tests/task-request-fix5-sentences.test.js (:233–235, kod sabiti emsali)
48. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingAttendeeSearchHttpMongoTests.cs (HTTP + gerçek Mongo, :413–507)
49. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingSeriesSweepEndToEndMongoTests.cs (seri satırlarının biçimi)
50. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingReportMongoTests.cs
51. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingReportExportCapTests.cs
52. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingReportExportDisclaimerTests.cs
53. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/RelatedRecordsProjectionTests.cs
54. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingDispatchAssertions.cs
55. docs/guides/operations/control-tower-operating-card.md
56. tests/known-failures.tsv

Sözleşme yüzeyleri:
- API: «through it too. Otherwise `CurrentMeetingTitle` becomes the placeholder code `MEETING_NOT_ACCESSIBLE` (a» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4153), «export (review 03 F-21; the field becomes `Guid?`, `MeetingReportModels.cs:112`), by the same rule that» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4155), «Guid CurrentMeetingId,» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingReportModels.cs:112)
- API: «public const string InaccessibleTitleCode = "TASK_NOT_ACCESSIBLE";» (services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskLinks.cs:36), «("CurrentMeetingId", row => row.CurrentMeetingId),» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingReportExportSerializer.cs:52), «**drops** every meeting the caller cannot view (`CanView`), so neither title nor route is shown (owner answer» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4173)
- İzin: «`ITaskReadAccessPolicy.ReadableTaskIdsAsync` call: the title becomes `TaskLinks.InaccessibleTitleCode`;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4148), «S28-8 — The write matrix: {organizer, attendee, `read-all` non-participant, non-participant holding the write» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4176), «public const string ReadAll = "platform.meetings.read-all";» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs:25)
- Test: «every S28-1 handler on a **cancelled** and on a **completed** meeting: a non-viewer gets 404, never the 409» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4188), «**body equality**, for every handler: the body for an existing meeting the caller cannot view is» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4192), «**attack:** a `series-manage` holder who cannot view the series' last generated meeting rewrites the series» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4240)
- Test: «The export in each of the 7 locales carries that locale's text for both placeholders and no raw code.» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4171), «E5 (the write matrix over HTTP with real Mongo, its series rows at handler level with real Mongo,» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4865)
- Sabotaj: «Restore each one and it goes green.» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4706), «remove the report task mask → S28-6 goes red;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4685), «make the resolver skip `CanView` → S28-7 goes red;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4689), «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/mg-meetings-discovery-01/MOD-0357-management-review-cadence.txt@4105c8167f4a1822b1c592e1a79767f55e175b2e · sha256 f541fd3109e7518ea5987df5a2bb50cda64b804d4177c1ba12a96b65d6d1fbc6
Keşif kararı 1: parent — blueprint `MOD-0357 'Management Review & Cadence'` işin kendi modülüdür; yeni kimlik açılmaz.
Keşif kararı 2: parent — SoR `management review cycles; review instances; agendas; …` sahibi MOD-0357'dir; bu iş aynı kaydın erişim kuralını kapatır, yeni kayıt sahibi doğurmaz.
Keşif kararı 3: parent — kimlik kaydı satırı `MOD-0357 (planned)` işin kimliğidir; `planned` durumunun eşitlenmesi iş listesi 0.2 takibindedir ve bu işi engellemez.
Keşif kararı 4: parent — `MOD-0357-management-review-cadence.md` işin onaylı paketidir; 207 ref aynı dosyanın dallardaki kopyasıdır, ayrı bir iş değildir.
Keşif kararı 5: parent — gateway `meetings` → 5057 (Platform Service) modülün var olan rotasıdır; bu iş yeni rota ve port eklemez, `ocelot.json` değişmez.
Keşif kararı 6: parent — görev `MG-MEET-S28` bu işin kendi kuyruk satırıdır.
Keşif kararı 7: parent — görev `MG-MEET-S13` aynı paketin sonraki dilimidir; kuyruk tek yazarlıdır, çakışma yok.
Keşif kararı 8: parent — görev `MG-MEET-S14` aynı paketin sonraki dilimidir; kuyruk tek yazarlıdır, çakışma yok.
Keşif kararı 9: parent — görev `MG-MEET-S25` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 10: parent — görev `MG-MEET-S15` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 11: parent — görev `MG-MEET-S16` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 12: parent — görev `MG-MEET-S17` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 13: parent — görev `MG-MEET-S18` aynı paketin sonraki dilimidir; çakışma yok.
Keşif kararı 14: parent — görev `MG-MEET-S26` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 15: parent — görev `MG-MEET-S27` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 16: parent — görev `MG-MEET-S19` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 17: parent — görev `MG-MEET-S20` aynı paketin sonraki dilimidir; çakışma yok.
Keşif kararı 18: parent — görev `MG-MEET-S21` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 19: parent — görev `MG-MEET-S22` aynı paketin sonraki dilimidir; çakışma yok.
Keşif kararı 20: parent — görev `MG-MEET-S23` aynı paketin sonraki dilimidir ve bu işe bağlıdır; çakışma yok.
Keşif kararı 21: parent — görev `MG-MEET-S24` aynı paketin sonraki dilimidir; çakışma yok.

Hedef ajan giriş alanları (§17.4, orchestrator; tek çalışma hattı):
- **Paket ve durum:** yol yukarıda · `status: ready-for-dev` · `pack_approval.py check` çıkış 0 (kayıt satırı `RECORD.md` satır 8; dağıtımda yeniden ölçülür).
- **Alan ve servis:** management-governance · Diten.Platform · `shell: tenant` · `golden_reference: compact`, `form_field_count: 11`.
- **Dal:** P1–P3'ün işçi dalı; CT yazar. AGENTS §9 biçimi `feature/mg/mod-0357-…`.
- **Alt ajan zinciri (sırayla, aynı dalda):** backend-architect (rapor, dışa aktarım, çözümleyici) → l10n-agent (Report resx) → frontend-ui-ux (`Report/index.js`) → testing-agent (matris, satırlar, sabotaj).
- **frontend-ui-ux girdileri:** alan ve modül Meetings / Report; compact; kabuk `_LayoutTenantShell`; kolon ve süzgeç değişmez; anahtarlar K-19; DOĞRULA 6'daki doğrulayıcı.
- **l10n-agent girdileri:** kiracı modülü, 7 dil; `Resources/Views/Meetings/Report/MeetingReportIndex.{en,tr,fr,es,zh,ar,ru}.resx`; `SharedResource` değil; metinler NASIL tablosunda.
- **testing-agent girdileri:** akışlar paket satırlarıyla NASIL'da; kiracı satırı ve silinmiş kayıt satırı var; test projesi `services/Diten.Platform/tests/Diten.Platform.Application.Tests`.
- **İzin anahtarları:** yeni anahtar yok. `platform.meetings.read`, `.create`, `.update`, `.minutes-write`, `.minutes-publish`, `.read-all`, `.series-manage`; köprü uçları denetleyicideki gibi `platform.tasks.*` ile birlikte.

NE: MOD-0357 Toplantılar, S28 P4. Rapor ve dışa aktarım okunamayan görevin ayrıntısını, Görev Merkezi göremediği toplantının başlığını göstermez; yazma kuralının bütünü HTTP ve gerçek Mongo ile ölçülür; paketin sabotajları tek tek koşulur; E5 kanıtı çıkar. İşler:
- S28-6: rapor ve dışa aktarım maskesi; `CurrentMeetingId` `Guid?`; `MeetingReportExportPlaceholders` 7 dil; ekran metinleri.
- S28-7: `MeetingRelatedRecordResolver` göremediği toplantıyı düşürür.
- S28-8: yazma matrisi {düzenleyen, katılımcı, `read-all`, anahtarlı yabancı, platform aktörü, başka kiracı} × 15 işleyici; durum satırları; bayt eşitliği; Respond; seri satırları işleyici düzeyinde.
- Paketin 34 sabotaj maddesi (K-17): her biri kırılır ve kırmızı görülür, geri alınır ve yeşil görülür.
- E5 kanıt paketi.

Bu prompt'ta yok: S28-9'un kayıt adımı (CT'nin; paket satır 4313–4314), PR, S15 ve sonraki dilimler.

NEDEN:
- Paket S28-6 ve S28-7: BL-541'in toplantı tarafı. Rapor görev ayrıntısını okuma kuralına bakmadan verir (`MeetingReportQueryHandlers.cs:148–151`), `titleById` bütün toplantılardan kurulur (`:63`), `MeetingRelatedRecordResolver.cs:28–31` görev okuyana toplantı başlığını verir.
- Sahip cevabı Q24 ("duzelt-2"): göremediği bağlı toplantı hiç görünmez, MOD-0024 değişmez.
- Paket §26 DL-28: kanıt E5, MOD-0024 okuma kuralını ve Görev Merkezi izdüşümünü geçer. Çekirdek madde 10: sabotaj kanıtı olmayan koruma, koruma değildir.

NASIL:
- **Makine yükü, ortam, taban.** `(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average` 80 üstündeyse 60 saniye bekle; sabotaj koşuları sırayla, paralel değil. `$env:DITEN_TEST_MONGOD = 'C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe'`; docker yok. Ekran testleri için `Test-Path frontend/Diten.Web/node_modules`; yoksa `Push-Location frontend/Diten.Web; npm ci; Pop-Location`; `npm ci` başarısızsa DUR. Değişiklikten önce DOĞRULA 1 ve 2'yi koş; P3 tabanından farklıysa DUR.
- **S28-6, görevler** (`MeetingReportCore.BuildAsync`; satırlar eb561752). `ITaskReadAccessPolicy` parametre olarak gelir; `taskById` değerleri için tek toplu `ReadableTaskIdsAsync(tasks, callerId, ct)` (imza `TaskReadAccessPolicy.cs:69`). Okunamayan görev satırında: başlık `TaskLinks.InaccessibleTitleCode`, `AssigneeUserId` ve `DueAt` null. `Lifecycle` ve `IsOverdue` kalır; `IsOverdue` maskeden önceki `DueAt` ile hesaplanır (`:146`), kutular doğru kalır. `GetMeetingReportHandler` ve `ExportMeetingReportHandler` kurucuları yeni bağımlılığı alır. Test kurulumları: `MeetingReportExportCapTests.cs:50, :95`, `MeetingReportMongoTests.cs:392, :420`.
- **S28-6, güncel toplantı.** `titleById` yalnız görünür toplantılardan kurulur. Güncel toplantı başlığı yalnız `MeetingEligibility.CanView` geçerse yazılır; karar `:44–53`'te yüklenen toplantı ve katılımcılarla verilir. `:122–133`'teki ek toplu okuma da aynı süzgeçten geçer; o toplantıların katılımcıları tek toplu `ListByMeetingIdsAsync` ile okunur. Geçmezse ya da toplantı okunamadıysa `CurrentMeetingTitle = MEETING_NOT_ACCESSIBLE` ve `CurrentMeetingId = null`. Kaynak toplantı başlığı değişmez.
- **Model.** `MeetingReportModels.cs:112` `Guid? CurrentMeetingId`; sabit bu dosyada (K-19).
- **Dışa aktarım.** Yeni `Services/MeetingReportExportPlaceholders.cs`, `MeetingReportExportDisclaimer.cs:21–44` deseni: `StringComparer.OrdinalIgnoreCase` harita, `Locale` ile seçim, `ToLowerInvariant`, İngilizce yedek, tek yer. Satırlar serileştirmeden önce çevrilir; ham kod CSV ya da JSON'a ulaşmaz. CSV `CurrentMeetingId` hücresi boş, JSON değeri `null`. Dışa aktarım satırları ekran satırlarıyla aynıdır (BL-346); tek `BuildAsync`.
- **Ekran.** `Report/index.js:120, :124`: `a.title === 'TASK_NOT_ACCESSIBLE'` ise `ActionTaskNotAccessible`; `a.currentMeetingTitle === 'MEETING_NOT_ACCESSIBLE'` ise `ActionCurrentMeetingNotAccessible`. Sabit deseni `Meetings/form.js:372–373`. Yeni bağlantı yok; `Report/_IndexL10n.cshtml` içine `Anahtar = Localizer["Anahtar"].Value,` biçiminde iki satır (`:43` deseni); yedi resx'e iki anahtar. Ham kod ekranda görünmez. `meeting-report-screen.test.js` bugün jQuery'yi sildiği için satır çizmez (`:87`, `index.js:116–117`): DataTable taklitli bir satır eklenir; hücre iki çevrilmiş metni gösterir, iki ham kod görünmez. Eşleme silinirse bu satır kırmızı olur.
- **Metinler.** Görev metni, `MeetingsIndex.*.resx:74` `LinkedTaskNotAccessible` değerinin aynısıdır (ölçüldü). Toplantı metni CT taslağıdır; l10n-agent kontrol eder, değiştirirse Decisions'a yazar. Ekran ve dışa aktarım aynı metni taşır; `frontend/Diten.Web/tests/meeting-report-export-placeholders-consistency.test.js` iki dosyayı metin olarak okur ve yedi dilde eşitliği ölçer (`meeting-report-export-disclaimer-consistency.test.js` deseni).

  | Dil | `ActionTaskNotAccessible` | `ActionCurrentMeetingNotAccessible` |
  |---|---|---|
  | en | A task you do not have access to | A meeting you do not have access to |
  | tr | Erişiminiz olmayan bir görev | Erişiminiz olmayan bir toplantı |
  | fr | Une tâche à laquelle vous n'avez pas accès | Une réunion à laquelle vous n'avez pas accès |
  | es | Una tarea a la que no tiene acceso | Una reunión a la que no tiene acceso |
  | zh | 您无权访问的任务 | 您无权访问的会议 |
  | ar | مهمة ليس لديك صلاحية الوصول إليها | اجتماع ليس لديك صلاحية الوصول إليه |
  | ru | Задача, к которой у вас нет доступа | Совещание, к которому у вас нет доступа |

- **S28-7** (`MeetingRelatedRecordResolver.cs`). Kurucuya `IMeetingAttendeeRepository`, `ICurrentUserContext`, `IActorPermissionContext` eklenir. `ListByIdsAsync` sonrası tek toplu `ListByMeetingIdsAsync`; `CanView` geçmeyen toplantı sözlükte yoktur: ne başlık ne rota. `hasReadAll` S28-5'teki gibi. Bugün testte elle kurulan örneği yok (ölçüldü); DI satırı değişmez. Bu sınıf `MeetingWriteAccess` kullanmaz: tarama kapsamında değildir ve toplu okur. Çağıranlar `TaskWorkItemProvider.cs:1916` ve `MeetingWorkItemProvider.cs:214`; `Features/Tasks/**` değişmez. Kullanıcısız bir çağıran bulursan DUR.
- **Yazma matrisi** (S28-8, S28-5; paket satır 4176–4187). `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingWriteMatrixHttpMongoTests.cs`, P2'nin `MeetingAccessHttpMongoHost.cs` konağı. Rotalar ve anahtarlar `MeetingsController.cs`'ten okunur.
  - 15 işleyici, 6 rol. Düzenleyen, katılımcı, `read-all`, platform aktörü: anahtarın izin verdiği kadar başarılı. Anahtarlı yabancı: o işleyicinin 404'ü ve veri **değişmez**: veritabanında katılımcı satırı, düzenleyen, tutanak sürümü, görev, posta gönderimi ölçülür. Başka kiracı: 404. Anahtarsız: 403 ve gövdede toplantı bilgisi yok (S28-5). Silinmiş toplantı: 404 (paket satır 4622).
  - **Durum:** her işleyici iptal ve tamamlanmış toplantıda, yabancı 404, asla 409 (satır 4188–4189).
  - **`SaveMinutesDraft`:** `Attendance` katılımcı olmayanı anar, yabancı 404, asla 400 (satır 4190–4191).
  - **Gövde eşitliği:** her işleyicide, göremediği toplantının gövdesi olmayan kimliğin gövdesiyle bayt eşit. İki istek aynı bağıntı kimliğini taşır; başka istek başına değer varsa test onu sabitler ve adlandırır (satır 4192–4194). Beklenen gövdeler: `ScheduleFollowUpMeeting` 404 `MEETING_FOLLOW_UP_NOT_FOUND`, "This meeting could not be found."; köprü 404 `MEETING_NOT_FOUND`, "This meeting could not be found."; ötekiler 404 `MEETING_NOT_FOUND`, "The meeting does not exist.".
  - **`RespondToInvitation`:** satırsız çağıran, iptal toplantı, eksik toplantı gövdesi, asla 409 (satır 4195–4196).
  - Satır adları işleyici ve rolü söyler. Rapora işleyici × rol tablosu yazılır.
- **Seri satırları** (S28-11; paket satır 4239–4253). İşleyici düzeyi, gerçek Mongo, `MeetingSeriesSweepEndToEndMongoTests` biçimi: uç yoktur, iş `MeetingSeriesSweepJob.cs:81`'den koşar. Altı satır: attack (zincirsiz, kaynaktan yok, bir uyarı, test günlükleyici), legitimate hand-over (iki olay, ikincisi zincirli), true organizer (bugünkü gibi; düzenleyen ve seri katılımcıları dışında satır yok, satır 4276–4277), collision (`failed`, yazma yok, işaretçi kalır, sonraki olay aynı kaynaktan yine zincirlenir, satır 4252–4253), bağlantısız iptal ya da tamamlanmış takip çarpışması (`failed`, satır 4469–4470), zincirsiz yedek (oluşturma başarısızsa `failed` ve işaretçi kalır; son toplantısı silinmiş seri zincirsiz sürer, satır 4587–4589, 4605).
- **S28-6 satırları** (satır 4167–4171). `read-all` tutan ama görevi okuyamayan: ekranda ve dışa aktarımda maskeli. Güncel toplantıyı göremeyen: yer tutucu ve null kimlik, yanıtta ve dışa aktarımda; CSV hücresi boş, JSON `null`. Görebilen kimliği alır (`MeetingReportMongoTests.cs:227` yeşil kalır). Yedi yerelde iki yer tutucu o dilin metni, ham kod yok. Bu satırlar gerçek okuma kuralıyla koşar (`RealTaskReadRule.Over`, `MeetingTaskReadRuleTests.cs:48`); taklit kural kullanılmaz.
- **S28-7 satırı** (satır 4172–4175). Görev Merkezi izdüşümü, bağlı toplantının parçası olmayan görev sahibi: toplantı satırı yok. Gerçek çözümleyici (`RelatedRecordsProjectionTests.cs` deseni). Aynı kurulumda toplantıyı gören kullanıcı satırı görür. Görev Merkezi izdüşümü sağlayıcı düzeyinde kalırsa ve konak üzerinden uçtan ölçülmezse bunu Known gaps'e yaz.
- **Sabotaj koşusu (K-17; paket satır 4646–4706). Her madde için sıra:** dokunulan dosyaların `Get-FileHash -Algorithm SHA256` değeri → sabotaj → `dotnet build` → beklenen satırın filtresiyle koşu, kırmızı → elle geri al → özet aynı → aynı filtre, yeşil. Yalnız İzinli yollarda. Korunan yola geçici de yazılmaz. `git checkout` ile geri alınmaz. Bir madde kırmızı olmuyorsa: satır bu prompt'ta yazıldıysa düzelt; P1–P3 satırıysa dokunma, DUR ve raporla. Satır gerçekten kıramıyorsa DUR.

  | # | Paket satırı | Sabotaj | Kırmızı olması gereken |
  |---|---|---|---|
  | 1 | 4650 | `AddMeetingAttendeesHandler`'dan çağrıyı kaldır | matris satırı, S28-2 koruması |
  | 2 | 4652 | `UpdateMeetingHandler`'da kontrolü `CheckEditable` sonrasına al | iptal toplantı satırı |
  | 3 | 4653 | takip işleyicisinin reddi `MEETING_NOT_FOUND` döner | onun gövde eşitliği satırı |
  | 4 | 4654 | tekrarda var olan toplantıyı herkese döndür | create replay satırı |
  | 5 | 4655 | yaratıcı kuralını atla | creator rule satırı |
  | 6 | 4656 | `MeetingsController.cs:42`'den `RequireCreatorCanView: false` geçir | tarama, creator rule satırı |
  | 7 | 4658 | tamamlamadan `FollowUpOfMeetingId` = kaynak koşulunu kaldır | "can view, not a follow-up" satırı |
  | 8 | 4660 | taşımada satır düzeyi atlamayı kaldır | carry cut off satırı |
  | 9 | 4662 | bağlantıyı yeniden taşımadan önce yaz | carry cut off satırı |
  | 10 | 4664 | taşınan satır tekil dizinini kaldır | two completions satırı |
  | 11 | 4665 | tamamlamadan yaşam döngüsü kontrolünü kaldır | iptal ve tamamlanmış tamamlama satırları |
  | 12 | 4667 | zamanlayanı gönderilen listeden çıkar | Q25 satırı |
  | 13 | 4669 | `PreAcceptedUserId`'yi kaldır | Q25 satırı |
  | 14 | 4671 | kaynak kontrolünü geçen herkesi ekle | `read-all` taraf olmayan satırı |
  | 15 | 4673 | işaretleyici altında da çağıranı ekle | işaretleyici birim satırı |
  | 16 | 4675 | `PreAcceptedUserId`'yi ikinci dosyada ayarla | `PreAcceptedUserId` taraması |
  | 17 | 4676 | `FollowUpOfMeetingId` üzerinde `CanView`'ı atla | onun satırı |
  | 18 | 4677 | işaretleyiciyi ikinci dosyada oluştur | S28-4 koruması |
  | 19 | 4678 | işaretleyiciye koşulsuz geçiş ver | series attack satırı |
  | 20 | 4680 | görünmeyen kaynağı yeniden `failed` say | hand-over satırı |
  | 21 | 4682 | taramada zincir kaynağı kontrolünü atla | "source not visible" birim satırı |
  | 22 | 4684 | zincirsiz yedek `FollowUpOfMeetingId` yazsın ya da satır taşısın | series attack satırı |
  | 23 | 4685 | rapor görev maskesini kaldır | S28-6 |
  | 24 | 4686 | güncel toplantı başlığını yine bütün toplantılardan kur | S28-6 güncel toplantı testi |
  | 25 | 4687 | maskeli satırda `CurrentMeetingId`'yi tut | S28-6 dışa aktarım testi |
  | 26 | 4688 | ham kodu dışa aktarıma yaz | S28-6 yerel testi |
  | 27 | 4689 | çözümleyici `CanView`'ı atlasın | S28-7 |
  | 28 | 4690 | her takip çağıranı için `RequireCreatorCanView: false` geçir | taraf olmayan creator rule birim satırı |
  | 29 | 4692 | bağlantı erken dönüşünden hedef kontrolünü kaldır | idempotency-link target satırı |
  | 30 | 4694 | yalnız katılımcı satırını taraf say | organizer branch satırı |
  | 31 | 4696 | kendini anan taraf olmayana da `PreAcceptedUserId` ver | non-party self-listed satırı |
  | 32a | 4698 | yaşam döngüsü kontrolünü bağlantı kontrolünün önüne al | "link exists" satırları |
  | 32b | 4698 | yaşam döngüsü kontrolünü `FollowUpOfMeetingId` kontrolünün önüne al | cancelled non-follow-up satırı |
  | 33 | 4701 | süzgeç dışındaki zamanlayanın satırını yine yaz | eligibility satırı |
  | 34 | 4703 | `CreateMeetingRequest`'e `PreAcceptedUserId` ekle ve geçir | HTTP gövde testi, yansıma testi |

  Her satırın sonucu rapor tablosuna: sabotaj, kırmızı olan test adları, özet önce ve sonra, yeşil dönüş.
- **E5 kanıt paketi.** Kanıt seviyesi E5, DL-28'in tanımıyla: matris HTTP ve gerçek Mongo; seri satırları işleyici düzeyinde gerçek Mongo; yarıda kesilme ve iki tamamlama işleyici düzeyinde gerçek Mongo (P3); MOD-0024 okuma kuralı ve Görev Merkezi izdüşümü. Bu seviye için ayrıca: düzenleme bittikten sonra, sabotajlar geri alınmışken DOĞRULA 1–5 tek seferde koşulur, TRX ve `known_failures.py` çıktıları rapora girer. Kabul koşusu (`scripts/run_record.py run --out docs/records/audits/…`) temiz ağaçta CT'nindir; işçi `docs/records/**`'a yazmaz.
- **Test planı dondurma.** Seçenek (a): yedek kapsam `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**` içindeki kurulum onarımlarıdır (çözümleyici ve rapor kurucuları); üst sınır bu klasördür. Başka her durumda ilk hatada dur.
- **Teslim.** Push ve PR yok. Bu prompt sahibin onayıyla dağıtılırsa commit adımı commit onayıdır: DOĞRULA temiz; işçi dalında tek yerel commit, açık yollarla; `git diff --cached --name-only`, `--stat`, `--check` rapora; tam içerik `git diff --cached` ile gözden geçirilir. Mesaj: `feat(mg): MOD-0357 S28 P4 — report and export masking, related record drop, write matrix, sabotage evidence (E5)`. Commit'li ağaçta DOĞRULA 2 ve 8'i bir kez daha koş. CT'ye bildir ve dur (git-safety §7). S28-9 kaydı, PR ve kabul CT'nindir.
- **Satır sonu.** Rapor ve kayıtlar LF. Resx dosyalarının mevcut satır sonu korunur. Yorum ve günlük metinleri İngilizce.

YAPMA:
- `Features/Tasks/**` değişmez; MOD-0024 yalnız `ITaskReadAccessPolicy` ve `TaskLinks.InaccessibleTitleCode` ile kullanılır.
- Yeni izin anahtarı yok.
- S28'de yeni denetim olayı yok. `IDataExportAuditWriter` satırı bugünkü gibi kalır; `tests/architecture/audit-ledger/**` değişmez (§25).
- Paket, `gateway/Diten.ApiGateway/ocelot.json`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, Archive ve `.antigravity/**` değişmez.
- `main`'e commit ya da push yok. Yalnız açık yolla stage (`git add -A` ve `git add .` yok). Yıkıcı git yok: `reset --hard`, `clean`, `git checkout -- <yol>`, force push, `stash`.
- Port, kimlik ya da tanımlayıcı uydurma. K-19 dışında sözleşme adı gerekiyorsa DUR.
- Başka modülde düzeltme gerekirse dokunma: DUR ve sor (XMC-001). `Currency_minor_units_covers_every_code_the_currencies_lookup_returns` ve `tests/known-failures.tsv` değişmez.
- K-16 ikinci grubundaki dosyalarda kalıcı değişiklik bırakma. Sabotajlı dosyayla commit atma.
- Ham kodu ekrana ya da dosyaya yazma. Boş dizeyle maskeleme: boş hücre "güncel toplantı yok" okunur, yanlıştır (satır 4158–4159).
- `Lifecycle` ya da `IsOverdue`'yu maskeleme. Satır çıkarma: satır okuyucunun görebildiği toplantıya aittir.
- Çözümleyicide başlığı maskeleyip rotayı bırakma; tek tek sorgu atma.
- `Features/WorkAggregation/**`, `WorkCenterNext/**`, sayfa kabuğu ve ortak JS değiştirme.
- Sabotajı atlama, "benzer" sayma ya da toplu kırıp tek koşuda değerlendirme.
- Mevcut test adlarını değiştirme, `Skip` etme, iddia zayıflatma.
- `docs/records/**`, `execution/**`, kuyruk ve BL-541 satırına yazma: S28-9 kaydı CT'nindir.

DOĞRULA: Komutlar çalışma ağacı kökünde, Windows PowerShell'de. Her ağır komuttan önce makine yükü ölçülür. `T = services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj`, `R = $env:TEMP\mg-s28-p4`. Gerekli kanıt E5.
1. Kademe 1: `dotnet build T -c Debug` → 0 hata; `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` → 0 hata; uyarılar tabana göre yazılır.
2. Kademe 2: `dotnet test T -c Debug --no-build --filter "FullyQualifiedName~Diten.Platform.Application.Tests.Meetings" --results-directory R --logger "trx;LogFileName=meetings.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\meetings.trx --suite Diten.Platform.Application.Tests` → çıkış 0, 0 kırmızı.
3. Kademe 3, proje: `dotnet test T -c Debug --no-build --results-directory R --logger "trx;LogFileName=platform-all.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\platform-all.trx --suite Diten.Platform.Application.Tests` → çıkış 0 ya da yalnız K-20'deki tek ad nedeniyle çıkış 1. Başka YENİ, YENİ-AYRINTI ya da çıkış 2 DUR.
4. Kademe 3, mimari: `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests -c Debug --results-directory R --logger "trx;LogFileName=arch.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\arch.trx --suite TenantArchitecture.ArchitectureTests` → çıkış 0; yalnız kayıtlı arızalar, parmak izi aynı. Kapsayıcı denetimi: `dotnet test services/Diten.Platform/tests/Diten.Platform.BackgroundJobs.Tests/Diten.Platform.BackgroundJobs.Tests.csproj -c Debug --filter "FullyQualifiedName~PlatformContainerValidationTests" --results-directory R --logger "trx;LogFileName=container.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\container.trx --suite Diten.Platform.BackgroundJobs.Tests` → çıkış 0 (`MeetingRelatedRecordResolver` ve rapor işleyicilerinin yeni bağımlılıkları).
5. Ekran ve köprü: `Push-Location frontend/Diten.Web; npx vitest run tests/meeting-report-screen.test.js tests/meeting-report-l10n-parity.test.js tests/meeting-report-markup-contract.test.js tests/meeting-report-export-disclaimer-consistency.test.js tests/meeting-report-export-placeholders-consistency.test.js tests/meetings-reason-code-bridge.test.js tests/task-not-accessible-sentence.test.js; Pop-Location` → 0 kırmızı.
6. Doğrulayıcı (yalnız bilgi; önce ve sonra): `py -3 -I -B -X utf8 .antigravity/scripts/verify_datatable_page.py . --area Meetings --module Report --reference compact --format gaps` → sonra yeni madde yok. Araç bu sayfada koşmuyorsa çıktıyı raporla, üretmeye çalışma.
7. Sabotaj: tablodaki 35 koşunun hepsi; her biri kırmızı sonra yeşil.
8. Kapsam ölçütleri, çıktıları rapora: `git diff --stat <P3 commit>` yalnız ilk grup yollar; `git ls-files --others --exclude-standard` yalnız ilk grup yollar. `git diff <P3 commit> -- services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs services/Diten.Platform/src/Diten.Platform.API services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs` boş (K-16). `git diff <P3 commit> -- services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation gateway tests/architecture execution .antigravity frontend/Diten.Web/Views/Shared` boş. `git diff --check` temiz.

Durma koşulları:
- Worktree HEAD P3 commit'i değil, dal farklı, ya da başlangıçta kirli dosya var.
- CT'nin dağıtım mesajında P3'ün ortam başına sayım tablosu yok, bir ortamda `collectionExists=false` ya da mükerrer grup sayısı sıfırdan büyük.
- `ITaskReadAccessPolicy` istenen toplu okumayı `Features/Tasks/**` değişmeden veremiyor.
- `MeetingRelatedRecordResolver` kullanıcısız bir yoldan çağrılıyor.
- Matris satırı için uç, anahtar ya da fikstür paketle çözülemiyor; 15 işleyiciden biri beklenen 404 gövdesini vermiyor: bu P1 hatasıdır, ikinci grup dosyada kalıcı düzeltme bu prompt'ta yapılmaz, raporla.
- Gövde eşitliğini bozan, sabitlenemeyen istek başına değer var.
- Bir sabotaj satırı kırmızı olmuyor ve satır düzeltilerek kırılamıyor.
- Geri alınan dosyanın özeti ilk değerle aynı değil.
- Korunan yola ya da ikinci grup dışındaki üretim dosyasına sabotaj gerekiyor.
- Çeviri belirsiz ya da bir dil İngilizce kopya kalacak.
- `known_failures.py` çıkış 2, ya da K-20 dışında YENİ veya YENİ-AYRINTI.
- Başka modülde düzeltme gerekiyor (XMC-001).
- 15 dakika boyunca işlemci yükü 80'in altına inmiyor.

Rapor formatı: SOP §22 yapılandırılmış rapor.
- Başlıklar: Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes.
- Tests: taban ve son sayılar; işleyici × rol tablosu; satırlar için dosya:satır; 35 koşuluk sabotaj tablosu; TRX yolları; bütün `known_failures.py` çıktıları; K-20 durumu adıyla.
- Audit/Evidence: E5 paketi ve her parçanın koşu çıktısı. "E5 tamam" iddia edilmez, kanıt listelenir; kabul CT'nindir.
- Decisions: test yerleşimi, l10n-agent'ın değiştirdiği metinler. Sahip ve CT kararları buraya yazılmaz, yalnız anılır.
- Known gaps, en az: S28-9 kaydı CT'de (paket satır 4313–4314); S28'den önceki yarım taşıma onarılmaz (satır 4459–4461); S28-3'ün gizli kuralı, S15 HTTP satırları ve `IsPlatformActor` daraltması S15'te; üç yazma değişikliği denetim olayı olmadan ertelendi, S25 ve S21 taşır (§25); `ScheduleReviewMeetingForTaskHandler` tekrar dalı için pakette ayrı satır yok; `Currency_minor_units_covers_every_code_the_currencies_lookup_returns` kayıtsız ve bu işin değil; staging ve canlı dizin kurulumu dağıtım adımıdır, burada ölçülmez.
- Her iddia için yol:satır ya da komut çıktısı verilir. Ölçmediğini yaz.

Senin PASS'in kapanış değildir; kabul CT'nindir.
