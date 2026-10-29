# HCM-EMP-02 — MOD-0251 gerçeklik denetimi (paket iddiası ↔ kod ↔ kanıt)

> Control Tower kaydıdır (Profile C — inceleme, salt okunur). Onay, paket değişikliği, kod ya da dağıtım izni değildir.
> Kod yazılmadı, düzeltilmedi. Bulgular düzeltme önerisidir; her biri ayrı onay ister.

## Künye

- WP ID: `HCM-EMP-02` · Görev sınıfı: yönetişim / inceleme · Risk: LOW (kayıt) — bulgular arasında güvenlik ve yetki maddeleri var
- Görev kaynağı satırı (birebir): `MOD-0251 Core HR / Employee Master — yapılmış ama bitmemiş: paketin iddiaları kodla ve kanıtla karşılaştırılır (P2 kabul raporu main'de yok, spec yolu taşınmış, domain-config'te eski cümleler, smoke fixture uç noktası, onaya gönderme 409); build ve test koşusu kaydı; kod yok`
- Kuyruk: `hcm-employee` (taban 457ce4146; kayıt satırı `main`'de değil)
- Branch: `chore/ct/hcm-employee-ct-01` · Ölçülen HEAD: ba9dd912987b66981cbee9aafba43a60f415f4dc (kod yolları origin/main 826150ef4 ile aynı) · Worktree: `.claude/worktrees/hcm-employee-ct-01`
- Sahip sözü: `denetim-basla` (2026-10-27, CT · Çalışan kaydı sohbeti)
- Kod okuması: CT + bir salt-okunur alt ajan (Explore, model ana oturumla aynı — Opus 5.5, effort varsayılan; 46 araç çağrısı). CT ajanın şu iddialarını kendi okumasıyla doğruladı: madde 4, 5, 8, 9, 10, 11 ve uç nokta yetkisi (aşağıda ✔). Doğrulanmayanlar "ajan okuması" diye işaretli.

## Derleme ve test (kanıt düzeyi E2)

- `dotnet build services/Diten.HcmService/Diten.HcmService.sln --no-incremental` → 0 uyarı, 0 hata (SDK 10.0.103, hedef net8.0).
- `dotnet test services/Diten.HcmService/Diten.HcmService.sln --logger trx` → 60 geçti / 0 başarısız / 0 atlandı (1 test projesi: `Diten.HcmService.Application.Tests`).
- `python3 scripts/known_failures.py check <trx> --suite hcm-application` → çıkış 0, `BİLİNEN=0 YENİ=0` (HCM için kayıtlı kırmızı yok).
- TRX Git'e alınmadı: bu bir kabul koşusu değil (kart §5 "Kanıt saklama"); geçici klasörde kaldı.
- Çalışma (runtime) testi yapılmadı: servis, Gateway ve ekran ayağa kaldırılmadı (E3 yok).

## Paket iddiaları ↔ kod

| # | Paketin iddiası | Sonuç | Kanıt (`yol:satır@ba9dd9129`) |
|---|---|---|---|
| 1 | Onaya gönderme ve iş akışı kararı kapalı | **Uyuyor** | `Api/Controllers/Hcm/EmployeeDraftsController.cs:80-98` ✔ ikisi de doğrudan 409; durum değişmez |
| 2 | İş akışı başlatma istemcisi çalışma zamanına kayıtlı değil | **Uyuyor** | `Infrastructure/DependencyInjection.cs:22-24` (yalnız audit + referans istemcileri); kalıcılık MongoDB (`Persistence/DependencyInjection.cs:28-39`) — ajan okuması |
| 3 | Aktif çalışan oluşturulmaz, `employee.activated` / `employee.created` üretilmez | **Uyuyor** | servis içinde arama 0; `Employee` yazan tek kod smoke fixture, durum `draft` (`EmployeeSmokeFixtureRepository.cs:49,77,89`) — ajan okuması |
| 4 | Çalışan listesi (registry) ve detay P2'de yok, onaylanmamış | **Uymuyor** | `Api/Controllers/Hcm/EmployeesController.cs:12,24-25,55-56` ✔ `GET /api/v1/hcm/employees` ve `GET /{id}` canlı; ekran `frontend/Diten.Web/Controllers/HCM/EmployeesController.cs:36,44,148-190` (Index, Details, proxy'ler) |
| 5 | Smoke fixture uç noktası yalnız yerel | **Kısmen** | `EmployeeSmokeFixturesController.cs:43-46` ✔ Development/Testing/Local dışında 404; ama Gateway `{everything}` rotası POST/DELETE'i geçirir (madde 10) — koruma yalnız ortam adı |
| 6 | Kiracı izolasyonu, kiracılar arası 404 | **Uyuyor** | depo sorguları TenantId + IsDeleted (`EmployeeDraftSessionRepository.cs:19-48`, `EmployeeRepository.cs:24-165`); yok → 404 — ajan okuması; testler: RegistrySearch, DraftBackend |
| 7 | ETag uyuşmazlığı 409 | **Uyuyor** | `PatchEmployeeDraftHandler.cs:39-42,77-81` — ajan okuması; eksik `If-Match` de 409 (428 değil) |
| 8 | Taslak yazımı güvenli audit ile; audit düşerse yazım durur (GWT-A12) | **Uymuyor** | `Infrastructure/Audit/DraftAuditService.cs:15-30` ✔ yalnız log ("non-authoritative audit fallback"), her zaman başarılı; yönetişimli `GovernedHcmAuditAppendClient` kayıtlı ama hiçbir işleyici kullanmıyor. Paketin "audit wiring pending" notuyla tutarlı, ama GWT-A12 "PASS" anlatımıyla değil |
| 9 | İzin tohumu (permission seed) "pending" | **Uymuyor (yapılmış)** | `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs:35-55` ✔ 14 `mod0251.*` anahtarı; Admin hepsini, Viewer search/view/view_status_history alır. Onaylı dilim dışı anahtarlar (submit, approve, export, change_status …) da tohumlanmış |
| 10 | Gateway yalnız taslak rotaları | **Uymuyor** | `gateway/Diten.ApiGateway/ocelot.json:2788-2854` ✔ 4 rota: `/employees` (GET), `/employees/{everything}` (GET/POST/PATCH/DELETE), `/drafts`, `/drafts/{everything}`; `git blame` ✔ (satır 2788 ve 2806) 9ce74c97f'ye bağlar (2026-07-07 "WIP: recover MOD-0251 HR platform backbone work") |
| 11 | Ekranda dışa aktarma, onaya gönderme, durum/kanıt yer tutucuları yok | **Uyuyor**, bir istisna | düğme yok (`_DraftActionBar.cshtml:5-17`); detay başlığı durum rozeti + sürüm + etag gösterir (`_DetailHeader.cshtml:14-29`) — ajan okuması. İstisna ✔: Platform menü bildirgesi `/HCM/Employees` liste sayfasını kaydeder (`services/Diten.Platform/src/Diten.Platform.Application/Features/HumanCapital/SelfRegistration/HcmEmployeeMasterManifestProvider.cs:26,36`) |
| 12 | Devlet kimlik no alınmaz; hassas okuma yalnız güvenli alanlar | **Uyuyor** | yük koruması (`EmployeeDraftPayloadGuard.cs:9-38`); detay DOB/kişisel e-posta/telefon null döner, `WorkEmail` ve `NationalityCode` maskesiz (`GetEmployeeHandler.cs:42-60`) — ajan okuması |
| 13 | "Focused tests 5/0" | **Eskimiş** | bugün 60 test (52 Fact/Theory); paket sayısı eski |

