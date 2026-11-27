# WP-0360-FU01-DL2.0 — Tedarikçi Kullanıcı Bağı DL-2.0: tedarikçi portalı izinleri yalnız açık atamayla (`ExplicitGrantOnlyPermissions`, AC-28)

| Alan | Değer |
|---|---|
| Durum | **draft** — dal kararı (S-1) gelmeden çalıştırılmaz. Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir |
| Sahip | Beste (merkez, DC-03 sahibi). Geliştirici: merkez geliştirici oturumu. CT doğrulayıcı: MVP-1 CT |
| Tarih | 2026-11-27 · Sahip kararı: "MOD-0360-FU01 DL-2.0 — devam edelim" (Beste, `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3) |
| Modül | **MOD-0360-FU01** Supplier User Binding (ebeveyn MOD-0360; platform-shared-services; servis `Diten.AuthService` 5056, dışarıya yalnız Gateway 5000) |
| Kimlik ön kontrolü | `python -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0360-FU01 --name "Supplier User Binding" --parent MOD-0360` → `OK … proven against Blueprint/registry`, çıkış 0 (2026-11-27) |
| Paket | `execution/domains/platform-shared-services/module-packs/MOD-0360-FU01-supplier-user-binding.md` **r4**, LF sha256 `c6c9fbf1bb8cb730d0e1bccb9929852540f29c230c02cf1b542a3c3f391dc390` (`git show origin/main:<yol> \| sha256sum`, 2026-11-27) |
| Onay | `execution/approvals/approvals.tsv:112` (origin/main `85e3b1eec`) — onaylayan Ali (GMG-ERP) «onaylıyorum» 2026-11-24, yazan `bestepullukcu-alt`. `python scripts/pack_approval.py check --ref origin/main <paket>` → **onaylı — HIGH · GMG-ERP · 2026-11-24, çıkış 0** (2026-11-27). Not: paket künyesi `status: draft` / `approval_status: pending` metni onaydan önceki hâlidir; yetki kaynağı onay satırıdır (S-49 notu, künye "onaydan sonra ready-for-dev'e döner") |
| İnceleme | r4: `docs/records/reviews/MOD-0360-FU01/2026-11-24-c6c9fbf1.md` KOŞULLU — tek düşük bulgu karar kaydıyla kapandı (`dc03-supplier-user-binding-owner-decisions-01.md` son ek) |
| XMC-001 | Auth (MOD-0018 / MOD-0360 sahibi **Ali**) — izin `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` "Ek — MOD-0360-FU01 r3: MOD-0148 F-2 / F-8 (Ali, 2026-11-24)" ve paket Bölüm 0.3 r3 satırları (§4) |
| Dilim | **DL-2.0** — DL-2'nin ilk commit'i (SQ-17), adlı istisna (S0'ı ve DL-1 kabulünü beklemez, SQ-18). Kanıt seviyesi **E4** |

## 0. Başlama kapıları

| # | Kapı | Durum (ölçüldü 2026-11-27) |
|---|---|---|
| G-1 | r4 CT-APR-02 + CT-APR-01 (Bölüm 0.4 r4, Bölüm 22) | **kapalı** — onay satırı main'de, `check` çıkış 0 |
| G-2 | XMC-001 Auth izni: `ExplicitGrantOnlyPermissions.cs` (yalnız ekleme) ve `AccountKindTests.cs:60-80` | **kapalı** — Ali 2026-11-24 (karar kaydı r3 eki, paket 0.3) |
| G-3 | S0 (Bölüm 0.4) ve DL-1 kabulü | **beklenmez** (SQ-18 KAPANDI, Ali 2026-11-24) |
| G-4 | "MOD-0148 anahtarları Auth kataloğunda" (DL-2 kapısı) | **muaf** (SQ-17: liste anahtar adıdır, katalog kaydı gerektirmez) |
| G-5 | MOD-0148 P4 henüz hiçbir yerde değil (DL-2.0 P4'ten **önce** main'de olmalı) | **sağlanıyor** — `supplychain.supplier-portal` anahtarı `origin/main`'de, hat dalında ve `origin/*` uzak dallarının hiçbirinde `services/**` altında geçmiyor (2026-11-27 taraması). P4 Natig'in işi; P4 birleşmeden önce DL-2.0 main'de olmalı — sıra kanıtı §6 AC-28 (f) |
| G-6 | **S-1 (açık): dal** — DL-2.0'ın main'e P4'ten önce girmesi için hangi dal | **açık** — §1 |

**Sonuç:** başlama kapısı **açık (yetki tamam), tek açık madde dal kararı (S-1)**. P4 yaklaşırsa (Natig P4 PR'ı açarsa) DL-2.0 PR'ı ondan önce birleşmelidir; CT bunu izler.

## 1. Adım 0 — dal (S-1) ve ölçümler

**S-1 — dal kararı (sahip: Beste; Auth olduğu için Ali bilgilendirilir).** DL-1 kodu (`SupplierActorBinding*`) bugün yalnız hat dalında (`feature/sce/mod-0173-inventory-ledger-valuation`, `8c32f4d90`), main'de değil; MVP-1 hattının main'e PR'ı modül sonunda açılır. DL-2.0'ın değiştirdiği üç dosya DL-1'den **bağımsızdır** (hat ile main arasında `ExplicitGrantOnlyPermissions.cs` ve `AccountKindTests.cs` aynı — ölçüldü).
- **Seçenek A (CT önerisi):** `origin/main`'den kısa dal `feature/pss/mod-0360-fu01-dl2-0-explicit-grant` — tek commit, ayrı küçük PR (push ve PR yalnız sahip onayıyla; Ali Auth sahibi olarak inceler). Hat dalı commit'i sonraki main alımıyla alır (aynı içerik, çakışma beklenmez). Gerekçe: "P4'ten önce main'de" (SQ-17) hat PR'ını beklemeden sağlanır.
- **Seçenek B:** commit tek çalışma dalına (hat); main'e hat PR'ıyla girer. Risk: hat PR'ı P4'ten sonra birleşirse sıra kuralı bozulur (AC-28 (f) kırmızı, Bölüm 7: P4 önce gelirse `InternalPermissionsController.cs:96` anahtarları SuperAdmin'e anında verir).
- Cevap gelmeden kod yazılır ama commit atılmaz.

Ölçümler (dilim başı, rapora):
1. Paketin LF sha256'sı `c6c9fbf1` ile başlamalı (`MSYS_NO_PATHCONV=1 git show HEAD:<paket> | sha256sum`); değilse dur.
2. `git log --oneline -5 -- services/Diten.AuthService` (taban ve `origin/main`). 2026-11-27'de Auth'ta main'deki son commit `353090ca5` (2026-11-03). **Eşzamanlı Auth işi:** WP-0018-FIX-01 (rol ataması sessiz düşüyor; merkezi ekip, XMC-001 Beste kararı 2026-11-27) `UserRoleRepository.cs`, `InternalEventsController.cs`, `AssignRoleCommandHandler.cs`, `MongoDbIndexConfigurations.cs` dosyalarına dokunur — DL-2.0'ın dosyalarıyla kesişmez; yine de her commit'ten **hemen önce** tabanı yeniden al, beklenmeyen Auth değişikliği (Ali) görürsen dur, CT'ye yaz.
3. Bugünkü değerler: `ExplicitGrantOnlyPermissions.cs` `Keys` kümesi `:108-114`, 13 anahtar (son girdi `LeaveSyncRunsRead`, `:113`); `AccountKindTests.cs:78` `Assert.Equal(13, ExplicitGrantOnlyPermissions.Keys.Count)`; okuyan üç yol `DefaultRolePermissionTemplate.cs:182`, `FullCatalogPermissionGrantService.cs:39`, `EntitlementPermissionSyncService.cs:294`.
4. **Ana checkout'ta derleme / test yok** (fleet `dotnet watch` ana checkout'u kilitler). Derleme, test ve sabotaj ayrık worktree'de (`git -C C:\Users\Ali\erp-vnext worktree add --detach C:\Users\Ali\erp-vnext\.claude\worktrees\verify-0360-dl20 <SHA>`).

## 2. Kapsam — paketten birebir

**Paket Bölüm 5, "r3 eklenenler", DL-2.0 maddesi (alıntı):**

> **DL-2.0 — otomatik verme dışı (AC-28):** değişir `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` (`:8` sınıf; `:108-114` `Keys` kümesi — iki sabit + iki girdi, yalnız ekleme). Listeyi okuyan yollar (`DefaultRolePermissionTemplate.cs:181-183`, `FullCatalogPermissionGrantService.cs:39`, `EntitlementPermissionSyncService.cs:293-295`) **değişmez**. Değişir (yalnız sayı / ad): `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs:60-80` (`:78` 13 → 15). Yeni test: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/SupplierPortalExplicitGrantPermissionTests.cs` (emsal `Roles/LeaveExplicitGrantPermissionTests.cs`, `Permissions/FullCatalogPermissionGrantServiceTests.cs:61-64`, `Roles/EntitlementPermissionSyncServiceTests.cs:312-316`). **Sıra (bağlayıcı; SQ-17, Beste 2026-11-19, Ali onayı 2026-11-24):** DL-2.0 DL-2'nin **ilk commit'idir**; DL-2'nin katalog kapısından ("MOD-0148 anahtarları Auth kataloğunda", Bölüm 22) muaftır; MOD-0148 P4'ten (anahtarların kataloğa girmesi) **önce** main'de olmalıdır; DL-1 kabulünü ve Bölüm 0.4 S0 kapanışını beklemez (SQ-18 KAPANDI, Ali 2026-11-24); yalnız r4'ün CT-APR-02 + CT-APR-01 onayını ve XMC-001 iznini (Bölüm 0.3) bekler. Kanıt AC-28 (f). DL-2'nin geri kalanı Bölüm 22 kapılarına bağlıdır.

**Paket "Teslim dilimleri", DL-2 satırının DL-2.0 parçası (alıntı):** "r3 DL-2.0 (bağlayıcı sıra, SQ-17: DL-2'nin ilk commit'i, katalog kapısından muaf, DL-1 kabulünü beklemez, MOD-0148 P4'ten önce main'de; r4: S0'ı beklemez, yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001 — SQ-18 kapandı): portal anahtarları `ExplicitGrantOnlyPermissions`'ta (AC-28)".

**Ne eklenir:** iki sabit + iki küme girdisi — `supplychain.supplier-portal.read`, `supplychain.supplier-portal.submit` (tam adlar; sabit adları emsal üslupla, ör. `SupplierPortalRead`, `SupplierPortalSubmit`; XML yorumu karar kaynağını anar: MOD-0360-FU01 r3/r4, Ali 2026-11-24, MOD-0148 D2 / K-5). Küme 13 → **15**.

**Kapsam DIŞI (DL-2'nin geri kalanı, Bölüm 22 kapılarına bağlı ve P4'ten sonra):** `Features/SupplierAuth/**`, tedarikçi kapısı uçları, `supplier_user` token'ı (AC-12 — `SupplierTokenClaimsTests`), S2S `binding_rev` ucu (AC-19, AC-29, AC-30), Auth `TenantResolutionMiddleware.cs` muafiyeti (r4 F-1), `AuthRefusalCodes`, kiracı kapısı reddi, SQ-15; MOD-0148 manifesti ve P4 (Natig); Gateway / `ocelot.json`; Platform; frontend; rol şablonunun kendisi; var olan rol-izin satırlarının silinmesi (liste yalnız **yeni** otomatik vermeyi durdurur — `ExplicitGrantOnlyPermissions.cs:38-40` yorumu).

## 3. Dokunulacak dosyalar

| Yol | Ne |
|---|---|
| `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` | iki `const string` + `Keys`'e iki girdi; yalnız ekleme (var olan satır değişmez) |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs` | `:60-80` testinin adı (`…thirteen…` → `…fifteen…`), iki ada göre `Contains` satırı, `:78` `13` → `15`, üstteki tarihçe yorumuna bir satır; başka değişiklik yok |
| `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/SupplierPortalExplicitGrantPermissionTests.cs` (yeni) | AC-28 (a)…(d) |

Emsal: `5bdf42fa4` (WP-0280-FU02-DL0, aynı üç dosya tipi: liste + `AccountKindTests` + `Roles/…ExplicitGrantPermissionTests.cs`).

## 4. Dokunulmayacaklar ve XMC-001

**Korunan yollar:** `.antigravity/**`; `gateway/**` (ocelot yalnız `integration-agent`); `services/Diten.Building.Blocks/**`; `services/Diten.ProcurementService/**`, `Diten.SupplyChainService/**`, `Diten.MdmService/**`, `Diten.Platform/**`; `frontend/**`; Auth `appsettings*.json`; listeyi okuyan üç yol (`DefaultRolePermissionTemplate.cs`, `FullCatalogPermissionGrantService.cs`, `EntitlementPermissionSyncService.cs`) — **yalnız ölçülür, değişmez**; `InternalPermissionsController.cs`; `AssignPermissionCommandHandler.cs`; paket ve karar kayıtları.

| # | Dosya | Değişiklik | Sahip | Onay kaydı | Hangi commit |
|---|---|---|---|---|---|
| X-1 | `…/Domain/Authorization/ExplicitGrantOnlyPermissions.cs` | iki sabit + iki girdi, yalnız ekleme | Ali (MOD-0018) | dc03 karar kaydı r3 eki (Ali 2026-11-24, "F-2 — evet"); paket 0.3 r3 satırı | DL-2.0 tek commit |
| X-2 | `…/Application.Tests/Users/AccountKindTests.cs:60-80` | sayı / ad, `Contains` satırı | Ali | paket 0.3 r3 satırı ("izin verildi 2026-11-24") | aynı commit |
| X-3 | `…/Application.Tests/Roles/SupplierPortalExplicitGrantPermissionTests.cs` | yeni test | MOD-0360-FU01 (bu paket) | paket Bölüm 5 | aynı commit |

Bu tablonun dışındaki her Auth dosyasında **dur**, CT'ye yaz. Sır tarayıcısı ya da hook takılırsa atlatma, CT'ye yaz.

## 5. Kurallar

- **Yalnız ekleme:** var olan 13 anahtar, sabit adları, yorumlar ve `StringComparer.OrdinalIgnoreCase` aynen kalır.
- **Kiracı / yetki:** liste kiracıdan bağımsız bir yasaktır; hiçbir kiracıya rol / izin satırı yazılmaz, silinmez. Tohum / varsayılan rol değişikliği yok.
- **Dil:** kullanıcıya görünen metin yok (resx yok). Rol İzinleri ekranındaki etiket köprüsü (`perm-label.js` ailesi, label-ledger) DL-2.0'da değişmez — anahtar katalogda değil (P4 yok); P4 sonrası etiket ihtiyacı MOD-0148'in işidir (paket kapsamı dışı).
- **Sırlar:** test değerinde token / parola yok.

## 6. Kabul ölçütleri (paket AC-28 — DL-2.0'a düşen kısım)

Paket AC-28 metni (Bölüm 16) bağlayıcıdır; burada dilimlere ayrılmıştır:

| Parça | İçerik (paketten) | Bu WP'de mi | Test |
|---|---|---|---|
| küme | `Keys` iki tam adı içerir (büyük / küçük harften bağımsız), küme 15 | **evet** | `AccountKindTests.The_explicit_grant_only_set_is_exactly_the_fifteen_owner_decided_keys` + `SupplierPortalExplicitGrantPermissionTests` |
| (a) | `DefaultRolePermissionTemplate.SelectFor` SuperAdmin, Admin, Viewer iki anahtarı döndürmez; sıradan kardeş anahtar Admin ve Viewer'a ulaşır (iki anahtar Admin dalının ulaştığı bir modülün altına, yanına sıradan bir okuma anahtarı konarak — boş geçmesin) | **evet** | `SupplierPortalExplicitGrantPermissionTests` |
| (b) | `FullCatalogPermissionGrantService.GrantToFullCatalogRolesAsync` iki anahtar için rol-izin satırı yazmaz | **evet** | aynı (emsal `FullCatalogPermissionGrantServiceTests.cs:61-64`) |
| (c) | `EntitlementPermissionSyncService` iki anahtarı Admin'e ve Viewer'a vermez, kardeşi verir | **evet** | aynı (emsal `EntitlementPermissionSyncServiceTests.cs:312-316`) |
| (d) | Roller → İzinler açık ataması (`AssignPermissionCommandHandler`) iki anahtarı bir role verebilir | **evet** | aynı |
| (e) | `supplier_user` token'ı iki anahtarı hâlâ taşır (AC-12) | **hayır** — DL-2 (`SupplierAuth/SupplierTokenClaimsTests` henüz yok) | — |
| (f) | Sıra kanıtı: `git merge-base --is-ancestor <DL-2.0> <P4>` çıkış 0; P4 ilk kaydından sonra ortamda iki anahtarda `GrantSource.System` / `GrantSource.Module` satırı 0, yalnız `Manual` olabilir | **hayır** — P4 birleştiğinde CT ölçer, REP-001 koşu kaydına | — |

Testler **üretim** sınıflarını çağırır (`DefaultRolePermissionTemplate`, `FullCatalogPermissionGrantService`, `EntitlementPermissionSyncService`, `AssignPermissionCommandHandler`) — sahte repository'lerle, kopya kural yok.

## 7. Sabotaj listesi (paket AC-28 / Bölüm 17 — DL-2.0'a düşenler)

Her biri: tek değişiklik → hedef test **kırmızı** → bayt-eşit geri al → **yeşil**, `git status` temiz. Hiçbiri commit'e girmez.

| S | Bozulan şey (paket) | Beklenen |
|---|---|---|
| Sabotaj 1 | iki anahtarı `ExplicitGrantOnlyPermissions.Keys`'ten çıkar | (a) SuperAdmin ve Viewer `.read`, (b) ve (c) kırmızı; küme testi kırmızı |
| Sabotaj 2 | `FullCatalogPermissionGrantService.cs:39` denetimini kaldır | yalnız (b) kırmızı (yolun ayrı ölçüldüğünün kanıtı) |
| Sabotaj 5 | yalnız `DefaultRolePermissionTemplate.cs:182` süzgecini (`&& !ExplicitGrantOnlyPermissions.Keys.Contains(p.Key)`) kaldır | (a) kırmızı; (b), (c) yeşil |
| Sabotaj 6 | yalnız `EntitlementPermissionSyncService.cs:294` süzgecini (`.Where(permission => !ExplicitGrantOnlyPermissions.Keys.Contains(permission.Key))`) kaldır | (c) kırmızı; (a), (b) yeşil |

DL-2.0 dışı: Sabotaj 3 (token izin kümesi şablon / liste süzgecinden geçer → (e)) DL-2'de; Sabotaj 4 (P4'ü DL-2.0'dan önce birleştir → (f)) CT'nin P4 ölçümünde.

## 8. Test koşuları

- **Ortam (Auth test altyapısı; WP-0360-FU01-DL1 ile aynı):** üç değişken birlikte —
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`, `DITEN_REQUIRE_OWNED_MONGO=1`,
  `DITEN_ITEST_MONGOD_BIN_DIR="C:/Program Files/MongoDB/Server/9.0/bin"`. Verilmezse `[OwnedMongoFact]` testleri sessizce atlanır; rapor **sıfır atlama** göstermeli. **27017 kullanılmaz.**
- **Filtresiz tam koşu** (proje dosyasıyla, çözümle değil):
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj`,
  `services/Diten.AuthService/tests/Diten.AuthService.ServiceClientRegistry.Tests/Diten.AuthService.ServiceClientRegistry.Tests.csproj`,
  `tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj`.
  **`Diten.AuthService.AccountKindAcceptanceHost.Tests` KESİNLİKLE koşulmaz** (çözüm düzeyinde `dotnet test` de yok).
  Bu makinede iki Platform / Auth Mongo koşusu aynı anda çalıştırılmaz (makine geneli kilit); çıktı dosyaya yönlendirilir. Test ana makinesinde `WithWebHostBuilder` kopyası açma (paylaşılan dev Mongo'ya gider) — gerekirse `ConfigureTestServices`.
- Build: `dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug` PASS.
- Dilim başı ve sonu sayıları; başta kırmızı olanlar adıyla (CT aynı gün `origin/main` ile karşılaştırır).

## 9. Git kuralları (GIT-002)

- Dal: S-1 kararına göre. `main`'e doğrudan commit / push yok.
- Tek commit: `feat(auth): MOD-0360-FU01 DL-2.0 — supplier-portal read / submit are explicit-grant only (AC-28, before MOD-0148 P4)`.
- Stage yalnız açık yolla (üç dosya); `git diff --cached --stat` yalnız bu üç dosyayı gösterir.
- Hook atlanmaz; reset / amend / rebase / squash yok. **Push ve PR yalnız sahibin açık onayıyla** (seçenek A'da PR'ı Beste açar; Ali inceler / birleştirir).

## 10. Teslim raporu

1. S-1 kararı, dal adı, taban SHA; paket sha256.
2. Commit SHA'sı + `git show --stat`.
3. Küme önce → sonra (13 → 15), iki sabitin adı ve satırı.
4. AC-28 küme, (a)…(d) için test adı ve sonuç; (e), (f) "bu dilimde değil" diye.
5. Sabotaj tablosu (1, 2, 5, 6): üretimdeki değişiklik, kırmızı test, geri alma sonrası yeşil.
6. Filtresiz takım sayıları (önce / sonra, atlanan 0) ve taban kırmızıları.
7. Auth'ta eşzamanlı iş (WP-0018-FIX-01, Ali) ile çakışma oldu mu.
8. Açık: AC-28 (f) sıra kanıtı P4 birleştiğinde (CT); Ali'ye bilgi notu (onay değil): iki anahtar listede.

## 11. Kural haritası (AGENTS.md §6.1)

Daima: `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001). Yetki: `permission-key-standard` (PKS-001), `business-module-enforcement-standard`. Kanıt: `status-reporting-and-evidence` (REP-001). Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.
