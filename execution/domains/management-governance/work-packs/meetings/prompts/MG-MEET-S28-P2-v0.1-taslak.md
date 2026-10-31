@[.antigravity/agents/orchestrator.md]
WP: MG-MEET-S28 · Prompt P2 v0.1
Repository: C:\dev\ERP-vNext · Branch: chore/ct/mg-ct-01 · Expected HEAD: eb561752267ae5f7f5f573a43e451bdd0c9073bc · Worktree: C:/dev/ERP-vNext/.claude/worktrees/mg-ct-01
Risk: HIGH · §9.1 sinyalleri: yetki, sözleşme
Görev sınıfı: ürün · Paket: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md
İzinli yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Services/MeetingWriteAccess.cs, frontend/Diten.Web/wwwroot/assets/js/Meetings/api.js, frontend/Diten.Web/Views/Meetings/_IndexL10n.cshtml, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.en.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.tr.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.fr.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.es.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.zh.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.ar.resx, frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.ru.resx, services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/**
Korunan yollar: services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/**, services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/**, services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/**, services/Diten.AuthService/**, execution/**, .antigravity/**, AGENTS.md, .github/**, gateway/Diten.ApiGateway/ocelot.json, gateway/**, frontend/Diten.Web/Views/Shared/_Layout.cshtml, frontend/Diten.Web/Views/Shared/**, frontend/Diten.Web/Views/Archive/**, frontend/Diten.Web/Controllers/**, frontend/Diten.Web/Resources/SharedResource*.resx, frontend/Diten.Web/wwwroot/assets/js/shared/**, frontend/Diten.Web/wwwroot/assets/css/**, frontend/Diten.Web/Program.cs, frontend/Diten.Web/tests/meetings-reason-code-bridge.test.js, docs/records/**, docs/roadmap/**, tests/architecture/**, tests/known-failures.tsv
Kapsanan dilim: DL-28

> **TASLAK v0.1 — DAĞITIMA HAZIR DEĞİL. Bu metin kod, commit ya da dağıtım izni değildir.**
> - Kuyruk satırı `INSPECTED`, `READY` değil. Kayıt satırı `origin/main`'de değil. HIGH inceleme kaydı yok.
> - **Başlangıç noktası P1'in son commit'idir; bugün bilinmez.** Repository, Branch, Expected HEAD ve Worktree, kapının iskeletinden gelir (CT dalının bugünkü ucu). Dağıtımda CT bunları işçi klasörü, işçi dalı ve P1 commit'iyle yeniden yazar; aşağıdaki kod alıntıları ve satır numaraları eb561752'de ölçüldü ve P1 sonrası kayar, CT yeniden ölçer.
> - P1'in ürettiği `Features/Meetings/Services/MeetingWriteAccess.cs` ve `MeetingSystemOrigin.cs` bugünkü Expected HEAD'de yoktur; bu yüzden "Önce oku" listesinde değildir. İşçi ikisini de açar.
> - Tek dal, tek birleştirme isteği (DL-28). Bu prompt sonunda takip toplantısı kuralları (P3) ve rapor (P4) eksiktir; ara durum ana dala gitmez.
>
> **CT okumaları (paket yetkisi altında; paket değişmez):**
> - **K-0 — "draft" sözcüğü.** Paket §24 üst notu ve §24.31 satır 3892 "draft / not approved" der. Paket düzenlenmez: özet değişir, onay düşer. Aynı not bağlayıcı olanın onay satırı olduğunu söyler (satır 1147–1148). İşe başlarken `py -3 -I -B -X utf8 scripts/pack_approval.py check execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` koşulur; çıkış 0 değilse kod yazılmaz, DUR.
> - **K-5 — `RequireCreatorCanView` takip işleyicisinde bu prompt'ta ayarlanır.** Oluşturma kuralı burada açılınca `ScheduleFollowUpMeetingHandler` parametreyi göndermezse kaynağın katılımcısı olan zamanlayıcı bugün çalışan takip toplantısında 400 alır. Paket koruması da "tam bir dosya bu parametreyi geçer" der (satır 4343–4344). Bu yüzden yalnız bu parametrenin hesabı P2'de; Q25 ve taşıma kuralları P3'te kalır. Paket §25 satır 4795 "`RequireCreatorCanView: false`" der; bu cümle düzeltme turu 5'ten önce kalmıştır. Bağlayıcı olan S28-10'dur (satır 4330–4332): taraf ve işaretleyici `false`, taraf olmayan `true`.
> - **K-6 — `PreAcceptedUserId` koruması iki aşamalıdır.** Bu prompt `CreateMeetingHandler` tarafını yazar; parametreyi gönderen satır P3'tedir. P2 sonunda sayan dosya sayısı 0'dır. Koruma P2'de "sayan her dosya `MeetingFollowUpCommandHandlers.cs`'tir" iddiasıyla yazılır; P3 bunu paket metnindeki "tam bir dosya" biçimine sıkılaştırır.
> - **K-7 — yansıma testi iki adı denetler.** Paket satır 4507 yalnız `PreAcceptedUserId` der; aynı paket `RequireCreatorCanView` için de "never on an HTTP request model" der (satır 3984). Test iki adı da denetler. Bu daha sıkıdır, paketle çelişmez.
> - **K-8 — kod sabiti ve köprü adları.** Paket yalnız kod değerlerini verir. Ölçülen eşleme biçimine göre (`api.js:45–89`) adlar sabittir: `MeetingReasonCodes.CreateConflict`, `.CreatorCannotView`, `.FollowUpConflict`; ileti anahtarları `errorCreateConflict`, `errorCreatorCannotView`, `errorFollowUpConflict`; resx adları `ErrorCreateConflict`, `ErrorCreatorCannotView`, `ErrorFollowUpConflict`.
> - **K-9 — `FollowUpOfMeetingId`.** Çağrı P1'dedir (K-2). Bu prompt'ta onun HTTP satırı vardır.

Önce oku (sırayla):
1. execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md (§24.31 tamamı; özellikle S28-10 satır 4315–4570; S28-8 satır 4197–4212, 4278–4287; "the three guards" satır 4499–4526; §25 satır 4778)
2. AGENTS.md
3. execution/domains/management-governance/domain-config.md
4. docs/records/audits/2026-10/mg-meetings-discovery-01/RECORD.md
5. docs/records/audits/2026-10/mg-s28-inspection-01/INSPECTION-NOTE.md
6. .antigravity/agents/l10n-agent.md
7. .antigravity/agents/backend-architect.md
8. .antigravity/agents/testing-agent.md
9. .antigravity/rules/multi-tenancy.md
10. .antigravity/rules/security-jwt.md
11. .antigravity/rules/permission-key-standard.md
12. .antigravity/rules/handler-design.md
13. .antigravity/rules/response-envelope.md
14. .antigravity/rules/api-conventions.md
15. .antigravity/rules/audit-trail-standard.md
16. .antigravity/rules/localization-standard.md
17. .antigravity/rules/dynamic-localization-standard.md
18. .antigravity/rules/frontend-js-standard.md
19. .antigravity/rules/code-style.md
20. .antigravity/rules/git-safety.md
21. .antigravity/rules/docs-organization.md
22. .antigravity/rules/cross-module-change-consent.md
23. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs
24. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingFollowUpCommandHandlers.cs
25. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs
26. services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs
27. services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs
28. frontend/Diten.Web/wwwroot/assets/js/Meetings/api.js
29. frontend/Diten.Web/Views/Meetings/_IndexL10n.cshtml
30. frontend/Diten.Web/Resources/Views/Meetings/MeetingsIndex.en.resx
31. frontend/Diten.Web/tests/meetings-reason-code-bridge.test.js
32. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingAttendeeSearchHttpMongoTests.cs (HTTP + gerçek Mongo konak emsali, :413–507)
33. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingDispatchAssertions.cs
34. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingWorkItemProviderTests.cs
35. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/ReplicaSetClassFixture.cs
36. docs/guides/operations/control-tower-operating-card.md
37. tests/known-failures.tsv

Sözleşme yüzeyleri:
- API: «it is refused with **400 `MEETING_CREATOR_CANNOT_VIEW`** and a localized message ("You would not be able to» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4321), «replay is not a creation), and a new row still answers **201**. Any other caller gets **409» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4366), «`MEETING_CREATE_CONFLICT`** with one fixed message ("A meeting with these details could not be created.") and» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4367)
- API: «`RequireCreatorCanView` (default `true`) itself: **`false`** when the caller is a **party** of the source» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4330), «public sealed record CreateMeetingCommand(CreateMeetingRequest Request, string CorrelationId)» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Commands/MeetingCommands.cs:6), «return Response<MeetingDto>.Success(dto, 201, command.CorrelationId);» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/Handlers/CommandHandlers/MeetingCommandHandlers.cs:203)
- API: «MEETING_FOLLOW_UP_NOT_FOUND: 'errorFollowUpNotFound',» (frontend/Diten.Web/wwwroot/assets/js/Meetings/api.js:53), «public const string FollowUpNotFound = "MEETING_FOLLOW_UP_NOT_FOUND";» (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs:64)
- İzin: «counts, S28-5), and from S15 the chosen subtype's confidential flag. A creator who would not be able to view» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4320), «S28-5 — The existing permission keys still apply first. A caller without the key gets 403 from the attribute,» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4143), «[HasPermission(MeetingPermissions.Create)]» (services/Diten.Platform/src/Diten.Platform.API/Controllers/MeetingsController.cs:39)
- Test: «**create replay** (S28-10): a non-viewer holding `create` sends the five key values of an existing meeting» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4197), «**creator rule** (S28-10, F-19), over HTTP, sent directly to the API (no screen sends this request; review» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4205), «**pre-accepted only for that user** (S28-10, Q25), handler level: a `CreateMeetingCommand` with» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4278)
- Test: «**non-party creator rule on the follow-up path** (S28-10, review 05 F-29), handler level with a recording» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4283), «2. **Reflection test**: no public property or constructor parameter named `PreAcceptedUserId`, compared» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4507), «3. **What the source scan counts** (Roslyn syntax tree, the S28-2 package, over every `.cs` file under» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4514)
- Sabotaj: «return the existing meeting to every caller on a replay → the create-replay row goes red;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4654), «skip the creator rule → the creator-rule row goes red (a meeting is written);» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4655), «set `PreAcceptedUserId` in a second file → the `PreAcceptedUserId` source-scan guard goes red;» (execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md:4675), «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

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
- **Alan ve servis:** management-governance · Diten.Platform · `shell: tenant` · `golden_reference: compact`, `form_field_count: 11` (bu prompt ekran kurmaz).
- **Dal:** P1'in işçi dalı; CT yazar. AGENTS §9 biçimi `feature/mg/mod-0357-…`.
- **Alt ajan zinciri (sırayla, aynı dalda):** backend-architect (komut, işleyici, neden kodu) → l10n-agent (köprü ve resx) → testing-agent (satırlar ve korumalar).
- **l10n-agent girdileri:** kiracı modülü, 7 dil; sayfa kaynağı `Resources/Views/Meetings/MeetingsIndex.{en,tr,fr,es,zh,ar,ru}.resx`; `SharedResource` değil; anahtarlar ve metinler NASIL tablosunda.
- **testing-agent girdileri:** akışlar ve beklentiler paket satırlarıyla NASIL'da; kiracı senaryosu var olan repo filtresiyle; test projesi `services/Diten.Platform/tests/Diten.Platform.Application.Tests`.
- **İzin anahtarları:** yeni anahtar yok; `platform.meetings.create`, `.read-all`.

NE: MOD-0357 Toplantılar, S28 P2 (S28-10 birinci yarı). Bir toplantıyı göremeyecek biri onu oluşturma yoluyla ele geçiremez, görmediği toplantının içeriğini tekrar yanıtından okuyamaz. İşler:
- Oluşturanın göreceği kuralı: 400 `MEETING_CREATOR_CANNOT_VIEW`, hiçbir yazma olmadan.
- Tekrar dalı: görebilen 200 ve toplantı; göremeyen sabit gövdeli 409 `MEETING_CREATE_CONFLICT`, hiçbir yazma olmadan. Yeni satır 201 kalır.
- `FollowUpOfMeetingId` satırının HTTP ölçümü (çağrı P1'de).
- `RequireCreatorCanView` ve `PreAcceptedUserId`: yalnız komut parametresi. Tarama ve yansıma korumaları, HTTP gövde testi.
- `ScheduleFollowUpMeetingHandler`'ın `RequireCreatorCanView` hesabı (K-5).
- Üç neden kodu ve 7 dilde köprü metinleri.

Bu prompt'ta yok: takip toplantısının çarpışma kararı, tamamlama, Q25 satırı, taşıma sırası, tekil dizin, bağlantı hedefi kontrolü (P3); rapor, çözümleyici, matris, 34 sabotajın resmi koşusu (P4).

NEDEN:
- Paket S28-10 (satır 4315–4384): bugün anahtar yalnız düzenleyen, tür, başlangıç, bitiş ve başlıktan oluşur (`MeetingIdempotencyKeyResolver.cs:9-22`). `CreateMeetingHandler` eşleşmede var olan toplantıyı katılımcılarıyla ve 201 ile döner (`MeetingCommandHandlers.cs:151–152, 194, 202–203`). Beş değeri bilen, göremediği toplantıyı okur.
- Anahtara çağıranı eklemek reddedildi (paket satır 4377–4379): K11'i değiştirir.
- Bu kural yalnız doğrudan API çağıranını etkiler; form, Görev Merkezi ve takip penceresi etkilenmez (paket satır 4345–4362).

NASIL:
- **Makine yükü, ortam, taban.** P1 ile aynı: `(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average` 80 üstündeyse 60 saniye bekle. `$env:DITEN_TEST_MONGOD = 'C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe'`; docker yok. Değişiklikten önce DOĞRULA 1 ve 2'yi koş; P1 tabanından farklıysa DUR.
- **Komut** (`MeetingCommands.cs`). `CreateMeetingCommand` son parametreleri (P1'in `SystemOrigin`'inden sonra): `bool RequireCreatorCanView = true`, `Guid? PreAcceptedUserId = null`. HTTP istek modeline konmaz. Çağrılarda ad ile geçilir. Denetleyici değişmez: `MeetingsController.cs:42` iki konumsal argümanla kalır.
- **`CreateMeetingHandler`** (`MeetingCommandHandlers.cs`, satırlar eb561752).
  - **Sıra:** tür kontrolü → düzenleyen uygunluğu (`:115–121`) → `FollowUpOfMeetingId` (`:123–131`, P1) → **yaratıcı kuralı** → anahtar ve `FindOrCreateAsync` (`:133–151`).
  - **Yaratıcı kuralı.** `RequireCreatorCanView` true ise `MeetingEligibility.CanView` oluşturulacak toplantıya uygulanır: istenen düzenleyen, uygunluk süzgecinden (`:178–181`) geçen istenen katılımcılar, çağıran olarak oluşturan, `read-all` (platform aktörü sayılır, S28-5). İşaretleyici varsa eylem kimliği çağırandır ve `read-all` sorulmaz. Geçemezse 400 `MEETING_CREATOR_CANNOT_VIEW`, "You would not be able to see this meeting, so it was not created. Name yourself as organizer or attendee."; toplantı, satır, posta yok. `read-all` cevabı `MeetingWriteAccess` üzerinden gelir, işleyici izin arayüzüne ikinci yol açmaz.
  - **Tekrar dalı.** `isNew == false` (`:152`) ise katılımcı okumadan ve yanıt kurmadan önce `MeetingWriteAccess` var olan toplantıya uygulanır (`SystemOrigin` ile). Görünür: 200 ve toplantı. Görünmez: 409 `MEETING_CREATE_CONFLICT`, "A meeting with these details could not be created."; kimlik ya da alan yok. Bu dalda hiçbir yazma yok (satır, gündem, seri, posta). Yeni satır 201 kalır.
  - **`PreAcceptedUserId`.** Yalnız yeni toplantıda, `:171–189` döngüsünde o tek kullanıcının satırı `InvitationResponse.Accepted`; öteki düzenleyen olmayan satırlar bugünkü gibi `Pending`. Yalnız listede ve süzgeçten geçtiyse yazılır (paket satır 4530–4533). Düzenleyenin satırı değişmez.
- **`ScheduleFollowUpMeetingHandler`** (K-5; `MeetingFollowUpCommandHandlers.cs:92`). Taraf: kaynağın düzenleyeni ya da kaynakta katılımcı satırı olan (hangi yanıt olursa olsun); kimlikler P1'in `MeetingWriteAccess` yüklemesinden gelir. `SystemOrigin` varsa `false`; taraf ise `false`; taraf değilse `true`. `new CreateMeetingCommand(meetingRequest, command.CorrelationId, SystemOrigin: command.SystemOrigin, RequireCreatorCanView: …)`, ad vererek. Bu dosyada başka değişiklik yok; 200 tekrarında bugünkü davranış P3'e kadar sürer.
- **Dokunulmayanlar.** `ScheduleReviewMeetingForTaskHandler` değişmez; düzenleyen çağıran olduğu için kural hep geçer, tekrar dalı içeride uygulanır (paket satır 4094–4101). `GenerateDueMeetingSeriesHandler` değişmez.
- **Neden kodları** (`MeetingModels.cs:55+`, K-8). `CreateConflict = "MEETING_CREATE_CONFLICT"`, `CreatorCannotView = "MEETING_CREATOR_CANNOT_VIEW"`, `FollowUpConflict = "MEETING_FOLLOW_UP_CONFLICT"`. Sınıf içinde `public const string X = "…";` biçiminde: köprü testi bu biçimi okur (`meetings-reason-code-bridge.test.js:38`). `FollowUpConflict` P3'te kullanılır; köprü testi şimdiden ister.
- **Köprü.** `api.js` `REASON_CODE_MESSAGE_KEYS` içine üç satır (K-8). `Views/Meetings/_IndexL10n.cshtml` içine `ErrorX = Localizer["ErrorX"]` biçiminde üç satır (`meetings-reason-code-bridge.test.js:106`). Yedi resx'e üç anahtar. `meetings-reason-code-bridge.test.js` değişmez ve yeşil kalır.
- **Metinler (CT taslağı; terimler ölçülen `MeetingsIndex` satırlarına göre: tr "düzenleyen", "devam"; ru "совещание"; zh "延续会议"; ar "متابعة"; es "continuación"). l10n-agent kontrol eder; değiştirirse Decisions'a yazar. Boş ya da İngilizce kopya olmaz.**

  | Dil | `ErrorCreateConflict` | `ErrorCreatorCannotView` | `ErrorFollowUpConflict` |
  |---|---|---|---|
  | en | A meeting with these details could not be created. | You would not be able to see this meeting, so it was not created. Name yourself as organizer or attendee. | A meeting with these details already exists and is not a follow-up of this meeting. |
  | tr | Bu bilgilerle toplantı oluşturulamadı. | Bu toplantıyı göremeyeceğiniz için oluşturulmadı. Kendinizi düzenleyen ya da katılımcı olarak belirtin. | Bu bilgilerle bir toplantı zaten var ve bu toplantının devamı değil. |
  | fr | Impossible de créer une réunion avec ces informations. | Vous ne pourriez pas voir cette réunion ; elle n'a donc pas été créée. Indiquez-vous comme organisateur ou participant. | Une réunion avec ces informations existe déjà et n'est pas la suite de cette réunion. |
  | es | No se pudo crear una reunión con estos datos. | No podría ver esta reunión, por lo que no se ha creado. Indíquese como organizador o asistente. | Ya existe una reunión con estos datos y no es continuación de esta reunión. |
  | zh | 无法使用这些信息创建会议。 | 您将无法查看此会议，因此未创建。请将自己指定为组织者或参会者。 | 已存在具有这些信息的会议，且它不是此会议的延续会议。 |
  | ar | تعذر إنشاء اجتماع بهذه البيانات. | لن تتمكن من رؤية هذا الاجتماع، لذلك لم يتم إنشاؤه. حدّد نفسك منظّمًا أو مشاركًا. | يوجد بالفعل اجتماع بهذه البيانات وهو ليس متابعة لهذا الاجتماع. |
  | ru | Не удалось создать совещание с этими данными. | Вы не смогли бы видеть это совещание, поэтому оно не создано. Укажите себя организатором или участником. | Совещание с этими данными уже существует и не является продолжением этого совещания. |

- **HTTP konağı.** Yeni test yardımcısı `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Meetings/MeetingAccessHttpMongoHost.cs`, `MeetingAttendeeSearchHttpMongoTests.cs:426–507` desenine göre: gerçek `AddApplication()`, gerçek depolar, `ReplicaSetClassFixture`, JWT ile izin, `UsePlatformAccessPipeline`. Rotalar `MeetingsController`'dan okunur, uydurulmaz. P3 ve P4 bu konağı kullanır. Mail, kaydeden çift üzerinden ölçülür (`MeetingDispatchAssertions.cs`).
- **Satırlar** (paket satırları bağlayıcıdır; ad senin, rapora dosya:satır):
  - **create replay** (satır 4197–4200), HTTP: göremeyen `create` sahibi beş değeri gönderir, 409; gövde sabit baytlarla karşılaştırılır; kimlik, başlık, açıklama, yer, katılımcı, durum, iptal nedeni yok; satır, gündem, görev, bağlantı, posta yazılmaz. Düzenleyen aynısını gönderir, 200 ve toplantı.
  - **`FollowUpOfMeetingId`** (satır 4201–4202), HTTP: görünmeyen toplantıyı adlandıran oluşturma 404, olmayan kimlikle bayt eşit, yazma yok.
  - **creator rule** (satır 4205–4212), HTTP: dört oluşturma durumu ve kaynağın düzenleyen olmayan katılımcısının takip ucundan 201 alması. `organizerUserId: null` hep geçer.
  - **pre-accepted only for that user** (satır 4278–4280), işleyici.
  - **non-party creator rule on the follow-up path** (satır 4283–4287), kaydeden aracıyla: taraf olmayan `true`; düzenleyen ve katılımcı `false`; işaretleyici `false`. S15 HTTP satırı bu prompt'ta yok.
  - **HTTP gövde testi** (satır 4503–4506): `POST /` ve `POST /{id}/follow-up`, fazla alan `preAcceptedUserId` ve ayrı istekte `PreAcceptedUserId`, davetli V: V'nin satırı `Pending`, V'nin Görev Merkezi davet öğesi var (`MeetingWorkItemProviderTests.cs` deseni). P3 takip varyantına zamanlayıcının satırını ekler.
  - **Yansıma testi** (satır 4507–4513, K-7): adı `Request` ile biten `Features.Meetings` tiplerinde `PreAcceptedUserId` ve `RequireCreatorCanView` adlı public özellik ya da kurucu parametresi yok; karşılaştırma `OrdinalIgnoreCase`. Korumanın kendisi harfe duyarsız olduğu için bir satır kültürü `tr-TR` yapar (S-56).
  - **Kaynak taraması** (satır 4514–4523, 4343–4344): Roslyn, S28-2 paketi, `services/Diten.Platform/src/` altındaki her `.cs`. Üç biçim sayılır: `X:` adlı argüman, nesne başlatıcı ya da `with` içinde atama, ikiden çok konumsal argümanlı `new CreateMeetingCommand(…)`. `RequireCreatorCanView`: tam bir dosya, `MeetingFollowUpCommandHandlers.cs`. `PreAcceptedUserId`: K-6.
- **Var olan testler.** Pakete göre tekrarda 201 bekleyen test yok (satır 4382–4384); `MeetingCommandHandlerTests.cs:69–83` durum kodunu iddia etmez. Yeni tekrar testi yukarıdadır.
- **Test planı dondurma.** Seçenek (b): ilk hatada dur. Yedek kapsam yok.
- **Teslim.** Push ve PR yok. Bu prompt sahibin onayıyla dağıtılırsa commit adımı commit onayıdır: DOĞRULA 1–6 temiz; işçi dalında tek yerel commit, açık yollarla; `git diff --cached --name-only`, `--stat`, `--check` rapora; tam içerik `git diff --cached` ile gözden geçirilir. Mesaj: `feat(mg): MOD-0357 S28 P2 — create path: creator rule, replay 200/409, command-only parameters and guards, reason codes in 7 languages`. CT'ye bildir ve dur. P3 bu commit'in üstünden başlar.
- **Satır sonu.** Rapor ve kayıtlar LF. Resx dosyalarının mevcut satır sonu korunur.

YAPMA:
- `Features/Tasks/**` değişmez.
- Yeni izin anahtarı yok.
- S28'de yeni denetim olayı yok. `CreateMeetingCommand` bu parçada yol `a`'ya taşınmaz; `PreAcceptedUserId` yazma değişikliği R1-D12 ile ertelenir, S25 taşır (§25).
- Paket, `gateway/Diten.ApiGateway/ocelot.json`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, Archive ve `.antigravity/**` değişmez.
- `main`'e commit ya da push yok. Yalnız açık yolla stage (`git add -A` ve `git add .` yok). Yıkıcı git yok: `reset --hard`, `clean`, `git checkout -- <yol>`, force push, `stash`.
- Port, kimlik ya da tanımlayıcı uydurma. K-1 ve K-8'deki adlar dışında sözleşme adı gerekiyorsa DUR.
- Başka modülde düzeltme gerekirse dokunma: DUR ve sor (XMC-001).
- Anahtara çağıranı ekleme (`MeetingIdempotencyKeyResolver` değişmez). 404 yerine 409'u değiştirme (paket satır 4372–4379).
- Sabit 409 gövdesine kimlik ya da alan koyma. Göremeyen çağırana katılımcı okuma.
- `RequireCreatorCanView` ya da `PreAcceptedUserId`'yi `CreateMeetingRequest` ya da başka `…Request` tipine koyma; denetleyiciden geçirme.
- Hedef tipli `new(…)` ile `CreateMeetingCommand` kurma: tarama bulamaz (paket satır 4522).
- `form.js`, `follow-up-dialog.js` ve kullanıcı ekranlarını değiştirme. Formda ipucu yok (S26-10).
- Takip toplantısının bağlantı, taşıma, tamamlama ya da Q25 satırına girme (P3).
- `meetings-reason-code-bridge.test.js`'i değiştirme, test `Skip` etme, iddia zayıflatma.

DOĞRULA: Komutlar çalışma ağacı kökünde, Windows PowerShell'de. Her ağır komuttan önce makine yükü ölçülür. `T = services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj`, `R = $env:TEMP\mg-s28-p2`. Bu prompt E2 ve E3 üretir; gerekli E5 P4 sonundadır.
1. Kademe 1: `dotnet build T -c Debug` → 0 hata; uyarı tabana göre yazılır.
2. Kademe 2: `dotnet test T -c Debug --no-build --filter "FullyQualifiedName~Diten.Platform.Application.Tests.Meetings" --results-directory R --logger "trx;LogFileName=meetings.trx"`, sonra `py -3 -I -B -X utf8 scripts/known_failures.py check R\meetings.trx --suite Diten.Platform.Application.Tests` → çıkış 0, 0 kırmızı.
3. Yeni satırlar listede ve geçiyor (NASIL "Satırlar").
4. Köprü: `Push-Location frontend/Diten.Web; npx vitest run tests/meetings-reason-code-bridge.test.js tests/meetings-follow-up.test.js; Pop-Location` → 0 kırmızı.
5. Erken sabotaj (resmi koşu P4). Her biri elle; `Get-FileHash -Algorithm SHA256 <dosya>` önce ve sonra aynı.
   - `MeetingCommandHandlers.cs` içinde bir yerel değişkene `new CreateMeetingCommand(r, c, RequireCreatorCanView: false)` yaz → `RequireCreatorCanView` taraması kırmızı. Geri al → yeşil. Paketin denetleyici sabotajı (satır 4656) denetleyici dosyası gerektirir, P4'tedir.
   - Aynı dosyada `new CreateMeetingCommand(r, c, PreAcceptedUserId: x)` yaz → `PreAcceptedUserId` taraması kırmızı. Geri al → yeşil.
   - `CreateMeetingRequest`'e `PreAcceptedUserId` ekle → yansıma testi kırmızı. Geri al → yeşil.
6. Kapsam ölçütleri, çıktıları rapora: `git diff --stat <P1 commit>` yalnız İzinli yollar; `git ls-files --others --exclude-standard` yalnız İzinli yollar; `git diff <P1 commit> -- services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks services/Diten.Platform/src/Diten.Platform.API gateway tests/architecture execution .antigravity frontend/Diten.Web/Views/Shared frontend/Diten.Web/Resources/SharedResource.en.resx` boş; `git diff --check` temiz.

Durma koşulları:
- Worktree HEAD P1 commit'i değil, dal farklı, ya da başlangıçta kirli dosya var.
- P1'in `MeetingWriteAccess` cevabı katılımcı kimliklerini ya da `read-all` kararını vermiyor ve değişiklik P1'in sözleşmesini bozuyor.
- Taban kırmızı.
- HTTP konağı `MeetingAttendeeSearchHttpMongoTests.cs` deseniyle kurulamıyor, ya da kurmak korunan dosyaya dokunmayı gerektiriyor.
- `POST /` ya da takip ucu bir istek modelinden `PreAcceptedUserId` bağlıyor: o an güvenlik bulgusudur, DUR.
- Bir `Features.Meetings` dışı `…Request` tipi yansıma testine giriyor.
- Çeviri belirsiz, terim kurumsal karşılığı bilinmiyor ya da bir dil İngilizce kopya kalacak.
- `known_failures.py` çıkış 1 ya da 2.
- İzinli yolların dışına çıkmak ya da başka modülde düzeltme gerekiyor (XMC-001).
- 15 dakika boyunca işlemci yükü 80'in altına inmiyor.

Rapor formatı: SOP §22 yapılandırılmış rapor.
- Başlıklar: Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes.
- Tests: taban ve son sayılar; satırlar için dosya:satır; `meetings-reason-code-bridge` sonucu; erken sabotajlarda kırmızı test adları ve özet değerleri; TRX yolları; `known_failures.py` çıktıları.
- Decisions: konak yardımcısının biçimi, l10n-agent'ın değiştirdiği metinler. Sahip ve CT kararları buraya yazılmaz, yalnız anılır.
- Known gaps, en az: 200 tekrarında takip işleyicisi P3'e kadar bağlantı ve satır yazar; `PreAcceptedUserId` koruması P3'te sıkılaşır; `ScheduleReviewMeetingForTaskHandler` tekrar dalı için pakette ayrı satır yok, ölçülmedi; matris ve 34 sabotaj P4'te; `CreateMeetingCommand` denetim taşınması S25'te; bu prompt sonunda parça ana dala gidemez.
- Her iddia için yol:satır ya da komut çıktısı verilir. Ölçmediğini yaz.

Senin PASS'in kapanış değildir; kabul CT'nindir.