Ek bulgu (iddia dışı) ✔: `POST /api/v1/hcm/employees/drafts/workflow-decisions` (`EmployeeDraftsController.cs:92-98`) `[HasPermission]` taşımıyor; yalnız sınıf düzeyi kimlik doğrulama (ajan okuması: `:13`). Bugün yalnız 409 döndüğü için zarar üretmez, ama `AGENTS.md` Çekirdek 3 ("her eylem `[HasPermission]` ister") ile çelişir.

## Paket ve belge iddiaları ↔ kanıt

- P2 kabul raporu `docs/records/acceptance-reports/MOD-0251-scope-containment-rework-2026-06-21.md` **depoya hiç girmemiş**: hiçbir ref'te dosya yok; ad yalnız 9ce74c97f mesajında geçer. Paketin "browser-validated", "authenticated replay PASS" iddiaları bu yüzden kayıtlı kanıtsızdır (kart §4: E0 — anlatım).
- Aktif spec yolu yanlış: paket `docs/specs/…-v1.5-BLUEPRINT-ALIGNED.md` der; dosya `docs/reference/architecture/` altında (taşıma 9d8551e1e, 2026-09-07).
- Eskimiş cümleler: paket "Implementation Notes" ve `domain-config.md` "Repo packaging/commit remains unsafe until the no-valid-HEAD … condition is resolved" (repo bugün geçerli HEAD'e sahip); paket "Registry currently does not contain an active MOD-0251 row" (hâlâ doğru — HCM-EMP-01 bulgu 11).

## Sonuç

Taslak dilimi (oluştur, kaydet, yeniden yükle, referans doğrula, gözden geçir) kodda var ve testlerle korunuyor; onaya gönderme ve aktifleştirme gerçekten kapalı. Ama `main`'deki kod paketin onaylı diliminden **geniş**: çalışan listesi ve detay okuması (API + ekran + menü kaydı), Gateway'in geniş rotası ve dilim dışı izin anahtarlarının tohumu onaysız görünüyor. Audit gerçek değil (yalnız log). Kabul kanıtı depoda yok.

## Öneri (öneri — onay değil; her biri ayrı karar)

1. Paketin bağımsız incelemesi (HCM-EMP-03) bu notu girdi olarak alsın; "registry/detail canlı" ve "audit yalnız log" maddeleri incelemenin ilk soruları olsun.
2. Sahip kararı: canlı liste ve detay okuması — (a) P4 kapsamı olarak geriye dönük onaylanıp paket buna göre güncellenir, ya da (b) dilim dışı diye kapatılır (kod işi, ayrı WP). Karar verilmeden yeni dilim taslağı (HCM-EMP-05) bu iki yolu da yazmalı.
3. Küçük kod düzeltme adayları (ayrı WP, ayrı onay; Gateway değişikliği yalnız `integration-agent`): `workflow-decisions` uç noktasına `[HasPermission]`; Gateway `{everything}` rotasından DELETE'in kaldırılması ya da smoke fixture'ın Gateway dışına alınması; yönetişimli audit istemcisinin bağlanması (aktifleştirme diliminin parçası).
4. Paket metni düzeltmeleri (paket değişikliği → `pack_approval.py` onay satırı): spec yolu, test sayısı, kanıt satırlarının "kayıtlı kanıt yok" diye işaretlenmesi, izin tohumunun "pending" değil "yapılmış" yazılması, eskimiş HEAD cümlesi.
