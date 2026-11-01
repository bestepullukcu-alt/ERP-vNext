---
id: MOD-0018-FU29
name: RBAC Role Assignment Write-Ahead Audit
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: auth-owner
branch: chore/ct/auth-ct-01
started: 2026-11-01
target: TBD
form_field_count: 0
parent: MOD-0018
---

# MOD-0018-FU29 — RBAC Role Assignment Write-Ahead Audit

> **Taslak. Kod yazma izni vermez.** Bu paket kod, katalog, izin, rol, hak, gezinme, yapılandırma, defter ya da kiracı verisi
> değiştirmez. Kod yalnız paket `approved` / `ready-for-dev` olunca ve CT-APR-01 onay satırı main'e girince, aşağıdaki Repo Scope
> listesiyle ve Teslim dilimleri sırasıyla yazılır. Onay satırı yok (CT görev iletisi, 2026-11-01). `branch` alanı paket metninin
> dalıdır. Kod dalı ilk dilimde ayrıca açılır (öneri `feature/pss/mod-0018-fu29-rbac-role-assignment-write-ahead-audit`).
>
> **Sahip kararı (CT görev iletisi, 2026-11-01; sahip: MOD-0018 sahibi):** Hazine'nin D80/2 (b) şartı için "B" seçeneği bu ayrı
> ektir. "Dar yol" FU27 DL-5'in içindedir ve bu pakete girmez. Kapsamdaki yedi RBAC komutu ortak, önce-yaz denetimine geçer.
> Seçilen yol "Önerilen" yoldur. Değişiklikten **önce** bir "deneme" satırı yazılır. Bu yazım kapalı-başarısızdır: satır yazılamazsa
> komut durur. Değişiklikten sonra bir "sonuç" satırı yazılır. Sonuç satırının kimliği deneme kimliğinden türetilir ve satır tek
> yazılır (`WriteOnceAsync` anlamı). Sonuç satırı en iyi çabayla yazılır. Sonuç yoksa okuyucu "sonuçlanmadı / bilinmiyor" gösterir.
> Platform'a iletim (`IPlatformAuditForwarder`) en iyi çabayla, değişiklikten **sonra** kalır.
> **Kayıt durumu (ölçüm 2026-11-01):** Bu kararın depoda karar dosyası satırı bulunamadı. `git log --all -S "dar yol"` Hazine
> kaydı vermedi. `4fdb5dc5d` (Y-28, 2026-10-30) D80/2 (b) cevabını "not metni istendi" diye açık bırakıyor. Karar kaydı Açık
> kararlar (l) maddesindedir.
>
> **DCP-002 kimlik notu.** Paket yazılmadan hemen önce ön kontrol yeniden koşuldu (2026-11-01, HEAD = origin/main `0504c96ea`):
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU29 --name "RBAC Role Assignment Write-Ahead Audit" --parent MOD-0018`
> → `OK  MOD-0018-FU29: proven against Blueprint/registry.` (çıkış 0). Bu sonuç CT'nin önceki koşusuyla aynıdır.
> `--discover "RBAC Role Assignment Write-Ahead Audit"` çıkış 3 ("inceleme gerekli") verdi. Bulgular: Blueprint 0, registry 0,
> paket 0, gateway 2, görev 1; rapor-sha256 `78538be3f8a81d8d2fc93325745edde6e876b442fe2a99559df62fc38b54593f`. Gateway bulguları
> mevcut `audit` → 5057 ve `roles` → 5056 yollarıdır (üretici-tüketici ilişkisi). Görev bulgusu `mvp6-process-pilot-01` Q97'dir ve
> bu kapsamla ilgisizdir. Kayıtta (`execution/registries/module-id-registry.md`) `MOD-0018-FU29` satırı **yok** (`grep -c` → 0).
> Doğrulayıcı ana MOD'un FU'sunu satır olmadan da kabul ediyor. FU27 paketi aynı davranışı FU23 için ölçmüştü. Kayıt satırı bu
> görevin izinli dosyası dışındadır ve ayrı bir değişiklikle yazılır (Ready-for-dev Checklist). `git log --all -S "MOD-0018-FU29"`
> boş döndü: numara hiçbir dalda kullanılmamış. `MOD-0018-FU28` ise `origin/chore/ct/treasury-intake-ct-06` dalında MOD-0057 Auth
> eki için anılıyor (`8321c7709`, `f60522198`) ve kayıt satırı yok.
>
> **Golden Reference kararı.** Bu iş arka uç denetim işidir. Mevcut iki ekrana (Roles, UserRoleAssignments) salt-okunur birer
> geçmiş bölümü eklenir. Yeni sayfa ve yeni DataTable yok: `shell: none`, `golden_reference: none`, `form_field_count: 0`
> bilinçlidir. Yeni varlık da yoktur. Dokunulan tek kayıt türü mevcut `AuthAuditLog`'dur: `GlobalEntityBase` üzerinde durur ve
> kiracıyı kendi `TenantId` alanında taşır (`AuthAuditLog.cs:3, :19` @`0504c96ea`). Bu yüzden `entity_base: GlobalEntity`
> yazıldı. Gerekçe Runtime Constraints'tedir.
>
> **Ölçüm tabanı.** Aksi yazılmadıkça her `dosya:satır` `origin/main` @ `0504c96ea` üzerinde numaralı okumayla alındı. FU27 DL-5
> metni main'de değildir. O metin `origin/chore/ct/treasury-intake-ct-06` @ `47e314f5c` dalından okundu ve oradan anılan her yer
> "ct-06" diye işaretlidir.

## Module Summary

Bugün kişiye rol verme / geri alma ile rola izin bağlama / kaldırma, rol oluşturma, güncelleme ve silme denetim kaydını
değişiklikten **sonra** ve en iyi çabayla yazıyor. Kayıt düşerse değişiklik yine geçerli kalıyor. Kimse de bunu kayıttan göremiyor.
AUD-001 K2 kararına göre (`.antigravity/rules/audit-trail-standard.md` §4.3, satır 150) yetki değiştiren komutta kayıt
yazılamıyorsa komut durmalıdır.

Bu paket yedi komutu ortak bir **önce-yaz** kalıbına geçirir:

| # | Komut | İşleyici (bugün) | Bugünkü yazıcı | Defterdeki yeri (bugün) |
|---|---|---|---|---|
| 1 | `AssignRoleCommand` | `Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs` | `IUserAuditRecorder.RecordAsync` (`:67-68`) | `## K2 borcu` (`Diten.AuthService.md:67`) |
| 2 | `RevokeRoleCommand` | `…/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs` | `IUserAuditRecorder.RecordAsync` (`:85-86`) | `## K2 borcu` (`:74`) |
| 3 | `AssignPermissionCommand` | `Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs` | `IRoleAuditRecorder.RecordAsync` (`:94-95`) | `## Bilinen borç` (`:37`) |
| 4 | `RevokePermissionCommand` | `…/Roles/Handlers/CommandHandlers/RevokePermissionCommandHandler.cs` | `IRoleAuditRecorder.RecordAsync` (`:105-106`) | `## Bilinen borç` (`:50`) |
| 5 | `CreateRoleCommand` | `…/Roles/Handlers/CommandHandlers/CreateRoleCommandHandler.cs` | `IRoleAuditRecorder.RecordAsync` (`:57-58`) | `## Bilinen borç` (`:40`) |
| 6 | `UpdateRoleCommand` | `…/Roles/Handlers/CommandHandlers/UpdateRoleCommandHandler.cs` | `IRoleAuditRecorder.RecordAsync` (`:61-66`) | `## Bilinen borç` (`:53`) |
| 7 | `DeleteRoleCommand` | `…/Roles/Handlers/CommandHandlers/DeleteRoleCommandHandler.cs` | `IRoleAuditRecorder.RecordAsync` (`:66-67`) | `## Bilinen borç` (`:42`) |

İşleyici yolları `services/Diten.AuthService/src/Diten.AuthService.Application/` altındadır. Defter
`tests/architecture/audit-ledger/Diten.AuthService.md`'dir.

**Hedef akış (yedi komutun hepsinde):** korumalar → **deneme satırı** (kapalı-başarısız) → değişiklik → sürüm artırma →
(iki revoke'da refresh token iptali) → **sonuç satırı** (en iyi çaba, tek) → Platform'a iletim (en iyi çaba, bugünkü gibi).

**Kullanıcı:** Kiracı yöneticisi, Roller ekranında bir rolün geçmişini görür. Kullanıcı-rol atama ekranında bir kişinin rol
geçmişini görür. Her satırda kimin, neyi, ne zaman yaptığı ve sonucun ne olduğu yazar. Sonuç yoksa "sonuçlanmadı / bilinmiyor"
yazar. Ekran tarafı Açık kararlar (a) seçeneği (a1) seçilirse yapılır.

## Ownership and Boundaries

**Kapsam içi**

- Yedi komutun işleyicisinde deneme satırının değişiklikten önceye alınması, kapalı-başarısız davranış ve sonuç satırı.
- `IRoleAuditRecorder` ve `IUserAuditRecorder`'a **yalnız ekleme** iki üye (deneme; deneme kimliği alan `RecordAsync` aşırı
  yüklemesi). Mevcut `RecordAsync` üyelerinin anlamı değişmez. `CreateUserCommand`, `DeleteUserCommand`, e-imza komutları ve
  ötekiler aynı yazıcıyı bugünkü gibi kullanır.
- Ortak önce-yaz yazıcısı, deterministik kimlik türetme yardımcısı ve salt-okunur RBAC geçmiş okuyucusu.
- İki geçmiş okuma ucu, meta-denetim okuma satırı ve iki ekranın salt-okunur geçmiş bölümü. Bunlar (a1) seçilirse yapılır.
- Yeni hata kodlarının üç ekranda 7 dilde gösterilmesi.
- Defter ve `AuditTrailStandardTests` sabit sayılarının **aynı değişiklikte** düşürülmesi.

**Kapsam dışı**

- FU27 DL-5: muafiyet atama komutları (`CreatePaymentSodWaiverAssignmentCommand`, `RevokePaymentSodWaiverAssignmentCommand`),
  `auth-sod-muafiyet-gecmisi` izi ve D80/2 (b) "dar yol".
- Treasury / MOD-0057 anahtar-rol kayıtları (FU27 DL-1…DL-3, FU28).
- Platform ve MDM dosyaları. Buna Platform Audit Log ekranının etiket kaynakları da dahildir
  (`frontend/Diten.Web/Views/Platform/AuditLog/**`, `AuditLogIndex.*.resx`).
- Sistem yolları (tohum, `RoleProvisioningService`, hak eşitlemesi, `InternalPermissionsController`, `Q358ShipmentGrantRetractionRunner`).
  Bu yollar işleyicilerden geçmez ve kendi denetim durumları vardır.
- FU-0022-AUD-02 (e-imza komutlarının önce-yazı). Bu paketin temelini kullanabilir ama ayrı iştir (Follow-up Items).
- Reddedilen komutun kaydı (§3 "Reddedilen komut da kaydedilir"): bugün korumalar satır yazmıyor. Bu paket bunu değiştirmez;
  konu Açık kararlar (h) maddesindedir.

**Sahiplik:** MOD-0018 sahibi (auth-owner) yazar ve kabul eder. FU27 DL-5 temeli Treasury CT + MOD-0018 sahibinindir. Bu paket
o temeli tüketir ve kopyalamaz (Dependencies).

## Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| Yazma komutu (mevcut, davranışı değişir) | `AssignRoleCommand`, `RevokeRoleCommand`, `AssignPermissionCommand`, `RevokePermissionCommand`, `CreateRoleCommand`, `UpdateRoleCommand`, `DeleteRoleCommand` | Yeni yazma komutu yok |
| Arayüz üyesi (yalnız ekleme) | `IRoleAuditRecorder.RecordAttemptAsync`, `IRoleAuditRecorder.RecordAsync(…, RbacAuditOutcome, …)` aşırı yüklemesi | yeni üye |
| Arayüz üyesi (yalnız ekleme) | `IUserAuditRecorder.RecordAttemptAsync`, `IUserAuditRecorder.RecordAsync(…, RbacAuditOutcome, …)` aşırı yüklemesi | yeni üye |
| Arayüz | `IRbacWriteAheadAuditWriter` (deneme yaz: kapalı-başarısız; sonuç yaz: tek, `bool` döner, istisna atmaz) | yeni |
| Arayüz | `IRbacAuditHistoryReader` (tek okuma üyesi; güncelleme / silme üyesi yok) | yeni — (a1) |
| Yardımcı | `RbacAuditIds` (deneme kimliğinden sonuç kimliği; SHA-256, RFC 9562 v8) | yeni |
| Değer nesnesi | `RbacAuditOutcome` (deneme kimliği, sonuç `Succeeded` / `Failed` / `NotApplied`, aşama, `MutationApplied` (bool)) | yeni |
| Koruma (yeni) | `RevokeRoleCommandHandler` ve `RevokePermissionCommandHandler`'a eylemi yapan denetimi: `ICurrentUserAccessor.UserId` yoksa deneme satırından önce 401 `ROLE_ACTOR_REQUIRED` | yeni davranış (iki revoke) |
| İstisna | `RbacAuditUnavailableException` (yalnız deneme satırı yazılamayınca) | yeni |
| Sorgu | `GetRoleAuditHistoryQuery`, `GetUserRoleAuditHistoryQuery` | yeni — (a1) |
| Uç | `GET /api/roles/{id:guid}/history`, `GET /api/users/{id:guid}/role-history` | yeni — (a1) |
| Hata kodu | `RBAC_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_PAGING_INVALID` (400) | yeni |
| İzin | Yeni anahtar yok (öneri; Açık kararlar (f)) | — |
| Ekran bölümü | Roller hızlı görünümünde "Geçmiş", kullanıcı-rol atama ekranında "Rol geçmişi" | yeni — (a1) |
| Defter izi | `auth-rbac-gecmisi` (yol `c`, yazıcı) | yeni satır — (a1), CT kararı |

