@[.antigravity/agents/backend-architect.md]
WP: MG-MEET-S28 · Prompt P1 v0.1
Repository: C:\dev\ERP-vNext · Branch: chore/ct/mg-ct-01 · Expected HEAD: eb561752267ae5f7f5f573a43e451bdd0c9073bc · Worktree: C:/dev/ERP-vNext/.claude/worktrees/mg-ct-01
Risk: HIGH · §9.1 sinyalleri: yetki, kiracı, sözleşme
Görev sınıfı: ürün · Paket: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingSystemOrigin.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/AgendaItemCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MinutesCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingTaskBridgeCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**
Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/Archive/**, frontend/Diten.Web/**, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv
Kapsanan dilim: DL-28

> **TASLAK v0.1 — DAĞITIMA HAZIR DEĞİL. Bu metin kod, commit ya da dağıtım izni değildir.**
> - Kuyruk satırı `INSPECTED`, `READY` değil. Kayıt satırı `origin/main`'de değil. HIGH inceleme kaydı yok.
> - Repository, Branch, Expected HEAD ve Worktree, kapının iskeletinden gelir: CT dalının bugünkü ucudur. İşçi klasörü ve dalı açılmadı. Dağıtımda CT bu alanları işçi klasörü, işçi dalı ve ölçülen HEAD ile yeniden yazar, kapıyı yeniden koşar.
> - Parça S28 tek dal, tek birleştirme isteğidir (DL-28). Dört prompt ara adımdır; ara durum ana dala gitmez. Bu prompt'un sonunda oluşturma yolunun geri kalanı (P2) ve takip toplantısı (P3) henüz eksiktir.
>
> **CT okumaları (paket yetkisi altında; paket değişmez):**
> - **K-0 — "draft" sözcüğü.** Paket §24 üst notu ve §24.31 satır 3892 "draft / not approved" der. Paket düzenlenmez: özet değişir, onay düşer. Aynı not bağlayıcı olanın onay satırı olduğunu söyler (satır 1147–1148). İşe başlarken `py -3 -I -B -X utf8 scripts/pack_approval.py check execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` koşulur; çıkış 0 değilse kod yazılmaz, DUR.
> - **K-1 — işaretleyicinin adı.** Paket adını vermez (§24.31 "New system-origin marker type"). Bu işte ad sabittir: dosya `Features/Meetings/Services/MeetingSystemOrigin.cs`, tek public tip `MeetingSystemOrigin`, üye `ActingUserId` (`Guid`). Komutlardaki parametre adı `SystemOrigin`. Üyeye `Id` adı verilmez: S28-2 taraması komutun parametre tiplerinin üye adlarına bakar.
> - **K-2 — `FollowUpOfMeetingId` kontrolü bu prompt'ta.** S28-2'ye göre `CreateMeetingHandler` tarama kapsamındadır (istek modelinde `FollowUpOfMeetingId` var) ve 5 sınıflık istisna listesinde değildir. Koruma bu prompt'ta yazılınca bu sınıfın kendi gövdesinde `MeetingWriteAccess` çağrısı olmak zorundadır. Bu yüzden S28-10'un `FollowUpOfMeetingId` maddesi (paket satır 4567–4570) burada yapılır. Oluşturma yolunun geri kalanı P2'dedir.
> - **K-3 — kurulum sayısı.** Paket "14 dosya, yaklaşık 40" der. Ölçüm (eb561752, Grep): **14 dosya, 53 kurulum** = 39 açık `new …Handler(` ve 14 hedef tipli `new(`. `RespondToInvitationHandler` kurulucusu değişmez ve sayılmaz. Liste NASIL'dadır; atlanan kurulum varsa derleme hatası gösterir.
> - **K-4 — DI satırı.** §24.31 "Files" `DependencyInjection.cs`'i anmaz; `MeetingWriteAccess` kayıt ister. Paket §5 satır 321 bu dosyada ek satıra izin verir. Yalnız bir `AddScoped` satırı eklenir.

Önce oku (sırayla):
1. execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md (§24.31 tamamı, satır 3890–4731; §24.7 satır 1539; §25 satır 4734–4812; §26 DL-28 satır 4865; §6 satır 350–372)
2. AGENTS.md
3. execution/domains/management-governance/domain-config.md
4. docs/records/audits/2026-10/mg-meetings-discovery-01/RECORD.md
5. docs/records/audits/2026-10/mg-s28-inspection-01/INSPECTION-NOTE.md
6. .antigravity/rules/multi-tenancy.md
7. .antigravity/rules/security-jwt.md
8. .antigravity/rules/permission-key-standard.md
9. .antigravity/rules/business-module-enforcement-standard.md
10. .antigravity/rules/handler-design.md
11. .antigravity/rules/repository-standard.md
12. .antigravity/rules/response-envelope.md
13. .antigravity/rules/pipeline-behaviors.md
14. .antigravity/rules/entity-versioning.md
15. .antigravity/rules/api-conventions.md
16. .antigravity/rules/audit-trail-standard.md
17. .antigravity/rules/logging-observability.md
18. .antigravity/rules/configuration-safety.md
19. .antigravity/rules/code-style.md
20. .antigravity/rules/git-safety.md
21. .antigravity/rules/docs-organization.md
22. .antigravity/rules/cross-module-change-consent.md
23. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs
24. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/AgendaItemCommandHandlers.cs
25. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MinutesCommandHandlers.cs
26. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs
27. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingTaskBridgeCommandHandlers.cs
28. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/GenerateDueMeetingSeriesHandler.cs
29. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/QueryHandlers/MeetingQueryHandlers.cs (yalnız :50–57, görünürlük emsali)
30. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs
31. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/RecordLinkWriteGuardSourceTests.cs (kaynak taraması emsali)
32. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/GenerateDueMeetingSeriesHandlerTests.cs
33. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingS2TestDoubles.cs
34. services/Diten.Platform/tests/Shared/PlatformTestMongo.cs (gerçek Mongo seçimi)
35. docs/guides/operations/control-tower-operating-card.md
36. tests/known-failures.tsv

Sözleşme yüzeyleri:
- API: «S28-1 — These write handlers call `MeetingWriteAccess` **immediately after they load the meeting and before any» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4033), «**that handler's own not-found response**, exactly as for an id that does not exist: the same status, reason» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4038)
- API: «`MEETING_ATTENDEE_NOT_FOUND` "This meeting has no invitation for you.", so they never learn that the meeting» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4071), «`MEETING_FOLLOW_UP_NOT_FOUND`, "The meeting this follows up on does not exist.", `:128-129`), and nothing is» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4569)
- API: «public static bool CanView(Meeting meeting, Guid callerUserId, bool hasReadAll, IReadOnlySet<Guid> attendeeUserIds)» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs:63), «public sealed record CreateMeetingCommand(CreateMeetingRequest Request, string CorrelationId)» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs:6)
- İzin: «S28-5 — The existing permission keys still apply first. A caller without the key gets 403 from the attribute,» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4143), «var hasReadAll = _permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll);» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/QueryHandlers/MeetingQueryHandlers.cs:51), «marker is a command parameter, never on an HTTP request model, and never inferred from an empty user id. A» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4140)
- Test: «1. finds the handler classes and their command types by **reflection** on the Application assembly (every» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4117), «2. parses every `.cs` file in that folder with the **Roslyn syntax tree** (`CSharpSyntaxTree.ParseText`), takes» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4121), «**source visible** → `ScheduleFollowUpMeetingCommand` is sent with that source, and `CreateMeetingCommand` is» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4013), «**source not visible** (another organizer, and no attendee row for the series organizer) →» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4015)
- Test: «Assert.Equal(MeetingReasonCodes.AttendeeNotFound, response.ReasonCode);» (services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingInvitationTests.cs:120), «Assert.Equal(MeetingReasonCodes.AttendeeNotFound, refused.ReasonCode);» (services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingTwoUserFlowMongoTests.cs:120)
- Sabotaj: «remove the helper call from `AddMeetingAttendeesHandler` only (`UpdateMeetingHandler` in the same file still calls» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4650), «create the marker in a second file → the S28-4 guard goes red;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4677), «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

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

Hedef ajan giriş alanları (§17.4, backend-architect):
- **Hedef servis ve proje:** Diten.Platform · `services/Diten.Platform/src/Diten.Platform.Application` (Features/Meetings); test projesi `services/Diten.Platform/tests/Diten.Platform.Application.Tests`.
- **Aggregate ve alan:** `Meeting`, `MeetingAttendee`. Yeni alan yok. Yeni kalıcı veri yok.
- **Komutlar:** `CreateMeetingCommand` ve `ScheduleFollowUpMeetingCommand` isteğe bağlı son parametre `SystemOrigin` alır. Yeni komut yok.
- **Doğrulayıcı:** Yeni istek alanı olmadığı için yeni doğrulayıcı yok; mevcut doğrulayıcılar değişmez.
- **İzin anahtarları:** Yeni anahtar yok. Var olan: `platform.meetings.create`, `.update`, `.read-all`, `.series-manage`, `.types-manage`.
- **Yanıt:** `Response<T>` ve `CustomBaseController` değişmez; her işleyici kendi bulunamadı gövdesiyle yanıt verir.

NE: MOD-0357 Toplantılar, S28 P1. Yazma anahtarı olan ama toplantıyı göremeyen kullanıcı, S28-1 listesindeki 15 işleyicide o işleyicinin kendi 404 gövdesini alır. İşler:
- `MeetingWriteAccess` (tek public tip) ve `MeetingSystemOrigin` (K-1).
- S28-1'in 15 işleyicisi: yüklemeden hemen sonra kontrol, 409 ve 400'lerden önce.
- `CreateMeetingHandler`: yalnız `FollowUpOfMeetingId` kontrolü (K-2).
- `RespondToInvitationHandler`: katılımcı satırı kontrolü yüklemeden hemen sonraya alınır.
- Tarama: işaretleyici (S28-4) ve zincirsiz yedek (S28-11), 2 yeni birim satırı.
- 14 test dosyasında 53 kurulum (K-3) ve çağıranı ilişkisiz kalan fikstürler.
- Roslyn sınıf düzeyi koruma (S28-2), 5 sınıflık istisna listesi; işaretleyici koruması (S28-4).

Bu prompt'ta yok: yaratıcı kuralı, tekrar dalı, 3 neden kodu (P2); takip toplantısının tamamlama ve Q25 kuralları, tekil dizin (P3); rapor, çözümleyici, HTTP matrisi, bayt eşitliği, 34 sabotajın resmi koşusu (P4).

NEDEN:
- Paket §24.31, "Why first": bugün `platform.meetings.update` tutan ve toplantı kimliğini bilen biri parçası olmadığı toplantıyı değiştirebilir, kendini ekleyebilir ve toplantıyı okuyabilir. Sıra 1, bağımlılık yok (§24.7).
- `MeetingEligibility.CanView` yalnız sorgu işleyicilerinden çağrılır (§24.31 satır 3901–3903).
- Bir dosyada birden çok işleyici olduğu için koruma dosya değil sınıf düzeyindedir (S28-2).
- Sahip kuyruk onayı `kuyruk-onay` (2026-10-31); kod izni değildir.

NASIL:
- **Makine yükü.** Her `dotnet build`, `dotnet test` ve restore komutundan önce ölç: `(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average`. 80'in üstündeyse 60 saniye bekle, yeniden ölç. Çözüm geneli `dotnet test` yok.
- **Ortam ön kontrolü (bir kez, test bütçesinden önce; SOP §20.3).**
  - `git status --short` boş; `git rev-parse HEAD` Expected HEAD; dal doğru.
  - `$env:DITEN_TEST_MONGOD = 'C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe'` ve `Test-Path` bu yol için doğru. Docker yok. Testler kendi tek düğümlü replika kümesini başlatır (`PlatformTestMongo.cs`, kural 2). Bu makinede Toplantılar testleri daha önce koşmadı (inceleme notu "Ölçülmeyen").
- **Taban çizgisi (hiçbir dosya değişmeden).** DOĞRULA 1 ve 2'yi koş, TRX'i sakla, `known_failures.py` sonucunu yaz. Taban kırmızıysa kod yazma, DUR.
- **`MeetingWriteAccess`** (`Features/Meetings/Services/MeetingWriteAccess.cs`).
  - Tek public üst düzey tip, `sealed class`. Arayüz yok. Kurucu: `IMeetingRepository`, `IMeetingAttendeeRepository`, `ICurrentUserContext`, `IActorPermissionContext`.
  - Toplantıyı ve katılımcı kimliklerini yükler, `MeetingEligibility.CanView` uygular. Ayrı bir görünürlük kopyası yazılmaz.
  - `hasReadAll`: `_permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll)` (S28-5).
  - `MeetingSystemOrigin` verilirse `CanView`, `ActingUserId` ve `hasReadAll: false` ile uygulanır; `read-all` hiç sorulmaz (S28-4). Boş kullanıcı kimliğinden işaretleyici çıkarılmaz.
  - Cevap iki türlüdür: "görünür" (yüklenen `Meeting` ve katılımcı kimlikleri) ya da "görünmez" (yok, silinmiş, başka kiracı dahil). Yanıt gövdesi kurmaz. Katılımcı kimlikleri P2 ve P3'te taraf kuralı için aynı yüklemeden okunur; ikinci sorgu gerekmez.
  - Tek public tip kuralını bozmadan cevap iç içe tip ya da demet olur. Yöntem adı senindir; Decisions'a yaz.
  - DI: `DependencyInjection.cs` içinde satır 410–411'deki Meetings bloğuna `AddScoped` (K-4).
- **`MeetingSystemOrigin`** (K-1). Komut parametresi olarak taşınır. HTTP istek modeline konmaz. Yalnız `GenerateDueMeetingSeriesHandler.cs` oluşturur; `ScheduleFollowUpMeetingHandler` aldığını aynen `CreateMeetingCommand`'a iletir.
- **Komutlar** (`MeetingCommands.cs`). `CreateMeetingCommand` ve `ScheduleFollowUpMeetingCommand`'a `MeetingSystemOrigin? SystemOrigin = null` son parametresi. Çağrılarda ad ile geçilir. Mevcut dört `new CreateMeetingCommand(…)` kurulumu (`MeetingsController.cs:42`, `GenerateDueMeetingSeriesHandler.cs:160`, `MeetingFollowUpCommandHandlers.cs:92`, `MeetingTaskBridgeCommandHandlers.cs:468`) açık ve iki konumsal argümanlı kalır (paket satır 4518–4523); hedef tipli `new(…)` yazılmaz.
- **15 işleyici** (S28-1; satırlar eb561752). Her biri mevcut `GetByIdAsync` yüklemesini `MeetingWriteAccess` ile değiştirir, "görünmez" cevabında aynı bulunamadı gövdesini döner. Gövde metinlerine dokunulmaz.

  | Sınıf | Dosya | Yükleme | Kendi 404 gövdesi | Bundan önce |
  |---|---|---|---|---|
  | `UpdateMeetingHandler` | MeetingCommandHandlers.cs | :231 | :234 | :237 |
  | `CancelMeetingHandler` | MeetingCommandHandlers.cs | :319 | :322 | :325 |
  | `ReassignMeetingOrganizerHandler` | MeetingCommandHandlers.cs | :383 | :386 | :390 |
  | `AddMeetingAttendeesHandler` | MeetingCommandHandlers.cs | :453 | :456–457 | :460 |
  | `RemoveMeetingAttendeeHandler` | MeetingCommandHandlers.cs | :530 | :533 | :536 |
  | `AddAgendaItemHandler` | AgendaItemCommandHandlers.cs | :32 | :35 | :38 |
  | `UpdateAgendaItemHandler` | AgendaItemCommandHandlers.cs | :77 | :80 | :83 |
  | `DeleteAgendaItemHandler` | AgendaItemCommandHandlers.cs | :120 | :123 | :126 |
  | `ReorderAgendaHandler` | AgendaItemCommandHandlers.cs | :156 | :159 | :162 |
  | `SaveMinutesDraftHandler` | MinutesCommandHandlers.cs | :129 | :132–133 | :139 |
  | `PublishMinutesHandler` | MinutesCommandHandlers.cs | :247 | :250–251 | :256 |
  | `CorrectPublishedMinutesHandler` | MinutesCommandHandlers.cs | :402 | :405–406 | :410 |
  | `ScheduleFollowUpMeetingHandler` (kaynak) | MeetingFollowUpCommandHandlers.cs | :57 | :60–61, `MEETING_FOLLOW_UP_NOT_FOUND` | :68 |
  | `CreateTaskFromMeetingHandler` | MeetingTaskBridgeCommandHandlers.cs | :136 | :139–140 | :145 |
  | `LinkExistingTaskHandler` | MeetingTaskBridgeCommandHandlers.cs | :311 | :314–315 | :324 |

  - `ScheduleFollowUpMeetingHandler`'da bu prompt'ta yalnız kaynak kontrolü ve işaretleyiciyi iletme yapılır. Bağlantı hedefi, taşıma sırası ve tamamlama P3'tedir.
  - S27'nin `SetMeetingAttendeeRoleHandler` sınıfı bugün yoktur; yazılmaz.
- **`CreateMeetingHandler`** (K-2). `:123–131` bloğunda `FollowUpOfMeetingId` toplantısı `MeetingWriteAccess` ile yüklenir (`command.SystemOrigin` ile). Görünmezse kendi gövdesi: 404 `MEETING_FOLLOW_UP_NOT_FOUND`, "The meeting this follows up on does not exist." (`:128–129`). Yazma olmaz. Başka değişiklik yok: yaratıcı kuralı, tekrar dalı ve 200/409 P2'dedir.
- **`RespondToInvitationHandler`** (S28-1, istisna; `MeetingWriteAccess` kullanmaz, kurucusu değişmez). Katılımcı satırı aranması (`:623–628`) yüklemeden hemen sonraya, 409 durum kontrolünden (`:603–607`) ve 400 yanıt kontrolünden (`:616–620`) önceye alınır. Satırı olmayan çağırana eksik toplantı gövdesi: 404 `MEETING_NOT_FOUND`, "The meeting does not exist." (`:598`).
- **Tarama** (`GenerateDueMeetingSeriesHandler.cs`; S28-4, S28-11; paket satır 4572–4599).
  - Kurucu `MeetingWriteAccess` alır. Her seri için `MeetingSystemOrigin(series.OrganizerUserId)` oluşturur; `CreateMeetingCommand` ve `ScheduleFollowUpMeetingCommand` ile ad vererek gönderir.
  - Zincirlemeden önce (`:147` sonrası) `LastGeneratedMeetingId` kaynağını işaretleyiciyle kontrol eder. Seri katılımcıları sayılmaz.
  - Görünür: bugünkü gibi takip komutu. Görünmez (yok dahil): `:149–161`'deki düz `CreateMeetingCommand`, `FollowUpOfMeetingId` null. Kaynaktan görünürlük cevabı dışında hiçbir şey okunmaz, kaynağa yazılmaz.
  - Başarılıysa `LastGeneratedMeetingId` yeni toplantıya geçer, sayaç `generated`. Her böyle olayda bir uyarı, birebir: `meetings.series.chain_broken SeriesId={SeriesId} ProcessInstanceId={ProcessInstanceId} PreviousMeetingId={PreviousMeetingId} MeetingId={MeetingId} CorrelationId={CorrelationId}` (başlık yok).
  - Düz oluşturma başarısızsa mevcut `failed` yolu (`:93–103`) geçerlidir; işaretçi kalır. `UpdateMeetingSeriesHandler` değişmez (S28-11).
- **Test kurulumları (K-3; 14 dosya, 53 kurulum, eb561752).**
  - `AgendaItemCommandHandlerTests.cs`: :32, :49, :81, :85, :98, :102, :116, :119, :133, :136.
  - `GenerateDueMeetingSeriesHandlerTests.cs`: :47.
  - `MeetingCommandHandlerTests.cs`: :39, :41, :43, :45 (`=> new(`), :158.
  - `MeetingFollowUpCommandHandlerTests.cs`: :427.
  - `MeetingHttpTests.cs`: :222, :230, :233.
  - `MeetingInvitationTests.cs`: :35, :38 (`=> new(`), :299.
  - `MeetingReadVersionRaceMongoTests.cs`: :118, :121, :128, :131, :134 (`=> new(`), :350, :352, :399.
  - `MeetingSeriesSweepEndToEndMongoTests.cs`: :206, :208, :218.
  - `MeetingTaskBridgeCommandHandlerTests.cs`: :473, :492.
  - `MeetingTaskBridgeHttpTests.cs`: :219, :224.
  - `MeetingTaskReadRuleTests.cs`: :80, :164.
  - `MeetingTwoUserFlowMongoTests.cs`: :162, :196, :259, :287, :321, :348, :405, :425, :447.
  - `MinutesCommandHandlerTests.cs`: :78, :81, :84 (`=> new(`).
  - `MinutesDecisionTaskBridgeTests.cs`: :64.
- **Çağıranı ilişkisiz fikstürler (paket bunu saymaz; ölçüldü).** Yeni kontrol yalnız kurucuyu değil davranışı da değiştirir. Örnek: `AgendaItemCommandHandlerTests.cs:22` rastgele düzenleyen, `:25` rastgele çağıran kurar; yeni kuralla 404 alır. Bu testlerde çağıran gerçek ilişkiyle kurulur: düzenleyen ya da katılımcı. `read-all` yalnız test `read-all` hakkındaysa verilir. İddialar zayıflatılmaz. Her değişikliği önce / sonra raporla.
- **Değişen iki test** (S28-1). `MeetingInvitationTests.cs:119–120` ve `MeetingTwoUserFlowMongoTests.cs:119–120` artık 404 `MEETING_NOT_FOUND` bekler. Test adları değişmez.
- **Yeni testler** (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/`; adlar senin, rapora dosya:satır):
  - `MeetingWriteAccess` birim satırları: düzenleyen, katılımcı, `read-all`, platform aktörü (`FakeActorPermissionContext.IsPlatformActor`), ilişkisiz çağıran, olmayan kimlik; işaretleyiciyle eylem kimliği düzenleyen ya da katılımcı; işaretleyiciyle `read-all` izni olsa da ilişkisiz kimlik "görünmez" (S28-4).
  - `RespondToInvitation`: satırsız çağıran iptal toplantıda 404 `MEETING_NOT_FOUND`; satırı olan çağıran iptal toplantıda 409; satırı olan çağıran geçersiz yanıtta 400.
  - `FollowUpOfMeetingId`: görünmeyen toplantı 404 `MEETING_FOLLOW_UP_NOT_FOUND`, yazma yok. HTTP satırı P2'dedir.
  - `ScheduleFollowUp` kaynak: görünmeyen kaynak 404 `MEETING_FOLLOW_UP_NOT_FOUND`.
  - İki tarama birim satırı, `GenerateDueMeetingSeriesHandlerTests.cs` içinde, paket satır 4004–4021'e göre: `Build` `FakeMeetingRepository` ve `FakeMeetingAttendeeRepository` üzerinde `MeetingWriteAccess` kurar; `:71–88`'deki zincir testi kaynağı seri düzenleyeniyle tohumlar; `:90–102` değişmez. "Görünmez" satırı ayrıca bir test günlükleyiciyle `meetings.series.chain_broken` uyarısını ölçer.
- **S28-2 koruması (Roslyn).** `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingWriteAccessGuardSourceTests.cs`. Paket satır 4115–4127 birebir: yansıma ile sınıf ve kapsam; `CSharpSyntaxTree.ParseText` ile sınıfın kendi üyelerinde `MeetingWriteAccess` tipli alan, parametre ya da özellik üzerinden çağrı; kapsamda olup geçmeyen ve listede olmayan sınıf kırmızı; listede olup olmayan ya da kapsam dışı sınıf da kırmızı.
  - İstisna listesi tam beş sınıf, her biri gerekçeyle: `RespondToInvitationHandler`, `UpdateMeetingSeriesHandler`, `DeleteMeetingSeriesHandler`, `UpdateMeetingTypeHandler`, `DeleteMeetingTypeHandler`.
  - Ölçülen beklenti (eb561752): klasörde 25 işleyici sınıfı; kapsamda 21 (16 çağırır, 5 istisna); kapsam dışı 4 (`GenerateDueMeetingSeriesHandler`, `CreateMeetingSeriesHandler`, `ScheduleReviewMeetingForTaskHandler`, `CreateMeetingTypeHandler`). Sayıyı rapora yaz; testte sabitleme, S27 ve S19 ekleyecek.
  - Depo kökü `RecordLinkWriteGuardSourceTests` deseniyle bulunur. Karşılaştırmalar Ordinal (S-56).
- **S28-4 koruması.** Aynı dosyada ya da yanında: `services/Diten.Platform/src/` altında `new MeetingSystemOrigin(` nesne oluşturma ifadesi olan tam bir dosya vardır: `GenerateDueMeetingSeriesHandler.cs`. Tarama Roslyn sözdizimi ağacıyla yapılır; metin eşleşmesi kullanılmaz. Hedef tipli `new(…)` sözdizimiyle bulunamaz, bu yüzden üretim kodunda açık biçim kalır.
- **Test paketi.** `Diten.Platform.Application.Tests.csproj`'a yalnız `Microsoft.CodeAnalysis.CSharp`, sabit sürümle (merkezi paket dosyası yok; `:14–21` gibi). `net8.0` ile uyumlu en son kararlı sürümü seç; sürümü ve gerekçeyi Decisions'a yaz. Paket eklemeden önce ve sonra `dotnet build` süresini yaz (ortak test projesi; paket §24.31 satır 3997). `testing-agent.md:31` "yeni test paketi eklenmez" der; bu paket adıyla izinlidir, çünkü yetki sırasında paket üsttedir (AGENTS §1). Başka paket eklenmez. Üretim projesine paket eklenmez.
- **Erken sabotaj (yalnız yeni iki koruma; resmi koşu P4'tedir).** Yeni koruma altyapısı diğer üç prompt'un dayanağıdır: DOĞRULA 5.
- **Test planı dondurma (SOP §20.3).** Seçenek (a): yedek kapsam `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**` içindeki fikstür onarımlarıdır; üst sınır bu klasördür; bunun dışına çıkmak, iddia zayıflatmak ya da yeni test projesi koşmak gerekirse DUR.
- **Teslim.** Push ve PR yok. Bu prompt sahibin onayıyla dağıtılırsa aşağıdaki commit adımı commit onayıdır:
  1. DOĞRULA 1–6 temiz, sabotajlar geri alınmış.
  2. İşçi dalında tek yerel commit. Yalnız İzinli yollar, açık yolla `git add`. Önce `git diff --cached --name-only`, `git diff --cached --stat`, `git diff --cached --check` çıktılarını rapora koy; tam içeriği `git diff --cached` ile gözden geçir.
  3. Mesaj: `feat(mg): MOD-0357 S28 P1 — MeetingWriteAccess, write handlers guarded, sweep marker and unchained fallback, class-level guard`.
  4. CT'ye bildir ve dur (git-safety §7). P2 bu commit'in üstünden başlar.
- **Satır sonu.** Rapor ve kayıtlar LF (S-87). Yorum ve günlük metinleri İngilizce (code-style).

YAPMA:
- `Features/Tasks/**` değişmez. MOD-0024 yalnız var olan yollarla kullanılır.
- Yeni izin anahtarı yok.
- S28'de yeni denetim olayı yok. Komutlar `IAuditableCommand` kazanmaz; `tests/architecture/audit-ledger/**` değişmez (R1-D12, §25).
- Paket, `gateway/Diten.ApiGateway/ocelot.json`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, Archive ve `.antigravity/**` değişmez.
- `main`'e commit ya da push yok. Yalnız açık yolla stage (`git add -A` ve `git add .` yok). Yıkıcı git yok: `reset --hard`, `clean`, `git checkout -- <yol>`, force push, `stash`. Sabotaj geri alma elle yapılır.
- Port, kimlik ya da tanımlayıcı uydurma. K-1'deki adlar ve paketin adları dışında kamuya açık ad gerekiyorsa Decisions'a yaz; sözleşme adı gerekiyorsa DUR.
- Başka modülde düzeltme gerekirse dokunma: DUR ve sor (XMC-001). Ana daldaki iki kırmızı (`AuditTrailStandardTests`, `Currency_minor_units_covers_every_code_the_currencies_lookup_returns`) bu işin değildir.
- İşleyicilere ortak bulunamadı gövdesi koyma; `MeetingWriteAccess` yanıt kurmaz.
- `MeetingEligibility.CanView`'ın bir kopyasını yazma.
- İstisna listesine altıncı sınıf ekleme. Sabit `read-all` atlaması ya da "koşulsuz geçiş" işaretleyicisi yazma.
- İşaretleyiciyi `CreateMeetingRequest`, `ScheduleFollowUpMeetingRequest` ya da başka `…Request` tipine koyma; boş kullanıcı kimliğinden çıkarma.
- P2, P3, P4 işini bu prompt'ta yapma: tekrar dalı 200/409, yaratıcı kuralı, neden kodları, tamamlama, Q25, tekil dizin, rapor, çözümleyici.
- Test `Skip` etme, iddia zayıflatma, kırmızı testi silme.

DOĞRULA: Komutlar çalışma ağacı kökünde, Windows PowerShell'de. Her ağır komuttan önce makine yükü ölçülür. `T = services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj`, `R = $env:TEMP\mg-s28-p1`. Gerekli son kanıt E5, yalnız P4 sonunda; bu prompt E2 üretir.
1. Kademe 1: `dotnet build T -c Debug` → 0 hata; uyarı sayısı tabana göre yazılır.
2. Kademe 2: `dotnet test T -c Debug --no-build --filter "FullyQualifiedName~Diten.Platform.Application.Tests.Meetings" --results-directory R --logger "trx;LogFileName=meetings.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\meetings.trx --suite Diten.Platform.Application.Tests` → çıkış 0, 0 kırmızı. Çıkış 1 ya da 2 ise DUR.
3. Yeni satırlar listede ve geçiyor: `MeetingWriteAccess` birim satırları, Respond üç satırı, `FollowUpOfMeetingId`, kaynak, iki tarama satırı, S28-2 ve S28-4 korumaları.
4. Kademe 3 (mimari): `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests -c Debug --results-directory R --logger "trx;LogFileName=arch.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\arch.trx --suite TenantArchitecture.ArchitectureTests` → çıkış 0; yalnız kayıtlı arızalar, parmak izi aynı. Bu koşu yerel kod değişiminin denetim defterini etkilemediğini de ölçer. Kapsayıcı denetimi: `dotnet test services/Diten.Platform/tests/Diten.Platform.BackgroundJobs.Tests/Diten.Platform.BackgroundJobs.Tests.csproj -c Debug --filter "FullyQualifiedName~PlatformContainerValidationTests" --results-directory R --logger "trx;LogFileName=container.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\container.trx --suite Diten.Platform.BackgroundJobs.Tests` → çıkış 0. Birim testleri kapsayıcıyı kurmaz; `MeetingWriteAccess` kaydı burada ölçülür.
5. Erken sabotaj. Her biri yalnız İzinli yollarda, elle; önce ve sonra `Get-FileHash -Algorithm SHA256 <dosya>` aynı değeri verir. Filtre: `FullyQualifiedName~MeetingWriteAccessGuardSourceTests`.
   - `AddMeetingAttendeesHandler` içinden çağrıyı kaldır → S28-2 kırmızı, `UpdateMeetingHandler` aynı dosyada çağırsa da. Geri al → yeşil.
   - `MeetingFollowUpCommandHandlers.cs` içinde ikinci bir `new MeetingSystemOrigin(…)` yaz → S28-4 kırmızı. Geri al → yeşil.
   - İstisna listesine var olmayan bir sınıf adı ekle → S28-2 kırmızı. Geri al → yeşil.
6. Kapsam ölçütleri, çıktıları rapora: `git diff --stat <Expected HEAD>` yalnız İzinli yollar; `git ls-files --others --exclude-standard` yalnız İzinli yollar; `git diff <Expected HEAD> -- services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks frontend gateway tests/architecture execution .antigravity` boş; `git diff --check` temiz. Kaynak taraması: `git grep -n "new MeetingSystemOrigin" -- services/Diten.Platform/src` tek dosya.

Durma koşulları:
- Worktree HEAD, dal ya da temizlik beklenenden farklı.
- Taban çizgisi kırmızı, Mongo başlamıyor (`OWNED_TEST_MONGO_*`), ya da `dotnet restore` paketi alamıyor.
- Ölçülen satırlar bu prompt'taki tablodan farklı ve işleyici eşlemesi net değil.
- İstisna listesi 5'ten farklı olmak zorunda görünüyor, ya da kapsam ölçümü 21 değil ve sebebi pakette yok.
- `MeetingWriteAccess` 15 işleyicinin biri için yüklemeden hemen sonra çalışamıyor (örneğin bir transaction akışı nedeniyle).
- Bir `Fake…Repository` ya da test çift dosyası dışında üretim dosyası değiştirmek gerekiyor ve İzinli yollarda değil.
- İzinli yolların dışına çıkmak, paket kuralıyla çözülemeyen bir seçim, ya da başka modülde düzeltme gerekiyor (XMC-001).
- Yeni test paketi derlemeyi bozuyor ya da ortak test projesinde sürüm çakışması çıkıyor.
- `known_failures.py` çıkış 1 ya da 2. Kayıt dosyasına dokunma.
- 15 dakika boyunca işlemci yükü 80'in altına inmiyor.

Rapor formatı: SOP §22 yapılandırılmış rapor.
- Başlıklar: Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes.
- Tests: taban ve son sayılar; yeni satırlar için dosya:satır; değişen mevcut testler (önce / sonra iddia); kapsam listesi (21 sınıf, 16 / 5); erken sabotajlarda kırmızı test adları ve özet değerleri; TRX yolları; `known_failures.py` çıktıları.
- Decisions: yöntem imzası, paket sürümü, test dosya adları. Sahip ve CT kararları buraya yazılmaz, yalnız anılır.
- Known gaps, en az: HTTP matrisi ve 34 sabotaj P4'te; oluşturma yolu P2'de; takip kuralları P3'te; S28 üç yazma değişikliği R1-D12 ile denetimsiz ertelenir ve S25 taşır (§25); `Currency_minor_units_covers_every_code_the_currencies_lookup_returns` bu işin değil; bu prompt sonunda parça ana dala gidemez.
- Her iddia için yol:satır ya da komut çıktısı verilir. Ölçmediğini yaz.

Senin PASS'in kapanış değildir; kabul CT'nindir.
