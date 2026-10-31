@[.antigravity/agents/backend-architect.md]
WP: MG-MEET-S28 · Prompt P3 v0.1
Repository: C:\dev\ERP-vNext · Branch: chore/ct/mg-ct-01 · Expected HEAD: eb561752267ae5f7f5f573a43e451bdd0c9073bc · Worktree: C:/dev/ERP-vNext/.claude/worktrees/mg-ct-01
Risk: HIGH · §9.1 sinyalleri: yetki, sözleşme, migrasyon
Görev sınıfı: ürün · Paket: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IMeetingRepositories.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/MeetingRepositories.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**
Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/Archive/**, frontend/Diten.Web/**, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv
Kapsanan dilim: DL-28

> **TASLAK v0.1 — DAĞITIMA HAZIR DEĞİL. Bu metin kod, commit ya da dağıtım izni değildir.**
> - Kuyruk satırı `PLANNED`, `READY` değil. Kayıt satırı `origin/main`'de değil. HIGH inceleme kaydı yok.
> - **Başlangıç noktası P2'nin son commit'idir; bugün bilinmez.** Repository, Branch, Expected HEAD ve Worktree, kapının iskeletinden gelir (CT dalının bugünkü ucu). Dağıtımda CT bunları yeniden yazar. Kod alıntıları ve satır numaraları eb561752'de ölçüldü; P1 ve P2 sonrası kayar, CT yeniden ölçer.
> - P1'in ürettiği `MeetingWriteAccess.cs`, `MeetingSystemOrigin.cs` ve P2'nin ürettiği `MeetingAccessHttpMongoHost.cs` bugünkü Expected HEAD'de yoktur; bu yüzden "Önce oku" listesinde değildir. İşçi hepsini açar.
> - Tek dal, tek birleştirme isteği (DL-28). Bu prompt sonunda rapor ve matris (P4) eksiktir; ara durum ana dala gitmez.
>
> **CT okumaları (paket yetkisi altında; paket değişmez):**
> - **K-0 — "draft" sözcüğü.** Paket §24 üst notu ve §24.31 satır 3892 "draft / not approved" der. Paket düzenlenmez: özet değişir, onay düşer. Aynı not bağlayıcı olanın onay satırı olduğunu söyler (satır 1147–1148). İşe başlarken `py -3 -I -B -X utf8 scripts/pack_approval.py check execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` koşulur; çıkış 0 değilse kod yazılmaz, DUR.
> - **K-10 — kaynak kontrolü P1'dedir.** S28-1 `ScheduleFollowUpMeetingHandler`'ı 15 işleyicide sayar ve S28-2 koruması onu ister; kontrol P1'de yazıldı. Bu prompt onun üstüne bağlantı hedefi kontrolünü, Q25'i, tamamlamayı ve taşıma sırasını ekler.
> - **K-11 — yinelenen satır cevabı depodan gelir.** "MongoDB Driver types stay in Persistence" kuralı (`IMeetingRepositories.cs:220–221`) işleyicinin `MongoWriteException` yakalamasını yasaklar. §24.31 "Files" depo dosyalarını anmaz; paket §5 satır 313 ve 318 onlara izin verir. `IAgendaItemRepository`'ye `Task<AgendaItem?> TryCreateAsync(AgendaItem item, CancellationToken ct = default)` eklenir: yinelenende null. Emsal `IMeetingMinutesVersionRepository.TryCreateAsync` (`IMeetingRepositories.cs:216–223`) ve `MeetingRepositories.cs:425–436`.
> - **K-12 — `MEETING_FOLLOW_UP_CONFLICT` verisi.** Paket "`data` yalnız kimliği taşır" der (satır 4408). Yanıt tipi `ScheduleFollowUpMeetingResultDto(Guid MeetingId, int CarriedAgendaItemCount)`dır (`MeetingModels.cs:469`) ve bugünkü biçimiyle sayı da yazılır. `Response<T>.FailWithData` (`Response.cs:67`) kullanılır; `data` nesnesinin tek üyeli olması JSON testiyle ölçülür. En küçük yolu işçi seçer (ör. sayıyı boşken yazmayan null olabilir bir üye); 200 ve 201 yanıtındaki `carriedAgendaItemCount` ve `follow-up-dialog.js` değişmez. Seçim Decisions'a yazılır.
> - **K-13 — döngü kontrolü önce gelir.** Paket döngü kontrolünün kaldığını söyler (satır 4417) ama 200 karar ağacıyla sırasını söylemez. `MeetingFollowUpCommandHandlerTests.cs:162` ve `:184` bir çarpışmanın kaynağın kendisine ya da döngülü soya inmesini 400 `MEETING_SELF_FOLLOW_UP` ile sabitler. Bu iki test değişmez: `CreatesCycleAsync` oluşturma sonucundan hemen sonra, karar ağacından önce koşar.
> - **K-14 — ortam başına sayım.** Paket "dağıttığı her ortamda" der (satır 4453–4455); §24.26 (c) ortamları geliştirici makinesi, CI, staging, canlı diye sayar. İşçinin staging ve canlı bağlantısı yoktur ve bu prompt gizli bilgi taşımaz. Canlıya dokunmak sahibin her seferinde açık onayını ister (kart "Canlı ortam müdahalesi"). Sayım sonuçlarını CT dağıtım mesajında verir. Vermediyse işçi dizin satırını eklemez, DUR.
> - **K-15 — `PreAcceptedUserId` koruması burada sıkılaşır** (P2 K-6): "tam bir dosya, `MeetingFollowUpCommandHandlers.cs`".

Önce oku (sırayla):
1. execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md (§24.31 tamamı; S28-10 satır 4385–4566; S28-8 satır 4213–4238, 4256–4277, 4288–4312; §24.4 satır 1420; "Failure paths" satır 4618–4644; §25 satır 4795)
2. AGENTS.md
3. execution/domains/management-governance/domain-config.md
4. docs/records/audits/2026-10/mg-meetings-discovery-01/RECORD.md
5. docs/records/audits/2026-10/mg-s28-inspection-01/INSPECTION-NOTE.md
6. .antigravity/rules/multi-tenancy.md
7. .antigravity/rules/security-jwt.md
8. .antigravity/rules/permission-key-standard.md
9. .antigravity/rules/handler-design.md
10. .antigravity/rules/repository-standard.md
11. .antigravity/rules/response-envelope.md
12. .antigravity/rules/entity-versioning.md
13. .antigravity/rules/mongo-indexing.md
14. .antigravity/rules/audit-trail-standard.md
15. .antigravity/rules/code-style.md
16. .antigravity/rules/git-safety.md
17. .antigravity/rules/docs-organization.md
18. .antigravity/rules/cross-module-change-consent.md
19. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs
20. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs (yalnız :285–292 `CheckEditable` ve P2'nin oluşturma yolu)
21. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/RecordLinks/RecordLinkService.cs
22. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs
23. services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs
24. services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IMeetingRepositories.cs
25. services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/MeetingRepositories.cs
26. services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs
27. services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkingCalendar.cs (yalnız :31–43, kısmi dizin emsali)
28. services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/SchemaCollection.cs
29. frontend/Diten.Web/wwwroot/assets/js/Meetings/follow-up-dialog.js (yalnız okunur, :91–106)
30. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingFollowUpCommandHandlerTests.cs
31. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingReadVersionRaceMongoTests.cs (bariyer ve çağrı kancası emsali)
32. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingRepositoryMongoTests.cs
33. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingWorkItemProviderTests.cs
34. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingS2TestDoubles.cs
35. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/ReplicaSetClassFixture.cs
36. docs/guides/operations/control-tower-operating-card.md
37. tests/known-failures.tsv

Sözleşme yüzeyleri:
- API: «`MEETING_FOLLOW_UP_CONFLICT`** ("A meeting with these details already exists and is not a follow-up of» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4407), «the existing lifecycle conflict that every agenda write gives on such a» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4398), «3. Storage guarantee: the partial unique index `ux_meeting_agenda_items_tenant_meeting_carried_link` on» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4450), «and the link is not written, so a later retry completes the carry. **Before the index is added**, the» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4453)
- API: «public sealed record ScheduleFollowUpMeetingResultDto(Guid MeetingId, int CarriedAgendaItemCount);» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs:469), «stay in Persistence, never surface to a command handler).» (services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IMeetingRepositories.cs:221), «new CreateIndexOptions { Name = "ix_meeting_agenda_items_tenant_meeting_sort" })» (services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Meetings.cs:121)
- İzin: «`MeetingWriteAccess` with the caller (the same helper and acting identity as the source check). A caller who» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4559), «the caller is a **party of the source**: its organizer, or a user with an attendee row on it (any» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4476)
- Test: «**follow-up collision** (S28-10, F-24), over HTTP, a caller who can view the source, a follow-up whose five» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4213), «**carry cut off in the middle** (review 09 F-56; handler level, real Mongo): the source has three open» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4224), «**two completions at once** (review 09 F-56; handler level, real Mongo): the same cut-off first call; two» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4231)
- Test: «**scheduler as attendee** (S28-10, Q25), over HTTP with real Mongo:» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4261), «**idempotency-link target** (S28-10, review 06 F-35), over HTTP: a source attendee schedules a follow-up» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4288), «public async Task A_business_key_collision_resolving_back_to_the_source_meeting_itself_is_400_SelfFollowUp()» (services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingFollowUpCommandHandlerTests.cs:162)
- Sabotaj: «drop the line-level skip in the carry → the "carry cut off in the middle" row goes red (the retry's first insert» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4660), «write the follow-up link before the carry again → the "carry cut off in the middle" row goes red (the retry is» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4662), «drop the carried-line unique index → the "two completions at once" row goes red (two lines for one task);» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4664)

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
- **Hedef servis ve proje:** Diten.Platform · `Diten.Platform.Application` (Features/Meetings), `Diten.Platform.Domain` (yalnız depo arayüzü), `Diten.Platform.Infrastructure` (depo ve şema). Test projesi `services/Diten.Platform/tests/Diten.Platform.Application.Tests`.
- **Aggregate ve alan:** `AgendaItem`, `RecordLink`, `MeetingAttendee`, `Meeting`. Yeni alan yok. Yeni dizin bir tane: `ux_meeting_agenda_items_tenant_meeting_carried_link`.
- **Komutlar:** `ScheduleFollowUpMeetingCommand` (davranış); `CreateMeetingCommand`'ın `PreAcceptedUserId`'si artık bu işleyiciden gönderilir. Yeni komut yok.
- **Doğrulayıcı:** Yeni istek alanı yok; doğrulayıcı değişmez.
- **İzin anahtarları:** Yeni anahtar yok. Takip ucu `platform.meetings.create` (`MeetingsController.cs:197`).
- **Yanıt:** `Response<T>`; `FailWithData` yalnız `MEETING_FOLLOW_UP_CONFLICT` için.

NE: MOD-0357 Toplantılar, S28 P3 (S28-10 ikinci yarı). Takip toplantısı, göremediği toplantıya yazmaz; yarıda kalan taşıma tekrar deneyince tamamlanır; zamanlayan taraf yeni toplantıyı açabilir. İşler:
- Bağlantı hedefi kontrolü (`MeetingWriteAccess`; kaynak kontrolü P1'de, K-10).
- 200 tekrarı için karar ağacı: 409 `MEETING_FOLLOW_UP_CONFLICT`, tamamlama, yaşam döngüsü 409'ları, "bağlantı var" 200.
- Q25: taraf olan zamanlayan `Accepted` katılımcı (`PreAcceptedUserId`).
- **Önce taşı, bağlantıyı en son yaz**; satır düzeyi atlama.
- Kısmi tekil dizin `ux_meeting_agenda_items_tenant_meeting_carried_link`. **Önce** ortam başına salt-okunur mükerrer sayımı; sıfırdan büyükse DUR, CT'ye bildir (otomatik temizlik yok).
- Gerçek Mongo ile işleyici düzeyi: yarıda kalan taşıma ve eşzamanlı iki tamamlama.

Bu prompt'ta yok: rapor ve dışa aktarım, `MeetingRelatedRecordResolver`, HTTP yazma matrisi, bayt eşitliği, seri satırları, 34 sabotajın resmi koşusu (P4).

NEDEN:
- Paket S28-10 "Follow-up" (satır 4385–4416): bugün `ScheduleFollowUpMeetingHandler`, `CreateMeetingCommand` ne dönerse ona bağlantı ve taşınan satır yazar (`MeetingFollowUpCommandHandlers.cs:92–100, 115–122, 233–248`).
- "Carry first, link last" (satır 4433–4463): bugün bağlantı taşımadan önce yazılır (`:115–120`, `:122`) ve bağlantıyı bulan çağrı erken döner (`:68–77`). Bağlantıdan sonra kesilen taşıma sonsuza dek eksik kalır. Bağlantı tamamlanma işaretidir.
- Sahip cevabı Q25 ("devam-katilimci", 2026-10-29; paket satır 3100): kaynağın düzenleyen olmayan katılımcısı zamanlarsa yeni toplantıya eklenir. CT okumaları: `Accepted`; yalnız kaynağın tarafı.
- Bugün takip penceresi zamanlayanı açamadığı sayfaya gönderir (`follow-up-dialog.js:106`).

NASIL:
- **Makine yükü, ortam, taban.** P1 ile aynı: `(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average` 80 üstündeyse 60 saniye bekle. `$env:DITEN_TEST_MONGOD = 'C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe'`; docker yok. Değişiklikten önce DOĞRULA 1 ve 2'yi koş; P2 tabanından farklıysa DUR.
- **`Handle` sırası** (`MeetingFollowUpCommandHandlers.cs`, satırlar eb561752). Bu sıra değişmez; P4 sabotajları ona dayanır.
  1. Kaynak `MeetingWriteAccess` ile (P1).
  2. **Bağlantı hedefi** (paket satır 4556–4566). `existingLink` varsa (`:69–70`) hedefi önce `MeetingWriteAccess` ile yükle, aynı `SystemOrigin` ile. Görünür: 200, kimlik ve sayı, yaşam döngüsü ne olursa olsun. Görünmez ya da yok: sabit 409 `MEETING_CREATE_CONFLICT`, "A meeting with these details could not be created."; hedefin gündeminden okuma ve yazma yok.
  3. **Q25 listesi** (paket satır 4471–4485). `meetingRequest.AttendeeUserIds` = isteğin listesi; şu ikisi birlikte doğruysa çağıran da eklenir: çağıran kaynağın tarafı (düzenleyeni ya da katılımcı satırı olan, kimlikler P1'in yüklemesinden), çağıran yeni düzenleyen değil (`request.OrganizerUserId ?? source.OrganizerUserId`). Eklendiyse `PreAcceptedUserId: çağıran`, ad vererek. `SystemOrigin` varken ekleme yok, `PreAcceptedUserId` yok, boş kimlik yok. Liste kendini anan taraf olmayan için `PreAcceptedUserId` yok: satırı `Pending` (satır 4297–4302).
  4. `CreateMeetingCommand`. Başarısızsa kod ve metin aynen geçirilir (`:93–98`), hiçbir şey yazılmaz.
  5. **Döngü kontrolü** (K-13), bugünkü yerinde.
  6. **201:** taşı, sonra bağlantıyı yaz, 201 ve kimlik ve sayı.
  7. **200 karar ağacı, bu sırayla** (paket satır 4390–4408):
     - a. `FollowUpOfMeetingId` kaynak değilse: 409 `MEETING_FOLLOW_UP_CONFLICT`, "A meeting with these details already exists and is not a follow-up of this meeting.", `data` yalnız kimlik (K-12). Yazma yok.
     - b. Kaynaktan ona takip bağlantısı varsa (`RecordLinkTypes.FollowUp`, başka çağrının yazdığı): 200, aynı kimlik ve sayı, yaşam döngüsü ne olursa olsun, yazma yok.
     - c. `Cancelled` ya da `Completed` ise: `UpdateMeetingHandler.CheckEditable` cevabı, 409 `MEETING_CANCELLED` ya da `MEETING_COMPLETED`. Yazma yok.
     - d. `Scheduled`: **tamamlama.** Taşı, sonra bu çağrının anahtarıyla bağlantıyı yaz, 200, kimlik ve o toplantıda kaynaktan taşınan satır sayısı.
  8. Tekrarda satır yazılmaz: "bağlantı var" ve tamamlama yeni katılımcı satırı yazmaz; satır ilk çağrınındır (paket satır 4539–4541).
- **Taşıma** (`CarryForwardOpenAgendaTasksAsync`; paket satır 4441–4457).
  - Aday ve açık görev okuması bugünkü sırayla kalır (`:177–221`): iki eşzamanlı çağrı aynı ilk eksik göreve gitsin diye.
  - Yazmadan önce hedefin `CarriedFromMeetingId = kaynak` satırlarını oku. `AddLinkAsync` var olan gündem bağlantısını döner (`ux_meeting_record_links_tenant_source_target_type`); bu bağlantının satırı varsa o görevi atla.
  - Satır `TryCreateAsync` ile yazılır (K-11). Null dönerse çağrı 409 `MEETING_CONCURRENCY_CONFLICT`, "The meeting changed meanwhile; reload and retry." ile biter, bağlantı yazılmaz.
  - Satır başlık tutmaz (`MeetingAgendaCodes.CarriedOpenAction`, `:223–229`).
  - Devam eden taşımada `SortOrder` seçimi Decisions'a yazılır; yeni takip toplantısında bugünkü sıra korunur. S25-8 sıralamayı sonra ele alır.
  - Bağlantı en son yazılır. Bağlantıyı bulan erken dönüş taşımayı yeniden koşmaz (paket satır 4444–4446).
- **Depo** (K-11). `IAgendaItemRepository.TryCreateAsync` ve `AgendaItemRepository` karşılığı; yalnız `ServerErrorCategory.DuplicateKey` yakalanır (`MeetingRepositories.cs:431–432` deseni). `FakeAgendaItemRepository` (`MeetingS2TestDoubles.cs:255`) ve `CarryingAgendaStore` (`AgendaItemCommandHandlerTests.cs:60`) yeni üyeyi kazanır. Sahte depo tekil kuralı taklit etmez; tekil ölçümü gerçek Mongo'dadır.
- **Mükerrer sayımı (dizinden önce; K-14; paket satır 4453–4456).** Salt-okunur. Çıktı yalnız sayıdır; kimlik ve içerik rapora yazılmaz. Koleksiyon `meeting_agenda_items` (`PlatformCollections.cs:139`). Sorgu `docs/records/audits/2026-10/mg-s28-inspection-01/duplicate-carried-lines-count.js`; değiştirilmez, `collectionExists`, `carriedLines` ve `duplicateGroups` yazar. `collectionExists=false` yanlış veritabanı demektir, o koşunun sıfırı kanıt sayılmaz.
  - Alan adlarını önce gerçek Mongo test veritabanındaki bir satırla doğrula; farklıysa dizini ve sorguyu sen değiştirme, DUR.
  - Geliştirici makinesi: bu makinede kalıcı bir yerel Toplantılar veritabanı varsa say, yoksa "yok" yaz. CI ve test Mongo'su her koşuda boş başlar.
  - Staging ve canlı: sayımı CT verir. Üç değeri ortam ve tarih ile rapora al.
  - Herhangi bir ortamda sonuç sıfırdan büyükse dizin satırını ekleme, DUR, CT'ye bildir. Otomatik temizlik yok; onarım ayrı sahip kararıdır.
- **Dizin** (`PlatformSchemaManifest.Meetings.cs:114–122`). `AgendaItem` dizi bloğuna: anahtar `TenantId`, `MeetingId`, `RecordLinkId` artan; `Unique = true`; `Name = "ux_meeting_agenda_items_tenant_meeting_carried_link"`; `PartialFilterExpression` birebir `{ CarriedFromMeetingId: { $type: "binData" }, IsDeleted: false }` (§24.4 satır 1420). Biçim için `CreateIndexOptions<T>` (`PlatformSchemaManifest.WorkingCalendar.cs:31–43`, `SchemaCollection.cs:114`). Taşınmayan satırlar ve silinmiş satırlar dizine girmez. `:109–110`'daki "No unique index" yorumu güncellenir.
- **Satırlar** (paket satırları bağlayıcıdır; ad senin, rapora dosya:satır). HTTP satırları P2'nin `MeetingAccessHttpMongoHost.cs` konağında.
  - **follow-up collision** (satır 4213–4223), HTTP: üç satır (göremez 409 sabit; görür ve kaynak değil 409 yalnız kimlik; görür, kaynak, bağlantı yok, tamamlama ve ikinci tekrar). Üçüncü satırda hata `CreateMeetingCommand` sonrasına konur (paket satır 4220–4221), konakta kancalı sarmalayıcıyla; olmuyorsa eşdeğer tohumlamayı gerekçesiyle Decisions'a yaz. `data` tek üyeli (K-12).
  - **carry cut off in the middle** (satır 4224–4230), işleyici, gerçek Mongo: `IAgendaItemRepository` sarmalayıcı ikinci satırda atar. Yeniden deneme 200, sayı 3, görev başına bir satır, atlama koşuluna ulaşıldı; üçüncü çağrı bağlantıdan 200, yazma yok.
  - **two completions at once** (satır 4231–4238), işleyici, gerçek Mongo, dizinli veritabanı: bariyer iki çağrıyı okumadan sonra tutar. Tam biri 409 `MEETING_CONCURRENCY_CONFLICT`, öteki 200 sayı 3. Görev başına bir satır. Sonraki tekrar bağlantıdan 200. Şema `PlatformSchemaManifest.ApplyAsync` ile uygulanır (`mongo-indexing.md:52`); farklı `SortOrder` tohumlanır. Bu satır dizin ön koşulu çağırmaz, yalnız kendi kiracı satırlarını temizler: dizin sabotajında (P4) kırmızı nedeni "görev başına iki satır" olmalıdır (paket satır 4664). Bariyer ve kanca `MeetingReadVersionRaceMongoTests.cs` desenindedir.
  - **completion on a cancelled or completed meeting** (satır 4256–4260), HTTP: iki satır.
  - **scheduler as attendee** (satır 4261–4272), HTTP, gerçek Mongo: beş satır. `GET /{id}` 200, davet gönderimi yok, Görev Merkezi öğesi yok (`MeetingWorkItemProviderTests.cs` deseni).
  - **scheduler under the marker** (satır 4273–4277), kaydeden aracıyla.
  - **idempotency-link target** (satır 4288–4292), HTTP: çıkarılınca sabit 409 baytları; hâlâ katılımcıyken 200.
  - **party rule, organizer branch** (satır 4293–4296), HTTP: test düzenleyenin satırını veritabanında siler.
  - **non-party who lists themself** (satır 4297–4302), HTTP: `Pending` ve davet öğesi.
  - **"link exists" answers 200 whatever the lifecycle** (satır 4303–4306), HTTP: iki satır.
  - **a cancelled meeting that is not a follow-up of the source** (satır 4307–4309), HTTP.
  - **party outside the eligibility filter** (satır 4310–4312), HTTP: 201, takip var, zamanlayan satırı yok.
  - **Dizin satırı** (paket açıkça saymaz, K-11'in ölçümü): gerçek Mongo'da `TryCreateAsync` ikinci satırda null; `CarriedFromMeetingId` null iki satır çakışmaz; silinmiş satır çakışmaz. Yazmadan önce `UniqueIndexPrecondition.RequireAsync` (`Persistence/UniqueIndexPrecondition.cs`), kendi kiracı satırları `finally` ile silinir (`MeetingMinutesVersionMongoTests.cs:52–73`). `listIndexes` ile ad, `unique` ve `partialFilterExpression` karşılaştırılır (`RecordLinkRepositoryMongoTests.cs:241`).
  - **P2 tamamlaması:** takip ucu gövde testi: yalnız zamanlayanın kendi satırı `Accepted` (satır 4505–4506); tarama K-15.
- **Var olan testler.** `MeetingFollowUpCommandHandlerTests.cs:162, :184` değişmez (K-13). `:216` K11 testi yeşil kalır. Bağlantı sırasını iddia eden bir test varsa önce / sonra raporla. İddia zayıflatılmaz.
- **Test planı dondurma.** Seçenek (b): ilk hatada dur. Yedek kapsam yok.
- **Teslim.** Push ve PR yok. Bu prompt sahibin onayıyla dağıtılırsa commit adımı commit onayıdır: DOĞRULA 1–6 temiz; işçi dalında tek yerel commit, açık yollarla; `git diff --cached --name-only`, `--stat`, `--check` rapora; tam içerik `git diff --cached` ile gözden geçirilir. Mesaj: `feat(mg): MOD-0357 S28 P3 — follow-up: link target check, completion, Q25 scheduler, carry first and link last, carried-line unique index`. CT'ye bildir ve dur. P4 bu commit'in üstünden başlar.
- **Satır sonu.** Rapor ve kayıtlar LF. Yorum ve günlük metinleri İngilizce.

YAPMA:
- `Features/Tasks/**` değişmez. `RecordLinkService` yalnız var olan yöntemleriyle kullanılır.
- Yeni izin anahtarı yok.
- S28'de yeni denetim olayı yok. `ScheduleFollowUpMeetingCommand` yol `a`'ya taşınmaz; tamamlama ve zamanlayan satırı R1-D12 ile ertelenir, S25 taşır (§25).
- Paket, `gateway/Diten.ApiGateway/ocelot.json`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, Archive ve `.antigravity/**` değişmez.
- `main`'e commit ya da push yok. Yalnız açık yolla stage (`git add -A` ve `git add .` yok). Yıkıcı git yok: `reset --hard`, `clean`, `git checkout -- <yol>`, force push, `stash`.
- Port, kimlik ya da tanımlayıcı uydurma. Dizin adı ve filtre paketten birebir. K-11 dışında yeni depo üyesi gerekiyorsa DUR.
- Başka modülde düzeltme gerekirse dokunma: DUR ve sor (XMC-001).
- Mükerrer satırları silme, düzeltme ya da birleştirme. Canlı ya da staging'e bağlanma. Bağlantı dizesi, gizli bilgi ya da satır içeriği rapora yazma.
- İşleyicide `MongoWriteException` yakalama; Mongo sürücü tipini Application'a taşıma.
- Taşımayı tek transaction'a alma (paket satır 4461–4463 reddeder). Erken dönüşte taşımayı yeniden koşma.
- `read-all` ile geçen taraf olmayana satır ekleme. İşaretleyici altında çağıran ekleme.
- `MeetingEligibility.CanView` kopyası yazma; bağlantı hedefi için ayrı görünürlük yazma.
- Çağrının sırasını değiştirme: FollowUpOf → bağlantı → yaşam döngüsü → tamamlama.
- `follow-up-dialog.js`'i ya da herhangi bir ekranı değiştirme. S15, S25 ve S21 işini yazma.
- Test `Skip` etme, iddia zayıflatma.

DOĞRULA: Komutlar çalışma ağacı kökünde, Windows PowerShell'de. Her ağır komuttan önce makine yükü ölçülür. `T = services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj`, `R = $env:TEMP\mg-s28-p3`. Bu prompt E2 ve E4 satırları üretir; gerekli E5 P4 sonundadır.
1. Kademe 1: `dotnet build T -c Debug` → 0 hata; uyarı tabana göre yazılır.
2. Kademe 2: `dotnet test T -c Debug --no-build --filter "FullyQualifiedName~Diten.Platform.Application.Tests.Meetings" --results-directory R --logger "trx;LogFileName=meetings.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\meetings.trx --suite Diten.Platform.Application.Tests` → çıkış 0, 0 kırmızı. Ayrıca şema korumaları: `dotnet test T -c Debug --no-build --filter "FullyQualifiedName~Schema|FullyQualifiedName~MongoSchemaResidue" --results-directory R --logger "trx;LogFileName=schema.trx"`, aynı `known_failures.py` denetimi → çıkış 0.
3. Yeni satırlar listede ve geçiyor (NASIL "Satırlar"). Eşzamanlı satır ardı ardına üç kez koşulur, üçü de yeşil; sonuçlar yazılır.
4. Kademe 3 (mimari): `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests -c Debug --results-directory R --logger "trx;LogFileName=arch.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\arch.trx --suite TenantArchitecture.ArchitectureTests` → çıkış 0; yalnız kayıtlı arızalar. Bu koşu depo arayüzü ve şema değişikliğinin mimari kuralları bozmadığını da ölçer.
5. Erken sabotaj (resmi koşu P4). Her biri elle; `Get-FileHash -Algorithm SHA256 <dosya>` önce ve sonra aynı.
   - Satır düzeyi atlamayı kaldır → "carry cut off" kırmızı. Geri al → yeşil.
   - Dizin satırını kaldır → "two completions" kırmızı. Geri al → yeşil.
6. Kapsam ölçütleri, çıktıları rapora: `git diff --stat <P2 commit>` yalnız İzinli yollar; `git ls-files --others --exclude-standard` yalnız İzinli yollar; `git diff <P2 commit> -- services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks services/Diten.Platform/src/Diten.Platform.Domain/Entities frontend gateway tests/architecture execution .antigravity` boş; `git diff --check` temiz.

Durma koşulları:
- Worktree HEAD P2 commit'i değil, dal farklı, ya da başlangıçta kirli dosya var.
- Dağıtım mesajında staging ve canlı için sayım sonucu yok, herhangi bir ortamda `collectionExists=false` ya da mükerrer grup sayısı sıfırdan büyük. Dizin satırını ekleme.
- Sayım sorgusu alan adları gerçek satırla uyuşmuyor.
- `Accepted` ya da Q25 kuralı paketin metniyle çelişen bir kullanıcı akışı gösteriyor.
- `MeetingFollowUpCommandHandlerTests.cs:162` ya da `:184` yeşil kalamıyor (K-13).
- `data` yalnız kimlik yapılamıyor ya da 200 ve 201 yanıtının biçimini değiştirmeden çözülmüyor (K-12).
- "two completions" satırı beklenen sonucu üç koşunun herhangi birinde vermiyor.
- Şema uygulaması test veritabanında dizini kurmuyor.
- Taban kırmızı, `known_failures.py` çıkış 1 ya da 2.
- İzinli yolların dışına çıkmak ya da başka modülde düzeltme gerekiyor (XMC-001).
- 15 dakika boyunca işlemci yükü 80'in altına inmiyor.

Rapor formatı: SOP §22 yapılandırılmış rapor.
- Başlıklar: Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes.
- Migration/Rollback: ortam başına sayım sonucu ve tarih tablosu; dizinin nasıl kurulduğu; geri alma adımı (dizini silmek için gereken komut metni, koşulmaz).
- Tests: taban ve son sayılar; satırlar için dosya:satır; eşzamanlı üç koşu; erken sabotajlarda kırmızı test adları ve özet değerleri; TRX yolları; `known_failures.py` çıktıları.
- Decisions: `SortOrder` seçimi, `data` tek üye çözümü. Sahip ve CT kararları buraya yazılmaz, yalnız anılır.
- Known gaps, en az: S28'den önce yazılmış ve bağlantıdan sonra kesilmiş takip toplantısı eksik kalır, erken dönüş onarmaz (paket satır 4459–4461); sayım tanımı "dağıtılan her ortam" ve staging ile canlı CT'den gelir; S15 HTTP satırları ve gizli alt tür S15'te; tamamlama ve zamanlayan satırı denetim olayı S25'te; matris ve 34 sabotaj P4'te; bu prompt sonunda parça ana dala gidemez.
- Her iddia için yol:satır ya da komut çıktısı verilir. Ölçmediğini yaz.

Senin PASS'in kapanış değildir; kabul CT'nindir.