## Entity Fields

Yeni varlık yok. Satırlar mevcut `authAuditLogs` koleksiyonuna, mevcut `AuthAuditLog` biçimiyle yazılır (`AuthAuditLog.cs:3-22`).
Alanlar: `Id`, `EventName`, `UserId` (eylemi yapan), `TenantId`, `Metadata` (JSON), `OccurredAt`. Bunlara FU27 DL-5'in **yalnız ekleme**
alanları `EntityType` (string?) ve `EntityId` (Guid?) eklenir (ct-06 §5.3). Bu paket o alanları eklemez, kullanır.

| Satır | `Id` | `EventName` (öneri; Açık kararlar (c)) | `EntityType` / `EntityId` | `Metadata` |
|---|---|---|---|---|
| Deneme | yeni `Guid` (her komut çalışmasında bir) | `{olay}:attempt` | Rol komutlarında `Role` / rol kimliği; kişi-rol komutlarında `User` / hedef kullanıcı | Bugünkü olayın alanları (rol adı, izin kimliği / anahtarı, rol kimliği; güncellemede istenen `after`) + `actorId` + `phase = attempt` |
| Sonuç | `RbacAuditIds.OutcomeId(denemeKimliği)` | Başarıda bugünkü olay adı (`role_created`, `user_role_assigned` …); başarısızlıkta `{olay}:failed`; yapılmadıysa `{olay}:not_applied` | denemeyle aynı | `attemptId`, `outcome`, `stage` (başarısızlıkta), `mutationApplied` (bool), güncellemede `before` / `after` |

- **`mutationApplied`:** `Succeeded` → `true`. `NotApplied` → `false`. `Failed` + `stage = version` ya da `stage = sessions`
  → `true`, çünkü değişiklik depoya yazıldı ve sonraki adım düştü (yetki gerçekte kalktı ya da verildi). `Failed` +
  `stage = mutation` → `false`, çünkü depo yazımı hata verdi. Bilinen sınır: Mongo onayı yolda kaybolduysa yazım gerçekte uygulanmış
  olabilir. Okuyucu ve ekran rozeti bu alana göre seçilir (Frontend File Contract).

- **Neden olay adları `RoleAuditEvents` / `UserAuditEvents` sınıflarına girmez:** `UserAuditEventLabelGuardTests` iki sınıfın
  sabitlerini Platform Audit Log etiketleriyle eşitliyor (grep `-n`: `UserAuditEventLabelGuardTests.cs:20, :24, :31-45`). Yeni sabit
  eklemek Platform ekranı kaynaklarına dokunmayı gerektirir ve o dosyalar kapsam dışıdır. Ekler (`:attempt`, `:failed`,
  `:not_applied`) ayrı bir sabit sınıfında durur. Platform'a giden olay adları bugünküyle aynı kalır.
- **Dizin:** Yeni dizin yok. FU27 DL-5'in `(TenantId, EntityType, EntityId, OccurredAt)` dizini okumaya yeter (ct-06 §5.3).
  Bugünkü dizinler `MongoDbIndexConfigurations.cs:83-89` içindedir.
- **Değişmezlik (§4.1):** Yazıcıda ve okuyucuda güncelleme / silme / değiştirme üyesi yok. Sonuç satırı yalnız eklemeyle yazılır:
  aynı kimlik ikinci kez yazılmaz, var olan satır değişmez.
- **Kayda yazılmayan:** parola, token, e-posta, ad-soyad. Bu, bugünkü kayıt kuralıyla aynıdır (`IRbacAuditRecorder.cs:3-8`,
  `IUserAuditRecorder.cs:8-9`). Rol adı ve izin anahtarı kişisel veri değildir ve bugün de yazılıyor.

## Repo Scope

Yollar `services/Diten.AuthService/` altındadır. "(a1)" işaretli satırlar yalnız Açık kararlar (a) için (a1) seçilirse yapılır.

### DL-1 — önce-yaz temeli + kişiye rol verme / geri alma

- `src/Diten.AuthService.Application/Common/Interfaces/IRoleAuditRecorder.cs` — yalnız ekleme: iki üye (`:12-20` sonrasına).
- `src/Diten.AuthService.Application/Common/Interfaces/IUserAuditRecorder.cs` — yalnız ekleme: iki üye (`:11-33` içinde).
- `src/Diten.AuthService.Application/Common/Interfaces/IRbacWriteAheadAuditWriter.cs` — **yeni**.
- `src/Diten.AuthService.Application/Common/Services/RbacAuditIds.cs` — **yeni** (türetme kalıbı `ServiceClientRegistryReconciler.cs:208-218`).
- `src/Diten.AuthService.Application/Common/Services/RbacWriteAheadAudit.cs` — **yeni**: `RbacAuditOutcome`, `RbacAuditUnavailableException`, ek sabitleri.
- `src/Diten.AuthService.Application/Common/Services/RoleAuditRecorder.cs` — yeni iki üyenin uygulaması. Mevcut `RecordAsync` (`:24-73`) değişmez.
- `src/Diten.AuthService.Application/Common/Services/UserAuditRecorder.cs` — yeni iki üyenin uygulaması. Mevcut iki `RecordAsync` (`:24-80`) değişmez.
- `src/Diten.AuthService.Persistence/Repositories/RbacWriteAheadAuditWriter.cs` — **yeni**. `authAuditLogs`'a yalnız `InsertOne`
  yapar. Yinelenen anahtarı `AuthAuditService.WriteOnceAsync` (`AuthAuditService.cs:29-50`) gibi ele alır. `EntityType` / `EntityId` doldurur.
- `src/Diten.AuthService.Persistence/DependencyInjection.cs` — yazıcı kaydı (`:109` yanında).
- `src/Diten.AuthService.Application/Features/Roles/RoleErrorCodes.cs` — `RBAC_AUDIT_UNAVAILABLE` (503).
- `src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs`, `RevokeRoleCommandHandler.cs` — akış.
  `RevokeRoleCommandHandler`'a `ICurrentUserAccessor` enjekte edilir ve **yeni** eylemi yapan koruması eklenir. Bugün alanları
  `:13-21`'dedir ve `ICurrentUserAccessor` yoktur; `AssignRoleCommandHandler.cs:48-49` deseni kullanılır.
- **DL-1'de defter değişmez:** `Diten.AuthService.md`, `AuditTrailStandardTests.cs` `PinnedCounts` ve `CT-DECISIONS.md`'ye
  dokunulmaz. K2 çıkışı DL-3'tedir; DL-1'de yalnız CT'nin yazılı kararıyla yapılabilir (Açık kararlar (n)).
- Ön yüz (hata kodu, 7 dil): `frontend/Diten.Web/wwwroot/assets/js/Governance/UserRoleAssignments/index.js` (`ERROR_CODE_KEYS`, `:63`),
  `…/UserRoleAssignments/index.l10n.js`, `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_IndexL10n.cshtml`,
  `frontend/Diten.Web/Resources/Views/Governance/UserRoleAssignments/UserRoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (bugün 29 giriş × 7).
- Testler (yeni): `tests/Diten.AuthService.Application.Tests/Rbac/RbacWriteAheadAuditTests.cs`,
  `…/Users/AssignRoleWriteAheadTests.cs`, `…/Users/RevokeRoleWriteAheadTests.cs`,
  `tests/Diten.AuthService.ServiceClientRegistry.Tests/RbacWriteAheadAuditMongoTests.cs` (yeni) (gerçek Mongo; desen `ServiceClientRegistryMongoTests.cs:365-378`).
  Mevcut testler güncellenir: `…/Users/AssignRoleCommandHandlerTests.cs`, `…/Users/RevokeRoleCommandHandlerTests.cs`, `…/Users/LastStewardPathsTests.cs`.

### DL-2 — rol ve izin komutları + geçmiş uçları

- `src/…/Features/Roles/Handlers/CommandHandlers/{AssignPermission,RevokePermission,CreateRole,UpdateRole,DeleteRole}CommandHandler.cs` — akış.
  `RevokePermissionCommandHandler`'a `ICurrentUserAccessor` enjekte edilir ve **yeni** eylemi yapan koruması eklenir. Bugün alanları
  `:12-21`'dedir ve `ICurrentUserAccessor` yoktur; `AssignPermissionCommandHandler.cs:44-45` deseni kullanılır.
- (a1) `src/…/Common/Interfaces/IRbacAuditHistoryReader.cs` — **yeni**. `src/…/Persistence/Repositories/RbacAuditHistoryReader.cs` — **yeni** (yalnız `Find`).
- (a1) `src/…/Features/Roles/Queries/GetRoleAuditHistoryQuery.cs`, `…/Roles/Handlers/QueryHandlers/GetRoleAuditHistoryHandler.cs` — **yeni**.
- (a1) `src/…/Features/Users/Queries/GetUserRoleAuditHistoryQuery.cs`, `…/Users/Handlers/QueryHandlers/GetUserRoleAuditHistoryHandler.cs` — **yeni**.
- (a1) `src/…/Features/Roles/RoleAuditHistoryModels.cs` — **yeni** (iki sorgunun DTO'ları tek dosyada).
- (a1) `src/…/Features/Roles/RoleErrorCodes.cs` — `RBAC_HISTORY_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_PAGING_INVALID` (400).
- (a1) `src/Diten.AuthService.Api/Controllers/RolesController.cs` — yeni GET (`:65-71` deseni). `…/Api/Controllers/UsersController.cs` — yeni GET (`:328-342` yanında).
- Ön yüz (hata kodu, 7 dil): `frontend/Diten.Web/wwwroot/assets/js/Governance/Roles/index.js` (`:41`), `…/RoleAssignments/index.js` (`:106`),
  iki `index.l10n.js`, iki `_IndexL10n.cshtml`, `Resources/Views/Governance/Roles/RolesIndex.*.resx` (38 × 7),
  `Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.*.resx` (166 × 7).
- Testler (yeni): `tests/Diten.AuthService.Application.Tests/Roles/RoleWriteAheadTests.cs` (beş işleyici, parametreli),
  (a1) `…/Rbac/RbacAuditHistoryTests.cs` (HTTP; gerçek denetleyici), (a1) `…/Rbac/RbacAuditHistoryMongoTests.cs`.
  Mevcut testler güncellenir: `…/Roles/AssignPermissionCommandHandlerTests.cs`, `…/Roles/RevokePermissionCommandHandlerTests.cs`,
  `…/Roles/RoleAuditRecorderTests.cs`, `…/Roles/RoleActorStampTests.cs`, `…/Roles/SystemGrantPathsActorGuardTests.cs`,
  `…/Testing/RoleAuditForTests.cs`, `…/Operational/ProductIdentityEntitlementReconciliationCommandContractTests.cs` (yalnız gerekiyorsa).
- DL-2'de defter değişmez. Beş rol komutu `## Bilinen borç`'ta kalır, çünkü iz henüz kabul edilmedi.

### DL-3 — ekranlar, iz kabulü, borç kapanışı (a1)

- `frontend/Diten.Web/Views/Governance/Roles/_DetailsQuickView.cshtml` — "Geçmiş" bölümü (kısmi görünüm satırı).
- `frontend/Diten.Web/Views/Governance/Roles/_RoleHistory.cshtml` (yeni) — **yeni** kısmi görünüm.
- `frontend/Diten.Web/Views/Governance/UserRoleAssignments/Index.cshtml` — seçili kullanıcı paneline "Rol geçmişi" satırı (`:111-144` bölgesi).
- `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_UserRoleHistory.cshtml` (yeni) — **yeni** kısmi görünüm.
- `frontend/Diten.Web/wwwroot/assets/js/Governance/rbac-history.js` (yeni) — **yeni**. İki ekranın ortak okuyucu-çizici modülüdür (tek kopya).
- `…/js/Governance/Roles/index.js`, `…/Roles/index.l10n.js`, `…/UserRoleAssignments/index.js`, `…/UserRoleAssignments/index.l10n.js`, iki `_IndexL10n.cshtml`.
- `frontend/Diten.Web/Controllers/RolesController.cs` (`:18` kökü) ve `…/Controllers/UserRoleAssignmentsController.cs` (`:18` kökü) — yeni GET eylemleri.
  Eylemler Gateway üzerinden Auth uçlarına gider ve aynı kökten (same-origin) çağrılır.
- `Resources/Views/Governance/Roles/RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx`, `…/UserRoleAssignments/UserRoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
- `tests/architecture/audit-ledger/Diten.AuthService.md` — `## İzler`'e yeni satır ve beş koşul notu. `## Bilinen borç`'tan beş
  satır çıkar (`:37`, `:40`, `:42`, `:50`, `:53`). `## K2 borcu`'ndan iki satır çıkar (`:67` `AssignRoleCommand`, `:74` `RevokeRoleCommand`).
- `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` — `AcceptedTrails` (`:99-103` yanında) yeni
  üçlü. `PinnedCounts` Auth `Debt` ve `K2Debt` (`:61`) aynı değişiklikte değişir. Değerler teslimde ölçülür (Açık kararlar (d)).
- `tests/architecture/audit-ledger/CT-DECISIONS.md` — iz kabulü ve K2 çıkışı satırı.
- Testler: `frontend/Diten.Web.Tests` içinde iki ekranın geçmiş bölümü (izin, 7 dil eşliği, `rbac-history.js` birim testi).

## Protected Paths

- `.antigravity/**` (sahip onayı ister). `audit-trail-standard.md` §4.4'teki eski cümle bu pakette düzeltilmez; bkz. Implementation Notes.
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız `integration-agent`; bu pakette değişiklik gerekmiyor).
- `services/Diten.Platform/**`, `services/Diten.MdmService/**` ve öteki domain servisleri.
- `frontend/Diten.Web/Views/Platform/AuditLog/**` ve `AuditLogIndex.*.resx` (Platform ekranı etiketleri; Entity Fields "neden" maddesi).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IAuthAuditService.cs` ve
  `…/Persistence/Repositories/AuthAuditService.cs`. FU27 D98 "değişmez" diyor (ct-06 §5.3). Açık kararlar (c) (c-ii) seçilirse
  ayrıca onay gerekir.
- `…/Features/ServiceIdentityTokens/**` (İ5 istisnası, `Diten.AuthService.md:26`) ve `ServiceClientRegistryReconciler.cs`.
- FU27 DL-5 dosyaları: `PaymentSodWaiver*`, `IPaymentSodWaiverAuditWriter`, `IPaymentSodWaiverAuditReader`, `_SodWaiverAssignments.cshtml`,
  `sod-waivers.js`. `AuthAuditLog.cs` alan eklemesi ile `PlatformAuditForwarder` / `IPlatformAuditForwarder` `TryForwardAsync` ve
  `IsMetaAudit` eklemeleri de FU27 DL-5'indir. Bu paket onları kullanır ama yazmaz (Dependencies).
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs`, `RoleProvisioningService.cs`, hak eşitlemesi yolları.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, arşiv yolları.
- Aynı klasördeki izlenmeyen yerel taslak `MOD-0018-FU27-finance-treasury-contract-permission-onboarding.md` (bu paketin değil).

## Dependencies

| Bağımlılık | Bugünkü durum (ölçüm) | Bu pakete etkisi |
|---|---|---|
| FU27 DL-5 — `AuthAuditLog.EntityType` / `EntityId` (yalnız ekleme) + `(TenantId, EntityType, EntityId, OccurredAt)` dizini | Paket metni yalnız `origin/chore/ct/treasury-intake-ct-06` @ `47e314f5c` (ct-06 §5.3). D98 `4d9f4b9d8` ve D100 `f60522198` main'in atası değil (`git merge-base --is-ancestor … origin/main` → 1). Kod hiçbir dalda yok: `git log --all -S IPaymentSodWaiverAuditWriter -- services` boş. `AuthAuditLog.cs:3-22`'de alan yok | **DL-1 önkoşulu.** Deneme / sonuç satırı bu alanlarla yazılır. Alanlar yoksa geçmiş okuyucusu satırları bulamaz |
| FU27 DL-5 — `IPlatformAuditForwarder.TryForwardAsync` (yalnız ekleme) + `PlatformAuditEvent.IsMetaAudit` | Yalnız ct-06 metninde (§5.3, AC-46). Kod yok | **DL-2 (a1) önkoşulu.** Geçmiş okuması meta-denetim satırını teslim teyidiyle gönderir |
| FU27 DL-5 — Roller sayfası değişikliği (`_SodWaiverAssignments.cshtml`, `RolesIndex.*.resx`) | ct-06 metni (§5.3 "Ekran") | DL-3 aynı dosyalara dokunur. Birleştirme çatışmasını önlemek için FU27 DL-5 ekranından **sonra** gelir |
| Sıra | — | **FU27 DL-5 (temel + ekran) main'de → FU29 DL-1 → DL-2 → DL-3.** FU27 DL-5 gecikirse sıranın değişmesi Açık kararlar (i)'dedir |
| `auth-kullanici-iletimi` izi (yol `b`, kabul edilmiş) | `AuditTrailStandardTests.cs:103`; `Diten.AuthService.md:12` | DL-1'de iki kişi-rol komutunun "denetleniyor" ölçümü bu izle sürer. Sonuç çağrısı `IUserAuditRecorder.RecordAsync` aşırı yüklemesidir, belirteç aynı kalır (Backend File Convention). İki komut DL-1'de `## K2 borcu`'nda **kalır**. Kapalı-başarısız satır kabul edilmemiş `aday` günlükte durur ve okuyan uç DL-3'te gelir. Bu yüzden K2 çıkışı DL-3'tedir (Açık kararlar (a), (n)) |
| `auth-rbac-gunlugu` izi | `aday` (`Diten.AuthService.md:14, :20`), belirteç `IRbacAuditRecorder.RecordAsync` | Değişmez. Beş rol işleyicisi bu üyeyi doğrudan çağırmıyor (Implementation Notes ölçüm 4) |
| Rol komutlarının borçtan çıkışı | Kabul edilmiş iz yok | **Engel: Açık kararlar (a).** Karar verilmeden beş rol komutu `## Bilinen borç`'tan çıkamaz. DL-2 davranışı kapalı-başarısız yapar ama defteri değiştirmez |
| Mongo | Roller, rol-izin, kullanıcı-rol ve `authAuditLogs` aynı veritabanında (`Persistence/DependencyInjection.cs:89-92` tek `IMongoDatabase`; depolar `:99-103`, denetim `:109`) | Kapalı-başarısız denetim ile acil geri alma dengesi: Açık kararlar (b) |
| FU-0022-AUD-02 (e-imza önce-yaz) | Açık (`MOD-0022-e-signature-service.md:1842`) | Bu paketin `IUserAuditRecorder.RecordAttemptAsync` üyesini kullanabilir. Kapsam ayrı kalır (Follow-up Items) |
| MOD-0021 / Platform merkezi günlük | Değişmez | İletim bugünkü gibi en iyi çabayla ve sonra. Platform dosyası değişmez |
| Kayıt satırı | `MOD-0018-FU29` satırı yok | Ready-for-dev Checklist |

## Runtime Constraints

- **Kiracı (multi-tenancy):** Satırın `TenantId` değeri `ITenantContext.TenantId`'dir (JWT). İstekte kiracı okunmaz. Korumalar
  (kullanıcı / rol `GetByIdAndTenantAsync`) deneme satırından önce koşar. Başka kiracının kimliği bugünkü gibi 404 döner ve satır
  yazılmaz. Geçmiş uçları satırları `TenantId + EntityType + EntityId` ile süzer. Varlık önce aynı kiracıda aranır; yoksa 404 döner.
- **Önce-yaz sırası (yedi işleyicide aynı):** (1) eylemi yapan + varlık + politika korumaları ve son-yetkili korumaları. Beş
  komutta bunlar bugünkü sıra ve kodlardır. **İki revoke'da eylemi yapan koruması yenidir:** `RevokeRoleCommandHandler` ve
  `RevokePermissionCommandHandler` bugün `ICurrentUserAccessor` kullanmıyor (alanlar `RevokeRoleCommandHandler.cs:13-21`,
  `RevokePermissionCommandHandler.cs:12-21`). Bu paketle `UserId` yoksa ilk adımda 401 `ROLE_ACTOR_REQUIRED` döner. Deneme satırı,
  değişiklik ve oturum kapatma yapılmaz. Gerekçe: deneme satırı eylemi yapanı taşımalıdır (§3 alan 1). Revoke'u bugün aktörsüz
  çağıran bir yol yok: iki uç `[HasPermission]` taşır (`UsersController.cs:336-337`, `RolesController.cs:81-82`). (2) Etkisiz işlem (no-op) erken dönüşleri bugünkü gibi satır yazmaz: `AssignRoleCommandHandler.cs:57-58` ve
  `RevokePermissionCommandHandler.cs:65-66`. (3) **Deneme satırı.** Yazılamazsa 503 `RBAC_AUDIT_UNAVAILABLE` döner; değişiklik,
  sürüm artırma, oturum kapatma ve iletim yapılmaz. (4) Değişiklik. (5) Sürüm artırma. (6) İki revoke'da refresh token iptali.
  (7) Sonuç satırı. (8) Platform iletimi.
- **Değişiklikten sonraki hata:** (4)–(6) adımlarından biri istisna atarsa sonuç satırı `Failed` + `stage` (`mutation` /
  `version` / `sessions`) olarak yazılır. `version` / `sessions` aşamasında `mutationApplied = true` olur: değişiklik geçerlidir ve
  ekran bunu "Başarısız" değil, "Uygulandı, sonrası hata verdi" diye gösterir. Bu durumda Platform'a iletim yapılıp yapılmayacağı
  Açık kararlar (m)'dedir. Ardından istisna bugünkü gibi yükselir (bugünkü durum kodu değişmez). Bugün bu durumda hiç
  satır yok: `RevokeRoleCommandHandler.cs:77-81` ve `RevokePermissionCommandHandler.cs:86-101` "kaldırma geçerli kalır, komut
  başarısız" diyor ve satır yazımına (`:85`, `:105`) varılmıyor. `AssignPermission` yarış dalı
  (`DuplicateRolePermissionException`, `AssignPermissionCommandHandler.cs:84-87`) sonuç satırını `NotApplied` yazar ve 409 döner.
- **Sonuç satırı en iyi çabadır:** Yazılamazsa komutun cevabı değişmez ve hata `LogError` ile yazılır (sessiz değil, §4.3 son satır).
  Deneme satırı durur. Okuyucu o denemeyi "sonuçlanmadı / bilinmiyor" gösterir.
- **İşlem (transaction):** Kullanılmaz. Sahip önce-yaz kalıbını seçti. Auth'ta Mongo oturumu başka yerde kullanılıyor
  (`EntitlementReconciliationOperationStore.cs:166`, oturumlu ekleme). İşlem içi yazım seçilmeyen yol olarak kayıtlıdır; aynı
  veritabanı gerçeği Açık kararlar (b)'de tartılır.
- **Mevcut `RecordAsync` anlamı korunur:** `IRbacAuditRecorder.RecordAsync` istisna yutmaya devam eder (`RbacAuditRecorder.cs:28-42`).
  Kapalı-başarısız davranış yalnız yeni `RecordAttemptAsync` yolundadır. Yeni yol `IRbacAuditRecorder`'dan geçmez; doğrudan
  `IRbacWriteAheadAuditWriter`'a yazar.
- **Kimlik türetme:** `OutcomeId = v8(SHA-256("diten:auth:rbac-write-ahead:audit\noutcome\n" + attemptId:D))`. Kalıp
  `ServiceClientRegistryReconciler.cs:208-218`'dir. Deneme kimliği her çalışmada yeni üretilir. Seçenekler Açık kararlar (c)'dedir.
- **Portlar ve yol:** Auth arka ucu Gateway üzerinden çağrılır (`ocelot.json:2205-2221` `/api/users/{everything}`, `:2224-2240`
  `/api/roles/{everything}`; ikisi de 5056'ya gider). Web (5001) Auth'a doğrudan gitmez.
- **Silme:** Satır silinmez. Silinmiş rolün geçmişi bugünkü `GetByIdAndTenantAsync` ile bulunamayabilir; bkz. Açık kararlar (k).
- **`GlobalEntity` gerekçesi:** Yeni kayıt türü yoktur. `AuthAuditLog` mevcut haliyle `GlobalEntityBase`'tir ve kiracıyı kendi
  `TenantId` alanında taşır. Denetim satırı güncellenmez ve yumuşak silinmez (§4.1); taban sınıfın `IsDeleted` / `UpdatedAt`
  alanları bu satırlarda kullanılmaz. DTO ve istek gövdesinde `TenantId` yoktur.

## Layout & Shell Contract

- `shell: none`. Yeni sayfa yok, Razor düzeni değişmez.
- Dokunulan kiracı sayfaları bugünkü düzeni **açıkça** taşır ve öyle kalır:
  - `frontend/Diten.Web/Views/Governance/Roles/Index.cshtml:8` → `Layout = "_LayoutTenantShell";`
  - `frontend/Diten.Web/Views/Governance/UserRoleAssignments/Index.cshtml:7` → `Layout = "_LayoutTenantShell";`
  - `frontend/Diten.Web/Views/Governance/RoleAssignments/Index.cshtml:8` → `Layout = "_LayoutTenantShell";` (yalnız hata metni, DL-2)
- Yeni kısmi görünümler (`_RoleHistory.cshtml`, `_UserRoleHistory.cshtml`) `Layout` atamaz. Sayfanın düzeninde çizilir. AC-24 / AC-25 bunu sınar.
- Kiracı ekranı olduğu için metin 7 dildedir (Çekirdek kural 4).

## Backend File Convention

Golden Reference arka uç düzeni (`module-pack-standard.md` §4): komut / sorgu `sealed record`, işleyici
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` altında `sealed class`. **Yeni** dosyalarda işleyici adında `Command` /
`Query` soneki yoktur (`GetRoleAuditHistoryHandler`, `GetUserRoleAuditHistoryHandler`).

- **Mevcut sapma (bu paketin değil):** Main'deki işleyici dosyaları `…CommandHandler.cs` / `…QueryHandler.cs` sonekini taşıyor
  (ör. `Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs`, `…/QueryHandlers/GetAllRolesQueryHandler.cs`).
  Bu paket o dosyaları düzenler, yeniden adlandırmaz. Ad değiştirmek ayrı bir iştir ve defter belirteçlerini etkilemez.
- **Yeni üyeler (önerilen imzalar; DL-1 başında sabitlenir, sonra yalnız paket revizyonuyla değişir):**

```csharp
// IRoleAuditRecorder — yalnız ekleme
Task<Guid> RecordAttemptAsync(string eventName, Guid tenantId, Guid roleId,
    IReadOnlyDictionary<string, object?> metadata, CancellationToken ct);            // yazılamazsa RbacAuditUnavailableException
Task RecordAsync(string eventName, Guid tenantId, Guid roleId,
    IReadOnlyDictionary<string, object?> metadata, RbacAuditOutcome outcome, CancellationToken ct); // istisna atmaz

// IUserAuditRecorder — yalnız ekleme (targetUserId ile aynı iki üye)
```

- **Belirteç ve ölçüm:** Mimari ölçüm, işleyicide türü bildirilmiş bir değişken üzerinden `ad.Metot(` çağrısı arar
  (`AuditTrailMeasurement.cs:361-375`, `:277-279`). Kişi-rol işleyicileri sonuç için `_userAudit.RecordAsync(…, outcome, ct)`
  çağırır. Bu çağrı kabul edilmiş `IUserAuditRecorder.RecordAsync` belirtecini (`AuditTrailStandardTests.cs:103`) karşılamaya devam
  eder. Aşırı yükleme bugünkü anlamı taşır: tamamlanan olayı yerel satıra yazar ve Platform'a iletir; tek farkı kimliğin
  türetilmesidir. Belirteç bir yöntem adı eşleşmesidir; aşırı yüklemenin bugünkü işi yapması AC-8 ile sınanır (sabotaj kanıtlı).
- **Ortak yazıcı:** `IRbacWriteAheadAuditWriter` yalnız iki yazma üyesi taşır: `WriteAttemptAsync` (istisna atar) ve
  `WriteOutcomeAsync` (`bool` döner, istisna atmaz). Okuma üyesi taşımaz. İşleyiciler bu yazıcıyı doğrudan çağırmaz; kaydediciler
  çağırır. Böylece belirteç kaydedici üyesidir ve `IAuthAuditService.WriteOnceAsync`'i kullanan İ5 / kayıt defteri yolları bu izden
  not almaz. Gerekçe FU27 D97/2 (a) ile aynıdır.
- **Okuyucu (a1):** `IRbacAuditHistoryReader.GetHistoryAsync(Guid tenantId, string entityType, Guid entityId, string[] eventFamily,
  int page, int pageSize, CancellationToken ct)` tek üyedir. Güncelleme / silme üyesi yoktur. Deneme ve sonuç satırlarını
  `attemptId` ile eşler.
- **DI:** Yazıcı `Persistence/DependencyInjection.cs` (`:109` deseni) içine, kaydediciler mevcut kayıtlarıyla
  (`Application/DependencyInjection.cs:34-36`) kalır.
- **Doğrulama boru hattı:** `ValidationBehavior` işleyiciden önce koşar (`Application/DependencyInjection.cs:21`). Yeni sorguların
  doğrulayıcısı yoktur; sayfalama denetimi işleyicidedir ve 400 döner. Bu, FU27 D101 deseniyle aynıdır.

## Frontend File Contract

Yeni sayfa yok. Mevcut Slim ekranlarına bölüm ve metin eklenir.

| Ekran | Dosyalar | Dilim |
|---|---|---|
| Kullanıcı-rol atama (`Views/Governance/UserRoleAssignments/`) | `index.js` `ERROR_CODE_KEYS` + `RBAC_AUDIT_UNAVAILABLE`; `index.l10n.js`; `_IndexL10n.cshtml`; 7 resx | DL-1 |
| Rol-izin atama (`Views/Governance/RoleAssignments/`) | `index.js` `ERROR_CODE_KEYS` (`:106`); `index.l10n.js`; `_IndexL10n.cshtml`; 7 resx | DL-2 |
| Roller (`Views/Governance/Roles/`) | `index.js` `ERROR_CODE_KEYS` (`:41`); `index.l10n.js`; `_IndexL10n.cshtml`; 7 resx | DL-2 |
| Roller hızlı görünümü — "Geçmiş" (a1) | `_DetailsQuickView.cshtml` + **yeni** `_RoleHistory.cshtml`; **yeni** `wwwroot/assets/js/Governance/rbac-history.js`; Web vekili `Controllers/RolesController.cs` | DL-3 |
| Kullanıcı-rol atama — "Rol geçmişi" (a1) | `Index.cshtml` + **yeni** `_UserRoleHistory.cshtml`; aynı `rbac-history.js`; Web vekili `Controllers/UserRoleAssignmentsController.cs` | DL-3 |

- Bölüm salt-okunurdur. Yazma düğmesi yoktur. İzin yoksa bölüm çizilmez; iskelet ya da yönlendirme yoktur (UAS-001).
- Satırda olay, zaman (kullanıcının saat diliminde), eylemi yapan, konu (izin anahtarı / rol adı) ve sonuç rozeti yer alır. Rozet
  değerleri: Başarılı, Başarısız (`mutationApplied = false`), **Uygulandı, sonrası hata verdi** (`Failed` + `mutationApplied = true`,
  `stage = version` / `sessions`), Yapılmadı, Sonuçlanmadı. Beş rozetin metni 7 dil resx dosyalarındadır ve İngilizce dışı değer
  `en` ile aynı olmaz. Güncelleme satırında önce / sonra gösterilir.
- Olay ve sonuç adları sabit koddan kaynak anahtarıyla çevrilir. Ham kod ekranda görünmez.
- `rbac-history.js` iki ekranın tek kopyasıdır. Ekrana özgü dosyada aynı çizim kodu olmaz.
- **Liste ekranı dokunma protokolü (BL-440):** Roller `index.js` dosyasına dokunulduğu için
  `python3 .antigravity/scripts/verify_datatable_page.py . --area Governance --module Roles --format gaps` koşturulur. Sapmalar
  numaralı listeyle sahibe sorulur; sessizce düzeltilmez, sessizce atlanmaz (AC-27). Veri modu değişmez
  (`Roles/_DataTable.cshtml:9`: istemci verisi).

## Validation Rules

Yeni form alanı yok. Komutların mevcut doğrulayıcıları (ör. `Features/Roles/Validators/CreateRoleCommandValidator.cs`) değişmez.

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `id` (geçmiş uçları, rota) | Evet | `{id:guid}` rota kısıtı | — | Rol: `IRoleRepository.GetByIdAndTenantAsync`; kullanıcı: `IUserRepository.GetByIdAndTenantAsync`. Yoksa 404 |
| `page` | Hayır | Varsayılan 1; `< 1` → 400 `RBAC_HISTORY_PAGING_INVALID` | — | — |
| `pageSize` | Hayır | Varsayılan 20, üst sınır 100; `< 1` ya da `> 100` → 400 (kırpma yok). Değerler FU27 D101/1 ile aynıdır | — | — |
| Deneme satırı `metadata` | Evet | Yalnız kimlikler, rol adı, izin anahtarı, güncellemede istenen `after`; e-posta / ad / token yok | `_id` tekil | — |
| Sonuç satırı `Id` | Evet | `RbacAuditIds.OutcomeId(attemptId)`; `Guid.Empty` reddedilir | `_id` tekil (aynı kimlik ikinci kez yazılmaz) | Yinelenen anahtarda satır gerçekten o kimlikte mi diye bakılır (`AuthAuditService.cs:41-48` deseni) |

İlişkili kontroller: `attemptId` boş olamaz. Sonuç satırının `EntityType` / `EntityId` değeri deneme satırıyla aynı olmalıdır;
farklıysa yazıcı reddeder ve `LogError` yazar.

## Failure Path to Verify

- **Deneme satırı yazılamıyor (Mongo hatası / zaman aşımı)** — yedi komut
  - Expected: 503 `RBAC_AUDIT_UNAVAILABLE`. Rol / atama / izin değişmez. Sürüm artmaz. Refresh token iptal edilmez. Platform'a
    iletim yok. Ekran 7 dilde "şu an yapılamadı" der.
- **Duplicate — rol zaten izni taşıyor (yarış)**
  - Expected: 409 `ROLE_PERMISSION_ALREADY_GRANTED` (bugünkü kod). Deneme satırı vardır, sonuç `NotApplied` olur. Sürüm artmaz, iletim yok.
- **Duplicate — rol adı alınmış / kişi zaten rolde**
  - Expected: Bugünkü 409 `ROLE_NAME_TAKEN` / 204 etkisiz işlem. Deneme satırından önce döner; satır yok.
- **Missing — rol / kullanıcı yok ya da başka kiracıda**
  - Expected: 404 (`ROLE_NOT_FOUND` / `USER_ROLE_USER_NOT_FOUND`). Satır yok. Geçmiş uçlarında da 404.
- **Unauthorized — izin yok**
  - Expected: 403 (`[HasPermission]`). İşleyiciye ulaşmaz, satır yok. Geçmiş bölümü çizilmez.
- **Eylemi yapan yok**
  - Expected: 401 `ROLE_ACTOR_REQUIRED`. Beş komutta bu bugünkü davranıştır. `RevokeRole` ve `RevokePermission`'da **yeni**
    korumadır: bugün iki revoke aktörsüz de çalışıyor. Satır, değişiklik ve oturum kapatma yok.
- **Değişiklikten sonra sürüm artırma ya da oturum kapatma hatası (iki revoke; sürüm hatası yedi komutta)**
  - Expected: Değişiklik geçerli kalır (bugünkü gibi). Sonuç `Failed`, `stage = version` / `sessions`, `mutationApplied = true`.
    Bugünkü hata cevabı değişmez. Geçmişte "Uygulandı, sonrası hata verdi" görünür, "Başarısız" görünmez. Platform iletimi Açık
    kararlar (m)'ye göre yapılır.
- **Sonuç satırı yazılamıyor**
  - Expected: Komutun cevabı başarı olarak kalır, `LogError` yazılır. Geçmişte o deneme "Sonuçlanmadı" görünür.
- **Concurrency — aynı sonuç iki kez yazılıyor (yeniden deneme)**
  - Expected: Tek satır kalır. İkinci yazım `false` döner, var olan satır değişmez.
- **Geçmiş okuması meta-denetimi teslim edilemiyor (a1)**
  - Expected: 503 `RBAC_HISTORY_AUDIT_UNAVAILABLE`, veri dönmez. Sebepler: anahtar boş, 2xx dışı yanıt, bağlantı hatası, zaman aşımı.
- **Sayfalama geçersiz (a1)**
  - Expected: 400 `RBAC_HISTORY_PAGING_INVALID`.
- **Son yetkili koruması (bugünkü)**
  - Expected: 409 `USER_ROLE_REVOKE_LAST_STEWARD` / `ROLE_PERMISSION_REVOKE_LAST_STEWARD` / `ROLE_DELETE_LAST_STEWARD`. Deneme satırından önce döner, satır yok.

## Authorization Convention

```text
Policy:     [Authorize]  (kiracı uçları; CustomBaseController)
Permission: [HasPermission("auth.{resource}.{action}")]   (PKS-001: küçük harf, noktalı, ≥ 3 parça)
Actor type: tenant_user | platform_admin (kiracı bağlamında)
```

| Uç | İzin (bugün / öneri) | Kaynak |
|---|---|---|
| `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}` | `auth.users.assign-role` (değişmez) | `Api/Controllers/UsersController.cs:328-342` |
| `POST /api/roles` · `PUT /api/roles/{id}` · `DELETE /api/roles/{id}` | `auth.roles.create` · `.update` · `.delete` (değişmez) | `RolesController.cs:39-40`, `:48-49`, `:57-58` |
| `POST /api/roles/{id}/permissions`, `DELETE /api/roles/{id}/permissions/{permissionId}` | `auth.roles.assign-permission` (değişmez) | `RolesController.cs:73-74`, `:81-82` |
| (a1) `GET /api/roles/{id}/history` | **öneri `auth.roles.read`** — rolü gören geçmişini de görür (§5.c-3, CT kararı 2026-10-02) | Açık kararlar (f) |
| (a1) `GET /api/users/{id}/role-history` | **öneri `auth.users.assign-role`** — kaydın gösterildiği ekranı açan anahtar (`UserRoleAssignments/Index.cshtml:21`, `Controllers/UserRoleAssignmentsController.cs:23`) | Açık kararlar (f) |

- Yeni anahtar önerilmiyor. Yeni anahtar seçilirse PKS-001 biçimine uyar (ör. `auth.roles.history.read`) ve katalog / tohum / açık
  atama listesi ayrı bir onayla gelir.
- Ekran bölümü `Perms.Has(...)` ile yalnız izin varken çizilir. Web vekili yalnız gösterim ve vekil kapısıdır; yetkinin sahibi Auth `[HasPermission]`'dır.
- Kapalı-başarısız davranış için yeni bir izin yolu açılmaz. Platform yöneticisi de aynı kurala tabidir.

## Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**.

- Ön yüz Gateway 5000 üzerinden çağırır, servis portuna doğrudan gitmez.
- Mevcut `ocelot.json`'da `/api/users/{everything}` (`:2205-2221`) ve `/api/roles/{everything}` (`:2224-2240`) var. Yöntemleri
  GET / POST / PUT / OPTIONS / DELETE'dir. İki yeni GET ucu bu yolların altındadır.
- `integration-agent` görevi yok. `ocelot.json` korumalı yoldur ve bu paket ona yazmaz.

## Acceptance Criteria

- [ ] AC-1 (zorunlu) — **Deneme önce (kişi-rol):** `AssignRoleCommand` deneme satırını `IUserRoleRepository.AssignAsync`'den,
  `RevokeRoleCommand` deneme satırını `IUserRoleRepository.RevokeAsync`'den önce yazar. Sıra, çağrı sırasını kaydeden sahte depolarla
  sınanır. Sabotaj: deneme çağrısı değişiklikten sonraya taşınınca sıra testi kırmızıya döner.
- [ ] AC-2 (zorunlu) — **Kapalı-başarısız (kişi-rol):** Deneme yazımı başarısız olunca iki komut 503 `RBAC_AUDIT_UNAVAILABLE` döner.
  Atama / geri alma, `IRoleAssignmentVersionService.IncrementAsync`, `RevokeAllByUserAsync` ve Platform iletimi **hiç çağrılmaz**.
  Gerçek Mongo testinde `user_roles` değişmez. Sabotaj: kaydedici deneme hatasını yutup devam edince test kırmızıya döner.
- [ ] AC-3 (zorunlu) — **Tek sonuç satırı:** Sonuç satırının kimliği `RbacAuditIds.OutcomeId(attemptId)`'dir. Aynı sonucun ikinci
  yazımı gerçek Mongo'da ikinci satır üretmez, `false` döner ve ilk satır değişmez. Farklı deneme kimlikleri farklı sonuç kimliği
  verir; aynı deneme kimliği her süreçte aynı sonucu verir (sabit girdi → sabit beklenen GUID). Sabotaj: türetme yerine
  `Guid.NewGuid()` kullanılınca tekillik ve sabit değer testleri kırmızıya döner.
- [ ] AC-4 (zorunlu) — **Sonuç en iyi çaba:** Sonuç yazımı istisna atınca komut yine 204 döner ve `LogError` kaydı oluşur
  (günlük yakalayıcıyla sınanır). Deneme satırı durur. Sabotaj: sonuç hatası yükseltilince 204 testi kırmızıya döner. Günlük satırı
  kaldırılınca günlük testi kırmızıya döner.
- [ ] AC-5 (zorunlu) — **Değişiklikten sonraki hata (RevokeRole):** `RevokeAllByUserAsync` istisna atınca sonuç satırı `Failed`,
  `stage = sessions`, `mutationApplied = true` olarak yazılır ve istisna bugünkü gibi yükselir. `IRoleAssignmentVersionService.IncrementAsync`
  istisna atınca aynı satır `stage = version` ile yazılır. Rol kaldırma geçerli kalır. `IUserRoleRepository.RevokeAsync` istisna atınca
  sonuç `stage = mutation`, `mutationApplied = false` olur. Sabotaj: hata dalındaki sonuç yazımı kaldırılınca, ya da
  `mutationApplied` `version` / `sessions` aşamasında `false` yazılınca test kırmızıya döner.
- [ ] AC-6 (zorunlu) — **Korumalar ve etkisiz işlem satır yazmaz:** 401 (eylemi yapan yok; beş komutta bugünkü, iki revoke'da AC-29 /
  AC-30 ile yeni), 404 (kullanıcı / rol yok, başka kiracı), 409 (son yetkili) ve 204 etkisiz atama deneme satırı **yazmaz**. 404 / 409 /
  204 cevapları bugünkü gibidir. Sabotaj: deneme çağrısı korumaların önüne alınınca 401 / 404 / 409 testleri satır bulur ve kırmızıya
  döner.
- [ ] AC-7 (zorunlu) — **İletim sonra ve aynı:** Başarıda Platform'a değişiklikten sonra, bugünkü yükle iletim yapılır: `EntityType User`,
  hedef kullanıcı, `Operation` Assign / Revoke, `Succeeded`, aynı `Metadata` anahtarları. Yük bugünkü testlerin beklenen değeriyle
  eşittir. `Failed` + `mutationApplied = true` durumunda iletim Açık kararlar (m) kararına göre yapılır: öneri kabul edilirse bilinen
  olay adı + `Failed` sonucu iletilir. `mutationApplied = false` ya da `NotApplied` durumunda iletim yapılmaz. İletim hatası komutu
  bozmaz. Sabotaj: iletim deneme satırından önceye alınınca, ya da `mutationApplied = false` sonucunda iletilince test kırmızıya döner.
- [ ] AC-8 (zorunlu) — **Mevcut `RecordAsync` anlamı korunur:** `IUserAuditRecorder.RecordAsync` ve `IRoleAuditRecorder.RecordAsync`
  eski imzaları ile `IRbacAuditRecorder.RecordAsync` istisna atmamaya devam eder. `CreateUser`, `DeleteUser`, `SetUserActiveStatus` ve
  e-imza işleyicilerinin mevcut testleri değişmeden yeşildir. Yeni `RecordAsync(…, outcome, …)` aşırı yüklemesi bugünkü yerel satırı
  ve iletimi üretir; tek farkı türetilmiş kimliktir (birim testi). Sabotaj: aşırı yükleme yalnız sonuç satırı yazıp iletimi atlayınca
  AC-7 testi kırmızıya döner. `RbacAuditRecorder` istisna yükseltince `CreateUser` testi kırmızıya döner.
- [ ] AC-9 (zorunlu) — **Defter, DL-1 — değişmez:** `AssignRoleCommand` ve `RevokeRoleCommand` `## K2 borcu`'nda kalır.
  `PinnedCounts` Auth (`Debt 18, Exceptions 1, K2Debt 11`) DL-1'de değişmez. Erken çıkış ancak Açık kararlar (n)'deki yazılı CT
  kararıyla olur. İki komut `auth-kullanici-iletimi` ile "denetleniyor" ölçülmeye devam eder.
  `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` yeşildir.
  `git diff <taban>..HEAD -- tests/architecture/audit-ledger/` boştur (`<taban>` açıkça yazılır). Sabotaj: işleyicide
  `_userAudit.RecordAsync(` çağrısı kaldırılınca komut denetlenmiyor olur ve `K2Debt_NamesAuditedCommands_AndOnlyShrinks` kırmızıya
  döner ("denetlenmiyor; yeri Bilinen borç"). K2 satırı çıkarılıp sabit sayı bırakılınca `DebtAndExceptionCounts_ArePinned_…`
  kırmızıya döner. Satır ve sayı birlikte değiştirilirse bunu yalnız inceleme yakalar (§9): CT kararı yoksa reddedilir.
- [ ] AC-10 (zorunlu) — **Hata metni 7 dil (kullanıcı-rol ekranı):** `RBAC_AUDIT_UNAVAILABLE` `UserRoleAssignments` ekranında kaynak
  anahtarına eşlenir. Anahtar 7 resx dosyasında vardır ve İngilizce dışı değer `en` ile aynı değildir. `index.l10n.js` anahtar listesi
  eksiksizdir. Sabotaj: bir dil dosyasından anahtar silinince `Diten.Web.Tests` kırmızıya döner.
- [ ] AC-11 (zorunlu) — **Kiracı ve içerik:** Deneme ve sonuç satırının `TenantId` değeri token kiracısıdır, `UserId` eylemi
  yapandır. `Metadata` yalnız izinli anahtarları taşır (izin listesi testte sabit). E-posta, ad, token yoktur. Sabotaj: `Metadata`'ya
  `email` eklenince izin listesi testi kırmızıya döner. Kiracı istekten alınınca A / B kiracı testi kırmızıya döner.
- [ ] AC-12 (zorunlu) — **Önce-yaz ve kapalı-başarısız (beş rol komutu):** `CreateRole` (`CreateAsync` öncesi), `UpdateRole`
  (`UpdateAsync` öncesi), `DeleteRole` (`DeleteAsync` öncesi), `AssignPermission` (`held` denetiminden sonra, `AssignAsync` öncesi) ve
  `RevokePermission` (S-GUARD ve son-yetkili korumasından sonra, `RevokeAsync` öncesi) için AC-1, AC-2, AC-4, AC-6 ve AC-7 maddeleri
  parametreli tek testle sınanır. Rol tarafında iletim `EntityType Role` taşır. Sabotaj: herhangi bir işleyicide deneme çağrısı
  kaldırılınca ya da sonraya alınınca o işleyicinin satırı kırmızıya döner.
- [ ] AC-13 (zorunlu) — **Yarış dalı (AssignPermission):** `DuplicateRolePermissionException` gelince 409
  `ROLE_PERMISSION_ALREADY_GRANTED` döner. Sonuç satırı `NotApplied` olur. Sürüm artmaz, iletim yapılmaz. Sabotaj: dal sonuç yazmadan
  dönünce geçmiş testi "Sonuçlanmadı" bulur ve kırmızıya döner.
- [ ] AC-14 (zorunlu) — **Değişiklikten sonraki hata (RevokePermission):** Bir sahibin refresh token iptali istisna atınca sonuç
  `Failed`, `stage = sessions`, `mutationApplied = true` olur ve istisna yükselir. Sürüm artırma hatasında aynı satır
  `stage = version` ile yazılır. Beş rol komutunun hepsinde sürüm artırma hatası aynı biçimde sınanır. İletim AC-7 kuralına uyar
  (Açık kararlar (m)). Sabotaj: AC-5 ile aynı.
- [ ] AC-15 (zorunlu) — **Güncellemenin önce / sonrası:** `UpdateRole` deneme satırı istenen `after` değerini, sonuç satırı
  `before` / `after` değerini taşır. Platform'a giden `BeforeState` / `AfterState` bugünkü gibidir (`RoleAuditRecorder.cs:45-65`).
  Sabotaj: `before` yakalaması değişiklikten sonraya alınınca test kırmızıya döner.
- [ ] AC-16 (zorunlu) — **Rol geçmişi ucu (a1):** `GET /api/roles/{id}/history` yalnız o rolün (`EntityType Role`, `EntityId`) ve
  token kiracısının satırlarını döner. Satırlar en yeniden eskiye sıralanır ve deneme + sonuç tek öğede eşlenir. Sonucu olmayan deneme
  `outcome = unknown` döner. İzinsiz 403, başka kiracının rolü 404, olmayan rol 404. Sayfalama Validation Rules'daki değerlerle
  yapılır: 21 satırlı rolde varsayılan istek 20 öğe + `TotalCount = 21`, `pageSize = 101` → 400. Uç gerçek denetleyiciyle HTTP testinde
  sınanır. Sabotaj: kiracı süzgeci kaldırılınca başka kiracı testi kırmızıya döner. Eşleme kaldırılınca `unknown` testi kırmızıya döner.
- [ ] AC-17 (zorunlu) — **Kişi rol geçmişi ucu (a1):** `GET /api/users/{id}/role-history` yalnız o kullanıcının
  (`EntityType User`) rol atama / geri alma olaylarını döner. Öteki kullanıcı olayları (`user_created` vb.) dönmez. Kiracı, izin,
  404 ve sayfalama AC-16 gibidir. Sabotaj: olay ailesi süzgeci kaldırılınca `user_updated` satırı döner ve test kırmızıya döner.
- [ ] AC-18 (zorunlu) — **Meta-denetim teslimi (a1):** Her geçmiş okuması `IsMetaAudit = true` okuma satırını
  `TryForwardAsync` ile gönderir. Veri yalnız teslim teyidi gelince döner. Gerçek `PlatformAuditForwarder`, Platform iç ucunun
  sözleşmesine uyan saplama sunucuya karşı koşar: (i) 2xx → 200 + veri; (ii) anahtar boş → 503 `RBAC_HISTORY_AUDIT_UNAVAILABLE` ve
  saplamaya çağrı yok; (iii) 500 / 401 → 503; (iv) bağlantı reddi / zaman aşımı → 503. Bu madde FU27 AC-46 deseniyle aynıdır
  (ct-06). Sabotaj: teslim teyidi yok sayılınca (iii) ve (iv) kırmızıya döner.
- [ ] AC-19 (zorunlu) — **Okuyucu salt-okunur (a1):** `IRbacAuditHistoryReader` ve `IRbacWriteAheadAuditWriter` arayüzlerinde
  `Update` / `Delete` / `Replace` adlı üye yoktur (yansıma testi). Gerçek Mongo'da geçmiş satırları hiçbir uçtan değiştirilemez. Sabotaj:
  arayüze `DeleteAsync` eklenince yansıma testi kırmızıya döner.
- [ ] AC-20 (zorunlu) — **Hata metni 7 dil (Roller + rol-izin ekranı):** `RBAC_AUDIT_UNAVAILABLE` iki ekranda eşlenir.
  `RBAC_HISTORY_AUDIT_UNAVAILABLE` ve `RBAC_HISTORY_PAGING_INVALID` geçmiş bölümlerinde eşlenir. Anahtarlar 7 dosyada vardır ve
  İngilizce dışı değer `en` ile aynı değildir. Sabotaj: AC-10 ile aynı.
- [ ] AC-21 (zorunlu) — **DL-2'de defter değişmez:** Beş rol komutu `## Bilinen borç`'ta kalır ve `AuditTrailStandard` yeşildir
  (iz kabul edilmediği için "denetlenmiyor" ölçülürler). `git diff <taban>..HEAD -- tests/architecture/audit-ledger/` boştur.
  Sabotaj: DL-2'de bir borç satırı çıkarılınca `KnownDebt_OnlyShrinks_…` kırmızıya döner (komut denetlenmiyor ama listede yok).
- [ ] AC-22 (zorunlu) — **İz kabulü ve borç kapanışı (a1, DL-3):** `## İzler`'e
  `auth-rbac-gecmisi | c | yazıcı | IRoleAuditRecorder.RecordAttemptAsync/IUserAuditRecorder.RecordAttemptAsync` satırı ve beş koşulun
  kanıt notu girer. `AcceptedTrails`'e aynı üçlü eklenir. `## Bilinen borç`'tan beş rol komutu, `## K2 borcu`'ndan `AssignRoleCommand`
  ve `RevokeRoleCommand` çıkar. `PinnedCounts` Auth `Debt` ve `K2Debt` aynı değişiklikte ölçülen değere iner. Beklenen değerler
  (hesap; teslimde ölçülür): `Debt 18 → 13`, `K2Debt 11 → 9`, `Exceptions 1`. `CT-DECISIONS.md` satırı yazılır. `AuditTrailStandard`
  yeşildir ve `grep -E '^\+- '` boştur. Sabotaj: `AcceptedTrails` üçlüsü kaldırılınca beş rol komutu "yeni denetimsiz" olur ve
  kırmızıya döner. Bir rol işleyicisinden `RecordAttemptAsync` çağrısı kaldırılınca o komut kırmızıya döner. Sayılardan biri eski
  değerinde bırakılınca `DebtAndExceptionCounts_ArePinned_…` kırmızıya döner.
- [ ] AC-23 (zorunlu) — **K2 sınıfı tutarlılığı:** K2 çıkışı DL-3'tedir. DL-1 ve DL-2 birleştikten sonra iki kişi-rol komutu
  `## K2 borcu`'nda, beş rol komutu `## Bilinen borç`'ta kalır; davranışları ise kapalı-başarısızdır (AC-2, AC-12). DL-3 birleştiğinde
  yedi komutun hiçbiri iki listede değildir, yedisi de kabul edilmiş ve okunabilen `auth-rbac-gecmisi` izine yazar ve AC-2 / AC-12
  testlerini taşır. `## K2 borcu` sayısı hiçbir dilimde büyümez. DL-1'de erken çıkış yalnız Açık kararlar (n)'deki yazılı CT
  kararıyla olur. Sabotaj: rol komutlarına kredi DL-2 kapalı-başarısız davranışı olmadan verilirse AC-12 kırmızıdır. Bu yüzden DL-3,
  DL-1 ve DL-2'den önce birleşemez (Teslim dilimleri sırası).
- [ ] AC-24 (zorunlu) — **Roller ekranı "Geçmiş" (a1):** `auth.roles.read` (ya da (f) kararındaki anahtar) iznine sahip kullanıcı
  hızlı görünümde rolün geçmişini görür. İzni olmayana bölüm çizilmez; iskelet ve yönlendirme yoktur (UAS-001). Yazma düğmesi yoktur.
  Ham olay kodu görünmez. `Roles/Index.cshtml:8` `Layout = "_LayoutTenantShell";` aynen kalır ve kısmi görünüm `Layout` atamaz
  (ön yüz testi dosyayı okur). Bütün metin `RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx` dosyalarındadır ve İngilizce dışı değer `en` ile
  aynı değildir. Sabotaj: izin denetimi kaldırılınca ya da bir dil dosyasından anahtar silinince `Diten.Web.Tests` kırmızıya döner.
- [ ] AC-25 (zorunlu) — **Kişi-rol ekranı "Rol geçmişi" (a1):** AC-24 aynı biçimde `UserRoleAssignments` ekranı
  (`Index.cshtml:7` düzeni) ve `UserRoleAssignmentsIndex.*.resx` için geçerlidir. Sabotaj: AC-24 ile aynı.
- [ ] AC-26 (zorunlu) — **Çalıştırılmış kanıt (§8-(3)):** `dev` ortamında bir rol oluşturma, bir güncelleme ve bir silme koşulur.
  Üç deneme + sonuç çifti geçmiş ucundan gösterilir. Aynı üç olay Platform `audit_events` sorgusuyla
  (`EntityType: "Role"`, `EntityId`) gösterilir. Bir izin bağlama ve bir kişi-rol atama da gösterilir. Rapor ortamın adını yazar.
  §3'ün on alanı tek tek işaretlenir; eksik alan "bilinen boşluk" olarak yazılır (ör. eylemi yapanın o andaki adı, Açık kararlar (j)).
  Sabotaj kanıtı bu maddede aranmaz: madde çalışma kanıtıdır, test değildir.
- [ ] AC-27 (zorunlu) — **Liste ekranı protokolü:** Roller `index.js` dokunuşundan sonra
  `python3 .antigravity/scripts/verify_datatable_page.py . --area Governance --module Roles --format gaps` çıktısı teslim kaydına
  yazılır. Sapma varsa numaralı liste sahibe sorulur ve cevap kayda geçer.
- [ ] AC-28 (isteğe bağlı) — **Sayfa yardımı:** Roller ya da kullanıcı-rol ekranının sayfa yardımı varsa (DL-3 başında ölçülür) yeni
  bölüm için 7 dilde bir paragraf eklenir. Sabotaj: bir dil dosyasından paragraf silinince yardım eşlik testi kırmızıya döner.
- [ ] AC-29 (zorunlu) — **Yeni eylemi yapan koruması (RevokeRole, DL-1):** `RevokeRoleCommandHandler` `ICurrentUserAccessor` alır.
  `UserId` yoksa ilk adımda 401 `ROLE_ACTOR_REQUIRED` döner. Kullanıcı / rol araması, son-yetkili denetimi, deneme satırı,
  `RevokeAsync`, sürüm artırma ve `RevokeAllByUserAsync` **hiç çağrılmaz**. Aktör varsa bugünkü davranış ve cevaplar aynıdır
  (gerileme). Deneme satırının `UserId` değeri bu aktördür. Sabotaj: koruma kaldırılınca aktörsüz test 204 alır ya da deneme satırı
  `UserId = Guid.Empty` ile yazılır ve test kırmızıya döner.
- [ ] AC-30 (zorunlu) — **Yeni eylemi yapan koruması (RevokePermission, DL-2):** AC-29 aynı biçimde `RevokePermissionCommandHandler`
  için geçerlidir. Çağrılmayanlar: rol / hak araması, S-GUARD, son-yetkili denetimi, deneme satırı, `RevokeAsync`, sürüm artırma ve
  sahiplerin oturum kapatması. Etkisiz işlem (204, hedef hak yok) da aktörsüz istekte 401 döner, çünkü koruma ilk adımdır.
  Sabotaj: AC-29 ile aynı.
- [ ] AC-31 (zorunlu) — **"Uygulandı, sonrası hata verdi" rozeti (a1, DL-3):** Okuyucu `Failed` + `mutationApplied = true` sonucunu
  ayrı bir değerle döner (`outcome = applied_with_errors` ya da eşdeğeri; DL-2 sözleşmesinde sabitlenir). İki ekran bunu
  "Başarısız"dan ayrı rozetle ve `stage` bilgisiyle gösterir. Rozet metni `RolesIndex.*.resx` ve `UserRoleAssignmentsIndex.*.resx`
  dosyalarında 7 dilde vardır ve İngilizce dışı değer `en` ile aynı değildir. Sabotaj: okuyucu `mutationApplied`'ı yok sayınca ya da
  bir dil dosyasından rozet anahtarı silinince sırasıyla HTTP testi ve `Diten.Web.Tests` kırmızıya döner.

## Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Birim — sıra | Yedi işleyici: korumalar → deneme → değişiklik → sürüm → (oturum) → sonuç → iletim. Sahte depolar çağrı sırasını kaydeder (AC-1, AC-12) |
| Birim — kapalı-başarısız | Deneme hatası enjeksiyonu; değişiklik / sürüm / oturum / iletim çağrı sayısı 0 (AC-2, AC-12) |
| Birim — sonuç | Başarı, `Failed` + aşama + `mutationApplied`, `NotApplied` (yarış), sonuç yazım hatası → 204 + `LogError`; aşamaya göre iletim kuralı (AC-4, AC-5, AC-7, AC-13, AC-14) |
| Birim — yeni aktör koruması | İki revoke: aktörsüz istek → 401, hiçbir depo / deneme / oturum çağrısı yok; aktörlü istek gerileme (AC-29, AC-30) |
| Birim — kimlik | Sabit girdi → sabit `OutcomeId`; farklı deneme → farklı kimlik; `Guid.Empty` reddi (AC-3) |
| Gerileme | `RoleAuditRecorderTests`, `AssignRoleCommandHandlerTests`, `RevokeRoleCommandHandlerTests`, `AssignPermissionCommandHandlerTests`, `RevokePermissionCommandHandlerTests`, `LastStewardPathsTests`, `RoleActorStampTests`, `SystemGrantPathsActorGuardTests`, `UserAuditEventLabelGuardTests` ve e-imza / kullanıcı yaşam döngüsü testleri yeşil (AC-8) |
| Gerçek Mongo | Ayrı sahipli örnek, sabit veritabanı, `MONGO_TEST_URI`; uygulamanın 27017'si yasak. Tek sonuç satırı, deneme hatasında `user_roles` / `role_permissions` / `roles` değişmez, `EntityType` / `EntityId` dizini kullanılır (AC-2, AC-3, AC-19) |
| HTTP (a1) | Gerçek denetleyici: izinsiz 403, başka kiracı 404, olmayan 404, sayfalama 400, meta-denetim teslimi (i)–(iv) (AC-16, AC-17, AC-18) |
| Mimari | `--filter AuditTrailStandard` yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ \| grep -E '^\+- '` boş; sayılar aynı değişiklikte (AC-9, AC-21, AC-22) |
| Ön yüz | `Diten.Web.Tests`: üç ekranda hata kodu eşlemesi; iki geçmiş bölümünün izin kapısı; 7 dil resx eşliği (İngilizce dışı ≠ en); `index.l10n.js` listesi; düzen satırları; `rbac-history.js` birim testi (AC-10, AC-20, AC-24, AC-25) |
| Derleme / takımlar | Odaklı Auth testleri, tam AuthService takımı, Auth API Release derlemesi, Web derlemesi ve odaklı `Diten.Web.Tests`. Gerçek sayılar ve önceden bilinen kırmızılar taban karşılaştırmasıyla yazılır |
| Çalışma kanıtı | `dev` ortamında AC-26 |

Kural üretim koduyla ölçülür: testler gerçek işleyicileri, gerçek kaydedicileri ve gerçek `RbacWriteAheadAuditWriter`'ı çağırır.
Sahte nesne yalnız depo / iletici sınırındadır. Sabotaj kanıtı teslim kaydında "kaldır → kırmızı → geri koy → yeşil" biçiminde
gösterilir. Gösterilemeyen sabotajın koruması koruma sayılmaz.

## Page Help (UXH-001)

Yeni sayfa yok. Var olan iki sayfanın yardım kodu bu ölçümde bulunamadı: `SharedResource.en.resx` içinde `Help.ROLES` ve
`Help.USERROLEASSIGNMENTS` için `grep -c` → 0. Kod başka bir adla tanımlı olabilir. DL-3 başında ölçülür. Yardım varsa AC-28
uygulanır. Tur gerekmez: "kullanıcı + rol kurulumu" akışında yazma adımı değişmez, yalnız salt-okunur bölüm eklenir.

## Ready-for-dev Checklist

- [ ] Golden Reference: `golden_reference: none` gerekçesi okundu; yeni dosyalar Backend File Convention'a uyuyor
- [ ] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`); `target` TBD (Açık kararlar (g))
- [ ] Layout & Shell Contract: üç sayfanın `Layout = "_LayoutTenantShell";` satırları yazılı ve AC-24 / AC-25'te sınanıyor
- [ ] Validation Rules, Failure Path to Verify (en az 4 senaryo), Authorization Convention, Gateway kararı yazılı
- [ ] Audited Events tablosu yedi komutu tam bir kez içeriyor; K2 davranışı ve testi yazılı
- [ ] **Kayıt satırı:** `execution/registries/module-id-registry.md`'ye `MOD-0018-FU29` satırı ayrı bir değişiklikle yazıldı ve
  `verify_module_id.py --check-id` yeniden koşuldu
- [ ] **Sahip kararı kaydı:** "B ayrı ek / dar yol FU27 DL-5'te / Önerilen önce-yaz" kararı karar dosyasına kaynaklı yazıldı (Açık kararlar (l))
- [ ] **FU27 DL-5 temeli main'de** (EntityType / EntityId + dizin) — DL-1 için; `TryForwardAsync` + `IsMetaAudit` — DL-2 (a1) için
- [ ] Açık kararlar kapandı (DL-1 öncesi: (b), (c), (m), (n); DL-2 öncesi: (a), (f); DL-3 öncesi: (a) için CT defter kararı)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) yapıldı ve `reviews.tsv` satırı var
- [ ] CT-APR-01 onay satırı (yüksek risk: yetki, denetim, ürün kodu yolu) — paketi yazmamış ve `high: evet` olan onaylayıcı
- [ ] Sayfa yardım metni: DL-3 başında ölçülecek (Page Help (UXH-001)); tur yok

## Implementation Notes

**Okunan ve ölçülen (origin/main @ `0504c96ea`, numaralı okuma):**

1. Yedi işleyicinin bugünkü sırası: korumalar → değişiklik → sürüm artırma → (revoke'larda oturum kapatma) → denetim **sonra**.
   Değişiklik satırları: `AssignRoleCommandHandler.cs:60`, `RevokeRoleCommandHandler.cs:72`, `AssignPermissionCommandHandler.cs:81-82`,
   `RevokePermissionCommandHandler.cs:81`, `CreateRoleCommandHandler.cs:50`, `UpdateRoleCommandHandler.cs:54`, `DeleteRoleCommandHandler.cs:59`.
   Sürüm satırları: `:63`, `:75`, `:90`, `:84`, `:53`, `:57`, `:62`. Oturum kapatma: `RevokeRoleCommandHandler.cs:81`,
   `RevokePermissionCommandHandler.cs:96-101`.
2. Makineyle okunan desen: `grep -nE "_(rbacAudit|userAudit)\.RecordAsync\(" <yedi işleyici>`. Ölçülen sonuç 7 eşleşmedir ve
   her işleyicide bir taneye denk gelir: `CreateRoleCommandHandler.cs:57`, `AssignPermissionCommandHandler.cs:94`,
   `RevokePermissionCommandHandler.cs:105`, `DeleteRoleCommandHandler.cs:66`, `AssignRoleCommandHandler.cs:67`,
   `UpdateRoleCommandHandler.cs:61`, `RevokeRoleCommandHandler.cs:85`. Her eşleşme değişiklik satırından sonra geliyor.
3. `RoleAuditRecorder.cs`: yerel satır önce (`:36`), sözlük denetimi (`:38-43`), iletici sonra (`:53-72`). Sınıf yorumu
   "Fail-safe: never throws" diyor (`:7`) ama `:36` çağrısı sarılı değil. Güvence `RbacAuditRecorder`'ın hatayı yutmasından geliyor
   (`RbacAuditRecorder.cs:30-41`, `LogError`). `UserAuditRecorder` yerel yazımı kendisi sarıyor (`UserAuditRecorder.cs:45-52`).
4. Defter `auth-rbac-gunlugu` belirteci `IRbacAuditRecorder.RecordAsync`'dir (`Diten.AuthService.md:14`). Beş rol işleyicisi
   `IRoleAuditRecorder` alanı tutuyor (ör. `AssignPermissionCommandHandler.cs:18`) ve `IRbacAuditRecorder`'ı doğrudan çağırmıyor.
   `IRoleAuditRecorder` hiçbir izde adlandırılmamış. Bu yüzden beş komut, Platform'a iletim yapsa da `## Bilinen borç`'tadır.
5. Defter sayıları (makineyle okunan desen: `## Bilinen borç` / `## K2 borcu` bölümündeki `^- ` satırları, awk): Bilinen borç **18**,
   K2 borcu **11**. Sabit sayılar `AuditTrailStandardTests.cs:61`: `Debt: 18, Exceptions: 1, K2Debt: 11`. Yedi komutun yedisi de
   beklenen bölümde bulundu (Module Summary tablosu).
6. `IAuthAuditService.WriteOnceAsync` arayüzde varsayılan olarak `NotSupportedException` atıyor (`IAuthAuditService.cs:18-19`).
   `AuthAuditService` bunu uyguluyor (`AuthAuditService.cs:29-50`); yinelenen anahtarda satırın o kimlikte olduğunu doğruluyor
   (`:41-48`). Örnek kullanım `ServiceClientRegistryReconciler.cs:94`, `:170`; türetme `:208-218`.
7. "Revoke path intentionally unguarded" cümlesi (`AssignPermissionCommandHandler.cs:53-54`, `:63-64`) denetim için değil,
   **izin uygunluğu koruması** için yazılmış: insan rolüne verilemez / kiracıya atanamaz denetimi revoke'ta yok, böylece yanlış
   verilmiş izin her zaman kaldırılabiliyor. Kapalı-başarısız denetim bu niyetle çatışabilir; bkz. Açık kararlar (b).
8. Platform `AuditOutcome` enum'unda `Unknown = 0` ve `Unconfirmed = 4` var (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:56-68`).
   Bu paket Platform'a sonuç iletmez; "sonuçlanmadı" yalnız Auth okuyucusunun gösterimidir.

**Ölçüm uyuşmazlıkları (paket metniyle düzeltilmedi, kaydedildi):**

- U-1: CT iletisinin andığı FU27 §5.3 / §8.5 / §14.3 / §21 / AC-44…AC-47 ve `auth-sod-muafiyet-gecmisi` main'deki FU27 paketinde
  (`ee1faf6d2` son değişiklik) **yok**. Bunlar yalnız `origin/chore/ct/treasury-intake-ct-06` @ `47e314f5c`'de var.
- U-2: FU27 DL-5 kodu hiçbir dalda yok. `AuthAuditLog`'da `EntityType` / `EntityId` yok.
- U-3: `audit-trail-standard.md:166-169` "`WriteOnceAsync` … bu dalda henüz yok" diyor. Main'de var (madde 6). Kural dosyası
  korumalı yoldur ve değiştirilmedi. Düzeltme sahip onayıyla ayrı yapılır.
- U-4: Sahibin "sonuç satırı `WriteOnceAsync` ile tekil" kararı ile FU27 D98'in "`IAuthAuditService` değişmez" kuralı (ct-06 §5.3)
  birlikte uygulanamaz, çünkü `WriteOnceAsync` imzası `EntityType` / `EntityId` taşımıyor (`IAuthAuditService.cs:18`). Açık kararlar (c).
- U-5: Sahip kararının (B / dar yol / Önerilen) depoda kaydı yok (başlık notu).
- U-6: Kayıt satırı yok ama doğrulayıcı `OK` veriyor (DCP-002 notu). `MOD-0018-FU28` bir dalda kayıt satırı olmadan kullanılıyor.
- U-7: İşleyici dosya adları Golden adlandırmasından sapıyor (Backend File Convention).

**Koşturulmayan:** Derleme, test, `Inventory_PrintsTheTable` envanter testi ve dev sunucusu koşturulmadı (görev kuralı). Defter
sayıları metin ölçümüdür, test çıktısı değildir.

## Follow-up Items

- FU-0022-AUD-02: e-imza komutları aynı `IUserAuditRecorder.RecordAttemptAsync` yoluna geçebilir (MOD-0022 sahibi; ayrı iş).
- `ServiceClientRegistryReconciler.AuditId` türetmesinin `RbacAuditIds` ile tek yardımcıda birleştirilmesi. Davranış değişikliği
  yoktur ama İ5'e yakın kod olduğu için ayrı iştir.
- Reddedilen komutların kaydı (§3): Açık kararlar (h) kararına göre ayrı paket.
- `audit-trail-standard.md` §4.4 eski cümlesinin düzeltilmesi (U-3; `.antigravity/**`, sahip onayı).
- `RoleAuditRecorder.cs:7` "never throws" yorumunun yerel yazımı da sararak gerçekle eşitlenmesi (madde 3). Bu paket mevcut `RecordAsync`'e
  dokunmaz; ayrı küçük iş.
- Silinmiş rolün geçmişinin okunması: Açık kararlar (k).
- İşleyici dosya adlarının Golden adlandırmasına çevrilmesi (U-7).

## Audited Events

**Servisin denetim altyapısı bugün:** `auth-kullanici-iletimi` (yol `b`, en iyi çaba, yapılandırmaya bağlı: anahtar boşsa iletim
atlanır, `PlatformAuditForwarder.cs:55-59`) kabul edilmiş. `auth-gunlugu` ve `auth-rbac-gunlugu` `aday`
(`Diten.AuthService.md:10-14`). Yedi komutun hepsi **K2 sınıfıdır** (rol / yetki değiştirir). Yeni yazma komutu yoktur.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| AssignRoleCommand | `user_role_assigned` (deneme `:attempt`, sonuç `:failed` / `:not_applied`) | User (hedef kullanıcı; konu rol) | `b` — `auth-kullanici-iletimi`, sonuç `IUserAuditRecorder.RecordAsync` aşırı yüklemesi (DL-1); (a1) ile ayrıca `c` — `auth-rbac-gecmisi`, önce-yaz `IUserAuditRecorder.RecordAttemptAsync` (DL-3). K2: önce-yaz, kapalı-başarısız (AC-1, AC-2) | AfterState: roleId, roleName | — |
| RevokeRoleCommand | `user_role_removed` (aynı ekler) | User | aynı (DL-1 / DL-3). K2: Açık kararlar (b) kararına bağlı (öneri: kapalı-başarısız) | BeforeState: roleId, roleName; sonuçta `stage` | — |
| AssignPermissionCommand | `role_permission_granted` | Role (konu izin) | Bugün kabul edilmiş iz yok (`## Bilinen borç`). (a1): `c` — `auth-rbac-gecmisi`, `IRoleAuditRecorder.RecordAttemptAsync` (DL-3). İletim `IRoleAuditRecorder.RecordAsync` aşırı yüklemesiyle sonra. K2: kapalı-başarısız (AC-12) | AfterState: permissionId, permissionKey, roleName | — |
| RevokePermissionCommand | `role_permission_revoked` | Role | aynı; K2: Açık kararlar (b) | BeforeState: permissionId, permissionKey, roleName; sonuçta `stage` | — |
| CreateRoleCommand | `role_created` | Role | aynı | AfterState: roleName, displayName, description | — |
| UpdateRoleCommand | `role_updated` | Role | aynı | Before/After: displayName, description | — |
| DeleteRoleCommand | `role_deleted` | Role | aynı | BeforeState: roleName | — |
| GetRoleAuditHistoryQuery (**okuma satırı**, §7: denetim kaydının kendisi okunur) | `auth.rbac.history.read` (öneri) | Role (okunan rol) | `b` — merkezi günlüğe meta-denetim (`IsMetaAudit = true`), teslim teyitli `TryForwardAsync` (FU27 DL-5); teslim yoksa okuma yok, 503 (AC-18). Eşdeğer iz okuma için kullanılmaz | — (okuyan, kiracı, rol kimliği, zaman) | — |
| GetUserRoleAuditHistoryQuery (**okuma satırı**, §7) | `auth.rbac.user-role-history.read` (öneri) | User (okunan kullanıcı) | aynı | — | — |

- **K2 yolu:** Deneme satırı işleyicinin içinde, iş yazımından **önce** yazılır. Yazılamazsa komut 503 ile başarısız olur ve
  kullanıcı "şu an yapılamadı" cevabını alır (§4.3, §4.4 "Auth içinde"). Sonuç satırı ve iletim en iyi çabadır. Değişiklik olup
  sonucu yazılamayan durum, okuyucuda "sonuçlanmadı" olarak görünür. Kayıp sessiz değildir (`LogError` ile yazılır). Değişiklik
  uygulanıp sonraki adım (sürüm / oturum) düşerse sonuç `Failed` + `mutationApplied = true` olur ve "Uygulandı, sonrası hata
  verdi" diye görünür. İletim Açık kararlar (m)'ye bağlıdır.
- **Defter zamanlaması:** İki kişi-rol komutu DL-1 ve DL-2 boyunca `## K2 borcu`'nda, beş rol komutu `## Bilinen borç`'ta kalır.
  Yedisinin de çıkışı DL-3'tedir (AC-22, AC-23). DL-1'de erken K2 çıkışı Açık kararlar (n)'ye bağlıdır.
- **İki revoke'da yeni koruma:** Aktör yoksa 401 `ROLE_ACTOR_REQUIRED` döner ve satır yazılmaz (AC-29, AC-30).
- **Yol `c` beş koşulu (a1, DL-3'te tek tek işaretlenir):** (1) kim (`UserId`) + ne (`EventName` + `EntityType` / `EntityId`) +
  ne zaman (`OccurredAt`, UTC) + kiracı (`TenantId`). (2) Yazıcı ve okuyucu arayüzünde güncelleme / silme yok (AC-19). (3) Kiracı
  tarafında, kaydın kendi ekranında okunabilir: rol → Roller hızlı görünümü, kişi-rol → kullanıcı-rol atama ekranı; okuma izne bağlı
  (AC-24, AC-25). (4) Gerçek depo: `authAuditLogs` Mongo koleksiyonu (AC-3 gerçek Mongo). (5) İz adı ve belirteç `## İzler`'de (AC-22).
- **Yol `c` neyi cevaplamaz:** Platform yöneticisinin "bu kiracıda dün kim ne yaptı" sorusunu cevaplamaz. O soru için iletim (yol
  `b`) bugünkü gibi sürer.
- **Kayda yazılmayan:** token, istek başlıkları, e-posta, ad-soyad (yalnız kimlikler ve rol / izin adları).
- **Bilinen boşluk (§3 alan 2):** Yerel satır, eylemi yapanın o andaki adını taşımıyor (kayıt kuralı kişisel veri yazmıyor).
  Platform iletimi maskeli adı taşıyor. Açık kararlar (j).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | önce-yaz temeli + kişiye rol verme / geri alma (AssignRole, RevokeRole) kapalı-başarısız; RevokeRole'a yeni aktör koruması; defter ve sabit sayılar **değişmez** (iki komut K2 borcunda kalır; erken çıkış yalnız Açık kararlar (n) CT kararıyla); kullanıcı-rol ekranı hata metni 7 dil; AC-1…AC-11, AC-29; **kanıt seviyesi E4** (kalıcılık + kiracı / RBAC + denetim + hata yolları; gerçek Mongo) | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Acceptance Criteria, Test Expectations, Audited Events |
| DL-2 | beş rol / izin komutu önce-yaz ve kapalı-başarısız; RevokePermission'a yeni aktör koruması; (a1) iki geçmiş okuma ucu + meta-denetim (sonuç değerleri `mutationApplied` dahil); Roller ve rol-izin ekranı hata metni 7 dil; defter değişmez; AC-12…AC-21, AC-30; **kanıt seviyesi E5** (Auth → Platform meta-denetim teslimi, servisler arası çalışma) | Owned Objects, Repo Scope, Runtime Constraints, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events |
| DL-3 | (a1) iki ekranın salt-okunur geçmiş bölümü 7 dil ("Uygulandı, sonrası hata verdi" rozeti dahil); iz kabulü (`auth-rbac-gecmisi`, CT defter kararı); borç kapanışı: Bilinen borçtan beş, K2 borcundan iki satır (`Debt` ve `K2Debt` aynı değişiklikte ölçülür); dev çalışma kanıtı; AC-22…AC-28, AC-31; **kanıt seviyesi E4** (ekran + kalıcılık + denetim; `dev` ortamında §8-(3)) | Layout & Shell Contract, Frontend File Contract, Authorization Convention, Acceptance Criteria, Test Expectations, Page Help (UXH-001), Audited Events |

**Sıra:** FU27 DL-5 temeli main'de → DL-1 → DL-2 → DL-3. DL-3, DL-1 ve DL-2'den önce birleşemez (AC-23). Kapalı-başarısız olmayan
bir K2 komutuna kredi verilemez ve K2 borcu büyüyemez. K2 çıkışı DL-3'tedir, çünkü kapalı-başarısız satır ancak o dilimde kabul
edilmiş ve okunabilen ize yazılmış olur (Açık kararlar (a), (n)). **(a2) seçilirse:** DL-3'ün ekran ve geçmiş kısmı düşer. Kabul (CT'nin
`IRoleAuditRecorder.RecordAsync`'i yol `b` olarak kabulü) DL-2'nin son adımı olur ve paket o kararla yeniden yazılır.
**(b2) seçilirse:** `RevokeRoleCommand` `## K2 borcu`'nda kalır. `RevokePermissionCommand` `## Bilinen borç`'ta kalır, çünkü K2 borcu
büyüyemez. AC-2 / AC-12 o iki komut için yeniden yazılır.

## Açık kararlar

| # | Konu | Seçenekler | CT önerisi (bu paketin önerisi; karar değil) | Sahip | Bağımlılık / son tarih |
|---|---|---|---|---|---|
| (a) | İzin ve rol komutlarının borçtan çıkması için kabul edilecek iz | **(a1)** yol `c`: yeni iz `auth-rbac-gecmisi`, belirteç `IRoleAuditRecorder.RecordAttemptAsync/IUserAuditRecorder.RecordAttemptAsync`, iki geçmiş ucu + Roller hızlı görünümü ve kullanıcı-rol ekranında geçmiş (beş koşul) · **(a2)** yol `b`: CT, `IRoleAuditRecorder.RecordAsync`'i kullanıcı iletimiyle aynı yapıdaki rol iletimi olarak kabul eder; ekran yok · **(a3)** önce (a2), sonra (a1) | **(a1).** Gerekçe: kapalı-başarısız yazılan satır, kabul edilmiş ve okunabilen iz olur. (a2)'de kabul edilen kayıt (Platform iletimi) en iyi çabadır ve anahtara bağlıdır (§4.2, K8); kapalı-başarısız satır ise kimsenin okuyamadığı `aday` günlükte kalır (K7). FU27 D97/2 kalıbıyla da tutarlıdır. Maliyeti DL-3'tür (iki ekran, 7 dil). Karşılaştırma: SAP değişiklik belgeleri (`CDHDR` / `CDPOS`) nesnenin kendi ekranından okunur; Oracle Fusion denetim raporu nesne başına süzülür | MOD-0018 sahibi (karar) + Control Tower (defter kararı, `CT-DECISIONS.md`) | DL-2 öncesi (karar); DL-3 öncesi (defter) |
| (b) | Revoke komutlarında kapalı-başarısız denetim ile acil kaldırma önceliği | **(b1)** revoke da kapalı-başarısız (K2 standardı, istisnasız) · **(b2)** revoke denetimsiz de geçer, yüksek sesli hata + "denetimsiz" işareti (K2'yi çiğner; CT kararı ister; Teslim dilimleri notu) · **(b3)** revoke kapalı-başarısız, acil durum için ayrı ve kendisi denetlenen bir işletim yolu (tohum / bakım) | **(b1).** Gerekçe: denetim ve roller aynı Mongo veritabanında (Dependencies). Deneme satırını yazamayan bir kesinti büyük olasılıkla `RevokeAsync`'i de yazamaz; (b2) gerçek bir kaldırma güvencesi vermez ama denetimsiz değişiklik kapısı açar. "Revoke unguarded" cümlesi izin uygunluğu koruması içindir, denetim için değildir (Implementation Notes madde 7). Ölçülmeyen: veritabanı yazılabilirken yalnız `authAuditLogs`'un yazılamadığı bir kesinti biçimi (ör. koleksiyon düzeyi hata) — ölçüm önerilir | MOD-0018 sahibi | DL-1 öncesi |
| (c) | Deneme / sonuç satırı biçimi ve deterministik kimlik | Olay adı: **(c1)** ekli adlar (`{olay}:attempt` / `:failed` / `:not_applied`; başarıda bugünkü ad) · (c2) tek ad + `phase` alanı. Deneme kimliği: **yeni GUID** · korelasyon + komut + konu türetmesi. Tekil yazım: **(c-i)** yeni yazıcı `WriteOnceAsync` anlamını (yalnız ekleme, yinelenen anahtar = zaten var) `EntityType` / `EntityId` ile uygular, `IAuthAuditService` değişmez (FU27 D98 ile uyumlu) · (c-ii) `IAuthAuditService.WriteOnceAsync`'e `EntityType` / `EntityId` alan ek aşırı yükleme (sahibin "`WriteOnceAsync` ile" sözüne harfiyen uyar; FU27 D98 ile çatışır, İ5 / kayıt defteri yollarına yakın) | **(c1) + yeni GUID + (c-i).** Bugünkü olay adını okuyan her şey (Platform iletimi, sayımlar) yalnız gerçekleşmiş olayı görür. Yeni GUID, bir isteğin yeniden denenmesinin ayrı bir deneme olduğunu doğru söyler. (c-i) sahibin istediği "aynı kimlik → tek satır" sonucunu verir (U-4) | MOD-0018 sahibi (+ (c-ii) seçilirse Treasury CT, FU27 D98) | DL-1 öncesi |
| (d) | Sayı sabitlemesinin yeni değerleri | Bugün ölçülen: `Debt 18, Exceptions 1, K2Debt 11` (`AuditTrailStandardTests.cs:61`; defter 18 / 11, Implementation Notes madde 5) | **Tahmin yazılmaz.** Her dilim, defter satırını çıkardığı aynı değişiklikte sayıyı mimari testin ölçtüğü değere çeker (AC-9, AC-22). Beklenen fark (hesap, ölçüm değil): DL-1 değişiklik yok; DL-3 `Debt` −5 ve `K2Debt` −2 (18 → 13,
11 → 9); `Exceptions` değişmez. Main bu arada değişirse (ör. FU27 DL-5 kendi sayısını değiştirmez — ct-06 AC-44) yeniden ölçülür | Dilimi yazan + CT (kabulde) | Her dilimin teslimi |
| (e) | Kapsam | Yedi komut bu pakette · `CreateRole` / `UpdateRole` / `DeleteRole` ayrı | **Yedisi bu pakette** (CT iletisi). Üç komut aynı `IRoleAuditRecorder`'ı kullanıyor (Module Summary). Ayrılırsa iki yarım kalıp ve iki defter kararı doğar | MOD-0018 sahibi | Paket onayı |
| (f) | Kiracı yalıtımı ve okuma izni adı (PKS-001) | Mevcut anahtarlar (`auth.roles.read`; `auth.users.assign-role` ya da `auth.users.read`) · yeni anahtarlar (`auth.roles.history.read`, `auth.users.role-history.read`) | **Mevcut anahtarlar:** rol geçmişi `auth.roles.read`, kişi-rol geçmişi `auth.users.assign-role` (ekranı açan anahtar). "Kaydı gören izini de görür" (§5.c-3). Yalıtım: kiracı JWT'den, süzgeç `TenantId + EntityType + EntityId`, başka kiracı 404 (Runtime Constraints) | MOD-0018 sahibi | DL-2 öncesi |
| (g) | Hedef tarih | — | TBD | MOD-0018 sahibi | Onay öncesi |
| (h) | Reddedilen komutun kaydı (§3 "yetkisiz bir silme denemesi … denetim bilgisidir") | Bu pakete ekle (korumalardan sonra `Denied` satırı) · ayrı paket · bugünkü gibi kayıtsız | **Ayrı paket.** Bu paketin kararı önce-yaz / sonuçtur; reddi kaydetmek 7 komutun 401/403/404/409 dallarının hepsini değiştirir | MOD-0018 sahibi | DL-1 öncesi (kapsam) |
| (i) | FU27 DL-5 temeli ve sıra | FU27 DL-5 önce (bu paket) · FU27 gecikirse alan / dizin eklemesi FU29 DL-1'e taşınır ve FU27 onu tüketir · ortak okuyucu (`IAuthAuditHistoryReader`, varlık türüyle) iki pakete tek tip | **FU27 DL-5 önce.** Ortak okuyucu önerisi FU27 sahibine iletilir; kabul edilirse bu paketin `IRbacAuditHistoryReader`'ı düşer (kopya kalmaz) | Treasury CT + MOD-0018 sahibi | DL-1 öncesi |
| (j) | Eylemi yapanın o andaki adı (§3 alan 2) | Yerel satıra maskeli ad yaz · okumada güncel adı çöz · bilinen boşluk olarak bırak | **Okumada güncel ad + bilinen boşluk kaydı.** Yerel kayıt kuralı kişisel veri yazmıyor (`IRbacAuditRecorder.cs:3-8`); Platform iletimi maskeli adı taşıyor | MOD-0018 sahibi | DL-3 öncesi |
| (k) | Silinmiş rolün geçmişi | Geçmiş ucu silinmiş rolü de bulur (silinmişler dahil arama) · bulmaz (404) | **Bulur, salt-okunur.** Aksi halde `role_deleted` satırı kendi ekranında hiç okunamaz (beş koşulun 3. maddesi). Ekranda silinmiş rol listesi yoksa giriş yolu ayrıca seçilir | MOD-0018 sahibi | DL-2 öncesi |
| (l) | Sahip kararının ve kimliğin kaydı | Karar dosyasına (Hazine D-serisi ya da MOD-0018 kayıtları) kaynaklı satır + kayıt satırı | Karar satırı: "D80/2 (b) B = MOD-0018-FU29; dar yol FU27 DL-5; Önerilen önce-yaz". Kayıt satırı `module-id-registry.md`'de | MOD-0018 sahibi + CT | Onay öncesi |
| (m) | Değişiklik uygulandı ama sonrası düştü (`Failed` + `mutationApplied = true`, `stage = version` / `sessions`): Platform'a iletim | **(m1)** bilinen olay adı (`user_role_removed`, `role_permission_revoked` …) + `Failed` sonucu ile iletilir · (m2) iletilmez, yalnız yerel satır · (m3) `Succeeded` olarak iletilir (yetki gerçekte değişti) | **(m1).** Platform günlüğü yetkinin gerçekte değiştiğini görür, sonraki adımın düştüğü de saklanmaz. Olay adı bilinen sözlükte kaldığı için Platform etiketleri ve `UserAuditEventLabelGuardTests` değişmez. Kullanıcı tarafında `UserAuditEvents.OutcomeFailed` var (`UserAuditEvents.cs:51-52`); rol tarafında `RoleAuditEvents` yalnız `OutcomeSucceeded` taşıyor (`RoleAuditEvents.cs:33-34`), bu yüzden (m1) için rol sözlüğüne `int` bir `OutcomeFailed` eklenir (dize sabiti değil, etiket kapısı onu okumaz) | MOD-0018 sahibi + Control Tower | DL-1 öncesi |
| (n) | DL-1'de erken K2 çıkışı | Çıkış DL-3'te (kapalı-başarısız satır kabul edilmiş, okunabilen ize yazılınca) · DL-1'de çıkış (satır `aday` günlükte, kredi `auth-kullanici-iletimi` yol `b`'den) | **DL-3.** DL-1'de erken çıkış **ancak** CT'nin yazılı kararıyla (`tests/architecture/audit-ledger/CT-DECISIONS.md` satırı) mümkündür. Karar yoksa çıkış DL-3'tedir. Paket, (a) gerekçesiyle tutarlı kalmak için bu varsayılanı kullanır | Control Tower | DL-1 öncesi |
