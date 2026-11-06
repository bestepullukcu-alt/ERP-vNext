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
target: 2026-11-20
form_field_count: 0
parent: MOD-0018
---

# MOD-0018-FU29 — RBAC Role Assignment Write-Ahead Audit

> **`status: ready-for-dev` yalnız inceleme içindir, kod izni değildir.** Bu alan yalnız bağımsız inceleme ZIP'inin kurulmasını
> sağlar. Kod için CT-APR-01 onay satırı şarttır: `python3 scripts/pack_approval.py check <paket>` onaylı (çıkış 0) demelidir.
> **Onay satırı yoksa kod yok.** Sahibin "onayla, hedef 11-20" sözü (karar kaydı 02, karar 7) paket onayı sözüdür. Kaydın notuna göre
> onay satırı yalnız düzeltilmiş sürümün bağımsız incelemesi geçtikten sonra yazılır. Bu paket kod, katalog, izin, rol, hak, gezinme,
> yapılandırma, defter ya da kiracı verisi değiştirmez. Onaydan sonra kod, aşağıdaki Repo Scope listesiyle ve Teslim dilimleri
> sırasıyla yazılır.
>
> **Onay satırının ön koşulları (yeniden ölçüm 2026-11-06; `origin/main` = `6be9bc3d6`, kule dalı `chore/ct/auth-ct-01` @ `11fa9b94c`):**
> (1) Sahip karar kaydı 01 main'de: **kapandı**. Kayıt `c1288400a` ile yazıldı; PR #375 `68dcf9fa5` ile birleşti (2026-11-05).
> `git diff origin/main -- <kayıt 01>` boş döndü.
> (2) FU27 DL-5 temeli: **kapandı, (i2)**. FU27'nin onaylı sürümü yok: `origin/main` üzerindeki `execution/approvals/approvals.tsv`
> içinde `MOD-0018-FU27` satırı yok (`grep -c` → 0). Main'deki FU27 paketinin künyesi `status: draft`, `target: TBD`. Karar kaydı 02
> notu da aynısını söyler. Bu yüzden `AuthAuditLog.EntityType` / `EntityId` alanlarını ve dizini FU29 DL-1 ekler (Repo Scope DL-1,
> Dependencies).
> (3) Kayıt satırı `MOD-0018-FU29`: **açık, CT'nin işi**. Satır kule dalında commit'lidir (`25a03bebd`,
> `module-id-registry.md:92`); `origin/main` üzerinde `grep -c` → 0. Main'e girmesi onay satırından önce gerekir.
> (4) Karar kayıtları 02 ve 03 (`docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-02.md` @ `25a03bebd`, `-03.md` @
> `11fa9b94c`): kule dalında commit'li, `origin/main`'de yok; **main'e girmeleri onay satırından önce gerekir (CT'nin işi)**.
> (5) Yeni seçimler S-1…S-5: **kapandı** (sahip 2026-11-06, "Hepsini kabul et"; karar kaydı 03, karar 9). S-6 ve S-7: **kapandı (sahip 2026-11-06, "İkisini kabul et"; karar kaydı 04, karar 11)**.
> (6) Bu sürümün bağımsız incelemesi kod dosyalı ZIP'le geçer ve `execution/approvals/reviews.tsv` satırı yazılır.
>
> **Dağıtım ön koşulu (S2S anahtarı):** Geçmiş uçları meta-denetim teslim teyidi olmadan veri döndürmez (AC-18). Teyit için Auth'un
> Platform iç anahtarı gerekir: `PlatformService:InternalApiKey` (`PlatformServiceOptions.cs:5, :9`). Anahtar temel
> `appsettings.json:68`'de boştur, `appsettings.Development.json:64`'te doludur. Boşsa bugünkü iletici iletimi atlar
> (`PlatformAuditForwarder.cs:55-59`) ve geçmiş uçları **tasarım gereği 503** `RBAC_HISTORY_AUDIT_UNAVAILABLE` döner.
> **Başlangıç denetimi (ölçüldü 2026-11-06):** Anahtar Auth'un zorunlu gizli bilgi listesindedir (`Infrastructure/DependencyInjection.cs:26-27`,
> `:226`; `Required` varsayılanı `true`, `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/RequiredSecretDefinition.cs:7`).
> Boş değer **her ortamda** hata sayılır ve başlangıç durur (`SecretRequirementValidator.cs:54-58`, `SecretServiceCollectionExtensions.cs:45-48`).
> Yani çalışan bir Auth'ta anahtar boş olamaz; boş dal yalnız doğrulayıcıyı atlayan test kurulumlarında görülür. **Asıl dağıtım riski
> anahtarın eşleşmemesidir:** Platform iç ucu anahtarı kendi `AuthService:InternalApiKey` değeriyle karşılaştırır
> (`services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalAuditController.cs:92-111`, `:95`). Eşleşmezse cevap 2xx
> değildir ve geçmiş uçları 503 döner. DL-2 ve DL-3 dağıtımında yazar şunu ölçer ve kaydeder: ortam adı, iki taraftaki anahtarların
> eşleştiği (değerleri değil) ve bir geçmiş okumasının 200 döndüğü. Teslim teyidi yoksa ekran bölümü 7 dilde "şu an gösterilemiyor" der (AC-31).
>
> `branch` alanı paket metninin dalıdır. Kod dalı ilk dilimde ayrıca açılır (öneri `feature/pss/mod-0018-fu29-rbac-write-ahead`).
>
> **Sahip kararı (CT görev iletisi, 2026-11-01; sahip: MOD-0018 sahibi):** Hazine'nin D80/2 (b) şartı için "B" seçeneği bu ayrı
> ektir. "Dar yol" FU27 DL-5'in içindedir ve bu pakete girmez. Kapsamdaki yedi RBAC komutu ortak, önce-yaz denetimine geçer.
> Seçilen yol "Önerilen" yoldur. Değişiklikten **önce** bir "deneme" satırı yazılır. Bu yazım kapalı-başarısızdır: satır yazılamazsa
> komut durur. Değişiklikten sonra bir "sonuç" satırı yazılır. Sonuç satırının kimliği deneme kimliğinden türetilir ve satır tek
> yazılır (`WriteOnceAsync` anlamı). Sonuç satırı en iyi çabayla yazılır. Sonuç yoksa okuyucu "sonuçlanmadı / bilinmiyor" gösterir.
> Platform'a iletim (`IPlatformAuditForwarder`) en iyi çabayla, değişiklikten **sonra** kalır.
> **Karar kayıtları:** `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-01.md` (main, PR #375; karar 1-3 kapsam ve
> önce-yaz, karar 3 Açık kararlar (e)), `…/mod-0018-fu29-owner-decisions-02.md` (kule dalı `25a03bebd`; karar 7 "onayla, hedef
> 11-20", karar 8 "Evet, düzelt") ve `…/mod-0018-fu29-owner-decisions-03.md` (kule dalı `11fa9b94c`; karar 9 "Hepsini kabul et",
> S-1…S-5). 02 ve 03 main'de değildir (başlık ön koşul 4). "Karar n", kaydın `#` sütunudur, dosya satırı değildir. Kayıtları CT yazar; bu paket yalnız onlara
> atıf verir. Önceki ölçüm (2026-11-01): Karar o tarihte depoda yoktu.
> U-5 ve Açık kararlar (l) bu kayıtla kapandı.
>
> **DCP-002 kimlik notu.** Paket yazılmadan hemen önce ön kontrol yeniden koşuldu (2026-11-01, HEAD = origin/main `0504c96ea`):
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU29 --name "RBAC Role Assignment Write-Ahead Audit" --parent MOD-0018`
> → `OK  MOD-0018-FU29: proven against Blueprint/registry.` (çıkış 0). Bu sonuç CT'nin önceki koşusuyla aynıdır.
> `--discover "RBAC Role Assignment Write-Ahead Audit"` çıkış 3 ("inceleme gerekli") verdi. Bulgular: Blueprint 0, registry 0,
> paket 0, gateway 2, görev 1; rapor-sha256 `78538be3f8a81d8d2fc93325745edde6e876b442fe2a99559df62fc38b54593f`. Gateway bulguları
> mevcut `audit` → 5057 ve `roles` → 5056 yollarıdır (üretici-tüketici ilişkisi). Görev bulgusu `mvp6-process-pilot-01` Q97'dir ve
> bu kapsamla ilgisizdir. 2026-11-01'de kayıtta (`execution/registries/module-id-registry.md`) `MOD-0018-FU29` satırı yoktu.
> 2026-11-06 ölçümü: satır `origin/main`'de yok (`grep -c` → 0). Kule dalında commit'li (`25a03bebd`, `:92`). Doğrulayıcı ana MOD'un
> FU'sunu satır olmadan da kabul ediyor; FU27 paketi aynı davranışı FU23 için ölçmüştü. Kayıt satırı CT'nin işidir ve ayrı bir
> değişiklikle main'e girer (Ready-for-dev Checklist). `git log --all -S "MOD-0018-FU29"`
> boş döndü: numara hiçbir dalda kullanılmamış. `MOD-0018-FU28` ise `origin/chore/ct/treasury-intake-ct-06` dalında MOD-0057 Auth
> eki için anılıyor (`8321c7709`, `f60522198`) ve kayıt satırı yok.
>
> **Golden Reference kararı.** Bu iş arka uç denetim işidir. Mevcut iki ekrana (Roles, UserRoleAssignments) salt-okunur birer
> geçmiş bölümü eklenir. Yeni sayfa ve yeni DataTable yok: `shell: none`, `golden_reference: none`, `form_field_count: 0`
> bilinçlidir. Yeni varlık da yoktur. Dokunulan tek kayıt türü mevcut `AuthAuditLog`'dur: `GlobalEntityBase` üzerinde durur ve
> kiracıyı kendi `TenantId` alanında taşır (`AuthAuditLog.cs:3, :19` @`0504c96ea`). Bu yüzden `entity_base: GlobalEntity`
> yazıldı. Gerekçe Runtime Constraints'tedir.
>
> **Ölçüm tabanı.** İlk sürümdeki her `dosya:satır` `origin/main` @ `0504c96ea` üzerinde numaralı okumayla alındı. **Yeniden ölçüm
> (2026-11-06, HEAD `e3dc475bd`, bu sürümün yazarı):** Paketin andığı her `dosya:satır` HEAD çalışma ağacında yeniden okundu (bulgu
> F-2). Doğru çıkmayan atıflar düzeltildi ve Implementation Notes "U-9"da tek tek yazılıdır. `git diff --name-only 0504c96ea HEAD`
> taranan yollarda 22 dosya verir. Taranan yollar: `services/Diten.AuthService`, `frontend/Diten.Web/Views/Governance`,
> `frontend/Diten.Web/wwwroot/assets/js/Governance`, `frontend/Diten.Web/Controllers`, `frontend/Diten.Web/Resources`,
> `frontend/Diten.Web/Services/Help`, `frontend/Diten.Web.Tests`, `gateway`, `tests/architecture` ve Platform denetim dosyaları. 22
> dosyanın hiçbiri bu pakette anılmıyor: üçü Auth (`ExplicitGrantOnlyPermissions.cs`, `LeaveExplicitGrantPermissionTests.cs`,
> `AccountKindTests.cs`), kalan 19'u Pharmacovigilance ve SupplyChain ön yüz dosyasıdır. Önceki sürüm "yalnız üç dosya" yazmıştı; ön
> yüz yollarını saymamıştı. 2026-11-05 ve sonrasında ilk kez ölçülenler "@`e3dc475bd`" diye işaretlidir. FU27 DL-5 metni main'de
> değildir. O metin `origin/chore/ct/treasury-intake-ct-06` @ `47e314f5c` dalından okundu ve oradan anılan her yer "ct-06" diye
> işaretlidir. Dal 2026-11-06'da `e5a0f8e77`'dedir; FU27 paket dosyası iki sürüm arasında değişmedi (`git diff --stat` boş).

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
yazar. Ekran tarafı Açık kararlar (a) (a1) ile bu pakettedir (kapandı, sahip 2026-11-05, onayla). Eylemi yapan bir platform hesabıysa
ad ve kimlik gösterilmez (Runtime Constraints "Eylemi yapanın gösterimi"). Geçmiş FU29 ile başlar: daha önceki satırlarda varlık alanı
yoktur ve görünmez (Frontend File Contract).

## Ownership and Boundaries

**Kapsam içi**

- Yedi komutun işleyicisinde deneme satırının değişiklikten önceye alınması, kapalı-başarısız davranış ve sonuç satırı.
- `IRoleAuditRecorder` ve `IUserAuditRecorder`'a **yalnız ekleme** iki üye (deneme; deneme kimliği alan `RecordAsync` aşırı
  yüklemesi). Mevcut `RecordAsync` üyelerinin anlamı değişmez. `CreateUserCommand`, `DeleteUserCommand`, e-imza komutları ve
  ötekiler aynı yazıcıyı bugünkü gibi kullanır.
- Ortak önce-yaz yazıcısı, deterministik kimlik türetme yardımcısı ve salt-okunur RBAC geçmiş okuyucusu.
- `AuthAuditLog`'a **yalnız ekleme** `EntityType` / `EntityId` alanları ve `(TenantId, EntityType, EntityId, OccurredAt)` dizini
  (Açık kararlar (i) (i2); DL-1).
- İki geçmiş okuma ucu, silinmiş roller okuma ucu, meta-denetim okuma satırı ve iki ekranın salt-okunur geçmiş bölümü. Ayrıca Roller
  ekranında "Silinmiş roller" süzgeci gelir (Açık kararlar (a) (a1), (k) bulur + (k-1); kapandı).
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
- Reddedilen komutun kaydı (§3 "Reddedilen komut da kaydedilir"): bugün korumalar satır yazmıyor. Bu paket bunu değiştirmez.
  Açık kararlar (h): ayrı paket (kapandı, sahip 2026-11-05, onayla).
- Silinmiş kullanıcının rol geçmişi: Uç silinmiş kullanıcıyı bulmaz ve bugünkü gibi 404 döner (AC-17). Bu bir bilinen boşluktur
  (Runtime Constraints "Silme"; seçim S-4).

**Sahiplik:** MOD-0018 sahibi (auth-owner) yazar ve kabul eder. (i2) gereği `AuthAuditLog` alanlarını ve dizini bu paket ekler.
FU27 DL-5 onları tüketir ve yeniden eklemez. FU27 metnini Treasury CT buna göre revize eder (Dependencies). İleticinin
`TryForwardAsync` / `IsMetaAudit` eklemeleri FU27 DL-5'te kalır. Tarih kapısı için bkz. seçim S-5.

## Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| Yazma komutu (mevcut, davranışı değişir) | `AssignRoleCommand`, `RevokeRoleCommand`, `AssignPermissionCommand`, `RevokePermissionCommand`, `CreateRoleCommand`, `UpdateRoleCommand`, `DeleteRoleCommand` | Yeni yazma komutu yok |
| Arayüz üyesi (yalnız ekleme) | `IRoleAuditRecorder.RecordAttemptAsync`, `IRoleAuditRecorder.RecordAsync(…, RbacAuditOutcome, …)` aşırı yüklemesi | yeni üye |
| Arayüz üyesi (yalnız ekleme) | `IUserAuditRecorder.RecordAttemptAsync`, `IUserAuditRecorder.RecordAsync(…, RbacAuditOutcome, …)` aşırı yüklemesi | yeni üye |
| Arayüz | `IRbacWriteAheadAuditWriter` (deneme yaz: kapalı-başarısız; sonuç yaz: tek, `bool` döner, istisna atmaz) | yeni |
| Arayüz | `IRbacAuditHistoryReader` (tek okuma üyesi; güncelleme / silme üyesi yok) | yeni — (a1) |
| Varlık alanı (yalnız ekleme) | `AuthAuditLog.EntityType` (`string?`), `AuthAuditLog.EntityId` (`Guid?`) + bu alanları alan ek kurucu; mevcut kurucu (`AuthAuditLog.cs:7-15`) değişmez | yeni — (i2), DL-1 |
| Dizin (yalnız ekleme) | `authAuditLogs` üzerinde `(TenantId, EntityType, EntityId, OccurredAt)`; mevcut iki dizin (`MongoDbIndexConfigurations.cs:83-89`) değişmez | yeni — (i2), DL-1 |
| Yardımcı | `RbacAuditIds` (deneme kimliğinden sonuç kimliği; SHA-256, RFC 9562 v8) | yeni |
| Değer nesnesi | `RbacAuditAttempt` (`Id`, `TenantId`, `EntityType`, `EntityId`, `ActorId`): `RecordAttemptAsync`'in dönüşü. Sonuç yazımının karşılaştırma kaynağıdır (Validation Rules "İlişkili kontroller"); yazıcı satır okumaz | yeni |
| Değer nesnesi | `RbacAuditOutcome` (deneme (`RbacAuditAttempt`), sonuç `Succeeded` / `Failed` / `NotApplied`, aşama `attempt` / `mutation` / `version` / `sessions`, `MutationApplied` (`bool?`; `null` = bilinmiyor), `Uncertainty` (`null` / `write_concern` / `transport`)) | yeni |
| Hata sınıflandırıcı | `RbacMutationFailureClassifier` (istisna + yazım türü `SingleDocument` / `MultiDocument` → `MutationApplied` + `Uncertainty`; Entity Fields; S-7) | yeni |
| Depo üyesi (yalnız ekleme) | `IRoleRepository.GetByIdAndTenantIncludingDeletedAsync` (kiracıyla sınırlı; silinmiş rolü de getirir; yalnız rol geçmişi ucu kullanır) | yeni — (a1), (k) |
| Depo üyesi (yalnız ekleme) | `IRoleRepository.GetDeletedByTenantAsync(Guid tenantId, int limit, CancellationToken ct)` (yalnız `IsDeleted = true` ve kiracı; `DeletedAt` azalan, eşitlikte `Id` azalan; yalnız silinmiş roller ucu kullanır) | yeni — (k-1) |
| Koruma (yeni) | `RevokeRoleCommandHandler` ve `RevokePermissionCommandHandler`'a eylemi yapan denetimi: `ICurrentUserAccessor.UserId` yoksa deneme satırından önce 401 `ROLE_ACTOR_REQUIRED` | yeni davranış (iki revoke) |
| İstisna | `RbacAuditUnavailableException` (yalnız deneme satırı yazılamayınca) | yeni |
| Sorgu | `GetRoleAuditHistoryQuery`, `GetUserRoleAuditHistoryQuery`, `GetDeletedRolesQuery` | yeni — (a1), (k-1) |
| Uç | `GET /api/roles/{id:guid}/history`, `GET /api/users/{id:guid}/role-history`, `GET /api/roles/deleted` | yeni — (a1), (k-1) |
| Hata kodu | `RBAC_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_PAGING_INVALID` (400) | yeni |
| Olay sabiti (`int`, yalnız ekleme) | `RoleAuditEvents.OutcomeFailed = 2` ((m1)) ve `RoleAuditEvents.OutcomeUnconfirmed = 4`, `UserAuditEvents.OutcomeUnconfirmed = 4` (S-3). Dize sabiti değildir; etiket kapısı yalnız `string` sabitleri okur (`UserAuditEventLabelGuardTests.cs:31-35, :43-46`) | yeni |
| İzin | Yeni anahtar yok (Açık kararlar (f), kapandı) | — |
| Ekran bölümü | Roller hızlı görünümünde "Geçmiş", kullanıcı-rol atama ekranında "Rol geçmişi", Roller ekranında "Silinmiş roller" süzgeci (salt-okunur; satırda yalnız "Geçmiş") | yeni — (a1), (k-1) |
| Defter izi | `auth-rbac-gecmisi` (yol `c`, yazıcı) | yeni satır — (a1); iz kabulü CT defter kararıyla (DL-3) |

## Entity Fields

Yeni varlık yok. Satırlar mevcut `authAuditLogs` koleksiyonuna, mevcut `AuthAuditLog` biçimiyle yazılır (`AuthAuditLog.cs:3-22`).
Alanlar: `Id`, `EventName`, `UserId` (eylemi yapan), `TenantId`, `Metadata` (JSON), `OccurredAt`. Bunlara **yalnız ekleme** alanları
`EntityType` (`string?`) ve `EntityId` (`Guid?`) eklenir. Bu alanları ve dizini **FU29 DL-1 ekler** (Açık kararlar (i) (i2),
kapandı; karar kaydı 02 notu). Alan adları ve türleri ct-06 §5.3'teki FU27 tasarımıyla aynıdır. Böylece FU27 DL-5 aynı alanları
yeniden eklemeden tüketir. Treasury CT FU27 metnini buna göre revize eder (Dependencies). Bu paket FU27 dosyasına dokunmaz. Mevcut
kurucu (`AuthAuditLog.cs:7-15`) değişmez; alanları alan ek bir kurucu eklenir. Eski satırlarda alan yoktur (BSON'da eksik) ve
`EntityType` eşitlik süzgecine takılmaz. Bu, bilinçli olarak FU29 öncesi satırları geçmişin dışında bırakır (Frontend File Contract
"Kapsam notu").

| Satır | `Id` | `EventName` (Açık kararlar (c) (c1), kapandı) | `EntityType` / `EntityId` | `Metadata` |
|---|---|---|---|---|
| Deneme | yeni `Guid` (her komut çalışmasında bir; (c), kapandı) | `{olay}:attempt` | Rol komutlarında `Role` / rol kimliği; kişi-rol komutlarında `User` / hedef kullanıcı | Bugünkü olayın alanları (rol adı, izin kimliği / anahtarı, rol kimliği; güncellemede istenen `after`) + `actorId` + `phase = attempt` |
| Sonuç | `RbacAuditIds.OutcomeId(denemeKimliği)` | Başarıda bugünkü olay adı (`role_created`, `user_role_assigned` …); başarısızlıkta `{olay}:failed`; yapılmadıysa `{olay}:not_applied` | denemeyle aynı | Bugünkü olayın alanları + `attemptId`, `outcome`, `stage` (başarısızlık ve yapılmama), `mutationApplied` (`true` / `false` / `null`), `uncertainty` (yalnız `null`'da), güncellemede `before` / `after` |

- **`mutationApplied` (`bool?`):** `Succeeded` → `true`. `NotApplied` → `false` (yarış dalı ya da `stage = attempt`, Runtime
  Constraints). `Failed` + `stage = version` ya da `stage = sessions` → `true`, çünkü değişiklik depoya yazıldı ve sonraki adım düştü
  (yetki gerçekte kalktı ya da verildi). `Failed` + `stage = mutation` **üç** dala ayrılır. Sınıflandırmayı `RbacMutationFailureClassifier`
  yapar. Sınıflandırıcı istisnanın türüne ve hata alanlarına bakar. Sürücü `MongoDB.Driver` 2.27.0'dır
  (`Diten.AuthService.Persistence.csproj:13`). Seçim S-3 (kabul edildi, karar kaydı 03).
  - **Kesin hata → `false`, `uncertainty` yok.** Bu dal yalnız **tek belgeli** değişiklik yazımına uygulanır (S-7,
    kapandı (sahip 2026-11-06, "İkisini kabul et"; karar kaydı 04, karar 11)): `InsertOneAsync` (`RepositoryBase.cs:38-41`; rol ve rol-izin), `UpdateOneAsync` (`UserRoleRepository.cs:54`,
    `RoleRepository.cs:143`), `ReplaceOneAsync` (`RepositoryBase.cs:44-50`), `DeleteOneAsync` (`RolePermissionRepository.cs:88`).
    **Çok belgeli** yazım `UserRoleRepository.RevokeAsync`'tir (`DeleteManyAsync`, `UserRoleRepository.cs:64-69`). Orada bir yazma
    hatası kısmi silmeyle birlikte gelebilir, yani "hiç uygulanmadı" denemez. Bu yüzden `RevokeRole`'un değişiklik adımında **her**
    istisna `null` + `uncertainty = transport` ("sonuç bilinmiyor") sınıflanır. Yazım türünü işleyici sınıflandırıcıya verir. Tek
    belgeli yazımda bu dala yalnız şu iki durum girer. (1) `MongoWriteException`: `WriteError != null`
    ve `WriteConcernError == null`. (2) `MongoBulkWriteException`: `WriteErrors` boş değil ve `WriteConcernError == null`. Bu
    durumda sunucu yazımı reddetmiştir (yinelenen anahtar, belge doğrulama ya da başka yazma hatası kategorisi) ve yazım uygulanmamıştır.
    Bugün depolar yinelenen anahtarı aynı alanla ayırıyor: `UserRoleRepository.cs:56`, `RolePermissionRepository.cs:49`,
    `RoleRepository.cs:75`, `:114`.
  - **Yazma onayı hatası → `null`, `uncertainty = write_concern`.** Bu dala `MongoWriteConcernException` girer. Hata alanı yokken
    `WriteConcernError != null` olan `MongoWriteException` / `MongoBulkWriteException` da girer. Bu durumda yazım birincil sunucuda
    uygulanmıştır ama istenen onay gelmemiştir. Birincil değişirse yazım geri alınabilir. **Bu hata "Başarısız" (`false`) sayılmaz.**
    Ekran "Sonuçlanmadı" gösterir. Platform'a iletim yapılır: bilinen olay adı + `AuditOutcome.Unconfirmed = 4` (S-3).
  - **Taşıma belirsizliği → `null`, `uncertainty = transport`.** Bu dala şunlar girer: `TimeoutException`, `MongoConnectionException`,
    `MongoExecutionTimeoutException`, istek gönderildikten sonra gelen `OperationCanceledException` ve yukarıdaki iki dala girmeyen
    **her** öteki istisna. Burada onay yolda kaybolmuş olabilir; yazım uygulanmış da olabilir. Depo yeniden okunmaz (Açık kararlar
    (o) (o1), kapandı). Değer `null` yazılır ve ekran "Sonuçlanmadı" gösterir. Platform'a iletilmez (S-3; (m2) dalı).

  Okuyucu ve ekran rozeti bu alana göre seçilir (Frontend File Contract). Bilinmeyen istisna türü güvenli tarafta kalır: hiçbir
  zaman `false` ("uygulanmadı") sayılmaz.

- **Neden olay adları `RoleAuditEvents` / `UserAuditEvents` sınıflarına girmez:** `UserAuditEventLabelGuardTests` iki sınıfın
  sabitlerini Platform Audit Log etiketleriyle eşitliyor (grep `-n`: `UserAuditEventLabelGuardTests.cs:20, :24, :31-45`). Yeni sabit
  eklemek Platform ekranı kaynaklarına dokunmayı gerektirir ve o dosyalar kapsam dışıdır. Ekler (`:attempt`, `:failed`,
  `:not_applied`) ayrı bir sabit sınıfında durur. **Platform'a giden yazma olayı adları** (beş rol olayı ve iki kişi-rol olayı:
  `role_created`, `role_updated`, `role_deleted`, `role_permission_granted`, `role_permission_revoked`, `user_role_assigned`,
  `user_role_removed`) bugünküyle aynı kalır. **İki yeni okuma / meta-denetim olayı** bu cümlenin dışındadır. Adları Açık kararlar
  (p) (p1) ile verilir (kapandı): FU27 DL-5'in ad kalıbı `auth.{nesne}.history.read` izlenir (ct-06 §21:
  `auth.sod-waiver-assignment.history.read`). Adlar şunlardır: `auth.role.history.read` (rol geçmişi) ve
  `auth.user-role.history.read` (kişi-rol geçmişi). Silinmiş roller listesi denetim kaydı okumadığı için olay yazmaz. Etiket yolu da FU27
  ile aynıdır: Platform Audit Log etiketi, tek bir XMC-001 isteğiyle iki pakete birlikte istenir (Dependencies). FU27 ile ortak tek
  genel ad seçilirse paket revizyonu + yeni onay satırı gerekir.
  **Okuma satırının Platform değerleri (bulgu F-5; S-6, kapandı (sahip 2026-11-06, "İkisini kabul et"; karar kaydı 04, karar 11)):** `RequestType` = olay adı; `Category` =
  `IdentityAccess` (2); `Operation` = `Execute` (15); `Outcome` = `Succeeded` (1); `EntityType` = `Role` / `User`
  (`RoleAuditEvents.EntityType`, `UserAuditEvents.EntityType`); `EntityId` = okunan rol / kullanıcı; `IsMetaAudit = true`;
  `Metadata` = `page`, `pageSize`, `returnedCount` (kişisel veri yok). Seçim gerekçesi: `AuditOperation`'da okuma değeri yoktur
  (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:32-54`). Platform'un kendi denetim okuması da `Execute`
  kullanır (`…/Features/Audit/Handlers/QueryHandlers/GetAuditEventListHandler.cs:39`, `GetAuditEventByIdHandler.cs:54`). Kategori
  Auth ileticisinin sabitidir (`PlatformAuditForwarder.cs:28`, `:88`). **Platform kabulü (kod okuması, 2026-11-06):** `ValidateRequest`
  `Unknown` kategori, işlem, sonuç ve aktör türünü reddeder; boş `EntityType` / `SourceService` ve boş `CorrelationId`'yi reddeder
  (`AuditService.cs:205-263`). Seçilen değerler bunların hiçbirine takılmaz. İletici `CorrelationId`'yi hiç boş göndermez
  (`PlatformAuditForwarder.cs:73-75`). `IsMetaAudit` iç uçta silinmez ve kayda geçer (`AuditService.cs:153`). Uçtan uca kabul
  **ölçülmedi**; Platform tarafı kabul testi XMC-001 isteğine yazılır (Dependencies).
  **Etiket kapısı (bulgu F-17; ölçüldü):** `UserAuditEventLabelGuardTests` Platform köprüsündeki etiketleri `[a-z_]+` deseniyle okur
  (`UserAuditEventLabelGuardTests.cs:57`) ve iki resx'teki `AuditLog.Event.*` kümesinin iki sözlüğün birleşimine **eşit** olmasını
  ister (`:20-26`, `:64-79`). Noktalı ve tireli adlar desene uymaz; resx'e eklenen etiket "fazla" sayılır ve test kırmızıya döner.
  Bu yüzden DL-2 bu test dosyasını günceller (Repo Scope DL-2): desen `[a-z_.\-]+` olur ve beklenen küme iki sözlük + iki okuma adı
  olur. Okuma adları yeni `RbacHistoryAuditEvents` sabit sınıfındadır (`Features/Roles/RoleAuditHistoryModels.cs` içinde, DL-2). Test güncellemesi ile
  Platform etiketleri **aynı değişiklikte** main'e girer; ayrı girerlerse ara sürümde test kırmızıdır (XMC-001 isteğine yazılır).
- **Dizin:** FU29 DL-1 `(TenantId, EntityType, EntityId, OccurredAt)` dizinini ekler ((i2)). Bugünkü dizinler
  `MongoDbIndexConfigurations.cs:83-89` içindedir ve değişmez. Geçmiş sorgusu bu dizinle süzer. Kişi-rol geçmişinin olay ailesi
  süzgeci (`EventName` listesi) dizinden sonra uygulanır.
- **Değişmezlik (§4.1):** Yazıcıda ve okuyucuda güncelleme / silme / değiştirme üyesi yok. Sonuç satırı yalnız eklemeyle yazılır:
  aynı kimlik ikinci kez yazılmaz, var olan satır değişmez.
- **Kayda yazılmayan:** parola, token, e-posta, ad-soyad. Bu, bugünkü kayıt kuralıyla aynıdır (`IRbacAuditRecorder.cs:3-8`,
  `IUserAuditRecorder.cs:8-9`). Rol adı ve izin anahtarı kişisel veri değildir ve bugün de yazılıyor.

## Repo Scope

Yollar aksi yazılmadıkça `services/Diten.AuthService/` altındadır. Yeni dosyalar "(yeni)" ile işaretlidir. "(a1)" ve "(k-1)"
işaretleri yalnız izlenebilirlik içindir: iki karar kapandı (sahip 2026-11-05, onayla) ve satırlar bu paketin kapsamındadır.
(a2) / (a3) ya da (k-2) / (k-3) seçilirse paket revizyonu + yeni onay satırı gerekir.

### DL-1 — önce-yaz temeli + kişiye rol verme / geri alma

- `src/Diten.AuthService.Domain/Entities/AuthAuditLog.cs` — yalnız ekleme ((i2)): `EntityType` (`string?`), `EntityId` (`Guid?`) ve
  bu iki alanı alan ek kurucu. Mevcut kurucu `:7-15` ve özellikler `:17-21` değişmez.
- `src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs` — yalnız ekleme ((i2)): `authAuditLogs`
  `CreateManyAsync` listesine (`:83-89`) `(TenantId, EntityType, EntityId, OccurredAt)` dizini eklenir. Mevcut iki dizin değişmez.
- `src/Diten.AuthService.Application/Common/Services/RbacMutationFailureClassifier.cs` (yeni) — Entity Fields'teki üç dal.
- `src/Diten.AuthService.Application/Features/Users/Services/UserAuditEvents.cs` — yalnız ekleme `public const int OutcomeUnconfirmed = 4;`
  (`:49-52` yanında; S-3).

- `src/Diten.AuthService.Application/Common/Interfaces/IRoleAuditRecorder.cs` — yalnız ekleme: iki üye, fırlatan DIM (`:12-20`
  sonrasına); belge güncellemesi `:3-11` ("NEVER throws" `:10` → yeni üyenin istisnası adlandırılır).
- `src/Diten.AuthService.Application/Common/Interfaces/IUserAuditRecorder.cs` — yalnız ekleme: iki üye, fırlatan DIM (`:11-33` içinde);
  belge güncellemesi `:3-10` (`:9`). Mevcut `:25-32` sessiz-düşen DIM'e dokunulmaz ve emsal alınmaz.
- `src/Diten.AuthService.Application/Common/Interfaces/IRbacWriteAheadAuditWriter.cs` (yeni).
- `src/Diten.AuthService.Application/Common/Services/RbacAuditIds.cs` (yeni) (türetme kalıbı `ServiceClientRegistryReconciler.cs:208-218`).
- `src/Diten.AuthService.Application/Common/Services/RbacWriteAheadAudit.cs` (yeni): `RbacAuditAttempt`, `RbacAuditOutcome`, `RbacAuditUnavailableException`,
  ek sabitleri, `RbacMutationWrite` (`SingleDocument` / `MultiDocument`; S-7), `RbacAuditTimeouts` (`Attempt` = 5 sn, `Outcome` = 5 sn; DL-1'de sabittir, değişikliği paket revizyonu ister).
- `src/Diten.AuthService.Application/Common/Services/RoleAuditRecorder.cs` — yeni iki üyenin uygulaması. Mevcut `RecordAsync` (`:24-73`) değişmez.
- `src/Diten.AuthService.Application/Common/Services/UserAuditRecorder.cs` — yeni iki üyenin uygulaması. Mevcut iki `RecordAsync` (`:24-80`) değişmez.
  Sonuç aşırı yüklemesinin ne yazdığı Backend File Convention "Sonuç aşırı yüklemesi" maddesindedir.
- `src/Diten.AuthService.Persistence/Repositories/RbacWriteAheadAuditWriter.cs` (yeni). `authAuditLogs`'a yalnız `InsertOne`
  yapar. Yinelenen anahtarı `AuthAuditService.WriteOnceAsync` (`AuthAuditService.cs:29-50`) gibi ele alır. `EntityType` / `EntityId` doldurur.
- `src/Diten.AuthService.Persistence/DependencyInjection.cs` — yazıcı kaydı (`:109` yanında).
- `src/Diten.AuthService.Application/Features/Roles/RoleErrorCodes.cs` — `RBAC_AUDIT_UNAVAILABLE` (503).
- `src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs`, `RevokeRoleCommandHandler.cs` — akış.
  `RevokeRoleCommandHandler`'a `ICurrentUserAccessor` enjekte edilir ve **yeni** eylemi yapan koruması eklenir. Bugün alanları
  `:13-21`'dedir ve `ICurrentUserAccessor` yoktur; `AssignRoleCommandHandler.cs:48-49` deseni kullanılır.
- **DL-1'de defter değişmez:** `Diten.AuthService.md`, `AuditTrailStandardTests.cs` `PinnedCounts` ve `CT-DECISIONS.md`'ye
  dokunulmaz. K2 çıkışı DL-3'tedir (Açık kararlar (n), kapandı). DL-1'de erken çıkış seçilirse paket revizyonu + yeni onay satırı gerekir.
- Ön yüz (hata kodu, 7 dil): `frontend/Diten.Web/wwwroot/assets/js/Governance/UserRoleAssignments/index.js` (`ERROR_CODE_KEYS`, `:63`),
  `…/UserRoleAssignments/index.l10n.js`, `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_IndexL10n.cshtml`,
  `frontend/Diten.Web/Resources/Views/Governance/UserRoleAssignments/UserRoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (bugün 29 giriş × 7).
- Testler (yeni): `tests/Diten.AuthService.Application.Tests/Rbac/RbacWriteAheadAuditTests.cs` (yeni),
  `…/Rbac/RbacMutationFailureClassifierTests.cs` (yeni), `…/Users/AssignRoleWriteAheadTests.cs` (yeni),
  `…/Users/RevokeRoleWriteAheadTests.cs` (yeni). Platform'un `Outcome = 4` kabulü bu pakette testle değil, kod okumasıyla ölçülür
  (Implementation Notes madde 8). Uçtan uca kabul testi Platform dosyasıdır ve XMC-001 isteğine yazılır (Dependencies).
  `tests/Diten.AuthService.ServiceClientRegistry.Tests/RbacWriteAheadAuditMongoTests.cs` (yeni) (gerçek Mongo; desen
  `ServiceClientRegistryMongoTests.cs:365-378`). Mongo testi mevcut `Support/OwnedMongo.cs` ile koşar: test kendi `mongod`'unu
  başlatır (`DITEN_TEST_MONGOD` / `DITEN_ITEST_MONGOD_BIN_DIR`, `:52-58`), 27017'yi reddeder (`:29`) ve sabit veritabanını
  kullanır (`diten_auth_service_client_registry_itest`, `:21`) @`e3dc475bd`.
  Mevcut testler güncellenir: `…/Users/AssignRoleCommandHandlerTests.cs`, `…/Users/RevokeRoleCommandHandlerTests.cs`, `…/Users/LastStewardPathsTests.cs`.
- Ön yüz testleri (DL-1): `frontend/Diten.Web.Tests/Controllers/UserRoleAssignmentsAccessTests.cs` (mevcut; `:241`
  `UserRoleAssignmentsAccessTests`) — `RBAC_AUDIT_UNAVAILABLE` eşlemesi ve 7 dil resx eşliği. Eşlik testi için
  `frontend/Diten.Web.Tests/Governance/RbacAuditTextTests.cs` (yeni) açılır, çünkü `Governance/` klasöründe bugün yalnız
  `Mod0357CalendarSliceRowTests.cs` var @`e3dc475bd`.
  **`ct` geçişi testleriyle uyum (ölçüldü):** `RevokeRoleCommandHandlerTests.cs:95-106`
  (`Cancellation_token_is_propagated_to_both_repositories`) istek belirtecinin `RevokeAsync`'e ve `RevokeAllByUserAsync`'e aynen
  geçtiğini sınıyor. Bu iki çağrı istek `ct`'siyle kalır, yani test geçerli kalır. Yalnız sonuç satırı ve iletim `CancellationToken.None`
  alır. Bu ayrı ve yeni bir testle sınanır (AC-5); eski test ona genişletilmez.
- **Arayüz ekleme biçimi (DIM, istisna fırlatan):** Yeni arayüz üyelerinin varsayılan uygulaması `NotSupportedException` fırlatır.
  Emsaller: `IAuthAuditService.cs:18-19` (`WriteOnceAsync` → `throw new NotSupportedException(...)`) ve `IRoleRepository.cs:19-20`
  (`CreateSystemRoleIfMissingAsync` → `throw new NotSupportedException(...)`). **Kullanılmayacak emsal:** `IUserAuditRecorder.cs:25-32`
  (`succeeded` aşırı yüklemesi sessizce eski üyeye düşüyor). Böyle bir düşüş, önce-yaz üyesi olmayan bir sahtenin kapalı-başarısız
  yolu "başarılı" geçirmesine izin verir. Belge güncellemesi: `IRoleAuditRecorder.cs:3-11` (`:10` "NEVER throws") ve
  `IUserAuditRecorder.cs:3-10` (`:9` "NEVER throws") yeni üyeyi istisna olarak adlandıracak biçimde değişir: "Üretim kaydedicisinin
  `RecordAsync` aşırı yüklemeleri asla fırlatmaz; `RecordAttemptAsync` yazamazsa `RbacAuditUnavailableException` fırlatır. Arayüzdeki
  varsayılan uygulamalar (DIM) yalnız üyeyi uygulamayan sahteler içindir ve `NotSupportedException` fırlatır." **"Asla fırlatmaz"
  sözü üretim sınıfı içindir, arayüz için değildir (bulgu F-11).** İşleyici de sonuç çağrısını `try/catch` içine alır: DIM'li bir
  sahte ya da beklenmeyen bir hata değişiklikten sonra cevabı bozmaz; hata `LogError` ile yazılır (AC-4).
- **Etkilenen sahteler (ölçüldü, `grep -rnE "class .*[:,]\s*I(RoleRepository|UserAuditRecorder|RoleAuditRecorder)\b"`):**
  - `IUserAuditRecorder`: 3 sahte — `Roles/DefaultRoleTemplateOnceTests.cs:376` (`NoUserAudit`),
    `Users/UserAdminActionsTests.cs:314`, `Email/RecipientLanguageEmailTests.cs:173`. DIM fırlattığı için bu sahteler yalnız yeni
    üyeyi çağıran yolda kırmızıya döner. **Üçü de bu yolda değildir (ölçüldü @`e3dc475bd`).** `NoUserAudit`
    `InternalEventsController`'a verilir (`DefaultRoleTemplateOnceTests.cs:260`). `RecordingAudit`
    `UpdateMyPreferencesCommandHandler`'a verilir (`RecipientLanguageEmailTests.cs:129-130`). `RecordingUserAudit` yalnız
    `AdminResetPasswordCommandHandler`'a verilir (`UserAdminActionsTests.cs:237-238`, `:339-341`). Aynı dosyada
    `SetUserActiveStatusCommandHandler` sahte değil, `UserAuditForTests.None()` alır (`:127`). Hiçbiri
    `AssignRole` / `RevokeRole` işleyicisini kurmaz. Bu yüzden **güncellenmez** ve Repo Scope'ta değildir. Teslimde içlerinden biri
    kırmızıya dönerse kod durur ve dosya paket revizyonuyla eklenir; sessizce düzeltilmez.
  - `IRoleAuditRecorder`: 0 sahte. Testler üretim `RoleAuditRecorder`'ı `Testing/RoleAuditForTests.cs:15-16` ile kurar.
  - `IRoleRepository`: 18 sahte. Bunlar arasında `Auth/RegisterCommandHandlerTests.cs:143`, `Auth/PlatformAccountAtTenantDoorTests.cs:1966`
    ve `Permissions/InternalPermissionsControllerTests.cs:675` var. İki yeni üyeyi yalnız iki okuma işleyicisi çağırır
    (`GetRoleAuditHistoryHandler` → `GetByIdAndTenantIncludingDeletedAsync`, `GetDeletedRolesHandler` → `GetDeletedByTenantAsync`).
    Bu yüzden yalnız geçmiş ve silinmiş roller testleri onlara uygulama verir; 18 sahte DIM'le derlenir ve değişmez.
  - **Kurucu değişikliği:** `RoleAuditRecorder` ve `UserAuditRecorder` kurucusuna `IRbacWriteAheadAuditWriter` eklenir. Bu yüzden
    `Testing/UserAuditForTests.cs:14-17` (`Over` / `None`) ve `Testing/RoleAuditForTests.cs:15-16` (`Over`) yeni parametreyi alır.
    Varsayılan değer, kaydeden bir sahte yazıcıdır; mevcut çağıranlar değişmez.
  - **Sabotaj:** DIM sessiz no-op yapılırsa (`Task.FromResult<RbacAuditAttempt>(default!)` / `Task.CompletedTask`), önce-yaz üyesi olmayan bir sahteyle
    kurulan işleyici testi 503 yerine 204 alır ve kırmızıya döner. Bu test ayrı yazılır: "DIM yolu fırlatır, komut değişiklik yapmaz".

### DL-2 — rol ve izin komutları + geçmiş uçları

- `src/…/Features/Roles/Handlers/CommandHandlers/{AssignPermission,RevokePermission,CreateRole,UpdateRole,DeleteRole}CommandHandler.cs` — akış.
  `RevokePermissionCommandHandler`'a `ICurrentUserAccessor` enjekte edilir ve **yeni** eylemi yapan koruması eklenir. Bugün alanları
  `:12-21`'dedir ve `ICurrentUserAccessor` yoktur; `AssignPermissionCommandHandler.cs:44-45` deseni kullanılır.
- (a1) `src/…/Common/Interfaces/IRbacAuditHistoryReader.cs` (yeni). `src/…/Persistence/Repositories/RbacAuditHistoryReader.cs` (yeni) (yalnız `Find`).
- (a1) `src/…/Features/Roles/Queries/GetRoleAuditHistoryQuery.cs` (yeni), `…/Roles/Handlers/QueryHandlers/GetRoleAuditHistoryHandler.cs` (yeni).
- (a1) `src/…/Features/Users/Queries/GetUserRoleAuditHistoryQuery.cs` (yeni), `…/Users/Handlers/QueryHandlers/GetUserRoleAuditHistoryHandler.cs` (yeni).
- (a1) `src/…/Features/Roles/RoleAuditHistoryModels.cs` (yeni) (iki sorgunun DTO'ları tek dosyada).
- (a1) `src/…/Features/Roles/RoleErrorCodes.cs` — `RBAC_HISTORY_AUDIT_UNAVAILABLE` (503), `RBAC_HISTORY_PAGING_INVALID` (400).
- (a1, (k)) `src/Diten.AuthService.Application/Common/Interfaces/IRoleRepository.cs` — yalnız ekleme, iki üye (`:7` yanında):
  `GetByIdAndTenantIncludingDeletedAsync(Guid id, Guid tenantId, CancellationToken ct)` ve
  `GetDeletedByTenantAsync(Guid tenantId, int limit, CancellationToken ct)` ((k-1)).
  `src/Diten.AuthService.Persistence/Repositories/RoleRepository.cs` — iki uygulama. Bugünkü `GetByIdAndTenantAsync` (`:15-20`)
  `IsDeleted = false` ile süzer ve değişmez. İlk yeni üye yalnız `Id + TenantId` ile süzer. İkinci yeni üye `TenantId + IsDeleted = true`
  ile süzer ve `DeletedAt` azalan, eşitlikte `Id` azalan sıralar. `DeletedAt`'ı silme yazar (`RoleRepository.cs:129-143`, `:139`). Yeni
  üyeleri yalnız iki okuma işleyicisi çağırır; komut işleyicileri çağırmaz.
- (k-1) `src/…/Features/Roles/Queries/GetDeletedRolesQuery.cs` (yeni), `…/Roles/Handlers/QueryHandlers/GetDeletedRolesHandler.cs` (yeni).
  DTO `DeletedRoleDto(Id, Name, DisplayName, DeletedAt)` `RoleAuditHistoryModels.cs` içindedir.
- `src/…/Features/Roles/RoleAuditEvents.cs` — yalnız ekleme `public const int OutcomeFailed = 2;` ((m1)) ve
  `public const int OutcomeUnconfirmed = 4;` (S-3), `:33-34` yanında. Bunlar `int` sabitidir; etiket kapısı yalnız `string`
  sabitleri okur (`UserAuditEventLabelGuardTests.cs:31-35, :43-46`).
- (a1) `src/Diten.AuthService.Api/Controllers/RolesController.cs` — iki yeni GET (`:65-71` deseni): `{id:guid}/history` ve
  `deleted`. `deleted` adı `{id:guid}` kısıtına takılmaz (`:31`). `…/Api/Controllers/UsersController.cs` — yeni GET (`:328-342` yanında).
- Ön yüz (hata kodu, 7 dil): `frontend/Diten.Web/wwwroot/assets/js/Governance/Roles/index.js` (`:41`), `…/RoleAssignments/index.js` (`:106`),
  iki `index.l10n.js`, iki `_IndexL10n.cshtml`, `Resources/Views/Governance/Roles/RolesIndex.*.resx` (38 × 7),
  `Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.*.resx` (166 × 7).
- Testler (yeni): `tests/Diten.AuthService.Application.Tests/Roles/RoleWriteAheadTests.cs` (yeni) (beş işleyici, parametreli),
  (a1) `…/Rbac/RbacAuditHistoryTests.cs` (yeni) (HTTP, gerçek denetleyici), (a1) `…/Rbac/RbacAuditHistoryActorMaskTests.cs` (yeni) (AC-32),
  (k-1) `…/Roles/DeletedRolesEndpointTests.cs` (yeni) (HTTP, AC-33),
  (a1) `tests/Diten.AuthService.ServiceClientRegistry.Tests/RbacAuditHistoryMongoTests.cs` (yeni) (`OwnedMongo` ile).
- Ön yüz testleri (DL-2): `frontend/Diten.Web.Tests/Governance/RbacAuditTextTests.cs` (DL-1'de açılır) Roller ve rol-izin ekranı
  için genişler. Ayrıca `frontend/Diten.Web.Tests/Controllers/GovernanceRoleRefusalProxyTests.cs` (mevcut, `:31`) güncellenir:
  Web vekili `RBAC_AUDIT_UNAVAILABLE` kodunu düşürmeden taşır.
  Mevcut testler güncellenir: `…/Roles/AssignPermissionCommandHandlerTests.cs`, `…/Roles/RevokePermissionCommandHandlerTests.cs`,
  `…/Roles/RoleAuditRecorderTests.cs`, `…/Roles/RoleActorStampTests.cs`, `…/Roles/SystemGrantPathsActorGuardTests.cs`,
  `…/Testing/RoleAuditForTests.cs`, `…/Operational/ProductIdentityEntitlementReconciliationCommandContractTests.cs` (yalnız gerekiyorsa).
  `…/Users/UserAuditEventLabelGuardTests.cs` (mevcut; bulgu F-17): köprü deseni `:57` `[a-z_]+` → `[a-z_.\-]+`; beklenen küme `:20-26`
  iki sözlük + `RbacHistoryAuditEvents` (iki okuma adı). Değişiklik Platform etiketleriyle aynı değişiklikte girer (Dependencies, XMC-001).
  **`ct` geçişi testleriyle uyum (ölçüldü):** `RevokePermissionCommandHandlerTests.cs:179-183` istek belirtecinin `RevokeAsync`'e,
  sahip aramasına ve her `RevokeAllByUserAsync`'e geçtiğini sınıyor. Bu çağrılar istek `ct`'siyle kalır ve test geçerli kalır. Sonuç
  satırı ve iletim `CancellationToken.None` alır (AC-14; AC-5 deseni). `IRoleRepository.GetByIdAndTenantIncludingDeletedAsync` de DIM
  olarak eklenir ve `NotSupportedException` fırlatır (DL-1 "Arayüz ekleme biçimi").
- **Koşullu (S-5; yalnız sahip S-5'i kabul ettiyse ve 2026-11-11 tarih kapısında FU27 DL-5 ileticisi main'de değilse):**
  `src/Diten.AuthService.Application/Common/Interfaces/IPlatformAuditForwarder.cs` — yalnız ekleme: `PlatformAuditEvent`'e
  `bool IsMetaAudit = false` (`:9-21`) ve sonucu dönen `TryForwardAsync` üyesi (bugünkü `ForwardAsync` `:31` değişmez).
  `src/Diten.AuthService.Infrastructure/Services/PlatformAuditForwarder.cs` — yalnız ekleme: `TryForwardAsync` uygulaması ve yükte
  `IsMetaAudit` (yük bloğu `:77-100`; `IsPlatformGlobal` `:98` yanına). Bugünkü `ForwardAsync` (`:53-59` anahtar boş dalı dahil) değişmez. Ad, tür ve anlam ct-06 §5.3
  (FU27 satır 514-521 @`47e314f5c`) ile aynıdır. Test: `tests/Diten.AuthService.Application.Tests/Rbac/PlatformAuditForwarderTryForwardTests.cs`
  (yeni) (AC-18'in (i)–(iv) saplama sunucusu). `TryForwardAsync`'in arayüzdeki varsayılan uygulaması `false` döner ("teslim edilmedi";
  güvenli taraf: veri dönmez). Bu yüzden iki `IPlatformAuditForwarder` sahtesi (`RoleAuditRecorderTests.cs:139` `ThrowingForwarder`,
  `Testing/UserAuditForTests.cs:25` `RecordingPlatformAuditForwarder`; ölçüldü 2026-11-06) derlenir ve değişmez; geçmiş testleri
  gerçek ileticiyi saplama sunucusuna karşı kullanır. Tarih kapısında CT ölçümü kaydeder (`git grep -c TryForwardAsync origin/main -- services/Diten.AuthService/src`)
  ve Treasury CT'ye bildirir. İleticiler main'deyse bu madde uygulanmaz ve dosyalar korumalı kalır (Protected Paths).
- DL-2'de defter değişmez. Beş rol komutu `## Bilinen borç`'ta kalır, çünkü iz henüz kabul edilmedi.

### DL-3 — ekranlar, iz kabulü, borç kapanışı (a1)

- `frontend/Diten.Web/Views/Governance/Roles/_DetailsQuickView.cshtml` — "Geçmiş" bölümü (kısmi görünüm satırı).
- `frontend/Diten.Web/Views/Governance/Roles/_RoleHistory.cshtml` (yeni) — kısmi görünüm.
- `frontend/Diten.Web/Views/Governance/UserRoleAssignments/Index.cshtml` — seçili kullanıcı paneline "Rol geçmişi" satırı (`:111-144` bölgesi).
- `frontend/Diten.Web/Views/Governance/UserRoleAssignments/_UserRoleHistory.cshtml` (yeni) — kısmi görünüm.
- `frontend/Diten.Web/wwwroot/assets/js/Governance/rbac-history.js` (yeni) — İki ekranın ortak okuyucu-çizici modülüdür (tek kopya).
- `…/js/Governance/Roles/index.js`, `…/Roles/index.l10n.js`, `…/UserRoleAssignments/index.js`, `…/UserRoleAssignments/index.l10n.js`, iki `_IndexL10n.cshtml`.
- `frontend/Diten.Web/Controllers/RolesController.cs` (`:18` kökü) — bir yeni GET eylemi: `history/{id:guid}`.
  `…/Controllers/UserRoleAssignmentsController.cs` (`:18` kökü) — bir yeni GET eylemi: `role-history/{id:guid}`. Silinmiş roller
  listesi canlı liste gibi istemciden Gateway'e gider (`RolesController.cs:13-16` yorumu: liste istemci tarafında Gateway'den yüklenir);
  onun için Web eylemi yoktur. Eylemler Gateway üzerinden Auth uçlarına gider ve aynı kökten (same-origin) çağrılır.
  **Vekil deseni (ölçüldü @`e3dc475bd`):**
  (1) **Kapı.** `UserRoleAssignmentsController` her yazma eyleminin ilk satırında `RefuseWithoutPermission()` çağırır
  (`UserRoleAssignmentsController.cs:57-70`, `:76-77`, `:105-106`). İzinsiz kullanıcıya Gateway'e çağrı yapmadan 403 `JsonResult`
  döner. Yeni `role-history/{id:guid}` eylemi de ilk satırda bunu çağırır. Web `RolesController`'da böyle bir kapı yoktur
  (`RolesController.cs:102-106`, yalnız `AddAuthHeaders`). Yeni `history/{id:guid}` eylemi bugünkü gibi kapısızdır ve yetki Auth
  `[HasPermission]`'dadır (S-1).
  (2) **Başarısızlık.** Gateway 2xx dışı dönünce eylem `Json(await GatewayRefusal.ReadAsync(...))` döner (`RolesController.cs:65`,
  `:94` deseni; `GatewayRefusal.cs:90-123`). Cevap `{ success: false, errors, errorCodes }`'dur: Auth'un kararlı kodları taşınır, Auth'un
  İngilizce cümlesi taşınmaz. Bu desende Web cevabının HTTP durumu Auth'un durum kodunu **taşımaz**; ekran kodu ve `success`'i okur.
  Başarısızlıkta `data` yoktur. Bugünkü `GetById` deseni (`RolesController.cs:102-135`) başarısızlıkta yalnız `{ success = false }`
  döner ve kodu düşürür; bu desen **kullanılmaz**. Kod taşıma `GovernanceRoleRefusalProxyTests.cs:31` sınıfının sınadığı yoldur.
- `Resources/Views/Governance/Roles/RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx`, `…/UserRoleAssignments/UserRoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
  Anahtarlar ve 7 dil değerleri Frontend File Contract "Metin tablosu"ndadır.
- (k-1) `frontend/Diten.Web/Views/Governance/Roles/_Filter.cshtml` — "Silinmiş roller" seçeneği. `…/Roles/_DataTable.cshtml` —
  silinmiş satır görünümü: salt-okunur, satırda yalnız "Geçmiş" eylemi var, düzenle / sil / izin yok. Veri modu değişmez
  (`_DataTable.cshtml:9`, istemci verisi); silinmiş roller ayrı uçtan (`GET /api/roles/deleted`) ve süzgeç açılınca yüklenir. Liste
  ekranı protokolü uygulanır (AC-27).
- Sayfa yardımı (UXH-001; Page Help bölümü): `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` — bu
  anahtarlar eklenir: `Help.ROLES.Step5` / `Help.ROLES.Step5Title` ve `Help.USERROLES.Step5` / `Help.USERROLES.Step5Title`.
  `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` — `ROLES` (`:30`) ve `USERROLES` (`:32`) simge listelerine birer
  `bx-history` eklenir. Simge sayısı adım sayısına eşit olmalıdır (`:34` yorumu).
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Rbac/RbacWriteAheadCallerPinTests.cs` (yeni) — yol `c`
  belirtecini çağıran işleyicilerin sabit listesi (F-16; Audited Events "Yol `c` belirteci").
- `tests/architecture/audit-ledger/Diten.AuthService.md` — `## İzler`'e yeni satır ve beş koşul notu. `## Bilinen borç`'tan beş
  satır çıkar (`:37`, `:40`, `:42`, `:50`, `:53`). `## K2 borcu`'ndan iki satır çıkar (`:67` `AssignRoleCommand`, `:74` `RevokeRoleCommand`).
- `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` — `AcceptedTrails` (`:99-103` yanında) yeni
  üçlü. `PinnedCounts` Auth `Debt` ve `K2Debt` (`:61`) aynı değişiklikte değişir. Değerler teslimde ölçülür (Açık kararlar (d)).
- `tests/architecture/audit-ledger/CT-DECISIONS.md` — iz kabulü ve K2 çıkışı satırı.
- Ön yüz testleri (DL-3): `frontend/Diten.Web.Tests/Controllers/UserRoleAssignmentsAccessTests.cs` (mevcut) ve
  `…/Controllers/AccessGovernanceScreenHttpTests.cs` (mevcut; `:267` `UserRoleAssignmentsHttpTests`) — bölüm sayfa kapısıyla çizilir,
  izinsize çizilmez, üç talep biçimi. **Yeni eylemin kapı testi doğrudandır (bulgu F-1):** `UserRoleAssignmentsAccessTests.cs:68-86`
  teorisi **genişletilmez**. O teori ortak soyut tabandadır (`AccessGovernanceScreenAccessTests`, `:26`) ve `assign` dışındaki her satırı
  `Revoke`'a götürür (`:76`, `:42-43`, `:265-268`); `role-history` satırı eklense yeni eylemi hiç çağırmazdı. Bunun yerine somut
  `UserRoleAssignmentsAccessTests` sınıfına (`:241-278`) iki yeni `[Fact]` eklenir. Testler
  `((UserRoleAssignmentsController)controller).RoleHistory(UserId)` eylemini doğrudan çağırır:
  (a) `Role_history_without_the_key_is_refused_with_403_and_never_reaches_the_gateway`: `auth.users.read` talebiyle sonuç 403
  `JsonResult`'tır, `CountingGateway.Calls == 0`'dır ve gövde `AccessDenied` taşır (`:78-85` deseni);
  (b) `Role_history_with_the_key_reaches_the_gateway`: anahtarla `Calls == 1`'dir ve istek `/api/users/{UserId}/role-history`'ye gider.
  Kiracı talebi taban sınıfta `private`'tır (`:45`); somut sınıf kendi kiracı talebini kurar. `CountingGateway` `protected`'dır (`:222`).
  `frontend/Diten.Web.Tests/Controllers/RbacHistoryProxyTests.cs` (yeni) — iki vekil eylemi: 2xx'te veri; Auth 403 / 404 / 503'te
  `success: false`, Auth kodu `errorCodes`'ta, `data` yok, Auth'un İngilizce cümlesi yok. `frontend/Diten.Web.Tests/Governance/RbacAuditTextTests.cs` — rozet, aşama ve kapsam notu anahtarlarının 7 dil
  eşliği (İngilizce dışı ≠ `en`) ve ham kod yokluğu. `frontend/Diten.Web.Tests/JavaScript/Node/rbac-history.test.mjs` (yeni) —
  `rbac-history.js` birim testi (eşleme, rozet seçimi, gizli eylemi yapan). **Yardım testleri (bulgu F-9):**
  `frontend/Diten.Web.Tests/Help/GovernanceRbacHistoryHelpTests.cs` (yeni) — `ROLES` ve `USERROLES` için 7 dilde `Step5` /
  `Step5Title` dolu, İngilizce dışı değer `en`'den farklı, `Step6` yok; `PageHelpCatalog.StepIcon(code, 5) == "bx-history"` ve
  `StepIcon(code, 6)` varsayılan simge. `MvpOnePageHelpTests.cs` kullanılmaz: onun anahtar deseni `Related.*`'ı reddeder
  (`MvpOnePageHelpTests.cs:67-68`) ve iki ekranın yardımı `Help.ROLES.Related.*` / `Help.USERROLES.Related.*` taşır. Ayrıca
  `frontend/Diten.Web.Tests/Services/PageHelpCatalogTests.cs` (mevcut) güncellenir: `:51` bugün `StepIcon("ROLES", 5)`'in varsayılan
  olduğunu sınar ve beşinci simge eklenince kırmızıya döner. Satır `StepIcon("ROLES", 6)` olur ve `StepIcon("ROLES", 5) == "bx-history"`
  eklenir.

## Protected Paths

- `.antigravity/**` (sahip onayı ister). `audit-trail-standard.md` §4.4'teki eski cümle bu pakette düzeltilmez; bkz. Implementation Notes.
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız `integration-agent`; bu pakette değişiklik gerekmiyor).
- `services/Diten.Platform/**`, `services/Diten.MdmService/**` ve öteki domain servisleri.
- `frontend/Diten.Web/Views/Platform/AuditLog/**` ve `AuditLogIndex.*.resx` (Platform ekranı etiketleri; Entity Fields "neden" maddesi).
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IAuthAuditService.cs` ve
  `…/Persistence/Repositories/AuthAuditService.cs`. FU27 D98 "değişmez" diyor (ct-06 §5.3). Açık kararlar (c) (c-i) ile kapandı:
  bu dosyalar değişmez. (c-ii) seçilirse paket revizyonu + yeni onay satırı gerekir.
- `…/Features/ServiceIdentityTokens/**` (İ5 istisnası, `Diten.AuthService.md:26`) ve `ServiceClientRegistryReconciler.cs`.
- FU27 DL-5 dosyaları: `PaymentSodWaiver*`, `IPaymentSodWaiverAuditWriter`, `IPaymentSodWaiverAuditReader`, `_SodWaiverAssignments.cshtml`,
  `sod-waivers.js`. `PlatformAuditForwarder` / `IPlatformAuditForwarder` `TryForwardAsync` ve `PlatformAuditEvent.IsMetaAudit`
  eklemeleri FU27 DL-5'indir. Bu paket onları kullanır ama yazmaz. Tek istisna seçim S-5'tir: tarih kapısı
  geçerse bu eklemeler FU29 DL-2'ye taşınır (Repo Scope DL-2 "Koşullu (S-5)", Dependencies).
  **Korumalı değil (i2):** `AuthAuditLog.cs` alan eklemesi ve `MongoDbIndexConfigurations.cs` dizin eklemesi bu paketin DL-1
  Repo Scope'undadır. Yalnız ekleme yapılır; mevcut kurucu, özellikler ve dizinler değişmez.
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs`, `RoleProvisioningService.cs`, hak eşitlemesi yolları.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, arşiv yolları.
- Aynı klasördeki izlenmeyen yerel taslak `MOD-0018-FU27-finance-treasury-contract-permission-onboarding.md` (bu paketin değil).

## Dependencies

| Bağımlılık | Bugünkü durum (ölçüm) | Bu pakete etkisi |
|---|---|---|
| `AuthAuditLog.EntityType` / `EntityId` (yalnız ekleme) + `(TenantId, EntityType, EntityId, OccurredAt)` dizini | **(i2) ile FU29 DL-1'in kendi işidir** (Açık kararlar (i), kapandı; karar kaydı 02 notu: FU27'nin onaylı sürümü yok). Ölçüm: `AuthAuditLog.cs:3-22`'de alan yok. FU27 DL-5 kodu hiçbir dalda yok (`git log --all -S IPaymentSodWaiverAuditWriter -- services` boş, 2026-11-01). Ad ve türler ct-06 §5.3 ile aynı | Bağımlılık değildir; DL-1 Repo Scope'tadır |
| **FU27 metninin revizyonu (Treasury CT)** | FU27 paketinin main sürümünde DL-5 yok (U-1). ct-06 metni (`47e314f5c`) alanları ve dizini FU27 DL-5'e yazıyor | Treasury CT FU27 metnini (i2)'ye göre revize eder: alanları ve dizini FU29 DL-1 ekler, FU27 DL-5 tüketir ve yeniden eklemez. FU29 bu revizyonu beklemez. FU27 metni bu alanları farklı ad ya da türle onaylarsa çatışma olur: Entity Fields, Validation Rules, AC-11, AC-16 ve AC-18 yeniden ölçülür ve paket revize edilir. Bu paket FU27 dosyasına dokunmaz |
| FU27 DL-5 — `IPlatformAuditForwarder.TryForwardAsync` (yalnız ekleme) + `PlatformAuditEvent.IsMetaAudit` | Yalnız ct-06 metninde (§5.3, AC-46). Kod yok. Platform tarafında `AuditAppendRequest.IsMetaAudit` zaten var (`AuditAppendRequest.cs:27`) @`e3dc475bd`; Auth tarafındaki `PlatformAuditEvent` alanı yok | **DL-2 (a1) önkoşulu.** Geçmiş okuması meta-denetim satırını teslim teyidiyle gönderir. **Tarih kapısı 2026-11-11** (seçim S-5). O güne kadar main'de değilse iki ekleme FU29 DL-2'ye taşınır: yalnız ekleme, aynı imza (ct-06 §5.3). FU27 DL-5 onları tüketir |
| FU27 DL-5 — Roller sayfası değişikliği (`_SodWaiverAssignments.cshtml`, `RolesIndex.*.resx`) | ct-06 metni (§5.3 "Ekran"); kod yok | DL-3 aynı dosyalara dokunur. **Sıra şartı değildir** ((i2) ile FU27 DL-5'i beklemek kalktı). Hangisi önce birleşirse öteki main'i alıp yeniden tabanlanır. resx dosyalarında yalnız ekleme yapılır, öteki paketin anahtarı silinmez |
| Sıra | — | **FU29 DL-1 → DL-2 → DL-3.** DL-2'nin meta-denetim parçası S-5 tarih kapısına bağlıdır. Takvim: Teslim dilimleri "Takvim" |
| Platform Audit Log etiketi (XMC-001, (p1)) | İki okuma olayının Platform etiketi yok. Etiket dosyaları korumalı Platform yoludur (`frontend/Diten.Web/Views/Platform/AuditLog/**`, `AuditLogIndex.*.resx`) | Tek XMC-001 isteği FU27 ile ortak gider (Platform sahibine; tarih 2026-11-09, Takvim). DL-2 kabulünden önce etiket main'de olmalıdır; yoksa DL-2 kabul edilmez ((p3) kabul edilmez). İsteğe şunlar yazılır: (1) iki etiket (`AuditLogIndex.{en,tr}.resx`, `_IndexL10n.cshtml` köprüsü) ve Auth `UserAuditEventLabelGuardTests` güncellemesi **aynı değişiklikte** (bulgu F-17; desen `[a-z_]+` noktalı adları okumaz, `UserAuditEventLabelGuardTests.cs:57`); (2) Platform tarafı okuma satırı kabul testi: S-6 yükü (`Category 2`, `Operation 15`, `IsMetaAudit true`) iç uçtan 2xx alır (bulgu F-5); (3) `Outcome = 4` kabul testi (S-3) |
| `auth-kullanici-iletimi` izi (yol `b`, kabul edilmiş) | `AuditTrailStandardTests.cs:103`; `Diten.AuthService.md:12` | DL-1'de iki kişi-rol komutunun "denetleniyor" ölçümü bu izle sürer. Sonuç çağrısı `IUserAuditRecorder.RecordAsync` aşırı yüklemesidir, belirteç aynı kalır (Backend File Convention). İki komut DL-1'de `## K2 borcu`'nda **kalır**. Kapalı-başarısız satır kabul edilmemiş `aday` günlükte durur ve okuyan uç DL-3'te gelir. Bu yüzden K2 çıkışı DL-3'tedir (Açık kararlar (a), (n); kapandı) |
| `auth-rbac-gunlugu` izi | `aday` (`Diten.AuthService.md:14, :20`), belirteç `IRbacAuditRecorder.RecordAsync` | Değişmez. Beş rol işleyicisi bu üyeyi doğrudan çağırmıyor (Implementation Notes ölçüm 4) |
| Rol komutlarının borçtan çıkışı | Kabul edilmiş iz yok | Açık kararlar (a) (a1) ile kapandı. Çıkış DL-3'te, CT'nin defter kararıyla (`CT-DECISIONS.md`) olur. DL-2 davranışı kapalı-başarısız yapar ama defteri değiştirmez |
| Mongo | Roller, rol-izin, kullanıcı-rol ve `authAuditLogs` aynı veritabanında (`Persistence/DependencyInjection.cs:89-92` tek `IMongoDatabase`; depolar `:98-104`, denetim `:109`) | Kapalı-başarısız denetim ile acil geri alma dengesi: Açık kararlar (b) (b1), kapandı |
| FU-0022-AUD-02 (e-imza önce-yaz) | Açık (`MOD-0022-e-signature-service.md:1842`) | Bu paketin `IUserAuditRecorder.RecordAttemptAsync` üyesini kullanırsa yol `c` kredisi **otomatik gelmez**: çağıran sabit listesi (AC-34) kırmızıya döner. Kredi için kendi iz satırı ya da CT defter kararı ve bu paketin revizyonu gerekir (Follow-up Items) |
| MOD-0021 / Platform merkezi günlük | Değişmez | İletim bugünkü gibi en iyi çabayla ve sonra. Platform dosyası değişmez |
| Kayıt satırı | `origin/main`'de `MOD-0018-FU29` satırı yok (`grep -c` → 0, 2026-11-06, `6be9bc3d6`). Kule dalında commit'li (`25a03bebd`, `module-id-registry.md:92`) | CT'nin işi; onay satırından önce main'de (başlık ön koşul 3) |
| Karar kayıtları 02 ve 03 | Kule dalında commit'li (`25a03bebd`, `11fa9b94c`), main'de yok | CT'nin işi; onay satırından önce main'de (başlık ön koşul 4) |
| S2S anahtarı (`PlatformService:InternalApiKey`) | Temel `appsettings.json:68` boş; `appsettings.Development.json:64` dolu | DL-2 / DL-3 dağıtım ön koşulu (başlık "Dağıtım ön koşulu") |
| Onay kapısı (CT-APR-01) | Onay satırı yok | DL-1 dağıtımından önce CT, onay satırı yokken `prompt_gate.py check` / `pack_approval.py check`'in dağıtımı reddettiğini ölçer ve kaydeder (Ready-for-dev Checklist). Künyeye `approval_status` eklenmez |

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
  `RevokePermissionCommandHandler.cs:65-66`. (3) **Deneme satırı.** Yazılamazsa (istisna, iptal ya da `RbacAuditTimeouts.Attempt`
  dolması) işleyici `RbacAuditUnavailableException`'ı yakalar ve `RoleErrorCodes.Refuse<T>(RBAC_AUDIT_UNAVAILABLE, …, 503)` döner.
  Bugünkü ret deseni budur (`RevokeRoleCommandHandler.cs:55`, `:68`). `CustomBaseController` 503'ü olduğu gibi taşır
  (`CustomBaseController.cs:22`, varsayılan dal) @`e3dc475bd`. Değişiklik, sürüm artırma, oturum kapatma ve iletim yapılmaz.
  **Zaman aşımında yetim deneme:** Deneme yazımı süre dolduğu için düşmüş ama satır gerçekte yazılmış olabilir. Bu yüzden 503'ten
  önce en iyi çabayla bir sonuç satırı yazılır: `NotApplied`, `stage = attempt`, `mutationApplied = false`. Bu yazım da
  `CancellationToken.None` + `RbacAuditTimeouts.Outcome` kullanır. Deneme satırı varsa okuyucu onu "Yapılmadı" gösterir;
  "Sonuçlanmadı" göstermez. Deneme satırı yoksa bu sonuç satırı eşsiz kalır ve okuyucu onu göstermez, çünkü sayfalama deneme
  satırları üzerindendir (Backend File Convention "Okuyucu"). Kullanıcı ekranda 7 dilde "şu an yapılamadı, değişiklik yapılmadı"
  görür. (4) Değişiklik. (5) Sürüm artırma. (6) İki revoke'da refresh token iptali. (7) Sonuç satırı. (8) Platform iletimi.
  (9) **Yalnız `UpdateRole`:** cevap için rolün izin sayısı okunur (`GetPermissionsByRoleAsync`, `UpdateRoleCommandHandler.cs:68`).
  Bu okuma bugün de kayıttan sonradır (`:61-66` → `:68`) ve yeni akışta sonuç satırından ve iletimden sonra kalır. Bir aşama değildir:
  değişiklik ve sürüm artırma tamamlanmıştır, sonuç `Succeeded` yazılmıştır. Okuma istisna atarsa sonuç satırı değişmez (satır
  değişmezdir, §4.1) ve istisna bugünkü gibi yükselir (bulgu F-19; AC-14).
- **Değişiklikten sonraki hata:** (4)–(6) adımlarından biri istisna atarsa sonuç satırı `Failed` + `stage` (`mutation` /
  `version` / `sessions`) olarak yazılır. `version` / `sessions` aşamasında `mutationApplied = true` olur: değişiklik geçerlidir ve
  ekran bunu "Başarısız" değil, "Uygulandı, sonrası hata verdi" diye gösterir. Platform'a bilinen olay adı + `Failed` (2) iletilir
  (Açık kararlar (m) (m1), kapandı). `stage = mutation` için Entity Fields'teki üç dal geçerlidir (S-3). Ardından istisna bugünkü
  gibi yükselir (bugünkü durum kodu değişmez). Bugün bu durumda hiç
  satır yok: `RevokeRoleCommandHandler.cs:77-81` ve `RevokePermissionCommandHandler.cs:86-101` "kaldırma geçerli kalır, komut
  başarısız" diyor ve satır yazımına (`:85`, `:105`) varılmıyor. `AssignPermission` yarış dalı
  (`DuplicateRolePermissionException`, `AssignPermissionCommandHandler.cs:84-87`) sonuç satırını `NotApplied` yazar ve 409 döner.
- **İptal belirteci ve zaman aşımı:**
  - **Deneme satırı** istek `ct`'siyle yazılır ve açık bir üst sınırı vardır: `ct` ile bağlı (linked) belirteç + sabit zaman aşımı
    `RbacAuditTimeouts.Attempt` (5 sn, sabit). İstek iptal edilmiş ya da süre dolmuşsa deneme yazılmaz ve komut 503
    `RBAC_AUDIT_UNAVAILABLE` ile durur; değişiklik yapılmaz. **Bu davranış yazıcıya bırakılmaz (bulgu F-13):** `RecordAttemptAsync`
    bağlı belirteci kurduktan sonra ve yazımdan önce `ThrowIfCancellationRequested()` çağırır. Böylece önceden iptal edilmiş istek,
    belirteci yok sayan bir yazıcıyla da 503 alır. Gerçek yazıcıda ayrıca sürücü iptali uygular (`InsertOneAsync(…, cancellationToken)`).
  - **Sonuç satırı ve Platform iletimi** istek `ct`'sini **kullanmaz**. `CancellationToken.None` + sabit zaman aşımı
    `RbacAuditTimeouts.Outcome` ile yazılır (5 sn; her biri ayrı). Bu kural yedi işleyicinin hepsinde aynıdır (AC-5, AC-14).
    Gerekçe: Değişiklik yapıldıktan sonra istemcinin bağlantıyı
    kesmesi, sonucu kaydetme işini durdurmamalıdır. Yoksa yapılmış bir değişiklik "Sonuçlanmadı" kalır.
  - Değişiklik, sürüm artırma ve oturum kapatma bugünkü gibi istek `ct`'siyle çağrılır.
- **Sonuç satırı en iyi çabadır:** Yazılamazsa komutun cevabı değişmez ve hata `LogError` ile yazılır (sessiz değil, §4.3 son satır).
  Deneme satırı durur. Okuyucu o denemeyi "sonuçlanmadı / bilinmiyor" gösterir. **Platform iletimi yine yapılır:** iletim sonuç
  satırının yazılıp yazılmadığına bakmaz; bugünkü kaydediciler de yerel hata sonrasında iletir (Implementation Notes madde 8, AC-4).
- **İşlem (transaction):** Kullanılmaz. Sahip önce-yaz kalıbını seçti. Auth'ta Mongo oturumu başka yerde kullanılıyor
  (`EntitlementReconciliationOperationStore.cs:166`, oturumlu ekleme). İşlem içi yazım seçilmeyen yol olarak kayıtlıdır; aynı
  veritabanı gerçeği Açık kararlar (b)'de tartıldı ((b1), kapandı: revoke da kapalı-başarısızdır). (b2) / (b3) seçilirse paket
  revizyonu + yeni onay satırı gerekir.
- **Mevcut `RecordAsync` anlamı korunur:** `IRbacAuditRecorder.RecordAsync` istisna yutmaya devam eder (`RbacAuditRecorder.cs:28-42`).
  Kapalı-başarısız davranış yalnız yeni `RecordAttemptAsync` yolundadır. Yeni yol `IRbacAuditRecorder`'dan geçmez; doğrudan
  `IRbacWriteAheadAuditWriter`'a yazar.
- **Kimlik türetme:** `OutcomeId = v8(SHA-256("diten:auth:rbac-write-ahead:audit\noutcome\n" + attemptId:D))`. Kalıp
  `ServiceClientRegistryReconciler.cs:208-218`'dir. Deneme kimliği her çalışmada yeni üretilir (Açık kararlar (c): (c1) + yeni
  GUID + (c-i), kapandı). Öteki dallar seçilirse paket revizyonu + yeni onay satırı gerekir.
- **Portlar ve yol:** Auth arka ucu Gateway üzerinden çağrılır (`ocelot.json:2205-2221` `/api/users/{everything}`, `:2224-2240`
  `/api/roles/{everything}`; ikisi de 5056'ya gider). Web (5001) Auth'a doğrudan gitmez.
- **Silme:** Satır silinmez. Bugünkü `GetByIdAndTenantAsync` silinmiş rolü getirmez (`RoleRepository.cs:15-20`, `IsDeleted = false`).
  Rol geçmişi ucu bu yüzden yeni `GetByIdAndTenantIncludingDeletedAsync` üyesini kullanır: kiracıda hiç olmamış ya da başka
  kiracının rolü 404 döner, silinmiş rol 200 + geçmiş döner (Açık kararlar (k) bulur, kapandı). Silinmiş rollere giriş Roller
  ekranındaki "Silinmiş roller" süzgecidir ((k-1); `GET /api/roles/deleted`, AC-33).
  **Silinmiş kullanıcı (seçim S-4):** Kişi-rol geçmişi ucu kullanıcıyı bugünkü `IUserRepository.GetByIdAndTenantAsync` ile
  arar. Bu üye silinmiş kullanıcıyı getirmez (`UserRepository.cs:44-49`, `IsDeleted = false`) ve D' sarmalayıcısından geçer
  (`PlatformAccountScopedUserRepository.cs:49-53`). Bu yüzden silinmiş kullanıcının rol geçmişi **404** döner ve yeni depo üyesi
  açılmaz (AC-17). Gerekçe: Silinmişi getiren bir kullanıcı okuması, D' korumasının (platform hesabını gizleme) ve yumuşak silme
  süzgecinin dışında yeni bir okuma yolu açar. Kullanıcı seçici de yalnız silinmemiş kullanıcıları listeler
  (`UserRoleAssignments/index.js:331`, `/api/users?page=1&pageSize=1000`), yani giriş yolu yoktur. **Bilinen boşluk:** Silinmiş
  kullanıcının rol atama / geri alma satırları `authAuditLogs`'ta durur ve Platform'a iletilmiştir (yol `b`). Ancak kiracı ekranında
  okunamaz. DL-3'teki CT defter kararı bu boşluğu yol `c` koşul 3'ün istisnası olarak yazar. Takip: Follow-up Items.
- **Eylemi yapanın gösterimi (BL-521 D'; seçim S-2):** Geçmiş okuyucusu her satırın `UserId`'sini (eylemi yapan) yalnız
  `IUserRepository` üzerinden çözer. Bu kayıt D' sarmalayıcısıdır (`Persistence/DependencyInjection.cs:98`). Çağıran gizli bir kiracı
  aktörüyse sarmalayıcı platform hesabını getirmez (`PlatformAccountScopedUserRepository.cs:7-15`, `:49-53`). Çözülemeyen her
  eylemi yapan için cevap **kimlik ve ad taşımaz**: `actor = { hidden: true }`. Bu durum platform hesabını, silinmiş kullanıcıyı ve
  bilinmeyen kimliği kapsar. Ekran tek bir etiket gösterir: "Görüntülenemeyen kullanıcı" (7 dil; Frontend File Contract). Üç durum
  ayrılmaz, çünkü D' platform hesabını "olmayan kimlikten ayırt edilemez" tutar (`PlatformAccountScopedUserRepository.cs:14-15`).
  Çözülen eylemi yapan için kimlik ve güncel ad döner (Açık kararlar (j), kapandı). Ham `UserId` cevaba konmaz; satırın kendisi
  değişmez. Okuyucu iç `UserRepository`'yi (`DependencyInjection.cs:97`'de ayrıca kayıtlı) **kullanmaz** (AC-32). Sayfadaki farklı eylemi yapanlar en fazla
  `pageSize` kez `GetByIdAndTenantAsync` ile aranır. Adı toplu çözen bir üye yoktur. Toplu tek üye `VisibleUserIdsAsync`'dir
  (`IUserRepository.cs:84-87` @`e3dc475bd`); yalnız kimlik süzer, ad döndürmez. Okuyucu onu kullanmak zorunda değildir.
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
// IRoleAuditRecorder — yalnız ekleme; varsayılan uygulama FIRLATIR (emsal IAuthAuditService.cs:18-19, IRoleRepository.cs:19-20)
Task<RbacAuditAttempt> RecordAttemptAsync(string eventName, Guid tenantId, Guid roleId,
    IReadOnlyDictionary<string, object?> metadata, CancellationToken ct)            // yazılamazsa RbacAuditUnavailableException;
                                                                                    // ActorId = ICurrentUserAccessor.UserId (o an)
    => throw new NotSupportedException("Write-ahead RBAC audit is unavailable.");
Task RecordAsync(string eventName, Guid tenantId, Guid roleId,
    IReadOnlyDictionary<string, object?> metadata, RbacAuditOutcome outcome, CancellationToken ct) // uygulama istisna atmaz
    => throw new NotSupportedException("Write-ahead RBAC audit is unavailable.");

// IUserAuditRecorder — yalnız ekleme (targetUserId ile aynı iki üye, aynı fırlatan DIM)
// IRoleRepository — yalnız ekleme
Task<Role?> GetByIdAndTenantIncludingDeletedAsync(Guid id, Guid tenantId, CancellationToken ct)
    => throw new NotSupportedException("Reading deleted roles is implemented by the Mongo role repository.");
Task<IReadOnlyList<Role>> GetDeletedByTenantAsync(Guid tenantId, int limit, CancellationToken ct)   // (k-1)
    => throw new NotSupportedException("Listing deleted roles is implemented by the Mongo role repository.");
```

- **Belirteç ve ölçüm:** Mimari ölçüm, işleyicide türü bildirilmiş bir değişken üzerinden `ad.Metot(` çağrısı arar
  (`AuditTrailMeasurement.cs:361-375`, `:277-279`). Kişi-rol işleyicileri sonuç için `_userAudit.RecordAsync(…, outcome, ct)`
  çağırır. Bu çağrı kabul edilmiş `IUserAuditRecorder.RecordAsync` belirtecini (`AuditTrailStandardTests.cs:103`) karşılamaya devam
  eder. Belirteç bir yöntem adı eşleşmesidir. Aşırı yüklemenin ne yaptığı aşağıdaki maddededir ve AC-8 ile sınanır (sabotaj kanıtlı).
- **Sonuç aşırı yüklemesi (`RecordAsync(…, RbacAuditOutcome, …)`) ne yazar:** Yedi komutun her çalışmasında **tek bir yerel satır**
  yazılır: sonuç satırı. Bugünkü yerel satır (`IRbacAuditRecorder.RecordAsync` → `authAuditLogs`, `RoleAuditRecorder.cs:36`;
  `UserAuditRecorder.cs:45-52`) bu yedi komut için **ayrıca yazılmaz**. Sonuç satırı onun yerini alır; aynı satır iki kez görünmez.
  **Bugünkü yerel satırın bileşimi (ölçüldü @`e3dc475bd`):** `EventName` = bugünkü olay adı. `UserId` = eylemi yapan; `RbacAuditRecorder`
  onu `ICurrentUserAccessor`'dan okur (`RbacAuditRecorder.cs:32`, `:35`). `Metadata` = işleyicinin sözlüğü + rol olaylarında başta
  `roleId` (`RoleAuditRecorder.cs:32-33`), kişi olaylarında `targetUserId` (`UserAuditRecorder.cs:40`) + `actorId`
  (`RbacAuditRecorder.cs:34`). `OccurredAt` = yazım anı ve `CreatedBy = "auth-service"` (`AuthAuditLog.cs:13-14`). Sonuç satırı bu
  alanları aynı değerlerle taşır.
  Bugünkü yerel satırla farkları şunlardır, ve yalnız bunlardır:
  (1) `Id` türetilmiştir: `RbacAuditIds.OutcomeId(attemptId)`.
  (2) `EntityType` / `EntityId` doludur.
  (3) `Metadata` bugünkü anahtarlara `attemptId`, `outcome`, `stage`, `mutationApplied` ve `uncertainty` ekler.
  (4) Yazım `IRbacWriteAheadAuditWriter.WriteOutcomeAsync` ile tek-yazımdır. `CancellationToken.None` ve `RbacAuditTimeouts.Outcome` kullanır.
  (5) Satır başarısızlık ve yapılmama dallarında da yazılır. Olay adı bu dallarda `{olay}:failed` / `{olay}:not_applied` olur.
  Platform iletimi bugünkü yükle (`PlatformAuditEvent`) aynıdır. İletim kuralı Implementation Notes madde 8'dedir ((m1) + S-3).
  Öteki komutlar (`CreateUser`, `DeleteUser`, e-imza …) eski üyeyi çağırır ve bugünkü gibi bugünkü yerel satırı yazar.
- **Ortak yazıcı:** `IRbacWriteAheadAuditWriter` yalnız iki yazma üyesi taşır: `WriteAttemptAsync` (istisna atar) ve
  `WriteOutcomeAsync` (`bool` döner, istisna atmaz). Okuma üyesi taşımaz. İşleyiciler bu yazıcıyı doğrudan çağırmaz; kaydediciler
  çağırır. Böylece belirteç kaydedici üyesidir ve `IAuthAuditService.WriteOnceAsync`'i kullanan İ5 / kayıt defteri yolları bu izden
  not almaz. Gerekçe FU27 D97/2 (a) ile aynıdır.
- **Okuyucu (a1):** `IRbacAuditHistoryReader.GetHistoryAsync(Guid tenantId, string entityType, Guid entityId, string[] eventFamily,
  int page, int pageSize, CancellationToken ct)` tek üyedir. Güncelleme / silme üyesi yoktur.
  **Sayfalama birimi öğedir. Bir öğe bir deneme satırıdır ve eşlenen sonucunu taşır.** Okuma iki adımlıdır.
  (1) Deneme satırları okunur. Süzgeç: `TenantId + EntityType + EntityId` ve `EventName ∈ {aile}:attempt`. Sıralama `OccurredAt`
  azalan, eşitlikte `Id` azalandır. Bu sıralama anahtarı sabittir ve iki sayfa arasında öğe tekrar etmez. Sonra `Skip((page-1)*pageSize)`
  ve `Limit(pageSize)` uygulanır. `TotalCount` aynı süzgeçteki deneme satırı sayısıdır.
  (2) Sonuçlar tek sorguyla okunur. Süzgeç: `_id ∈ { RbacAuditIds.OutcomeId(d) | d sayfadaki denemeler }` **ve** adım (1)'in
  `TenantId + EntityType + EntityId` süzgeci (bulgu F-7). Kimlik deterministik olduğu için eşleme kesindir ve `Metadata` ayrıştırması
  gerekmez. Kimliği tutan ama başka kiracının ya da başka varlığın satırı olan bir sonuç eşlenmez; o deneme `unknown` döner. Bugün
  `authAuditLogs`'a yazan başka yollar da vardır (`AuthAuditService.cs:23-27`, `EntitlementReconciliationOperationStore.cs:166`);
  kimliği yalnız `_id`'ye bağlamak bu yolların hatalı bir satırını başka kiracıya gösterebilirdi.
  Sonucu olmayan deneme `outcome = unknown` döner. Denemesi olmayan sonuç satırı (yalnız `stage = attempt` yetimi; Runtime
  Constraints) hiçbir öğede görünmez ve sayıya girmez. FU29 öncesi satırlarda `EntityType` yoktur ve süzgece girmez.
- **Silinmiş roller ucu ((k-1)):** `GetDeletedRolesHandler`, `IRoleRepository.GetDeletedByTenantAsync(tenantId, 200, ct)` çağırır.
  Cevap `{ items: DeletedRoleDto[], truncated: bool }`'dur. Sıralama `DeletedAt` azalan, eşitlikte `Id` azalandır. 200'den fazla
  silinmiş rol varsa en yeni 200 döner, `truncated = true` olur ve ekran 7 dilde "en yeni 200 gösteriliyor" yazar. Canlı liste de
  istemci modundadır ve sayfalamasızdır. Ancak canlı listenin sunucuda üst sınırı **yoktur**: `GetAllByTenantAsync` süzer ve
  `ToListAsync` ile hepsini döner (`RoleRepository.cs:37-45` @`e3dc475bd`). 200, ekranın beyan ettiği istemci veri modudur
  (`Roles/_DataTable.cshtml:9-10` yorumu, `Roles/index.js:6`). Silinmiş roller ucu ise sınırı sunucuda uygular (`limit` parametresi).
  Bu uç denetim kaydını okumaz, yalnız rol listesini okur. Canlı liste
  gibi meta-denetim satırı yazmaz ve S2S anahtarına bağlı değildir. Geçmişe geçiş, satırdaki "Geçmiş" eylemiyle rol geçmişi ucundan
  olur ve o okuma meta-denetimlidir (AC-18).
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
| Roller hızlı görünümü — "Geçmiş" (a1) | `_DetailsQuickView.cshtml` + `_RoleHistory.cshtml` (yeni); `wwwroot/assets/js/Governance/rbac-history.js` (yeni); Web vekili `Controllers/RolesController.cs` | DL-3 |
| Kullanıcı-rol atama — "Rol geçmişi" (a1) | `Index.cshtml` + `_UserRoleHistory.cshtml` (yeni); aynı `rbac-history.js`; Web vekili `Controllers/UserRoleAssignmentsController.cs` | DL-3 |
| Roller — "Silinmiş roller" süzgeci (k-1) | `_Filter.cshtml`, `_DataTable.cshtml`, `index.js`; liste Gateway'den (`GET /api/roles/deleted`) | DL-3 |
| Sayfa yardımı (UXH-001) | `PageHelpResource.*.resx` (7), `Services/Help/PageHelpCatalog.cs` | DL-3 |

- Bölüm salt-okunurdur. Yazma düğmesi yoktur.
- **İzin kapısı (UAS-001) — bölümün ayrı kapısı yoktur (seçim S-1).** Açık kararlar (f) kapandı ve bölümün anahtarı sayfanın
  anahtarıyla aynıdır. Roller ekranında ikisi de `auth.roles.read`'dir (`Roles/Index.cshtml:21`, `Perms.Has`). Kullanıcı-rol ekranında
  ikisi de `auth.users.assign-role`'dür (`UserRoleAssignments/Index.cshtml:21`, `PermissionClaims.HasPermission`). Sayfa kapısı
  izinsiz kullanıcıda sayfanın kalanını hiç çizmez: `return;` ile döner ve `@section Scripts` kaydolmaz (`UserRoleAssignments/Index.cshtml:10-14`
  yorumu). Bu yüzden bölüme ikinci bir `if` yazmak **koruma değildir**. Kaldırıldığında hiçbir test kırmızıya dönmez (bulgu F-3).
  Bölüm sayfa kapısının içinde çizilir ve kendi kapısını taşımaz. Mekanik korumalar şunlardır:
  (1) **Auth ucu `[HasPermission]`**. Yetkinin sahibi budur. Kaldırılınca HTTP testindeki 403 kırmızıya döner (AC-16, AC-17).
  (2) **Sayfa kapısı.** Kaldırılınca bugünkü erişim testleri kırmızıya döner (`UserRoleAssignmentsAccessTests.cs:241`,
  `AccessGovernanceScreenHttpTests.cs:267`). AC-24 / AC-25 bölümün izinsizde çizilmediğini bu kapı üzerinden sınar.
  (3) **Kullanıcı-rol Web vekilinin kapısı.** `UserRoleAssignmentsController` bugün her yazma eyleminde izni ilk satırda denetler
  (`RefuseWithoutPermission`, `UserRoleAssignmentsController.cs:57-70`). Yeni `role-history` eylemi de öyle yapar. Kaldırılınca
  yeni eylemi doğrudan çağıran test (Repo Scope DL-3, `Role_history_without_the_key_…`) Gateway çağrısı sayar ve kırmızıya döner (AC-25).
  (4) **Roller Web vekili.** Bugün izin denetimi yapmaz (`RolesController.cs:102-106`, yalnız `AddAuthHeaders`) ve bu desen
  korunur. Auth'un kararlı kodunu `GatewayRefusal` cevabında taşır; başarısızlıkta veri döndürmez (Repo Scope DL-3 "Vekil deseni").
  Ayrı anahtar (`auth.roles.history.read` vb.) seçilirse paket revizyonu + yeni onay satırı gerekir ve bölüm kendi kapısını alır.
  Her ekran yine kendi okuyucusunu kullanır: Roller `Perms.Has(...)`, kullanıcı-rol `PermissionClaims.HasPermission(User, ...)`
  (`UserRoleAssignments/Index.cshtml:18-20` yorumu: `"*"`, `permissions` talebi ve virgüllü liste aynı sayılır).
- **Satır içeriği:** olay, zaman, eylemi yapan, konu (izin anahtarı / rol adı), sonuç rozeti ve başarısızlıkta aşama. Zaman
  kullanıcının saat dilimindedir. Güncelleme satırında önce / sonra gösterilir. Eylemi yapan çözülemiyorsa yalnız
  "Görüntülenemeyen kullanıcı" yazar; kimlik, ad ve bağlantı yoktur (Runtime Constraints "Eylemi yapanın gösterimi", S-2).
- **Rozet seçimi:**
  - Başarılı ← `Succeeded`.
  - Başarısız ← `Failed` + `mutationApplied = false`.
  - Uygulandı, sonrası hata verdi ← `Failed` + `mutationApplied = true`.
  - Yapılmadı ← `NotApplied`.
  - Sonuçlanmadı ← sonuç satırı yok **ya da** `mutationApplied = null`.
- **Kapsam notu (FU29 öncesi satırlar):** Bölümün başında tek satırlık sabit bir not durur: "Bu geçmiş, rol değişikliklerinin önce-yaz
  kaydıyla başlar; daha eski değişiklikler merkezi denetim günlüğündedir." FU29 öncesi satırlarda `EntityType` yoktur (Entity
  Fields) ve gösterilmez. Not 7 dildedir. Tarih yazılmaz, çünkü yayın tarihi ortama göre değişir.
- **Deneme zaman aşımı:** Komut 503 `RBAC_AUDIT_UNAVAILABLE` alınca ekran mevcut ret yolundan (`window.DitenRefusal.message`,
  `Roles/index.js:42`, `UserRoleAssignments/index.js:64`) 7 dilde "Değişiklik yapılmadı: denetim kaydı şu an yazılamıyor." der.
  Geçmişte o deneme "Yapılmadı" görünür (Runtime Constraints (3)).
- **Metin tablosu (DL-3; anahtarlar iki ekranın resx'inde aynı adla; değerler öneridir, teslimde dil sahibince düzeltilebilir ama
  İngilizce dışı değer `en` ile aynı olamaz):**

| Anahtar | en | tr | fr | es | zh | ar | ru |
|---|---|---|---|---|---|---|---|
| `RbacOutcomeSucceeded` | Succeeded | Başarılı | Réussi | Exitoso | 成功 | ناجح | Успешно |
| `RbacOutcomeFailed` | Failed | Başarısız | Échec | Fallido | 失败 | فشل | Ошибка |
| `RbacOutcomeAppliedWithErrors` | Applied, follow-up step failed | Uygulandı, sonrası hata verdi | Appliqué, étape suivante en échec | Aplicado, paso posterior fallido | 已应用，后续步骤失败 | طُبّق، وفشلت الخطوة التالية | Применено, следующий шаг не выполнен |
| `RbacOutcomeNotApplied` | Not applied | Yapılmadı | Non appliqué | No aplicado | 未应用 | لم يُطبَّق | Не применено |
| `RbacOutcomeUnknown` | Unconfirmed | Sonuçlanmadı | Non confirmé | Sin confirmar | 未确认 | غير مؤكد | Не подтверждено |
| `RbacStageAttempt` | Audit record | Denetim kaydı | Enregistrement d'audit | Registro de auditoría | 审计记录 | سجل التدقيق | Запись аудита |
| `RbacStageMutation` | Change | Değişiklik | Modification | Cambio | 变更 | التغيير | Изменение |
| `RbacStageVersion` | Permission refresh | Yetki yenileme | Actualisation des autorisations | Actualización de permisos | 权限刷新 | تحديث الصلاحيات | Обновление прав |
| `RbacStageSessions` | Session sign-out | Oturum kapatma | Déconnexion des sessions | Cierre de sesiones | 会话注销 | إنهاء الجلسات | Завершение сеансов |
| `RbacActorHidden` | User not shown | Görüntülenemeyen kullanıcı | Utilisateur non affiché | Usuario no mostrado | 未显示的用户 | مستخدم غير معروض | Пользователь не показан |
| `RbacEventRoleCreated` | Role created | Rol oluşturuldu | Rôle créé | Rol creado | 已创建角色 | تم إنشاء الدور | Роль создана |
| `RbacEventRoleUpdated` | Role updated | Rol güncellendi | Rôle mis à jour | Rol actualizado | 已更新角色 | تم تحديث الدور | Роль обновлена |
| `RbacEventRoleDeleted` | Role deleted | Rol silindi | Rôle supprimé | Rol eliminado | 已删除角色 | تم حذف الدور | Роль удалена |
| `RbacEventPermissionGranted` | Permission granted to role | Role izin verildi | Autorisation accordée au rôle | Permiso concedido al rol | 已向角色授予权限 | تم منح الصلاحية للدور | Разрешение предоставлено роли |
| `RbacEventPermissionRevoked` | Permission removed from role | Rolden izin kaldırıldı | Autorisation retirée du rôle | Permiso retirado del rol | 已从角色移除权限 | تمت إزالة الصلاحية من الدور | Разрешение отозвано у роли |
| `RbacEventUserRoleAssigned` | Role assigned to user | Kullanıcıya rol verildi | Rôle attribué à l'utilisateur | Rol asignado al usuario | 已为用户分配角色 | تم تعيين الدور للمستخدم | Роль назначена пользователю |
| `RbacEventUserRoleRemoved` | Role removed from user | Kullanıcıdan rol alındı | Rôle retiré de l'utilisateur | Rol retirado del usuario | 已从用户移除角色 | تمت إزالة الدور من المستخدم | Роль снята с пользователя |
| `RbacHistoryTitle` | History | Geçmiş | Historique | Historial | 历史记录 | السجل | История |
| `RbacUserRoleHistoryTitle` | Role history | Rol geçmişi | Historique des rôles | Historial de roles | 角色历史 | سجل الأدوار | История ролей |
| `RbacHistoryScopeNote` | This history starts with the write-ahead record of role changes; earlier changes are in the central audit log. | Bu geçmiş, rol değişikliklerinin önce-yaz kaydıyla başlar; daha eski değişiklikler merkezi denetim günlüğündedir. | Cet historique commence avec l'enregistrement préalable des modifications de rôles ; les modifications antérieures figurent dans le journal d'audit central. | Este historial comienza con el registro previo de los cambios de roles; los cambios anteriores están en el registro de auditoría central. | 此历史记录始于角色变更的预先记录；更早的变更见中央审计日志。 | يبدأ هذا السجل مع التسجيل المسبق لتغييرات الأدوار؛ التغييرات السابقة موجودة في سجل التدقيق المركزي. | Эта история начинается с предварительной записи изменений ролей; более ранние изменения находятся в центральном журнале аудита. |
| `RbacHistoryEmpty` | No records yet. | Henüz kayıt yok. | Aucun enregistrement pour le moment. | Aún no hay registros. | 暂无记录。 | لا توجد سجلات بعد. | Записей пока нет. |
| `RbacHistoryUnavailable` | History cannot be shown right now. | Geçmiş şu an gösterilemiyor. | L'historique ne peut pas être affiché pour le moment. | El historial no se puede mostrar en este momento. | 暂时无法显示历史记录。 | لا يمكن عرض السجل حاليًا. | Историю сейчас нельзя показать. |
| `RbacHistoryPagingInvalid` | Invalid page request. | Geçersiz sayfa isteği. | Demande de page non valide. | Solicitud de página no válida. | 页面请求无效。 | طلب صفحة غير صالح. | Недопустимый запрос страницы. |
| `ErrorRbacAuditUnavailable` | No change was made: the audit record cannot be written right now. | Değişiklik yapılmadı: denetim kaydı şu an yazılamıyor. | Aucune modification effectuée : l'enregistrement d'audit ne peut pas être écrit pour le moment. | No se realizó ningún cambio: el registro de auditoría no se puede escribir en este momento. | 未进行任何更改：暂时无法写入审计记录。 | لم يتم إجراء أي تغيير: لا يمكن كتابة سجل التدقيق حاليًا. | Изменение не выполнено: запись аудита сейчас невозможна. |
| `RbacDeletedRolesFilter` | Deleted roles | Silinmiş roller | Rôles supprimés | Roles eliminados | 已删除的角色 | الأدوار المحذوفة | Удалённые роли |
| `RbacDeletedRolesTruncated` | Showing the newest 200. | En yeni 200 gösteriliyor. | Affichage des 200 plus récents. | Se muestran los 200 más recientes. | 显示最新的 200 条。 | يتم عرض أحدث 200. | Показаны 200 самых новых. |
| `RbacHistoryPrevious` | Previous | Önceki | Précédent | Anterior | 上一页 | السابق | Назад |
| `RbacHistoryNext` | Next | Sonraki | Suivant | Siguiente | 下一页 | التالي | Далее |

  Tablo, ekranın gösterdiği bütün yeni metni kapsar (bulgu F-17): rozetler, aşamalar, gizli eylemci, yedi olay adı, bölüm
  başlıkları, kapsam notu, boş liste, iki geçmiş hata metni, deneme 503 metni ve silinmiş roller süzgeci. Olay etiketi denemenin ek
  almamış olay adından seçilir (`role_created:attempt` → `RbacEventRoleCreated`); eşleme `rbac-history.js` içindedir ve ham olay adı
  ekrana çıkmaz. `ErrorRbacAuditUnavailable` üç ekranın resx'ine, geçmiş anahtarları iki ekranın resx'ine, silinmiş roller anahtarları
  yalnız `RolesIndex.*.resx`'e girer. `SharedResource`'ta sayfalama anahtarı yoktur (ölçüldü: `Previous` / `Next` / `Page` adlı genel
  anahtar yok); bu yüzden iki sayfalama anahtarı da tablodadır.
  `RbacAuditTextTests.cs` anahtar kümesinin yedi dosyada eşit olduğunu ve İngilizce dışı değerin `en`'den farklı olduğunu sınar.
- **RTL (`ar`) ve FG-003:** Kabuk `ar`'da yönü `rtl` yapar (`localization-standard.md` §5). Bölüm yalnız mantıksal sınıflar
  kullanır: `ms-*`, `me-*`, `ps-*`, `pe-*`, `text-start`, `text-end`. `margin-left` / `padding-right` yoktur. Satır içi `style=`
  yoktur (FG-003); gereken sınıf `backbone-custom.css`'e eklenir. Zaman ve kimlik gibi soldan sağa değerler `<bdi>` içindedir. Ön yüz
  testi kısmi görünümlerde `style=` ve fiziksel yön sınıfı (`ml-`, `mr-`, `pl-`, `pr-`, `text-left`, `text-right`) bulunmadığını sınar.
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
| `id` (geçmiş uçları, rota) | Evet | `{id:guid}` rota kısıtı | — | Rol: `IRoleRepository.GetByIdAndTenantIncludingDeletedAsync` (yeni; kiracıda hiç yoksa ya da başka kiracınınsa 404, silinmişse 200 + geçmiş — Açık kararlar (k), kapandı). Kullanıcı: `IUserRepository.GetByIdAndTenantAsync` (D' sarmalayıcısı); yoksa, silinmişse ya da çağırana gizli bir platform hesabıysa 404 (S-4) |
| `page` | Hayır | Varsayılan 1; `< 1` → 400 `RBAC_HISTORY_PAGING_INVALID` | — | — |
| `pageSize` | Hayır | Varsayılan 20, üst sınır 100; `< 1` ya da `> 100` → 400 (kırpma yok). Değerler FU27 D101/1 ile aynıdır. Birim **öğedir** (deneme + eşlenen sonucu); sıralama `OccurredAt` azalan, eşitlikte `Id` azalan (Backend File Convention "Okuyucu") | — | — |
| `GET /api/roles/deleted` | — | Parametre yok. Kiracı JWT'den; en çok 200 öğe, `truncated` bayrağı | — | — |
| Deneme satırı `metadata` | Evet | Yalnız kimlikler, rol adı, izin anahtarı, güncellemede istenen `after`; e-posta / ad / token yok | `_id` tekil | — |
| Sonuç satırı `Id` | Evet | `RbacAuditIds.OutcomeId(attemptId)`; `Guid.Empty` reddedilir | `_id` tekil (aynı kimlik ikinci kez yazılmaz) | Yinelenen anahtarda satır gerçekten o kimlikte mi diye bakılır (`AuthAuditService.cs:41-48` deseni) |

İlişkili kontroller: `attemptId` boş olamaz. Sonuç satırının `TenantId` / `EntityType` / `EntityId` değeri denemeyle aynı olmalıdır.
**Karşılaştırmanın veri kaynağı (bulgu F-10):** `RecordAttemptAsync` deneme satırını yazınca `RbacAuditAttempt`'i döner; işleyici
onu `RbacAuditOutcome` içinde sonuç çağrısına geri verir. Kaydedici, sonuç çağrısının `tenantId` ve `roleId` / `targetUserId`
değerini bu değer nesnesiyle karşılaştırır. Yazıcı satır okumaz. Farklıysa sonuç satırı yazılmaz, `LogError` yazılır ve iletim
bugünkü kurala göre yine yapılır (AC-4). **`UserId` kaynağı:** İki satırın `UserId`'si `RbacAuditAttempt.ActorId`'dir. Bu değer deneme
anında `ICurrentUserAccessor.UserId`'den okunur (bugünkü yerel satırla aynı kaynak, `RbacAuditRecorder.cs:32`). Sonuç satırı onu
yeniden okumaz.

## Failure Path to Verify

- **Deneme satırı yazılamıyor (Mongo hatası / zaman aşımı)** — yedi komut
  - Expected: 503 `RBAC_AUDIT_UNAVAILABLE`. Rol / atama / izin değişmez. Sürüm artmaz. Refresh token iptal edilmez. Platform'a
    iletim yok. En iyi çabayla `NotApplied` + `stage = attempt` sonuç satırı yazılır (Runtime Constraints (3)). Ekran 7 dilde
    "Değişiklik yapılmadı: denetim kaydı şu an yazılamıyor" der; geçmişte deneme satırı varsa "Yapılmadı" görünür.
- **Duplicate — rol zaten izni taşıyor (yarış)**
  - Expected: 409 `ROLE_PERMISSION_ALREADY_GRANTED` (bugünkü kod). Deneme satırı vardır, sonuç `NotApplied` olur. Sürüm artmaz, iletim yok.
- **Duplicate — rol adı alınmış / kişi zaten rolde**
  - Expected: Bugünkü 409 `ROLE_NAME_TAKEN` / 204 etkisiz işlem. Deneme satırından önce döner; satır yok.
- **Missing — rol / kullanıcı yok ya da başka kiracıda**
  - Expected: Bugünkü 404 dalları (`ROLE_NOT_FOUND` / `USER_ROLE_USER_NOT_FOUND`) deneme satırından önce döner; satır yok. Bu dallar:
    `AssignRole` kullanıcı ve rol (`AssignRoleCommandHandler.cs:51-55`), `RevokeRole` kullanıcı (`RevokeRoleCommandHandler.cs:52-56`),
    `AssignPermission` rol (`AssignPermissionCommandHandler.cs:47-48`), `UpdateRole` ve `DeleteRole` rol
    (`UpdateRoleCommandHandler.cs:40-41`, `DeleteRoleCommandHandler.cs:45-46`). Geçmiş uçlarında da 404.
  - **404 olmayan iki dal (bugünkü davranış, değişmez; ölçüldü @`e3dc475bd`):** `RevokeRole` rolü bulamazsa durmaz; rol yalnız
    kayıttaki ad içindir (`RevokeRoleCommandHandler.cs:58-61`; test `RevokeRoleCommandHandlerTests.cs:100` `role: null`). Bu yüzden
    olmayan rol kimliğiyle deneme satırı, `RevokeAsync`, sürüm artırma ve oturum kapatma yine yapılır. `RevokePermission` rolü
    bulamazsa hak da bulunmaz ve 204 etkisiz işlem döner (`RevokePermissionCommandHandler.cs:56`, `:63-66`); satır yok.
- **Unauthorized — izin yok**
  - Expected: 403 (`[HasPermission]`). İşleyiciye ulaşmaz, satır yok. Geçmiş bölümü çizilmez.
- **Eylemi yapan yok**
  - Expected: 401 `ROLE_ACTOR_REQUIRED`. Beş komutta bu bugünkü davranıştır. `RevokeRole` ve `RevokePermission`'da **yeni**
    korumadır: bugün iki revoke aktörsüz de çalışıyor. Satır, değişiklik ve oturum kapatma yok.
- **Değişiklikten sonra sürüm artırma ya da oturum kapatma hatası (iki revoke; sürüm hatası yedi komutta)**
  - Expected: Değişiklik geçerli kalır (bugünkü gibi). Sonuç `Failed`, `stage = version` / `sessions`, `mutationApplied = true`.
    Bugünkü hata cevabı değişmez. Geçmişte "Uygulandı, sonrası hata verdi" görünür, "Başarısız" görünmez. Platform'a bilinen olay
    adı + `Failed` (2) iletilir ((m1), kapandı).
- **Değişiklik yazımı hata veriyor — üç sınıf (S-3; Entity Fields)**
  - Kesin sunucu yazma hatası (`WriteError` var, `WriteConcernError` yok) → `mutationApplied = false`, "Başarısız", iletim yok.
  - Yazma onayı hatası (`MongoWriteConcernException` ya da yalnız `WriteConcernError`) → `mutationApplied = null`,
    `uncertainty = write_concern`, "Sonuçlanmadı", Platform'a `Unconfirmed` (4) iletilir.
  - Taşıma belirsizliği (zaman aşımı, bağlantı, gönderimden sonra iptal, bilinmeyen istisna) → `mutationApplied = null`,
    `uncertainty = transport`, "Sonuçlanmadı", Platform'a iletilmez.
  - Üçünde de istisna bugünkü gibi yükselir ve bugünkü durum kodu değişmez.
- **Sonuç satırı yazılamıyor**
  - Expected: Komutun cevabı başarı olarak kalır, `LogError` yazılır. Geçmişte o deneme "Sonuçlanmadı" görünür.
- **Concurrency — aynı sonuç iki kez yazılıyor (yeniden deneme)**
  - Expected: Tek satır kalır. İkinci yazım `false` döner, var olan satır değişmez.
- **Geçmiş okuması meta-denetimi teslim edilemiyor (a1)**
  - Expected: 503 `RBAC_HISTORY_AUDIT_UNAVAILABLE`, veri dönmez. Sebepler: anahtar boş, 2xx dışı yanıt, bağlantı hatası, zaman aşımı.
    Anahtar boşsa bu sonuç tasarım gereğidir (başlık "Dağıtım ön koşulu").
- **Eylemi yapan platform hesabı ya da çözülemiyor (a1)**
  - Expected: 200; o satırın `actor` alanı `{ hidden: true }`, kimlik ve ad yok; ekran "Görüntülenemeyen kullanıcı" der (S-2).
- **Silinmiş kullanıcının rol geçmişi (a1)**
  - Expected: 404 (bugünkü kullanıcı araması; S-4). Bilinen boşluk.
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

| Uç | İzin | Kaynak |
|---|---|---|
| `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{roleId}` | `auth.users.assign-role` (değişmez) | `Api/Controllers/UsersController.cs:328-342` |
| `POST /api/roles` · `PUT /api/roles/{id}` · `DELETE /api/roles/{id}` | `auth.roles.create` · `.update` · `.delete` (değişmez) | `RolesController.cs:39-40`, `:48-49`, `:57-58` |
| `POST /api/roles/{id}/permissions`, `DELETE /api/roles/{id}/permissions/{permissionId}` | `auth.roles.assign-permission` (değişmez) | `RolesController.cs:73-74`, `:81-82` |
| (a1) `GET /api/roles/{id}/history` | **`auth.roles.read`** — rolü gören geçmişini de görür (§5.c-3, CT kararı 2026-10-02) | Açık kararlar (f), kapandı |
| (k-1) `GET /api/roles/deleted` | **`auth.roles.read`** — canlı liste ile aynı anahtar (`RolesController.cs:23-24`) | Açık kararlar (f), (k), kapandı |
| (a1) `GET /api/users/{id}/role-history` | **`auth.users.assign-role`** — kaydın gösterildiği ekranı açan anahtar (`UserRoleAssignments/Index.cshtml:21`, `Controllers/UserRoleAssignmentsController.cs:23`) | Açık kararlar (f), kapandı |

- Yeni anahtar yok ((f) kapandı). Yeni anahtar seçilirse paket revizyonu + yeni onay satırı gerekir. Anahtar PKS-001 biçimine uyar
  (ör. `auth.roles.history.read`) ve katalog / tohum / açık atama listesi ayrı bir onayla gelir.
- Ekran bölümü kendi kapısını taşımaz; sayfa kapısının içinde çizilir (S-1; Frontend File Contract "İzin kapısı"). Web vekili
  yalnız gösterim ve vekil kapısıdır. Yetkinin sahibi Auth `[HasPermission]`'dır.
- Kapalı-başarısız davranış için yeni bir izin yolu açılmaz. Platform yöneticisi de aynı kurala tabidir.

## Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**.

- Ön yüz Gateway 5000 üzerinden çağırır, servis portuna doğrudan gitmez.
- Mevcut `ocelot.json`'da `/api/users/{everything}` (`:2205-2221`) ve `/api/roles/{everything}` (`:2224-2240`) var. Yöntemleri
  GET / POST / PUT / OPTIONS / DELETE'dir. Üç yeni GET ucu (`/api/roles/{id}/history`, `/api/roles/deleted`, `/api/users/{id}/role-history`) bu yolların altındadır (yeniden ölçüldü @`e3dc475bd`: `ocelot.json:2232` `/api/roles/{everything}`, `:2213` `/api/users/{everything}`, ikisi de 5056).
- `integration-agent` görevi yok. `ocelot.json` korumalı yoldur ve bu paket ona yazmaz.

## Acceptance Criteria

- [ ] AC-1 (zorunlu) — **Deneme önce (kişi-rol):** `AssignRoleCommand` deneme satırını `IUserRoleRepository.AssignAsync`'den,
  `RevokeRoleCommand` deneme satırını `IUserRoleRepository.RevokeAsync`'den önce yazar. Sıra, çağrı sırasını kaydeden sahte depolarla
  sınanır. Sabotaj: deneme çağrısı değişiklikten sonraya taşınınca sıra testi kırmızıya döner.
- [ ] AC-2 (zorunlu) — **Kapalı-başarısız (kişi-rol):** Deneme yazımı başarısız olunca iki komut 503 `RBAC_AUDIT_UNAVAILABLE` döner.
  Atama / geri alma, `IRoleAssignmentVersionService.IncrementAsync`, `RevokeAllByUserAsync` ve Platform iletimi **hiç çağrılmaz**.
  Gerçek Mongo testinde `user_roles` değişmez. Sonuç yazıcısı tam bir kez çağrılır: `NotApplied`, `stage = attempt`,
  `mutationApplied = false` ve istek belirteci olmayan bir belirteçle (Runtime Constraints (3)). Gerçek Mongo testinde deneme satırı
  yazılmış ama yazıcı zaman aşımı almışsa (sahte gecikme) okuyucu o denemeyi "Yapılmadı" döner, "Sonuçlanmadı" dönmez. Sabotaj:
  kaydedici deneme hatasını yutup devam edince test kırmızıya döner. `stage = attempt` sonuç yazımı kaldırılınca zaman aşımı testi
  `unknown` bulur ve kırmızıya döner.
- [ ] AC-3 (zorunlu) — **Tek sonuç satırı:** Sonuç satırının kimliği `RbacAuditIds.OutcomeId(attemptId)`'dir. Aynı sonucun ikinci
  yazımı gerçek Mongo'da ikinci satır üretmez, `false` döner ve ilk satır değişmez. Farklı deneme kimlikleri farklı sonuç kimliği
  verir; aynı deneme kimliği her süreçte aynı sonucu verir (sabit girdi → sabit beklenen GUID). Sabotaj: türetme yerine
  `Guid.NewGuid()` kullanılınca tekillik ve sabit değer testleri kırmızıya döner.
- [ ] AC-4 (zorunlu) — **Sonuç en iyi çaba:** Sonuç yazımı istisna atınca komut yine **bugünkü başarı kodunu** döner ve `LogError`
  kaydı oluşur (günlük yakalayıcıyla sınanır). Bugünkü kodlar (ölçüldü @`e3dc475bd`): `CreateRole` 201
  (`CreateRoleCommandHandler.cs:60`), `UpdateRole` 200 (`UpdateRoleCommandHandler.cs:70`); `DeleteRole`, `AssignPermission`,
  `RevokePermission`, `AssignRole` ve `RevokeRole` 204 (`DeleteRoleCommandHandler.cs:69`, `AssignPermissionCommandHandler.cs:97`,
  `RevokePermissionCommandHandler.cs:108`, `AssignRoleCommandHandler.cs:70`, `RevokeRoleCommandHandler.cs:88`). Deneme satırı durur.
  **İletim korunur (bulgu F-6):** Sonuç yazımı istisna attığında, `false` döndüğünde ya da Validation Rules karşılaştırması tutmadığında
  Platform'a iletim yine yapılır. Yük, aynı komutun sonuç yazımı başarılı olsaydı gidecek yükle eşittir (AC-7 kuralı). Test yedi
  komutun hepsinde parametrelidir: sahte yazıcı sonuçta istisna atar, kaydeden sahte iletici tam bir çağrı alır. Sabotaj: sonuç
  hatası yükseltilince başarı kodu testi kırmızıya döner. Günlük satırı kaldırılınca günlük testi kırmızıya döner. İletim sonuç
  yazımının başarısına bağlanınca (`if (wrote) Forward…`) iletim testi sıfır çağrı bulur ve kırmızıya döner. Sonuç çağrısındaki
  `try/catch` kaldırılınca (DIM'li sahteyle) başarı kodu testi kırmızıya döner.
- [ ] AC-5 (zorunlu) — **Değişiklikten sonraki hata (RevokeRole):** `RevokeAllByUserAsync` istisna atınca sonuç satırı `Failed`,
  `stage = sessions`, `mutationApplied = true` olarak yazılır ve istisna bugünkü gibi yükselir. `IRoleAssignmentVersionService.IncrementAsync`
  istisna atınca aynı satır `stage = version` ile yazılır. Rol kaldırma geçerli kalır. **Kesin hata dalı tek belgeli yazımdadır
  (S-7):** `AssignRole`'un `AssignAsync`'i (`UpdateOneAsync` upsert) `WriteError` dolu, `WriteConcernError` boş `MongoWriteException`
  atınca sonuç `stage = mutation`, `mutationApplied = false` olur. `RevokeRole`'un `RevokeAsync`'i çok belgelidir (`DeleteManyAsync`):
  aynı istisna orada `mutationApplied = null`, `uncertainty = transport` olur (kısmi silme olabilir). **Yazma onayı** hatasında (`MongoWriteConcernException` ya da `WriteError` boş,
  `WriteConcernError` dolu `MongoWriteException`) sonuç `mutationApplied = null`, `uncertainty = write_concern` olur. **Taşıma**
  belirsizliğinde (`TimeoutException`, `MongoConnectionException`, gönderimden sonra iptal ya da sınıflandırıcının tanımadığı bir
  istisna) sonuç `mutationApplied = null`, `uncertainty = transport` olur. İki `null` dalında okuyucu "Sonuçlanmadı" döner. Üç dal
  `AssignRole`'un `AssignAsync` çağrısında da sınanır. Sınıflandırıcının birim testi (`RbacMutationFailureClassifierTests`) her
  istisna biçimini ve iki yazım türünü (`SingleDocument` / `MultiDocument`) ayrı satırda taşır. **İptal:** İstek belirteci değişiklikten sonra iptal edilince (sahte oturum kapatma adımı `cts.Cancel()`
  çağırır) sonuç satırı yine yazılır. Sahte yazıcıya gelen belirteç iptal edilmemiştir ve istek belirteci değildir. Platform iletimi de
  aynı biçimde yapılır. Deneme satırı ise istek belirteciyle ve zaman aşımı sınırıyla çağrılır: önceden iptal edilmiş belirteçle
  komut 503 döner ve değişiklik çağrılmaz. Bu test **belirteci yok sayan** bir sahte yazıcıyla koşar (bulgu F-13), yani 503'ü
  kaydedicinin kendi `ThrowIfCancellationRequested()` denetimi üretir (Runtime Constraints "İptal belirteci"). Sabotaj: hata dalındaki sonuç yazımı kaldırılınca; `mutationApplied` `version` /
  `sessions` aşamasında `false` yazılınca; belirsiz hata kesin hata gibi `false` sınıflanınca (zaman aşımı testi `null` bekler);
  sonuç yazımına istek `ct`'si geçirilince (iptal testi sonuç satırı bulamaz) test kırmızıya döner. `WriteConcernError` taşıyan
  istisna kesin hata gibi `false` sınıflanınca yazma onayı testi kırmızıya döner. Bilinmeyen istisna `false` sınıflanınca güvenli
  taraf testi kırmızıya döner. `MultiDocument` yazımındaki `WriteError` `false` sınıflanınca `RevokeRole` kısmi silme testi kırmızıya
  döner. Kaydedicideki `ThrowIfCancellationRequested()` kaldırılınca önceden iptal testi değişiklik çağrısı bulur ve kırmızıya döner.
- [ ] AC-6 (zorunlu) — **Korumalar ve etkisiz işlem satır yazmaz:** 401 (eylemi yapan yok; beş komutta bugünkü, iki revoke'da AC-29 ve AC-30 ile
  yeni), 403 (`AssignPermission` uygunluk korumaları `PermissionNotForHumanRole` / `PermissionNotTenantAssignable`,
  `AssignPermissionCommandHandler.cs:57`, `:68`; sistem rolü `ROLE_SYSTEM_NOT_EDITABLE` / `ROLE_SYSTEM_NOT_DELETABLE`,
  `UpdateRoleCommandHandler.cs:46`, `DeleteRoleCommandHandler.cs:48`; bugünkü), 404 (Failure Path "Missing"teki bugünkü 404 dalları),
  409 (son yetkili; `RevokePermission`'da ayrıca `ROLE_PERMISSION_GRANT_MANAGED`, `:67-68`; `CreateRole`'da `ROLE_NAME_TAKEN`,
  `CreateRoleCommandHandler.cs:43-44`) ve 204 etkisiz işlem (`AssignRole` `:57-58`, `RevokePermission` `:65-66`) deneme satırı
  **yazmaz**. Bu cevaplar bugünkü gibidir. `RevokeRole`'un olmayan rol dalı koruma değildir ve deneme satırı yazar (Failure Path
  "404 olmayan iki dal"); test bunu ayrı satırda sabitler. Sabotaj: deneme çağrısı korumaların önüne alınınca 401 / 404 / 409 testleri satır bulur ve kırmızıya
  döner.
- [ ] AC-7 (zorunlu) — **İletim sonra ve aynı:** Başarıda Platform'a değişiklikten sonra, bugünkü yükle iletim yapılır: `EntityType User`,
  hedef kullanıcı, `Operation` Assign / Revoke, `Succeeded`, aynı `Metadata` anahtarları. Yük bugünkü testlerin beklenen değeriyle
  eşittir. `Failed` + `mutationApplied = true` → bilinen olay adı + `Failed` (2) iletilir ((m1), kapandı).
  `mutationApplied = null` + `uncertainty = write_concern` → bilinen olay adı + `Unconfirmed` (4) iletilir (S-3).
  `mutationApplied = null` + `uncertainty = transport` → iletilmez (S-3). `mutationApplied = false` ya da `NotApplied` → iletilmez.
  İletim hatası komutu bozmaz. Sabotaj: iletim deneme satırından önceye alınınca kırmızı. `mutationApplied = false` sonucu iletilince
  kırmızı. Taşıma belirsizliği `Unconfirmed` ile iletilince kırmızı. Yazma onayı hatası iletilmeyince kırmızı.
- [ ] AC-8 (zorunlu) — **Mevcut `RecordAsync` anlamı korunur:** `IUserAuditRecorder.RecordAsync` ve `IRoleAuditRecorder.RecordAsync`
  eski imzaları ile `IRbacAuditRecorder.RecordAsync` istisna atmamaya devam eder. `CreateUser`, `DeleteUser`, `SetUserActiveStatus` ve
  e-imza işleyicilerinin mevcut testleri değişmeden yeşildir. Yeni `RecordAsync(…, outcome, …)` aşırı yüklemesi Backend File
  Convention "Sonuç aşırı yüklemesi" maddesindeki beş farkla çalışır. Her başarılı komut çalışmasında `authAuditLogs`'a tam **iki**
  satır düşer: deneme ve sonuç. Bugünkü yerel satır ayrıca yazılmaz. Gerçek Mongo testi satırları sayar. Sabotaj: aşırı yükleme
  yalnız sonuç satırı yazıp iletimi atlayınca AC-7 testi kırmızıya döner. Aşırı yükleme bugünkü yerel satırı da yazınca sayım testi
  üç satır bulur ve kırmızıya döner. `RbacAuditRecorder` istisna yükseltince `CreateUser` testi kırmızıya döner.
- [ ] AC-9 (zorunlu) — **Defter, DL-1 — değişmez:** `AssignRoleCommand` ve `RevokeRoleCommand` `## K2 borcu`'nda kalır.
  `PinnedCounts` Auth (`Debt 18, Exceptions 1, K2Debt 11`) DL-1'de değişmez. Erken çıkış yoktur (Açık kararlar (n), kapandı). İki komut `auth-kullanici-iletimi` ile "denetleniyor" ölçülmeye devam eder.
  `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` yeşildir.
  `git diff <taban>..HEAD -- tests/architecture/audit-ledger/` boştur (`<taban>` açıkça yazılır). Sabotaj: işleyicide
  `_userAudit.RecordAsync(` çağrısı kaldırılınca komut denetlenmiyor olur ve `K2Debt_NamesAuditedCommands_AndOnlyShrinks` kırmızıya
  döner ("denetlenmiyor; yeri Bilinen borç"). K2 satırı çıkarılıp sabit sayı bırakılınca `DebtAndExceptionCounts_ArePinned_…`
  kırmızıya döner. Satır ve sayı birlikte değiştirilirse bunu mekanik test yakalamaz. Bu bir **inceleme kapısıdır** (§9): CT kararı
  yoksa reddedilir.
- [ ] AC-10 (zorunlu) — **Hata metni 7 dil (kullanıcı-rol ekranı):** `RBAC_AUDIT_UNAVAILABLE` `UserRoleAssignments` ekranında kaynak
  anahtarına eşlenir. Anahtar 7 resx dosyasında vardır ve İngilizce dışı değer `en` ile aynı değildir. `index.l10n.js` anahtar listesi
  eksiksizdir. Sabotaj: bir dil dosyasından anahtar silinince `Diten.Web.Tests` kırmızıya döner.
- [ ] AC-11 (zorunlu) — **Kiracı ve içerik:** Deneme ve sonuç satırının `TenantId` değeri token kiracısıdır, `UserId` eylemi
  yapandır. `Metadata` yalnız izinli anahtarları taşır (izin listesi testte sabit). E-posta, ad, token yoktur. Sabotaj: `Metadata`'ya
  `email` eklenince izin listesi testi kırmızıya döner. Kiracı istekten alınınca A / B kiracı testi kırmızıya döner.
- [ ] AC-12 (zorunlu) — **Önce-yaz ve kapalı-başarısız (beş rol komutu):** `CreateRole` (`CreateAsync` öncesi), `UpdateRole`
  (`UpdateAsync` öncesi), `DeleteRole` (`DeleteAsync` öncesi), `AssignPermission` (`held` denetiminden sonra, `AssignAsync` öncesi) ve
  `RevokePermission` (S-GUARD ve son-yetkili korumasından sonra, `RevokeAsync` öncesi) için AC-1, AC-2, AC-4, AC-6 ve AC-7
  parametreli tek testle sınanır. Rol tarafında iletim `EntityType Role` taşır. **CreateRole kimliği:** Deneme satırının `EntityId`
  değeri, oluşacak rolün kimliğidir ve `Guid.Empty` değildir. Kimlik `CreateAsync`'ten önce bellidir (yeniden ölçüldü
  2026-11-06): `Role` `EntityBase`'ten türer (`Role.cs:3`), `EntityBase` de `GlobalEntityBase`'ten (`EntityBase.cs:3`). `Id`
  başlatıcısı `Guid.NewGuid()` tabandadır (`GlobalEntityBase.cs:5`) ve `new Role(...)` (`CreateRoleCommandHandler.cs:46-49`) onu kurar.
  `RoleRepository.CreateAsync` taban `InsertOneAsync`'i çağırır ve aynı nesneyi döner (`RoleRepository.cs:47-51`,
  `RepositoryBase.cs:38-41`). Auth'ta Mongo kimlik üreteci ya da `MapIdMember` tanımı yoktur (`grep -rn "IdGenerator\|MapIdMember"
  services/Diten.AuthService/src` → 0); sürücü dolu bir `Guid`'i değiştirmez. Bu değişirse kimlik deneme satırından önce üretilip
  role verilir. Test: deneme satırının `EntityId`'si = sonuç satırının `EntityId`'si = dönen `RoleDto.Id`, ve üçü de boş değil.
  Sabotaj: herhangi bir işleyicide deneme çağrısı kaldırılınca ya da sonraya alınınca o işleyicinin satırı kırmızıya döner.
  CreateRole denemesi `Guid.Empty` ile ya da rolün kimliği olmayan ayrı bir `Guid.NewGuid()` ile yazılınca kimlik eşitliği testi
  kırmızıya döner.
- [ ] AC-13 (zorunlu) — **Yarış dalı (AssignPermission):** `DuplicateRolePermissionException` gelince 409
  `ROLE_PERMISSION_ALREADY_GRANTED` döner. Sonuç satırı `NotApplied` olur. Sürüm artmaz, iletim yapılmaz. Sabotaj: dal sonuç yazmadan
  dönünce geçmiş testi "Sonuçlanmadı" bulur ve kırmızıya döner.
- [ ] AC-14 (zorunlu) — **Değişiklikten sonraki hata (beş rol komutu):** `RevokePermission`'da bir sahibin refresh token iptali
  istisna atınca sonuç `Failed`, `stage = sessions`, `mutationApplied = true` olur ve istisna yükselir. Beş rol komutunun hepsinde
  parametreli testle şunlar sınanır. (1) Sürüm artırma hatası: `stage = version`, `mutationApplied = true`. (2) Değişiklik
  çağrısının (`CreateAsync`, `UpdateAsync`, `DeleteAsync`, `AssignAsync`, `RevokeAsync`) üç hata sınıfı: kesin → `false`; yazma onayı
  → `null` + `write_concern`; taşıma → `null` + `transport`. İstisna biçimleri AC-5 ile aynıdır. Beş rol değişikliği tek belgelidir
  (`InsertOneAsync`, `ReplaceOneAsync`, `UpdateOneAsync`, `DeleteOneAsync`; Entity Fields). `RevokePermission`'ın `RevokeAsync`'i
  silmeden önce okur (`RolePermissionRepository.cs:81-90`); okuma hatası yazma hatası değildir ve `transport` sınıflanır.
  (4) **`UpdateRole` sonrası okuma (bulgu F-19):** `GetPermissionsByRoleAsync` istisna atınca sonuç satırı `Succeeded` kalır, iletim
  `Succeeded` ile yapılmıştır ve istisna bugünkü gibi yükselir. Sabotaj: okuma sonuç satırından önceye alınınca bu test sonuç satırını
  `Failed` bulur ya da hiç bulmaz ve kırmızıya döner. (3) İptal: istek belirteci
  değişiklikten sonra iptal edilir ve sonuç satırı yine yazılır. Sonuç yazıcısına ve ileticiye gelen belirteç istek belirteci
  değildir ve iptal edilmemiştir. İletim AC-7 kuralına uyar. Sabotaj: AC-5 ile aynı. Beş işleyicinin her biri parametreli testte
  ayrı satırdır; biri bozulunca yalnız onun satırı kırmızıya döner.
- [ ] AC-15 (zorunlu) — **Güncellemenin önce / sonrası:** `UpdateRole` deneme satırı istenen `after` değerini, sonuç satırı
  `before` / `after` değerini taşır. Platform'a giden `BeforeState` / `AfterState` bugünkü gibidir (`RoleAuditRecorder.cs:45-65`).
  Sabotaj: `before` yakalaması değişiklikten sonraya alınınca test kırmızıya döner.
- [ ] AC-16 (zorunlu) — **Rol geçmişi ucu (a1):** `GET /api/roles/{id}/history` yalnız o rolün (`EntityType Role`, `EntityId`) ve
  token kiracısının satırlarını döner. Satırlar en yeniden eskiye sıralanır ve deneme + sonuç tek öğede eşlenir. Sonucu olmayan deneme
  `outcome = unknown` döner. **Sonuç değerleri (DL-2'de sabittir):** `succeeded` ← `Succeeded`; `failed` ← `Failed` +
  `mutationApplied = false`; `applied_with_errors` ← `Failed` + `mutationApplied = true`; `not_applied` ← `NotApplied`; `unknown` ←
  sonuç yok ya da `mutationApplied = null`. İzinsiz 403. Başka kiracının rolü 404. Kiracıda hiç olmamış rol 404. **Silinmiş rol 200 döner** ve
  `role_created` … `role_deleted` çiftleri görünür. Bunu `IRoleRepository.GetByIdAndTenantIncludingDeletedAsync` sağlar (Açık
  kararlar (k), kapandı). **Sayfalama birimi öğedir** (bir deneme + eşlenen sonucu; Backend File Convention "Okuyucu"). Örnek:
  21 deneme ve 21 sonuç (42 satır) taşıyan rolde varsayılan istek 20 öğe döner ve `TotalCount = 21` olur; `page = 2` 1 öğe döner.
  İki sayfa ortak öğe taşımaz. Aynı `OccurredAt`'ı taşıyan iki denemede sıra `Id` azalandır ve testte sabittir. `pageSize = 101` →
  400; `page = 0` → 400. Denemesi olmayan `stage = attempt` sonuç satırı hiçbir öğede görünmez ve `TotalCount`'a girmez. FU29
  öncesi biçimde (`EntityType` yok) yazılmış bir satır dönmez. Uç gerçek denetleyiciyle HTTP testinde sınanır. Sabotaj: kiracı
  süzgeci kaldırılınca başka kiracı testi kırmızıya döner. Eşleme kaldırılınca `unknown` testi kırmızıya döner. Sayfalama sonuç
  satırları üzerinden yapılınca `TotalCount = 21` testi 42 bulur ve kırmızıya döner. `Id` eşitlik bozucusu kaldırılınca sabit sıra
  testi kırmızıya döner. Uç silinmişi süzen `GetByIdAndTenantAsync`'e döndürülünce silinmiş rol testi 404 alır ve kırmızıya döner.
  Yeni üyede `TenantId` süzgeci kaldırılınca başka kiracının silinmiş rolü testi kırmızıya döner. Okuyucu `mutationApplied`'ı yok
  sayınca `applied_with_errors` ve `null` → `unknown` satırları kırmızıya döner. **Sonuç süzgeci (bulgu F-7):** test, A kiracısının
  denemesi için türetilmiş kimliği taşıyan ama `TenantId = B` olan bir sonuç satırı tohumlar (ve ayrı bir testte aynı kiracıda farklı
  `EntityId`'li bir satır). Beklenen: deneme `unknown` döner. Sabotaj: adım (2)'den `TenantId` ya da `EntityType + EntityId` süzgeci
  kaldırılınca okuyucu yabancı sonucu eşler ve test kırmızıya döner.
- [ ] AC-17 (zorunlu) — **Kişi rol geçmişi ucu (a1):** `GET /api/users/{id}/role-history` yalnız o kullanıcının
  (`EntityType User`) rol atama / geri alma olaylarını döner (aile: `user_role_assigned`, `user_role_removed`). Kiracı, izin ve
  sayfalama AC-16 gibidir. **404 durumları:** kiracıda olmayan kullanıcı, başka kiracının kullanıcısı, **silinmiş kullanıcı** (S-4;
  `UserRepository.cs:44-49`) ve çağırana gizli bir platform hesabı (D'; `PlatformAccountScopedUserRepository.cs:49-53`). Silinmiş
  kullanıcı testi kullanıcıyı `SoftDeleteAsync` ile siler ve 404 bekler. Bu bilinen boşluk AC-26 raporunda da yazılır. Sabotaj:
  testte aynı `EntityType User` / `EntityId` ile başka aileden bir deneme satırı (`user_updated:attempt`) tohumlanır. Olay ailesi
  süzgeci kaldırılınca bu satır döner ve test kırmızıya döner. Uç D' sarmalayıcısı yerine iç `UserRepository`'ye bağlanınca platform
  hesabı testi 200 alır ve kırmızıya döner.
- [ ] AC-18 (zorunlu) — **Meta-denetim teslimi (a1):** Her geçmiş okuması `IsMetaAudit = true` okuma satırını
  `TryForwardAsync` ile gönderir. Veri yalnız teslim teyidi gelince döner. Gerçek `PlatformAuditForwarder`, Platform iç ucunun
  sözleşmesine uyan saplama sunucuya karşı koşar: (i) 2xx → 200 + veri; (ii) anahtar boş → 503 `RBAC_HISTORY_AUDIT_UNAVAILABLE` ve
  saplamaya çağrı yok; (iii) 500 / 401 → 503; (iv) bağlantı reddi / zaman aşımı → 503. Bu madde FU27 AC-46 (ct-06) desenidir
  (ct-06). **Yük (bulgu F-5; S-6):** saplama sunucusu gelen gövdede `Category = 2`, `Operation = 15`, `Outcome = 1`, `EntityType`
  (`Role` / `User`), `EntityId` (okunan varlık), `RequestType` (iki okuma adından biri) ve `IsMetaAudit = true` olduğunu doğrular.
  Saplama Platform'un `ValidateRequest` kurallarını (`AuditService.cs:205-263`) uygular ve `Unknown` değerde 400 döner. Sabotaj:
  teslim teyidi yok sayılınca (iii) ve (iv) kırmızıya döner. `Operation` ya da `Category` 0 gönderilince saplama 400 döner, uç 503
  alır ve (i) kırmızıya döner. `IsMetaAudit` gönderilmeyince yük testi kırmızıya döner.
- [ ] AC-19 (zorunlu) — **Okuyucu salt-okunur (a1):** `IRbacAuditHistoryReader` ve `IRbacWriteAheadAuditWriter` arayüzlerinde
  `Update` / `Delete` / `Replace` adlı üye yoktur (yansıma testi; **mekanik**). Sabotaj: arayüze `DeleteAsync` eklenince yansıma
  testi kırmızıya döner. "Hiçbir uç geçmiş satırını değiştirmez" cümlesi ise **inceleme kapısıdır**, mekanik değildir. Başka bir
  sınıfın `authAuditLogs`'a güncelleme yazmasını bu test görmez. İnceleme, teslim farkında `authAuditLogs` koleksiyonuna
  `Update*` / `Replace*` / `Delete*` çağrısı eklenmediğini `grep` ile gösterir ve komutu teslim kaydına yazar.
- [ ] AC-20 (zorunlu, DL-2) — **Hata metni 7 dil (Roller + rol-izin ekranı):** `RBAC_AUDIT_UNAVAILABLE` iki ekranın
  `ERROR_CODE_KEYS` listesinde (`Roles/index.js:41`, `RoleAssignments/index.js:106`) bir kaynak anahtarına eşlenir. Anahtar iki ekranın
  7 resx dosyasında vardır ve İngilizce dışı değer `en` ile aynı değildir. Geçmiş bölümünün hata kodları
  (`RBAC_HISTORY_AUDIT_UNAVAILABLE`, `RBAC_HISTORY_PAGING_INVALID`) bölümle birlikte DL-3'tedir (AC-31). Sabotaj: AC-10 ile aynı.
- [ ] AC-21 (zorunlu) — **DL-2'de defter değişmez:** Beş rol komutu `## Bilinen borç`'ta kalır ve `AuditTrailStandard` yeşildir
  (iz kabul edilmediği için "denetlenmiyor" ölçülürler). `git diff <taban>..HEAD -- tests/architecture/audit-ledger/` boştur.
  Sabotaj: DL-2'de bir borç satırı çıkarılınca iki test kırmızıya döner. `EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`
  kırmızı olur, çünkü komut denetlenmiyor ve artık listede de yok. `DebtAndExceptionCounts_ArePinned_AddingDebtNeedsAControlTowerDecision`
  kırmızı olur, çünkü defter 17, sabit sayı 18. `KnownDebt_OnlyShrinks_AnEntryThatIsAuditedOrGone_MustBeRemoved` bu sabotajı
  yakalamaz: o test yalnız listede duran ama denetlenen ya da artık olmayan girdiye bakar.
- [ ] AC-22 (zorunlu) — **İz kabulü ve borç kapanışı (a1, DL-3):** `## İzler`'e
  `auth-rbac-gecmisi | c | yazıcı | IRoleAuditRecorder.RecordAttemptAsync/IUserAuditRecorder.RecordAttemptAsync` satırı ve beş koşulun
  kanıt notu girer. `AcceptedTrails`'e aynı üçlü eklenir. `## K2 borcu`'ndan `AssignRoleCommand` ve `RevokeRoleCommand` çıkar.
  `## Bilinen borç`'tan beş rol komutu birlikte çıkar: `AssignPermissionCommand`, `RevokePermissionCommand`, `CreateRoleCommand`,
  `UpdateRoleCommand`, `DeleteRoleCommand`.
  **Yedek yok.** İz `IRoleAuditRecorder.RecordAttemptAsync` belirteciyle kabul edilince `DeleteRoleCommandHandler` da ölçümde
  "denetleniyor" sayılır (`AuditTrailMeasurement.cs:277-279`). Böyle bir komut borçta bırakılırsa
  `KnownDebt_OnlyShrinks_AnEntryThatIsAuditedOrGone_MustBeRemoved` "ARTIK DENETLENİYOR" der ve kırmızıya döner
  (`AuditTrailStandardTests.cs:230-233`). Bu yüzden DeleteRole'ü borçta tutmak mekanik olarak mümkün değildir.
  **DL-3 giriş şartı:** Açık kararlar (k) iki parçasıyla kapandı (bulur + (k-1), sahip 2026-11-05, onayla). Kalan şart ölçümdür:
  AC-16'nın silinmiş rol testi ve AC-33 (silinmiş roller ucu) DL-2'de yeşil olmalıdır. Bu şart yoksa DL-3 başlamaz. CT defter
  kararı (`CT-DECISIONS.md`) silinmiş kullanıcı boşluğunu (S-4) yol `c` koşul 3'ün istisnası olarak adıyla yazar.
  `PinnedCounts` Auth `Debt` ve `K2Debt` aynı değişiklikte ölçülen değere iner. Beklenen değerler hesaptır ve teslimde ölçülür:
  `Debt 18 → 13`, `K2Debt 11 → 9`, `Exceptions 1`. `CT-DECISIONS.md` satırı yazılır. `AuditTrailStandard`
  yeşildir ve `grep -E '^\+- '` boştur. Sabotaj: `AcceptedTrails` üçlüsü kaldırılınca beş rol komutu "yeni denetimsiz" olur ve
  kırmızıya döner. Bir rol işleyicisinden `RecordAttemptAsync` çağrısı kaldırılınca o komut kırmızıya döner. Sayılardan biri eski
  değerinde bırakılınca `DebtAndExceptionCounts_ArePinned_…` kırmızıya döner.
- [ ] AC-23 (zorunlu) — **K2 sınıfı tutarlılığı:** K2 çıkışı DL-3'tedir. DL-1 ve DL-2 birleştikten sonra iki kişi-rol komutu
  `## K2 borcu`'nda, beş rol komutu `## Bilinen borç`'ta kalır; davranışları ise kapalı-başarısızdır (AC-2, AC-12). DL-3 birleştiğinde
  yedi komutun hiçbiri iki listede değildir, yedisi de kabul edilmiş ve okunabilen `auth-rbac-gecmisi` izine yazar ve AC-2 ile
  AC-12'nin testlerini taşır. `## K2 borcu` sayısı hiçbir dilimde büyümez. DL-1'de erken çıkış yoktur (Açık kararlar (n), kapandı).
  **Koruma türü:** Bu madde **inceleme kapısıdır**, mekanik değildir. DL-3'ün DL-1 ve DL-2'den önce birleşmesini hiçbir test
  engellemez. Rol komutlarına kredi DL-2 kapalı-başarısız davranışı olmadan verilirse AC-12 testi yoktur ve kırmızı da olmaz.
  Kapı şudur: CT, DL-3 birleşmesinden önce DL-1 ve DL-2'nin main'de olduğunu (`git merge-base --is-ancestor <DL-2 commit> HEAD`
  → 0) ve AC-2 / AC-12 testlerinin dosya adlarını teslim kaydına yazar (Teslim dilimleri sırası).
- [ ] AC-24 (zorunlu) — **Roller ekranı "Geçmiş" (a1):** `auth.roles.read` iznine sahip kullanıcı ((f), kapandı) hızlı görünümde
  rolün geçmişini görür. Bölüm kendi kapısını taşımaz. Sayfa kapısının (`Roles/Index.cshtml:21`, `Perms.Has("auth.roles.read")`)
  içinde çizilir (S-1; Frontend File Contract "İzin kapısı"). İzni olmayana sayfa `_AccessDenied` çizer. Bölüm, iskelet ve
  yönlendirme yoktur (UAS-001). Yazma düğmesi yoktur. Ham olay kodu görünmez. `Roles/Index.cshtml:8`
  `Layout = "_LayoutTenantShell";` aynen kalır ve kısmi görünüm `Layout` atamaz (ön yüz testi dosyayı okur). Bütün metin
  `RolesIndex.{en,tr,fr,es,zh,ar,ru}.resx` dosyalarındadır ve İngilizce dışı değer `en` ile aynı değildir. Sabotaj: (1) Sayfa
  kapısı kaldırılınca izinsiz kullanıcı testi bölümün işaretini (`data-rbac-history`) bulur ve kırmızıya döner. (2) Auth ucundaki
  `[HasPermission("auth.roles.read")]` kaldırılınca AC-16'nın 403 testi kırmızıya döner. (3) Web vekili Auth'un 403 cevabında
  `success: true` ya da veri dönerse, ya da `errorCodes`'u düşürürse `RbacHistoryProxyTests` kırmızıya döner (Repo Scope DL-3 "Vekil
  deseni": Web cevabının HTTP durumu Auth'un durumunu taşımaz, kod ve `success` taşır). (4) Bir dil dosyasından anahtar silinince
  `Diten.Web.Tests` kırmızıya döner.
  Bölüme ikinci bir `if` yazılması koruma sayılmaz ve sabotajı da aranmaz (F-3).
- [ ] AC-25 (zorunlu) — **Kişi-rol ekranı "Rol geçmişi" (a1):** Bölüm kendi kapısını taşımaz. Sayfa kapısının içinde çizilir:
  `Diten.Web.Security.PermissionClaims.HasPermission(User, "auth.users.assign-role")` (`UserRoleAssignments/Index.cshtml:21`; (f),
  kapandı; S-1). `Perms.Has` kullanılmaz. İzni olmayana sayfa `_AccessDenied` çizer. Bölüm, iskelet ve yönlendirme yoktur.
  `Index.cshtml:7` düzeni aynen kalır. Metin `UserRoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` dosyalarındadır ve İngilizce
  dışı değer `en` ile aynı değildir. Ön yüz testi üç talep biçimiyle koşar: tam `permission` talebi, `permissions` talebi ve
  virgüllü liste. Üçünde de bölüm çizilir; izinsiz kullanıcıda çizilmez. Sabotaj: (1) Sayfa kapısı `Perms.Has(...)` ile
  değiştirilince `permissions` / virgüllü liste testleri bölümü bulamaz ve kırmızıya döner. (2) Sayfa kapısı kaldırılınca izinsiz
  kullanıcı testi bölümü bulur ve kırmızıya döner. (3) Auth ucundaki `[HasPermission("auth.users.assign-role")]` kaldırılınca AC-17'nin
  403 testi kırmızıya döner. (4) Web `RoleHistory` eyleminden `RefuseWithoutPermission()` kaldırılınca
  `Role_history_without_the_key_is_refused_with_403_and_never_reaches_the_gateway` (yeni eylemi doğrudan çağırır) 403 yerine Gateway
  çağrısı bulur (`Calls == 1`) ve kırmızıya döner. Eylem her isteği reddedecek biçimde bozulursa `Role_history_with_the_key_reaches_the_gateway`
  kırmızıya döner. Ortak taban teorisi (`:68-86`) bu sabotajı sınamaz ve genişletilmez (F-1). (5) Vekil başarısızlıkta veri döner ya da kodu düşürürse `RbacHistoryProxyTests` kırmızıya döner. (6) Bir dil
  dosyasından anahtar silinince `Diten.Web.Tests` kırmızıya döner.
- [ ] AC-26 (zorunlu) — **Çalıştırılmış kanıt (§8-(3)):** `dev` ortamında bir rol oluşturma, bir güncelleme ve bir silme koşulur.
  Üç deneme + sonuç çifti geçmiş ucundan gösterilir. Silme çifti, silinmiş rolün geçmişinden okunur. Rol Roller ekranındaki
  "Silinmiş roller" süzgeciyle bulunur (AC-16, AC-33, AC-35). Aynı üç olay Platform `audit_events` sorgusuyla
  (`EntityType: "Role"`, `EntityId`) gösterilir. Bir izin bağlama ve bir kişi-rol atama da gösterilir. Rapor ortamın adını yazar.
  §3'ün on alanı tek tek işaretlenir; eksik alan "bilinen boşluk" olarak yazılır. Örnekler: eylemi yapanın o andaki adı (Açık
  kararlar (j), kapandı) ve silinmiş kullanıcının rol geçmişi (S-4). Ortam S2S anahtarının dolu olduğunu (değerini değil) yazar.
  Sabotaj kanıtı bu maddede aranmaz: madde çalışma kanıtıdır, test değildir.
- [ ] AC-27 (zorunlu) — **Liste ekranı protokolü:** Roller `index.js` dokunuşundan sonra
  `python3 .antigravity/scripts/verify_datatable_page.py . --area Governance --module Roles --format gaps` çıktısı teslim kaydına
  yazılır. Sapma varsa numaralı liste sahibe sorulur ve cevap kayda geçer. **Koruma türü:** Bu madde **inceleme kapısıdır**,
  mekanik değildir. Çıktının kayda yazılmasını ve sahibe sorulmasını hiçbir test sınamaz; inceleme teslim kaydında çıktıyı ve
  cevabı arar.
- [ ] AC-28 (zorunlu) — **Sayfa yardımı:** İki ekranın yardımı vardır (ölçüldü @`e3dc475bd`: `PageHelpResource.en.resx` içinde
  `Help.ROLES.*` 13 anahtar ve `Help.USERROLES.*`; simgeler `PageHelpCatalog.cs:30`, `:32`). Yeni bölüm için iki ekrana birer adım
  eklenir: `Help.ROLES.Step5` / `Step5Title` ve `Help.USERROLES.Step5` / `Step5Title`, 7 dilde. Simge listesine birer `bx-history`
  eklenir; simge sayısı adım sayısına eşittir (`PageHelpCatalog.cs:34` yorumu). Yardım paneli en çok beş adım okur
  (`_PageHelpPanel.cshtml:38`, `Enumerable.Range(1, 5)`); `Step5` son boş yerdir, altıncı adım çizilmez. Sabotaj (testler Repo Scope DL-3 "Yardım testleri"; bulgu
  F-9): bir dil dosyasından `Step5` ya da `Step5Title` silinince, ya da `ROLES` / `USERROLES` simgesi eklenmeyince
  `GovernanceRbacHistoryHelpTests` kırmızıya döner. `PageHelpCatalogTests.cs:51` güncellenmeden simge eklenirse o test kırmızıya döner;
  bu bir sabotaj değil, aynı değişiklikte yapılması gereken güncellemedir.
- [ ] AC-29 (zorunlu) — **Yeni eylemi yapan koruması (RevokeRole, DL-1):** `RevokeRoleCommandHandler` `ICurrentUserAccessor` alır.
  `UserId` yoksa ilk adımda 401 `ROLE_ACTOR_REQUIRED` döner. Kullanıcı / rol araması, son-yetkili denetimi, deneme satırı,
  `RevokeAsync`, sürüm artırma ve `RevokeAllByUserAsync` **hiç çağrılmaz**. Aktör varsa bugünkü davranış ve cevaplar aynıdır
  (gerileme). Deneme satırının `UserId` değeri bu aktördür. Sabotaj: koruma kaldırılınca aktörsüz test 204 alır ya da deneme satırı
  `UserId = Guid.Empty` ile yazılır ve test kırmızıya döner.
- [ ] AC-30 (zorunlu) — **Yeni eylemi yapan koruması (RevokePermission, DL-2):** AC-29 aynı biçimde `RevokePermissionCommandHandler`
  için geçerlidir. Çağrılmayanlar: rol / hak araması, S-GUARD, son-yetkili denetimi, deneme satırı, `RevokeAsync`, sürüm artırma ve
  sahiplerin oturum kapatması. Etkisiz işlem (204, hedef hak yok) da aktörsüz istekte 401 döner, çünkü koruma ilk adımdır.
  Sabotaj: AC-29 ile aynı.
- [ ] AC-31 (zorunlu) — **Rozetler, aşama etiketleri ve geçmiş hata metinleri (a1, DL-3):** İki ekran AC-16'nın sabit sonuç
  değerlerini (DL-2) Frontend File Contract "Rozet seçimi"ne göre gösterir; `applied_with_errors` "Başarısız"dan ayrı rozet ve aşama
  etiketiyle görünür. `RBAC_HISTORY_AUDIT_UNAVAILABLE` ve `RBAC_HISTORY_PAGING_INVALID` geçmiş bölümünde bir kaynak anahtarına
  eşlenir. S2S anahtarı boşken (başlık "Dağıtım ön koşulu") bölüm 7 dilde "şu an gösterilemiyor" der ve satır iskeleti çizmez. Rozet,
  aşama ve hata metinleri "Metin tablosu"ndaki anahtarlarla `RolesIndex.*.resx` ve `UserRoleAssignmentsIndex.*.resx` dosyalarında 7
  dilde vardır ve İngilizce dışı değer `en` ile aynı değildir. Sabotaj: `rbac-history.js` `applied_with_errors`'u `failed` rozetine
  eşleyince `rbac-history.test.mjs` kırmızıya döner. Bir dil dosyasından rozet, aşama ya da hata anahtarı silinince `Diten.Web.Tests`
  kırmızıya döner.
- [ ] AC-32 (zorunlu) — **Eylemi yapan gösterimi (a1, DL-2; S-2):** Geçmiş cevabında eylemi yapan yalnız D' sarmalayıcısı
  (`IUserRepository`) ile çözülür. Test üç satır tohumlar: biri görünen kiracı kullanıcısının, biri platform hesabının
  (`IsPlatformAccount = true`), biri bilinmeyen bir kimliğin. Çağıran gizli kiracı aktörüdür. Beklenen: ilk satırda kimlik + ad
  vardır. Öteki ikisinde `actor = { hidden: true }` vardır; cevabın hiçbir yerinde platform hesabının kimliği ya da adı geçmez (JSON
  metni üzerinde arama). İki gizli satır birbirinden ayırt edilemez. Sabotaj: okuyucu iç `UserRepository`'ye bağlanınca platform
  hesabı testi adı bulur ve kırmızıya döner. Gizli satıra ham `UserId` konunca JSON araması kırmızıya döner.
- [ ] AC-33 (zorunlu) — **Silinmiş roller ucu ((k-1), DL-2):** `GET /api/roles/deleted` yalnız token kiracısının silinmiş rollerini
  döner: `DeletedAt` azalan, eşitlikte `Id` azalan, en çok 200, `truncated` bayrağı. Silinmemiş rol dönmez. Başka kiracının silinmiş
  rolü dönmez. İzinsiz 403 (`auth.roles.read`). 201 silinmiş rolde 200 öğe + `truncated = true` döner. Uç gerçek denetleyiciyle HTTP
  testinde sınanır. Sabotaj: `IsDeleted = true` süzgeci kaldırılınca canlı rol testi kırmızıya döner. Kiracı süzgeci kaldırılınca
  başka kiracı testi kırmızıya döner. Üst sınır kaldırılınca `truncated` testi kırmızıya döner.
- [ ] AC-34 (zorunlu) — **Yol `c` belirtecinin çağıranları sabittir (DL-3; F-16):** `RbacWriteAheadCallerPinTests` üretim kaynağını
  (`services/Diten.AuthService/src`) tarar. `IRoleAuditRecorder.RecordAttemptAsync` ya da `IUserAuditRecorder.RecordAttemptAsync`
  çağıran işleyici kümesi tam olarak yedi işleyicidir. Tarama, ölçümün kullandığı aynı biçimdir: türü bildirilmiş alan +
  `ad.RecordAttemptAsync(` (`AuditTrailMeasurement.cs:361-375`). Sekizinci bir çağıran (ör. FU-0022 e-imza işleyicisi) testi
  kırmızıya döndürür. Liste ancak paket revizyonu + `CT-DECISIONS.md` satırıyla değişir. Sabotaj: test kümesine yedi işleyicinin
  dışından bir işleyici eklenince ya da bir e-imza işleyicisine `RecordAttemptAsync` çağrısı eklenince test kırmızıya döner.
- [ ] AC-35 (zorunlu) — **Silinmiş roller süzgeci, kapsam notu, RTL ve FG-003 (DL-3):** Roller ekranında "Silinmiş roller" seçeneği
  açılınca liste `GET /api/roles/deleted`'dan yüklenir. Satırda yalnız "Geçmiş" eylemi vardır: düzenle, sil ve izin düğmesi yoktur.
  "Geçmiş" AC-16'nın silinmiş rol cevabını gösterir. `truncated` ise 7 dilde "en yeni 200 gösteriliyor" yazar. Geçmiş bölümünün
  başında kapsam notu durur (Frontend File Contract). Kısmi görünümlerde ve yeni JS'in ürettiği HTML'de satır içi `style=` yoktur.
  Fiziksel yön sınıfı (`ml-`, `mr-`, `pl-`, `pr-`, `text-left`, `text-right`) da yoktur. `ar` kültüründe sayfa `dir="rtl"` ile çizilir
  ve bölüm mantıksal sınıfları taşır. Sabotaj: silinmiş satıra düzenle düğmesi eklenince ön yüz testi kırmızıya döner. Kısmi görünüme
  `style="margin-left:…"` eklenince FG-003 / RTL testi kırmızıya döner. Kapsam notu anahtarı bir dilden silinince eşlik testi
  kırmızıya döner.

## Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Birim — sıra | Yedi işleyici: korumalar → deneme → değişiklik → sürüm → (oturum) → sonuç → iletim. Sahte depolar çağrı sırasını kaydeder (AC-1, AC-12) |
| Birim — kapalı-başarısız | Deneme hatası enjeksiyonu; değişiklik / sürüm / oturum / iletim çağrı sayısı 0 (AC-2, AC-12) |
| Birim — sonuç | Başarı, `Failed` + aşama + `mutationApplied`, `NotApplied` (yarış ve `stage = attempt`), sonuç yazım hatası → bugünkü başarı kodu (201 / 200 / 204) + `LogError`; aşamaya ve `uncertainty`'ye göre iletim kuralı; beş rol komutunda üç hata sınıfı ve iptal (AC-2, AC-4, AC-5, AC-7, AC-13, AC-14) |
| Birim — sınıflandırıcı | `RbacMutationFailureClassifierTests`: her istisna biçimi bir satır; bilinmeyen istisna asla `false` değil (AC-5) |
| Birim — çağıran sabit listesi | `RbacWriteAheadCallerPinTests` (AC-34) |
| Birim — yeni aktör koruması | İki revoke: aktörsüz istek → 401, hiçbir depo / deneme / oturum çağrısı yok; aktörlü istek gerileme (AC-29, AC-30) |
| Birim — kimlik | Sabit girdi → sabit `OutcomeId`; farklı deneme → farklı kimlik; `Guid.Empty` reddi (AC-3) |
| Gerileme | `RoleAuditRecorderTests`, `AssignRoleCommandHandlerTests`, `RevokeRoleCommandHandlerTests`, `AssignPermissionCommandHandlerTests`, `RevokePermissionCommandHandlerTests`, `LastStewardPathsTests`, `RoleActorStampTests`, `SystemGrantPathsActorGuardTests`, `UserAuditEventLabelGuardTests` ve e-imza / kullanıcı yaşam döngüsü testleri yeşil (AC-8) |
| Gerçek Mongo | Mevcut `Support/OwnedMongo.cs` ile: test kendi `mongod`'unu başlatır (`DITEN_TEST_MONGOD` / `DITEN_ITEST_MONGOD_BIN_DIR`), 27017'yi reddeder (`OwnedMongo.cs:29`), sabit veritabanı `diten_auth_service_client_registry_itest` (`:21`). Tek sonuç satırı; komut başına iki satır; deneme hatasında `user_roles` / `role_permissions` / `roles` değişmez; `EntityType` / `EntityId` dizini kurulur; FU29 öncesi biçimde satır süzgece girmez (AC-2, AC-3, AC-8, AC-16) |
| HTTP (a1) | Gerçek denetleyici: izinsiz 403, başka kiracı 404, olmayan 404, silinmiş rol 200, silinmiş kullanıcı 404, gizli platform hesabı 404, sayfalama birimi ve 400, meta-denetim teslimi (i)–(iv), eylemi yapan gizleme, silinmiş roller ucu (AC-16, AC-17, AC-18, AC-32, AC-33) |
| İnceleme kapıları (mekanik değil) | AC-9 son cümlesi, AC-19 ikinci yarısı, AC-23, AC-27. İnceleme teslim kaydında komutu ve çıktısını arar; sabotaj kanıtı aranmaz |
| Mimari | `--filter AuditTrailStandard` yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ \| grep -E '^\+- '` boş; sayılar aynı değişiklikte (AC-9, AC-21, AC-22) |
| Ön yüz | `Diten.Web.Tests` (dosyalar Repo Scope DL-1 / DL-2 / DL-3): üç ekranda hata kodu eşlemesi; bölümün sayfa kapısıyla çizilmesi; kullanıcı-rol vekilinin kendi kapısı (izinsiz → 403, Gateway çağrısı 0); vekilin kodu taşıması ve başarısızlıkta veri dönmemesi; 7 dil resx eşliği (İngilizce dışı ≠ en; metin tablosu anahtarları); `index.l10n.js` listesi; düzen satırları; `style=` ve fiziksel yön sınıfı yokluğu; `rbac-history.js` birim testi; sayfa yardımı eşliği (`GovernanceRbacHistoryHelpTests`, `PageHelpCatalogTests.cs:51` güncellemesi); `role-history` vekil eylemini doğrudan çağıran iki test (AC-10, AC-20, AC-24, AC-25, AC-28, AC-31, AC-35) |
| Derleme / takımlar | Odaklı Auth testleri, tam AuthService takımı, Auth API Release derlemesi, Web derlemesi ve odaklı `Diten.Web.Tests`. Gerçek sayılar ve önceden bilinen kırmızılar taban karşılaştırmasıyla yazılır |
| Çalışma kanıtı | `dev` ortamında AC-26 |

Kural üretim koduyla ölçülür: testler gerçek işleyicileri, gerçek kaydedicileri ve gerçek `RbacWriteAheadAuditWriter`'ı çağırır.
Sahte nesne yalnız depo / iletici sınırındadır. Sabotaj kanıtı teslim kaydında "kaldır → kırmızı → geri koy → yeşil" biçiminde
gösterilir. Gösterilemeyen sabotajın koruması koruma sayılmaz.

## Page Help (UXH-001)

Yeni sayfa yok. **Düzeltilmiş ölçüm (@`e3dc475bd`):** Önceki sürüm yardımı yanlış dosyada aramıştı (`SharedResource.en.resx`).
Yardım metni `PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` dosyalarındadır. Kodlar `ROLES` ve `USERROLES`'tür (`USERROLEASSIGNMENTS`
değil). `PageHelpResource.en.resx` içinde `Help.ROLES.*` 13 anahtar var (`Purpose`, `Step1…4`, `Step1…4Title`, `Audience`,
`Immediate`, iki `Related`) ve `Help.USERROLES.*` da var. Simgeler `PageHelpCatalog.cs:30` (`ROLES`, 4) ve `:32` (`USERROLES`, 4)
satırlarındadır. Adımlar `_PageHelpPanel.cshtml:38-39` ile `Help.{CODE}.Step{n}` / `Step{n}Title` olarak, `n` = 1…5 okunur
(2026-11-06 ölçümü; yedi dilde `Help.ROLES.*` 13'er anahtar). `Step5` bu iki ekranın son boş adım yeridir. Bu yüzden AC-28
zorunludur: iki ekrana birer `Step5` adımı 7 dilde eklenir. Tur gerekmez: "kullanıcı + rol kurulumu" akışında yazma adımı değişmez,
yalnız salt-okunur bölüm eklenir.

## Ready-for-dev Checklist

Liste iki kapıya ayrılır (bulgu F-4). **A** maddeleri CT-APR-01 onay satırından önce kapanır; biri açıksa onay satırı yazılmaz.
**B** maddeleri onay satırını beklemez; her biri adı geçen dilimin başlamasını, kabulünü ya da dağıtımını tutar. B maddesi açıkken
onay satırı yazılabilir, ama o dilim ilerlemez.

**A — onay kapısı (CT-APR-01 satırından önce):**

- [ ] **Kod izni:** `status: ready-for-dev` yalnız inceleme içindir, kod izni değildir. Kod, CT-APR-01 onay satırı
  (`python3 scripts/pack_approval.py check <paket>` → 0) olmadan yazılmaz. Onay satırı yoksa kod yok.
- [ ] Golden Reference: `golden_reference: none` gerekçesi okundu; yeni dosyalar Backend File Convention'a uyuyor
- [ ] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`); `target` 2026-11-20 (Açık kararlar (g); karar kaydı 02, karar 7)
- [ ] Layout & Shell Contract: üç sayfanın `Layout = "_LayoutTenantShell";` satırları yazılı ve AC-24 / AC-25'te sınanıyor
- [ ] Validation Rules, Failure Path to Verify (en az 4 senaryo), Authorization Convention, Gateway kararı yazılı
- [ ] Audited Events tablosu yedi komutu tam bir kez içeriyor; K2 davranışı ve testi yazılı
- [ ] **Kayıt satırı (CT'nin işi):** `execution/registries/module-id-registry.md`'deki `MOD-0018-FU29` satırı main'de ve
  `verify_module_id.py --check-id` yeniden koşuldu. Ölçüm 2026-11-06: satır kule dalında commit'li (`25a03bebd`, `:92`), main'de
  (`6be9bc3d6`) yok
- [x] **Sahip kararı kaydı 01:** `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-01.md` main'de (PR #375, `68dcf9fa5`;
  Açık kararlar (l), U-5)
- [ ] **Sahip kararı kaydı 02 (CT'nin işi):** `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-02.md` main'de (karar 7:
  "onayla, hedef 11-20"). Ölçüm 2026-11-06: kule dalında `25a03bebd`, main'de yok
- [ ] **Sahip kararı kaydı 03 (CT'nin işi):** `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-03.md` main'de (karar 9:
  S-1…S-5 "Hepsini kabul et"). Ölçüm 2026-11-06: kule dalında `11fa9b94c`, main'de yok
- [x] **FU27 DL-5 temeli:** Açık kararlar (i) (i2) ile kapandı. Alanları ve dizini FU29 DL-1 ekler; FU27 metni Treasury CT'ce revize
  edilir (Dependencies)
- [x] Açık kararlar (a)–(p) kapandı (sahip 2026-11-05, "onayla"; karar kaydı 02, karar 7)
- [x] **Seçimler S-1…S-5** sahibe gösterildi ve kabul edildi (2026-11-06, karar kaydı 03, karar 9)
- [x] **Yeni seçimler S-6 ve S-7** (Sahip kararı gereken yeni seçimler, 2026-11-06 listesi) sahibe gösterildi ve kabul edildi
  (2026-11-06, karar kaydı 04, karar 11)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) **kod dosyalı ZIP'le** yapıldı (ZIP `--expand` ile kurulur) ve
  `execution/approvals/reviews.tsv` satırı var
- [ ] CT-APR-01 onay satırı (yüksek risk: yetki, denetim, ürün kodu yolu) — paketi yazmamış ve `high: evet` olan onaylayıcı

**B — teslim kapıları (onay satırını beklemez; dilim tutar):**

- [ ] **DL-1 dağıtımından önce — onaysız dağıtım reddi ölçüldü:** CT, onay satırı yokken `python3 scripts/prompt_gate.py check` ve
  `python3 scripts/pack_approval.py check <paket>`'in dağıtımı reddettiğini ölçer ve kaydeder (komut, çıkış kodu, ilgili satır).
  Künyeye `approval_status` alanı **eklenmez**: `prompt_gate` ikinci bir `approval_status` alanı `approved` değilse dağıtımı her zaman
  reddeder, ve onay hash'e bağlı olduğundan alanı sonradan değiştirmek onayı bozar
- [ ] **Her dilimin başında — ölçülmeyenler kapandı:** Implementation Notes "Ölçülmeyenler" tablosunda o dilime düşen her girdi, CT'nin
  numaralı ölçümüyle (`dosya:satır@commit`) kapatıldı
- [ ] **DL-2 meta-denetim parçasından önce — FU27 DL-5 ileticisi** (`TryForwardAsync` + `IsMetaAudit`) main'de; ya da 2026-11-11 tarih
  kapısı ölçümü CT'ce kaydedildi ve Repo Scope DL-2 "Koşullu (S-5)" maddesi uygulandı. Ölçüm 2026-11-06: `origin/main` (`6be9bc3d6`)
  Auth kaynağında `TryForwardAsync` / `IsMetaAudit` 0 eşleşme
- [ ] **DL-2 kabulünden önce — XMC-001 Platform isteği** (FU27 ile ortak) gönderildi; iki etiket ve `UserAuditEventLabelGuardTests`
  güncellemesi aynı değişiklikte main'de; Platform tarafı okuma satırı kabul testi (S-6 yükü) ve `Outcome = 4` kabul testi yazıldı
- [ ] **DL-2 / DL-3 dağıtımında — S2S anahtarı:** hedef ortamda Auth `PlatformService:InternalApiKey` ile Platform
  `AuthService:InternalApiKey` eşleşiyor ve bir geçmiş okuması 200 döndü (başlık "Dağıtım ön koşulu")
- [ ] **DL-3 başlamadan — giriş şartı:** AC-16 silinmiş rol testi ve AC-33 DL-2'de yeşil; CT defter kararı (`CT-DECISIONS.md`) DL-3'te
- [ ] **DL-3 kabulünde — sayfa yardım metni:** iki ekrana `Step5`, 7 dil (Page Help (UXH-001), AC-28); tur yok

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
   verilmiş izin her zaman kaldırılabiliyor. Kapalı-başarısız denetim bu niyetle çatışabilir; Açık kararlar (b) (b1) ile kapandı.
8. Platform `AuditOutcome` enum'unda `Unknown = 0` ve `Unconfirmed = 4` var (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:56-68`).
   `:63-67` açıklaması BL-501 içindir: veri işlendi ama komut sonunu teyit etmedi; kayıt tutulur, başarılı gösterilmez. Auth
   ileticisi `Outcome`'u `int` olarak değiştirmeden taşır (`IPlatformAuditForwarder.cs:15`; `PlatformAuditForwarder.cs:92`). Platform
   iç ucu bunu `AuditAppendRequest.Outcome` (`AuditAppendRequest.cs:18`, enum) olarak bağlar. Uçta sonuç denetimi yok
   (`InternalAuditController.cs:43-90`). **Kod okumasıyla ölçüldü (@`e3dc475bd`):** `AuditService.AppendAsync` önce
   `ValidateRequest`'i çağırır (`AuditService.cs:43-51`). Doğrulama sonuçta yalnız `Unknown`'u reddeder (`:247-250`, "Audit outcome
   is required."). `Unconfirmed = 4` reddedilmez. Platform kaynağında `Unconfirmed`'ı kullanan başka yer yok (`grep` yalnız
   `AuditEnums.cs:68`). Uçtan uca kabul **ölçülmedi**; testi Platform dosyasıdır ve XMC-001 isteğine yazılır (Dependencies). Bugün
   Auth yalnız 1 ve 2 gönderiyor (`UserAuditEvents.cs:49, :52`; `RoleAuditEvents.cs:34`).
   **Anlam ölçümü (bulgu F-9):** `Unconfirmed`'ın belgesi "verinin işlendiğini" söyler (`AuditEnums.cs:63-67`: "data committed").
   Taşıma belirsizliğinde (zaman aşımı, bağlantı) yazımın uygulandığı bilinmez. Bu yüzden o durumda 4 göndermek belgeyle çelişir.
   Yazma onayı hatasında ise yazım birincil sunucuda uygulanmıştır ve onayı eksiktir. Bu durum belgeyle örtüşür.
   **İletim kuralı (AC-7 ile aynı):**
   - Başarı → `Succeeded` (1).
   - `Failed` + `mutationApplied = true` → bilinen olay adı + `Failed` (2) ((m1), kapandı).
   - `null` + `write_concern` → `Unconfirmed` (4) (S-3).
   - `null` + `transport` → iletilmez (S-3; (m2) dalı).
   - `false` / `NotApplied` → iletilmez.
   **İletim sonuç satırının yazılmasına bağlı değildir (bulgu F-6).** Bugün iki kaydedici de yerel satır düşse bile Platform'a iletir:
   `UserAuditRecorder` yerel hatayı yakalar ve iletime devam eder (`UserAuditRecorder.cs:45-52`, `:62-79`); `RoleAuditRecorder`'da
   yerel hata `RbacAuditRecorder`'da yutulur (`RbacAuditRecorder.cs:30-41`) ve iletim `:53-72`'de koşar. Bu davranış korunur. Sonuç
   satırının yazımı istisna atsa, `false` dönse ya da karşılaştırma (Validation Rules) tutmasa da sonuç bellektedir ve yukarıdaki
   kurala göre iletilir. Platform'a gitmeyen tek durum, süreç değişiklikle iletim arasında durduğunda ortaya çıkar; o deneme Auth
   okuyucusunda "Sonuçlanmadı" görünür. Bu durum bugün de vardır (bugün yerel satır da yazılmaz).

**Ölçüm uyuşmazlıkları (paket metniyle düzeltilmedi, kaydedildi):**

- U-1: CT iletisinin andığı FU27 §5.3 / §8.5 / §14.3 / §21 / AC-44…AC-47 ve `auth-sod-muafiyet-gecmisi` main'deki FU27 paketinde
  (`ee1faf6d2` son değişiklik) **yok**. Bunlar yalnız `origin/chore/ct/treasury-intake-ct-06` @ `47e314f5c`'de var.
- U-2: FU27 DL-5 kodu hiçbir dalda yok. `AuthAuditLog`'da `EntityType` / `EntityId` yok.
- U-3: `audit-trail-standard.md:166-169` "`WriteOnceAsync` … bu dalda henüz yok" diyor. Main'de var (madde 6). Kural dosyası
  korumalı yoldur ve değiştirilmedi. Düzeltme sahip onayıyla ayrı yapılır.
- U-4: Sahibin "sonuç satırı `WriteOnceAsync` ile tekil" kararı ile FU27 D98'in "`IAuthAuditService` değişmez" kuralı (ct-06 §5.3)
  birlikte uygulanamaz, çünkü `WriteOnceAsync` imzası `EntityType` / `EntityId` taşımıyor (`IAuthAuditService.cs:18`). Açık kararlar
  (c) (c-i) ile kapandı: yeni yazıcı aynı anlamı uygular, `IAuthAuditService` değişmez.
- U-5: Sahip kararının (B / dar yol / Önerilen) depoda kaydı 2026-11-01 ölçümünde yoktu. **Kapandı:** kayıt 01 main'de (PR #375,
  `68dcf9fa5`). Kayıt 02 CT'nin işidir (başlık ön koşul 4).
- U-6: Kayıt satırı main'de yok ama doğrulayıcı `OK` veriyor (DCP-002 notu). Satır CT'nin değişikliğinde yazılıyor.
  `MOD-0018-FU28` bir dalda kayıt satırı olmadan kullanılıyor.
- U-7: İşleyici dosya adları Golden adlandırmasından sapıyor (Backend File Convention).
- U-8 (2026-11-05): Önceki sürüm sayfa yardımını `SharedResource.en.resx`'te aramış ve "yok" yazmıştı. Yardım `PageHelpResource.*.resx`
  dosyalarındadır (Page Help (UXH-001)). Önceki sürüm Mongo testi için `MONGO_TEST_URI` yazmıştı. `OwnedMongo.cs` bu değişkeni okumaz;
  kendi `mongod`'unu başlatır (Repo Scope DL-1). İkisi bu sürümde düzeltildi.
- U-10 (2026-11-06, inceleme 04 @`11fa9b94c` bulguları; yeniden ölçüm aynı HEAD): (a) AC-25 sabotaj 4 ortak taban teorisine
  dayanıyordu; teori `Revoke`'u çağırır (`UserRoleAssignmentsAccessTests.cs:42-43`, `:76`). Yerine yeni eylemi doğrudan çağıran iki
  test yazıldı (F-1). (b) Checklist onay ve teslim kapısı olarak ikiye ayrıldı (F-4). (c) Okuma satırının Platform değerleri ve
  kabulü yazıldı (F-5, S-6). (d) "Sonuç yoksa iletim yok" cümlesi bugünkü davranışı geriletiyordu; iletim sonuç yazımından bağımsız
  oldu ve AC-4'e test girdi (F-6). (e) Okuyucu sonuçları yalnız `_id` ile okuyordu; kiracı ve varlık süzgeci eklendi (F-7).
  (f) CreateRole kimliğinin dayanağı `Role : EntityBase : GlobalEntityBase` zinciriyle yeniden ölçüldü (F-8). (g) Yardım için ayrı test
  yazıldı; mevcut `PageHelpCatalogTests.cs:51` Step5'le kırmızıya dönüyordu ve kapsama girdi (F-9). (h) Karşılaştırmanın veri kaynağı
  `RbacAuditAttempt` oldu (F-10). (i) "Asla fırlatmaz" üretim sınıfına daraltıldı (F-11). (j) Çok belgeli silmede kesin hata dalı
  kaldırıldı (F-12, S-7). (k) Önceden iptal edilmiş istek yazıcıya bağlı olmaktan çıktı (F-13). (l) Kayıt 03 ile çelişen "kabul edilmiş
  sayılmaz" metinleri temizlendi (F-14). (m) Yük bloğu `:77-100`, ileticinin iki sahtesi ve Platform tarafı anahtar adı düzeltildi;
  anahtarın her ortamda zorunlu olduğu ölçüldü (F-15). (n) Metin tablosu ekranın bütün yeni metnine genişledi, `es` "Exitoso" oldu,
  etiket kapısının deseni ölçüldü (F-17). (o) `UpdateRole`'un sonuç sonrası okuması yazıldı (F-19). (p) `CentralAudit` alternatifi
  tartıldı (F-20). F-2 ve F-16 CT'nin işidir; F-18 kapsam dışıdır (Follow-up Items).
- U-9 (2026-11-06, yeniden ölçüm @`e3dc475bd`): 2026-11-05 revizyonundaki şu atıflar kodla uyuşmadı ve düzeltildi.
  (a) Ölçüm tabanı "yalnız üç dosya" diyordu; taranan yollarda 22 dosya değişti (anılan yok; başlık "Ölçüm tabanı").
  (b) `RecordingUserAudit`'in `SetUserActiveStatusCommandHandler`'a verildiği yazılmıştı (`UserAdminActionsTests.cs:127`). O satır
  `UserAuditForTests.None()` kullanır; sahte yalnız parola sıfırlama işleyicisine gider (`:237-238`, `:339-341`). Sonuç değişmedi:
  üç sahte de iki kişi-rol işleyicisini kurmaz.
  (c) "Web vekili izin denetimi yapmıyor" yalnız Roller için doğrudur. `UserRoleAssignmentsController` her yazmada izni ilk satırda
  denetler (`:57-70`). Önceki sürüm bu mekanik kapıyı F-3 çözümünde saymamıştı (Frontend File Contract "İzin kapısı" (3), AC-25).
  (d) "Vekil Auth'un durum kodunu taşır" ve AC-24'ün "403'ü 200'e çevirirse" sabotajı bugünkü desene uymuyordu. Governance vekilleri
  2xx dışında `Json(GatewayRefusal.ReadAsync(...))` döner, yani HTTP 200 + `success: false` + `errorCodes` (`GatewayRefusal.cs:90-123`,
  `RolesController.cs:65`, `:94`). Sabotaj, kod ve veri üzerinden yeniden yazıldı.
  (e) "Canlı rol listesinin üst sınırı 200 (`GetAllByTenantAsync`)" yazılmıştı. Sunucuda sınır yok (`RoleRepository.cs:37-45`); 200,
  ekranın istemci modu beyanıdır.
  (f) "Toplu arama üyesi yok (`IUserRepository.cs:7-87`)" eksikti: `VisibleUserIdsAsync` (`:84-87`) toplu üyedir ama ad döndürmez.
  (g) AC-6 ve Failure Path "rol yok → 404" diyordu. `RevokeRole` rol yoksa durmaz (`RevokeRoleCommandHandler.cs:58-61`) ve
  `RevokePermission` 204 döner (`:56`, `:63-66`). İki dal yazıldı. Ayrıca sistem rolü 403'leri (`UpdateRoleCommandHandler.cs:46`,
  `DeleteRoleCommandHandler.cs:48`) korumalar listesine eklendi.
  (h) Karar kayıtlarına "satır 7" diye atıf veriliyordu. Kayıt kararları `#` sütunuyla numaralar (karar 7 dosyanın 8. satırıdır); atıflar
  "karar n" oldu.
  (i) AC-20 (DL-2) DL-3'teki geçmiş bölümünü, AC-31 (DL-3) DL-2'deki okuyucu HTTP testini sınıyordu. Sonuç değerleri AC-16'ya (DL-2),
  geçmiş hata metinleri AC-31'e (DL-3) taşındı.
  (j) "Yeni üyeyi yalnız `GetRoleAuditHistoryHandler` çağırır" yazılmıştı. İki yeni depo üyesi ve iki okuma işleyicisi vardır.
  Yeniden ölçümde **doğru** çıkanlar: yedi işleyicinin bütün satır atıfları, iki kaydedici, `IRbacAuditRecorder`, `IAuthAuditService`,
  `AuthAuditService`, `AuthAuditLog`, `GlobalEntityBase`, `Role`, `IRoleRepository`, üç depo, `MongoDbIndexConfigurations`, iki DI dosyası,
  sürücü sürümü, `CustomBaseController`, Auth `UsersController` / `RolesController`, `ocelot.json`, `PlatformServiceOptions`, iki
  `appsettings`, ileticiler, Platform `AuditEnums` / `AuditAppendRequest` / `AuditService` / `InternalAuditController`, iki olay sabiti
  sınıfı, etiket kapısı testi, `AuditTrailMeasurement`, `AuditTrailStandardTests`, defter satırları, `audit-trail-standard.md` satır 150 ve
  `:166-169`, `ServiceClientRegistryReconciler`, `EntitlementReconciliationOperationStore.cs:166`, `OwnedMongo`, Mongo test deseni, iki
  `ct` testi, sahte sayıları (3 / 0 / 18), Web ekran ve JS satırları, resx sayıları (38 / 29 / 166 × 7), sayfa yardımı ve
  `MOD-0022-e-signature-service.md:1842`.

**Koşturulmayan:** Derleme, test, `Inventory_PrintsTheTable` envanter testi ve dev sunucusu koşturulmadı (görev kuralı). Defter
sayıları metin ölçümüdür, test çıktısı değildir.

**Ölçülmeyenler.** Bağımsız inceleme 03 (`e3dc475bd`) kod dosyalarını göremedi; ZIP `--expand` olmadan kurulmuştu (inceleme kaydı, CT
notu). Bir sonraki ZIP'i kod dosyalarıyla CT kurar. Her girdi, ilgili dilimin başında CT'nin numaralı ölçümüyle kapanır (Ready-for-dev
Checklist). Son sütun yazarın okumasını söyler; yazar okuması bağımsız ölçüm yerine geçmez.

| Girdi | İlgili dilim | Yazar okuması (2026-11-05; 2026-11-06 yeniden ölçüm, @`e3dc475bd`) |
|---|---|---|
| Web vekil deseni (`GatewayRefusal`, iki governance denetleyicisi) | DL-3 | okundu (2026-11-06): `GatewayRefusal.cs:90-123`, `UserRoleAssignmentsController.cs:47-110`, `RolesController.cs:1-40`, `:100-136`; `UserRoleAssignmentsAccessTests.cs:68-86` gövdesi okundu |
| FU27 paket metni (main sürümü) ve ct-06 §5.3 | DL-1 | okundu (2026-11-01): main 1-219 + grep, ct-06 433-542 |
| `InternalAuditController.cs:43-90` ve `AuditService.AppendAsync` (`Unconfirmed = 4` kabulü) | DL-1 (S-3) | okundu: `AuditService.cs:43-51`, `:205-263`; uçtan uca **ölçülmedi** |
| `UserRoleRepository`, `RolePermissionRepository`, `RoleRepository` (yazma / hata sınıfları) | DL-1 / DL-2 | okundu: `UserRoleRepository.cs:43-64`, `RolePermissionRepository.cs:43-108`, `RoleRepository.cs:15-20`, `:49`, `:73-75`, `:129-143` |
| `Role.cs`, `GlobalEntityBase.cs` (rol kimliği `CreateAsync`'ten önce) | DL-2 | okundu (`GlobalEntityBase.cs:5`, `Role.cs:7-15`) |
| `ExceptionHandlingBehavior`, `CustomBaseController` (503 / 401 eşlemesi) | DL-1 | okundu: `CustomBaseController.cs:9-22` (varsayılan dal 503'ü taşır), `ExceptionHandlingBehavior.cs` yalnız grep (kodlu yanıt deseni `:52-55`) |
| `IUserRepository`, `PlatformAccountScopedUserRepository`, `UserRepository` (404 / görünürlük) | DL-2 | okundu: `IUserRepository.cs:1-108` (2026-11-06; toplu üye yalnız `VisibleUserIdsAsync` `:84-87`), `PlatformAccountScopedUserRepository.cs:1-70`, `UserRepository.cs:44-52` |
| `Support/OwnedMongo.cs` (sahipli örnek) | DL-1 | okundu (grep): `:21`, `:29`, `:52-58` |
| Defter iz ayrıştırıcısı (`AuditTrailStandard/AuditLedger.cs`) — yeni iz satırının biçimi | DL-3 | **okunmadı**; defterdeki iz tablosu biçimi okundu (`Diten.AuthService.md:8-14`) |
| `scripts/prompt_gate.py` (onaysız dağıtım reddi) | DL-1 dağıtımı öncesi | kısmen (940-1009) |
| `frontend/Diten.Web.Tests` (izin kapısı, resx eşliği, düzen testleri) | DL-1 / DL-2 / DL-3 | dosya ve sınıf adları okundu (Repo Scope); `UserRoleAssignmentsAccessTests.cs:68-86` dışındaki test gövdeleri **okunmadı** (`MvpOnePageHelpTests.cs`, `HelpLabelTextTests.cs` dahil) |
| `PageHelpResource.*.resx`, `PageHelpCatalog.cs`, `_PageHelpPanel.cshtml` | DL-3 | okundu (2026-11-06): yedi dilde `Help.ROLES.*` 13, `PageHelpCatalog.cs:28-35`, `_PageHelpPanel.cshtml:37-41` |
| `MongoDB.Driver` 2.27.0'da yazma onayı hatasının istisna biçimi. Kapsanan yazma çağrıları: `UpdateOneAsync` upsert (`UserRoleRepository.cs:54`), `DeleteManyAsync` (`:66-68`), `InsertOneAsync` (`RepositoryBase.cs:40`, rol ve rol-izin), `ReplaceOneAsync` (`:50`), `UpdateOneAsync` (`RoleRepository.cs:143`) | DL-1 | **ölçülmedi**; sınıflandırıcı iki biçimi de kapsar ve tanımadığını `null` sayar (Entity Fields). Sarmalama: depolardan yalnız `DuplicateRolePermissionException` sarar (`RolePermissionRepository.cs:57`) ve onu işleyici sınıflandırıcıdan önce yakalar (`AssignPermissionCommandHandler.cs:84-87`) |
| Platform `AuditAppendRequest.Outcome`'un JSON'da sayı olarak 4'ü bağlaması (enum dönüştürücü ayarı) | DL-1 (S-3) | **ölçülmedi**; uçtan uca test XMC-001 isteğindedir |
| Platform iç ucunun S-6 okuma satırını kabulü (`Category 2`, `Operation 15`, `IsMetaAudit true`) | DL-2 (S-6) | kod okuması (2026-11-06): `AuditService.cs:205-263`, `:153`, `InternalAuditController.cs:43-111`; uçtan uca **ölçülmedi** (XMC-001) |
| Gizli bilgi doğrulayıcısı (`PlatformService:InternalApiKey` zorunluluğu) | DL-2 / DL-3 dağıtımı | okundu (2026-11-06): `SecretServiceCollectionExtensions.cs:31-49`, `SecretRequirementValidator.cs:35-66`, `RequiredSecretDefinition.cs:3-9` |
| `Diten.BuildingBlocks.CentralAudit` (F-20 karşılaştırması) | — | kısmen (2026-11-06): `CentralAuditBehavior.cs:1-20`, `CentralAuditOptions.cs:1-51` grep, `IsMetaAudit` grep; gönderici gövdesi **okunmadı** |

## Follow-up Items

- Ayrı iş, MOD-0018 sahibi kararı: `UserRoleRepository.RevokeAsync` ve `RolePermissionRepository.RevokeAsync` kaydı kalıcı siliyor;
  `RolePermissionRepository.RevokeAsync` silmeden önce bütün kiracıların hak satırlarını belleğe okuyor (inceleme 04, F-18).

- FU-0022-AUD-02: e-imza komutları aynı `IUserAuditRecorder.RecordAttemptAsync` yoluna geçebilir (MOD-0022 sahibi; ayrı iş). Geçerse yol
  `c` kredisi otomatik gelmez. AC-34'teki çağıran listesi kırmızıya döner. Kendi iz satırı ya da CT defter kararı ve bu paketin
  revizyonu gerekir.
- Silinmiş kullanıcının rol geçmişine kiracı tarafı giriş (S-4 bilinen boşluğu). Sahip açarsa ayrı FU olur.
- Platform `AuditOutcome.Unconfirmed` belgesinin (`AuditEnums.cs:63-67`) Auth'un yazma onayı kullanımını da anlatacak biçimde
  genişletilmesi (Platform dosyası, XMC-001).
- `ServiceClientRegistryReconciler.AuditId` türetmesinin `RbacAuditIds` ile tek yardımcıda birleştirilmesi. Davranış değişikliği
  yoktur ama İ5'e yakın kod olduğu için ayrı iştir.
- Reddedilen komutların kaydı (§3): Açık kararlar (h) (kapandı): ayrı paket; sahip açarsa yeni FU.
- `audit-trail-standard.md` §4.4 eski cümlesinin düzeltilmesi (U-3; `.antigravity/**`, sahip onayı).
- `RoleAuditRecorder.cs:7` "never throws" yorumunun yerel yazımı da sararak gerçekle eşitlenmesi (madde 3). Bu paket mevcut `RecordAsync`'e
  dokunmaz; ayrı küçük iş.

- İşleyici dosya adlarının Golden adlandırmasına çevrilmesi (U-7).

## Audited Events

**Servisin denetim altyapısı bugün:** `auth-kullanici-iletimi` (yol `b`, en iyi çaba, yapılandırmaya bağlı: anahtar boşsa iletim
atlanır, `PlatformAuditForwarder.cs:55-59`) kabul edilmiş. `auth-gunlugu` ve `auth-rbac-gunlugu` `aday`
(`Diten.AuthService.md:10-14`). Yedi komutun hepsi **K2 sınıfıdır** (rol / yetki değiştirir). Yeni yazma komutu yoktur.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| AssignRoleCommand | `user_role_assigned` (deneme `:attempt`, sonuç `:failed` / `:not_applied`) | User (hedef kullanıcı; konu rol) | `b` — `auth-kullanici-iletimi`, sonuç `IUserAuditRecorder.RecordAsync` aşırı yüklemesi (DL-1); (a1) ile ayrıca `c` — `auth-rbac-gecmisi`, önce-yaz `IUserAuditRecorder.RecordAttemptAsync` (DL-3). K2: önce-yaz, kapalı-başarısız (AC-1, AC-2) | AfterState: roleId, roleName | — |
| RevokeRoleCommand | `user_role_removed` (aynı ekler) | User | aynı (DL-1 / DL-3). K2: kapalı-başarısız (Açık kararlar (b) (b1), kapandı; AC-2) | BeforeState: roleId, roleName; sonuçta `stage` | — |
| AssignPermissionCommand | `role_permission_granted` | Role (konu izin) | Bugün kabul edilmiş iz yok (`## Bilinen borç`). (a1): `c` — `auth-rbac-gecmisi`, `IRoleAuditRecorder.RecordAttemptAsync` (DL-3). İletim `IRoleAuditRecorder.RecordAsync` aşırı yüklemesiyle sonra. K2: kapalı-başarısız (AC-12) | AfterState: permissionId, permissionKey, roleName | — |
| RevokePermissionCommand | `role_permission_revoked` | Role | aynı; K2: kapalı-başarısız ((b1), kapandı; AC-12, AC-30) | BeforeState: permissionId, permissionKey, roleName; sonuçta `stage` | — |
| CreateRoleCommand | `role_created` | Role | aynı | AfterState: roleName, displayName, description | — |
| UpdateRoleCommand | `role_updated` | Role | aynı | Before/After: displayName, description | — |
| DeleteRoleCommand | `role_deleted` | Role | aynı | BeforeState: roleName | — |
| GetRoleAuditHistoryQuery (**okuma satırı**, §7: denetim kaydının kendisi okunur) | `auth.role.history.read` (Açık kararlar (p) (p1), kapandı; Platform etiketi XMC-001) | Role (okunan rol) | `b` — merkezi günlüğe meta-denetim (`IsMetaAudit = true`), teslim teyitli `TryForwardAsync` (FU27 DL-5 ya da S-5); teslim yoksa okuma yok, 503 (AC-18). Eşdeğer iz okuma için kullanılmaz | — (okuyan, kiracı, rol kimliği, zaman). Platform değerleri: `Category IdentityAccess`, `Operation Execute`, `EntityType Role` (S-6; Entity Fields) | — |
| GetUserRoleAuditHistoryQuery (**okuma satırı**, §7) | `auth.user-role.history.read` ((p1)) | User (okunan kullanıcı) | aynı | — Platform değerleri aynı, `EntityType User` (S-6) | — |
| GetDeletedRolesQuery (rol listesi okuması; denetim kaydı okumaz) | — | Role | Okuma satırı yazılmaz; canlı rol listesi (`GetAllRolesQuery`) gibi. Geçmişe geçiş meta-denetimli rol geçmişi ucundandır | — | — |

- **K2 yolu:** Deneme satırı işleyicinin içinde, iş yazımından **önce** yazılır. Yazılamazsa komut 503 ile başarısız olur ve
  kullanıcı "şu an yapılamadı" cevabını alır (§4.3, §4.4 "Auth içinde"). Sonuç satırı ve iletim en iyi çabadır. Değişiklik olup
  sonucu yazılamayan durum, okuyucuda "sonuçlanmadı" olarak görünür. Kayıp sessiz değildir (`LogError` ile yazılır). Değişiklik
  uygulanıp sonraki adım (sürüm / oturum) düşerse sonuç `Failed` + `mutationApplied = true` olur ve "Uygulandı, sonrası hata
  verdi" diye görünür. İletim kuralı Implementation Notes madde 8'dedir ((m1) + S-3).
- **Defter zamanlaması:** İki kişi-rol komutu DL-1 ve DL-2 boyunca `## K2 borcu`'nda, beş rol komutu `## Bilinen borç`'ta kalır.
  Yedisinin de çıkışı DL-3'tedir (AC-22, AC-23). DL-1'de erken K2 çıkışı yoktur (Açık kararlar (n), kapandı).
- **Yol `c` belirteci ve çağıranları (bulgu F-16):** Belirteç `IRoleAuditRecorder.RecordAttemptAsync/IUserAuditRecorder.RecordAttemptAsync`
  bir yöntem adı eşleşmesidir (`AuditTrailMeasurement.cs:361-375`). Başka bir işleyici (ör. FU-0022 e-imza) aynı üyeyi çağırırsa
  ölçüm ona da kredi verir. O işleyicinin satırları ise rol geçmişi ekranlarında okunmaz (olay ailesi süzgeci, AC-17), yani koşul 3
  onun için sağlanmaz. Bu yüzden DL-3, çağıran kümesini yedi işleyicide sabitleyen `RbacWriteAheadCallerPinTests`'i getirir (AC-34).
  Sekizinci çağıran testi kırmızıya döndürür. Liste ancak paket revizyonu + `CT-DECISIONS.md` satırıyla genişler.
- **İki revoke'da yeni koruma:** Aktör yoksa 401 `ROLE_ACTOR_REQUIRED` döner ve satır yazılmaz (AC-29, AC-30).
- **Yol `c` beş koşulu (a1, DL-3'te tek tek işaretlenir):** (1) kim (`UserId`) + ne (`EventName` + `EntityType` / `EntityId`) +
  ne zaman (`OccurredAt`, UTC) + kiracı (`TenantId`). (2) Yazıcı ve okuyucu arayüzünde güncelleme / silme yok (AC-19). (3) Kiracı
  tarafında, kaydın kendi ekranında okunabilir: rol → Roller hızlı görünümü (silinmiş rol → "Silinmiş roller" süzgeci), kişi-rol →
  kullanıcı-rol atama ekranı; okuma izne bağlı (AC-24, AC-25, AC-35). **İstisna (S-4):** silinmiş kullanıcının rol satırları kiracı
  ekranında okunmaz; CT defter kararı bunu adıyla yazar. (4) Gerçek depo: `authAuditLogs` Mongo koleksiyonu (AC-3 gerçek Mongo).
  (5) İz adı ve belirteç `## İzler`'de (AC-22); çağıranlar sabit (AC-34).
- **Yol `c` neyi cevaplamaz:** Platform yöneticisinin "bu kiracıda dün kim ne yaptı" sorusunu cevaplamaz. O soru için iletim (yol
  `b`) bugünkü gibi sürer.
- **Kayda yazılmayan:** token, istek başlıkları, e-posta, ad-soyad (yalnız kimlikler ve rol / izin adları).
- **Bilinen boşluk (§3 alan 2):** Yerel satır, eylemi yapanın o andaki adını taşımıyor (kayıt kuralı kişisel veri yazmıyor).
  Platform iletimi maskeli adı taşıyor. Okumada güncel ad çözülür (Açık kararlar (j), kapandı); platform hesabı ve çözülemeyen
  eylemi yapan için ad ve kimlik gösterilmez (S-2, AC-32).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | önce-yaz temeli ((i2): `AuthAuditLog.EntityType` / `EntityId` + dizin dahil; hata sınıflandırıcı) + kişiye rol verme / geri alma (AssignRole, RevokeRole) kapalı-başarısız; RevokeRole'a yeni aktör koruması; defter ve sabit sayılar **değişmez** (iki komut K2 borcunda kalır; erken çıkış yok, (n) kapandı); kullanıcı-rol ekranı hata metni 7 dil; AC-1…AC-11, AC-29; **kanıt seviyesi E4** (kalıcılık + kiracı / RBAC + denetim + hata yolları; gerçek Mongo) | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Acceptance Criteria, Test Expectations, Audited Events |
| DL-2 | beş rol / izin komutu önce-yaz ve kapalı-başarısız (üç hata sınıfı ve iptal dahil); RevokePermission'a yeni aktör koruması; (a1) iki geçmiş okuma ucu + meta-denetim (sonuç değerleri `mutationApplied` dahil; eylemi yapan gizleme); (k-1) silinmiş roller ucu; Roller ve rol-izin ekranı hata metni 7 dil; defter değişmez; AC-12…AC-21, AC-30, AC-32, AC-33; **kanıt seviyesi E5** (Auth → Platform meta-denetim teslimi, servisler arası çalışma) | Owned Objects, Repo Scope, Runtime Constraints, Backend File Convention, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events |
| DL-3 | **giriş şartı: AC-16 silinmiş rol testi ve AC-33 yeşil** ((k) bulur + (k-1) kapandı); (a1) iki ekranın salt-okunur geçmiş bölümü 7 dil ("Uygulandı, sonrası hata verdi" rozeti, aşama etiketleri, kapsam notu dahil) ve "Silinmiş roller" süzgeci; sayfa yardımı `Step5`; iz kabulü (`auth-rbac-gecmisi`, CT defter kararı) ve çağıran sabit listesi; borç kapanışı: Bilinen borçtan beş rol komutu birlikte (DeleteRole dahil, yedek yok), K2 borcundan iki satır (`Debt 18 → 13`, `K2Debt 11 → 9`; aynı değişiklikte ölçülür); dev çalışma kanıtı; AC-22…AC-28, AC-31, AC-34, AC-35; **kanıt seviyesi E4** (ekran + kalıcılık + denetim; `dev` ortamında §8-(3)) | Layout & Shell Contract, Frontend File Contract, Authorization Convention, Acceptance Criteria, Test Expectations, Page Help (UXH-001), Audited Events |

**Sıra:** DL-1 → DL-2 → DL-3 ((i2) ile FU27 DL-5'i beklemek kalktı). DL-3, DL-1 ve DL-2'den önce birleşemez (AC-23, inceleme
kapısı). DL-3, AC-16 silinmiş rol testi ve AC-33 yeşil olmadan başlamaz (AC-22). Kapalı-başarısız olmayan bir K2 komutuna kredi
verilemez ve K2 borcu büyüyemez. K2 çıkışı DL-3'tedir, çünkü kapalı-başarısız satır ancak o dilimde kabul edilmiş ve okunabilen ize
yazılmış olur (Açık kararlar (a), (n); kapandı). **(a2) / (a3) ya da (b2) / (b3) seçilirse paket revizyonu + yeni onay satırı
gerekir;** bu sürüm o dalları uygulamaz.

**Takvim (plan, ölçüm değil; hedef 2026-11-20, karar kaydı 02, karar 7):** 2026-11-05 planı bir gün kaydı: bu revizyon
11-05'te değil 11-06'da yazıldı. Tarihler iş günüdür (11-07 / 11-08 ve 11-14 / 11-15 hafta sonu). Hafta sonu çalışılıp
çalışılmayacağı sahibe sorulmadı.

| Tarih | Adım | Sahibi | Kayarsa |
|---|---|---|---|
| 2026-11-06 (Cuma) | Bu revizyon (yüksek ayar); S-1…S-5 sahibe gösterilir | CT · Auth | — |
| 2026-11-09 (Pzt) | Bağımsız inceleme 04 (kod dosyalı ZIP); S-1…S-5 cevabı karar kaydında; kayıt satırı ve karar kaydı 02 main'de; geçerse CT-APR-01 onay satırı. XMC-001 etiket isteği Platform sahibine gider (FU27 ile ortak) | CT · Auth + onaylayıcı | Her gün DL-3 tamponundan düşer |
| 2026-11-10 (Salı) | DL-1 başlar | yazan + CT | — |
| 2026-11-11 (Çar) | **S-5 tarih kapısı:** FU27 DL-5 ileticisi (`TryForwardAsync`, `IsMetaAudit`) main'de mi? Değilse FU29 DL-2'ye taşınır | CT · Auth + Treasury CT | Taşıma DL-2'ye bir gün ekler |
| 2026-11-12 (Per) | DL-1 birleşir | yazan + CT | DL-2 bir gün kayar |
| 2026-11-16 (Pzt) | DL-2 birleşir; AC-16 silinmiş rol testi ve AC-33 yeşil (DL-3 giriş şartı); XMC-001 etiketi main'de | yazan + CT; Platform sahibi | DL-3 sıkışır; 11-17'de kayma hedefi tehdit eder ve sahibe sorulur |
| 2026-11-19 (Per) | DL-3 birleşir; CT defter kararı; dev çalışma kanıtı (AC-26; S2S anahtarı dolu) | yazan + CT | Hedef kayar; sahibe yeni tarih sorulur |
| 2026-11-20 (Cuma) | Hedef | MOD-0018 sahibi | — |

## Açık kararlar

**Durum (2026-11-05):** Sahip, yazılı CT önerilerinin hepsini kabul etti ("onayla, hedef 11-20"; karar kaydı 02, karar 7). Aşağıdaki
her satır **kapandı**. Gövde ve AC'ler bu kararlarla sabittir. Seçilmeyen bir dal sonradan seçilirse **paket revizyonu + yeni onay
satırı** gerekir. Sonradan doğan seçimler ayrı listededir ("Sahip kararı gereken yeni seçimler"): S-1…S-5 kabul edildi (karar kaydı
03, karar 9); S-6 ve S-7 kabul edildi (karar kaydı 04, karar 11).

| # | Konu | Seçenekler | Karar | Sahip | Durum / seçilmeyen dallar |
|---|---|---|---|---|---|
| (a) | İzin ve rol komutlarının borçtan çıkması için kabul edilecek iz | **(a1)** yol `c`: yeni iz `auth-rbac-gecmisi`, iki geçmiş ucu + iki ekranda geçmiş (beş koşul) · (a2) yol `b`: CT, `IRoleAuditRecorder.RecordAsync`'i rol iletimi olarak kabul eder; ekran yok · (a3) önce (a2), sonra (a1) | **(a1).** Kapalı-başarısız yazılan satır, kabul edilmiş ve okunabilen iz olur. (a2)'deki kayıt en iyi çabadır ve anahtara bağlıdır (§4.2, K8). Karşılaştırma: SAP değişiklik belgeleri (`CDHDR` / `CDPOS`) nesnenin kendi ekranından okunur; Oracle Fusion denetim raporu nesne başına süzülür | MOD-0018 sahibi (karar) + Control Tower (defter kararı, DL-3) | **Kapandı (sahip 2026-11-05, onayla).** Defter kararı DL-3'te CT'nindir; karar değil, adımdır. (a2) / (a3) seçilirse paket revizyonu + yeni onay satırı |
| (b) | Revoke komutlarında kapalı-başarısız denetim ile acil kaldırma önceliği | **(b1)** revoke da kapalı-başarısız · (b2) revoke denetimsiz de geçer (K2'yi çiğner) · (b3) ayrı, denetlenen acil yol | **(b1).** Denetim ve roller aynı Mongo veritabanında (Dependencies); (b2) gerçek kaldırma güvencesi vermez ama denetimsiz kapı açar. Ölçülmeyen: yalnız `authAuditLogs`'un yazılamadığı kesinti biçimi | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** (b2) / (b3) seçilirse paket revizyonu + yeni onay satırı |
| (c) | Deneme / sonuç satırı biçimi ve deterministik kimlik | Olay adı: **(c1)** ekli adlar · (c2) tek ad + `phase`. Deneme kimliği: **yeni GUID** · türetilmiş. Tekil yazım: **(c-i)** yeni yazıcı, `IAuthAuditService` değişmez · (c-ii) `IAuthAuditService.WriteOnceAsync`'e aşırı yükleme (FU27 D98 ile çatışır) | **(c1) + yeni GUID + (c-i).** Bugünkü olay adını okuyan her şey yalnız gerçekleşmiş olayı görür. Yeni GUID, yeniden denemenin ayrı deneme olduğunu söyler. (c-i) "aynı kimlik → tek satır" sonucunu verir (U-4) | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** (c2), türetilmiş deneme kimliği ya da (c-ii) seçilirse paket revizyonu + yeni onay satırı ((c-ii)'de Treasury CT onayı da) |
| (d) | Sayı sabitlemesinin yeni değerleri | Bugün: `Debt 18, Exceptions 1, K2Debt 11` (`AuditTrailStandardTests.cs:61`) | **Tahmin yazılmaz.** Her dilim sayıyı, defter satırını çıkardığı değişiklikte ölçülen değere çeker. Beklenen (hesap): DL-3 `18 → 13`, `11 → 9`; `Exceptions` değişmez | Dilimi yazan + CT (kabulde) | Karar değil, teslim kuralıdır; her dilimin tesliminde uygulanır |
| (e) | Kapsam | Yedi komut bu pakette · `CreateRole` / `UpdateRole` / `DeleteRole` ayrı | **Yedisi bu pakette** | MOD-0018 sahibi | **Kapandı:** karar kaydı 01, satır 3 ("dört ortak komut + rol oluştur/güncelle/sil"). Ayırma seçilirse paket revizyonu + yeni onay satırı |
| (f) | Okuma izni adı (PKS-001) ve kiracı yalıtımı | Mevcut anahtarlar · yeni anahtarlar (`auth.roles.history.read`, `auth.users.role-history.read`) | **Mevcut anahtarlar:** rol geçmişi ve silinmiş roller `auth.roles.read`, kişi-rol geçmişi `auth.users.assign-role`. Yalıtım: kiracı JWT'den, süzgeç `TenantId + EntityType + EntityId`, başka kiracı 404 | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** Sonucu: bölümün ayrı kapısı koruma değildir (F-3) → seçim S-1 (kabul edildi, karar kaydı 03). Yeni anahtar seçilirse paket revizyonu + yeni onay satırı |
| (g) | Hedef tarih | — | **2026-11-20** | MOD-0018 sahibi | **Kapandı:** karar kaydı 02, karar 7 ("onayla, hedef 11-20"; kayıt commit'siz, main'e girmesi CT'nin işi — başlık ön koşul 4). Takvim: Teslim dilimleri |
| (h) | Reddedilen komutun kaydı (§3) | Bu pakete ekle · **ayrı paket** · kayıtsız | **Ayrı paket.** Reddi kaydetmek yedi komutun 401/403/404/409 dallarının hepsini değiştirir | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** Bu pakete eklenmesi seçilirse paket revizyonu + yeni onay satırı |
| (i) | `AuthAuditLog` alanları ve dizin; FU27 DL-5 ile sıra | (i1) FU27 DL-5 önce; FU29 FU27'nin onaylı sürümüne bağlanır · **(i2)** alanları ve dizini FU29 DL-1 ekler, FU27 DL-5 tüketir (FU27 metni revize edilir) · ortak okuyucu | **(i1)**, FU27 onaylı sürümü onay satırına kadar yoksa **(i2)** | Treasury CT + MOD-0018 sahibi | **Kapandı: (i2)** (sahip 2026-11-05, onayla; karar kaydı 02 notu: FU27'nin onaylı sürümü yok). Gövde: Entity Fields, Repo Scope DL-1, Protected Paths, Dependencies. FU27 metnini Treasury CT revize eder; bu paket FU27 dosyasına dokunmaz. Ölçüm 2026-11-06: `approvals.tsv`'de FU27 satırı yok (başlık ön koşul 2). (i1) ya da ortak okuyucu seçilirse paket revizyonu + yeni onay satırı |
| (j) | Eylemi yapanın o andaki adı (§3 alan 2) | Yerel satıra maskeli ad · **okumada güncel ad** · bilinen boşluk | **Okumada güncel ad + bilinen boşluk kaydı** | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** Platform hesabı için gösterim kuralı yeni seçimdir: S-2. Yerel satıra ad yazılması seçilirse paket revizyonu + yeni onay satırı |
| (k) | Silinmiş rolün geçmişi ve giriş yolu | Uç: **bulur** · bulmaz. Giriş: **(k-1)** "Silinmiş roller" süzgeci, salt-okunur liste · (k-2) kişi-rol geçmişinden bağlantı · (k-3) giriş yok | **Bulur + (k-1).** Bulmazsa `role_deleted` satırı kendi ekranında okunamaz. (k-3) kabul edilemez | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** Uç tanımı: Owned Objects, Repo Scope DL-2, Validation Rules, Authorization Convention, AC-33; ekran AC-35. (k-2) / (k-3) ya da "bulmaz" seçilirse paket revizyonu + yeni onay satırı |
| (l) | Sahip kararının ve kimliğin kaydı | Karar kaydı 01 + kayıt satırı | Kayıt onay satırından önce main'de | MOD-0018 sahibi + CT | **Kapandı:** karar kaydı 01 main'de (PR #375 birleşti, `68dcf9fa5`, 2026-11-05). Kayıt satırı ve karar kaydı 02 CT'nin işidir (başlık ön koşul 3, 4) |
| (m) | Değişiklik uygulandı ama sonrası düştü ya da sonuç belirsiz: Platform'a iletim | **(m1)** bilinen ad + `Failed` · (m2) iletilmez · (m3) `Succeeded` · **(m4)** bilinen ad + `Unconfirmed = 4` | **Uygulandı + sonrası düştü → (m1); belirsiz (`null`) → (m4)**, Platform kabulü ölçülürse; 4 reddedilirse (m2). (m1) için `RoleAuditEvents`'e `int OutcomeFailed` eklenir (Repo Scope DL-2) | MOD-0018 sahibi + Control Tower | **Kapandı (sahip 2026-11-05, onayla)** (m1) için. Belirsiz dal için ölçüm iki şey buldu: Platform 4'ü reddetmiyor (`AuditService.cs:247-250`), ama 4'ün belgesi "veri işlendi" diyor (`AuditEnums.cs:63-67`; F-9). Bu yüzden `null` iki sınıfa ayrıldı (F-6) → seçim S-3 (kabul edildi, karar kaydı 03); çok belgeli yazım için daraltma S-7 (bekliyor). (m3) seçilirse paket revizyonu + yeni onay satırı. S-3 reddedilir de bütün belirsiz dala tek başına (m2) ya da (m4) uygulanırsa yine paket revizyonu + yeni onay satırı |
| (n) | DL-1'de erken K2 çıkışı | **Çıkış DL-3'te** · DL-1'de çıkış | **DL-3** | Control Tower | **Kapandı (sahip 2026-11-05, onayla).** DL-1'de çıkış seçilirse paket revizyonu + yeni onay satırı + `CT-DECISIONS.md` satırı |
| (o) | Belirsiz `stage = mutation` hatası | **(o1)** `null` yaz, "Sonuçlanmadı" · (o2) depoyu yeniden oku | **(o1).** Yeniden okuma aynı kesintiye düşer; `null` yanlış bir "uygulanmadı" demez | MOD-0018 sahibi | **Kapandı (sahip 2026-11-05, onayla).** (o2) seçilirse paket revizyonu + yeni onay satırı |
| (p) | İki okuma / meta-denetim olayının adı ve Platform etiketi | **(p1)** FU27 DL-5 meta-denetim adının kalıbı ve etiket yolu · (p2) ayrı, onaylı yeni etiketler · (p3) etiketsiz (kabul edilmez) | **(p1)** | MOD-0018 sahibi + Platform sahibi (XMC-001) | **Kapandı (sahip 2026-11-05, onayla).** Adlar `auth.role.history.read`, `auth.user-role.history.read` (Entity Fields); tek XMC-001 isteği FU27 ile ortak (Dependencies). FU27 ile tek genel ad ya da (p2) seçilirse paket revizyonu + yeni onay satırı |

## Sahip kararı gereken yeni seçimler

**S-1…S-5 — kabul edildi (2026-11-06).** Sahip beş seçimin hepsini `25a03bebd` sürümünde yazıldığı gibi kabul etti ("Hepsini kabul
et"; karar kaydı 03, karar 9). Gövde ve AC'ler bu seçimlerle sabittir. Başka bir seçenek sonradan seçilirse paket revizyonu + yeni
onay satırı gerekir. Seçimler 2026-11-05 revizyonunda doğdu; 2026-11-06 yeniden ölçümünde S-1, S-4 ve S-5'in metni düzeltildi,
seçenekleri değişmedi.

| # | Bulgu | Seçim | Yazılan seçenek | Gerekçe | Gövdedeki yeri |
|---|---|---|---|---|---|
| S-1 | F-3 | Geçmiş bölümünün izin kapısı | Bölüm ayrı kapı taşımaz; sayfa kapısının içinde çizilir. Mekanik korumalar: Auth `[HasPermission]` (AC-16 / AC-17 403), sayfa kapısı, kullanıcı-rol Web vekilinin bugünkü `RefuseWithoutPermission` kapısı (yeni `role-history` eyleminde de ilk satır; AC-25 sabotaj 4) ve kodu taşıyan, başarısızlıkta veri döndürmeyen vekil cevabı. Roller Web vekiline yeni kapı eklenmez | (f) kabul edildi ve bölüm anahtarı sayfa anahtarıyla aynı. Bu durumda ikinci `if` hiçbir testle kırmızıya dönmez, yani koruma değildir. Yeni anahtar katalog / tohum / atama işi doğurur ve (f) kararını değiştirir. Kullanıcı-rol vekilinin kapısı bugün vardır ve testlidir (`UserRoleAssignmentsAccessTests.cs:68-86`); yeni eylemi ona katmak yeni desen değildir. Roller vekilinin bugün kapısı yoktur (`RolesController.cs:102-106`); eklemek o denetleyicide yeni bir desen olur ve sahibin seçimidir. Güvenli taraf: yetkinin sahibi Auth'tur ve testlidir | Frontend File Contract "İzin kapısı"; Authorization Convention; AC-24, AC-25 |
| S-2 | F-4 | Eylemi yapan platform hesabıysa ya da çözülemiyorsa gösterim | Eylemi yapan yalnız D' sarmalayıcısıyla (`IUserRepository`) çözülür. Çözülemeyen her durumda kimlik ve ad dönmez; tek etiket "Görüntülenemeyen kullanıcı" gösterilir ve durumlar ayrılmaz | BL-521 D' platform hesabını "olmayan kimlikten ayırt edilemez" tutar (`PlatformAccountScopedUserRepository.cs:14-15`). "Platform yöneticisi" yazmak da bu ayrımı sızdırır. (j) önerisine en yakın seçenek budur: güncel ad yalnız görülebilen kullanıcı için çözülür | Runtime Constraints "Eylemi yapanın gösterimi"; Frontend File Contract; AC-32 |
| S-3 | F-6, F-9 | `stage = mutation` hata sınıflandırması ve belirsiz dalın iletimi | Üç sınıf vardır. (1) Kesin hata → `false`, iletim yok (2026-11-06: yalnız tek belgeli yazımda; çok belgeli yazım için daraltma S-7, bekliyor). (2) Yazma onayı hatası → `null` + `write_concern`, `Unconfirmed` (4) iletilir. (3) Taşıma belirsizliği ya da bilinmeyen istisna → `null` + `transport`, iletilmez ((m2) dalı) | `WriteConcernError` yazımın birincilde uygulandığını söyler. Onu "Başarısız" saymak uygulanmış değişikliği gizler (F-6). Bu durum 4'ün "veri işlendi, sonu teyit edilmedi" belgesiyle örtüşür. Taşıma belirsizliğinde verinin işlendiği bilinmez, bu yüzden 4 göndermek belgeyle çelişir (F-9). Orada (m) önerisinin kendi geri dönüşü (m2) kullanıldı. Bilinmeyen istisna hiçbir zaman `false` sayılmaz | Entity Fields; Runtime Constraints; Failure Path to Verify; Implementation Notes madde 8; AC-5, AC-7, AC-14 |
| S-4 | F-11 | Silinmiş kullanıcının rol geçmişi | Uç bulmaz: 404 kalır. Yeni depo üyesi açılmaz ve giriş yolu yoktur. Bilinen boşluk olarak kaydedilir; CT defter kararı bunu yol `c` koşul 3'ün istisnası olarak yazar; takip FU açılabilir | Silinmişi getiren bir kullanıcı okuması, D' korumasının ve yumuşak silme süzgecinin dışında yeni bir yol açar (güvenlik açısından güvenli taraf). Kullanıcı seçici yalnız silinmemiş kullanıcıları listeler, yani giriş yolu da yoktur. Satırlar `authAuditLogs`'ta ve S2S anahtarı doluysa Platform günlüğünde (yol `b`; anahtar boşsa iletim atlanır, `PlatformAuditForwarder.cs:55-59`) kalır. Rol tarafındaki (k) farklıdır: orada okunamayan satır komutun kendi `role_deleted` kaydıydı | Ownership and Boundaries; Runtime Constraints "Silme"; Validation Rules; AC-17, AC-22, AC-26; Follow-up Items |
| S-5 | F-8 | FU27 DL-5 ileticisinin (`TryForwardAsync`, `IsMetaAudit`) gecikmesi | Tarih kapısı 2026-11-11. O güne kadar main'de değilse iki ekleme FU29 DL-2'ye taşınır: yalnız ekleme, ct-06 §5.3 imzasıyla. Dosyalar Repo Scope DL-2 "Koşullu (S-5)" maddesinde adlandırılır; sahip kabul ederse tarih kapısında ayrıca paket revizyonu gerekmez. FU27 DL-5 onları tüketir | Bu (i2)'nin aynı mantığıdır: onaylı FU27 yoksa ortak temeli FU29 ekler. DL-2'nin meta-denetim parçası olmadan geçmiş uçları açılamaz ve 11-20 hedefi tutmaz. Güvenli taraf: imza değişmez, yalnız ekleme yapılır, Treasury CT'ye 11-11'de sorulur | Ownership and Boundaries; Repo Scope DL-2 "Koşullu (S-5)"; Protected Paths; Dependencies; Ready-for-dev Checklist; Teslim dilimleri "Takvim" |

**S-5 ve ortak `CentralAudit` ileticisi (bulgu F-20; tartıldı 2026-11-06).** Depoda ikinci bir merkezi günlük yolu vardır:
`services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit`. SupplyChain ve Inventory bunu kullanır
(`Diten.SupplyChainService.Api/Program.cs:189`); Auth kullanmaz (`grep -rl CentralAudit services/Diten.AuthService` → 0). Yol bir
komut boru hattı davranışıdır: başarılı bir komut servisin kendi Mongo `outbox`'ına bir olay bırakır, gönderici sonra Platform'a
iletir (`CentralAuditBehavior.cs:7-17`, `CentralAuditDispatcher.cs`). Okuma satırı için üç nedenle seçilmedi. (1) Teslim teyidi
vermez: olay outbox'a yazılınca çağrı biter, Platform'un kabul ettiği o anda bilinmez. AC-18 ise verinin yalnız teslim teyidiyle
dönmesini ister (FU27 D99/1 ile aynı). (2) `IsMetaAudit` alanı yoktur (`grep -c IsMetaAudit` → 0); eklemek ortak BuildingBlocks
dosyasını ve onu kullanan servisleri etkiler. (3) Auth'a yeni koleksiyon, barındırılan hizmet ve yapılandırma bölümü getirir
(`CentralAuditOptions.cs:6-7`, `PlatformRegistration:*`). S-5'in yolu ise mevcut ileticiye iki yalnız-ekleme üye koyar. `CentralAudit`
seçilirse paket revizyonu + yeni onay satırı gerekir; bu bir öneri değil, kayıttır.

### 2026-11-06 — yeni seçimler (S-6, S-7)

**Durum (2026-11-06): kapandı (sahip 2026-11-06, "İkisini kabul et"; karar kaydı 04, karar 11).** Başka bir seçenek sonradan seçilirse paket revizyonu + yeni onay
satırı gerekir. Aşağıdaki paragraf seçimlerin doğuşunu anlatır.

Dördüncü inceleme turunun bulgularını kapatırken iki yeni seçim doğdu. Gövdeye yazılı önerilere en yakın ve güvenli tarafta kalan
**tek** seçenek yazıldı. **Bunlar kabul edilmiş sayılmaz.** Sahip cevabı karar kaydına (CT) geçmeden onay satırı yazılmaz
(Ready-for-dev Checklist A). Sahip başka bir seçenek seçerse paket revize edilir.

| # | Bulgu | Seçim | Yazılan seçenek | Seçilmeyen seçenek | Gerekçe | Gövdedeki yeri |
|---|---|---|---|---|---|---|
| S-6 | F-5 | Geçmiş okuma satırının Platform `Category` / `Operation` değerleri | `Category = IdentityAccess` (2), `Operation = Execute` (15), `EntityType = Role` / `User`, `Outcome = Succeeded`, `IsMetaAudit = true` | `Category = DataPrivacy` (10): Platform'un kendi denetim okuması bunu kullanır (`GetAuditEventListHandler.cs:38`, `GetAuditEventByIdHandler.cs:53`) | `AuditOperation`'da okuma değeri yok (`AuditEnums.cs:32-54`); `Execute` Platform emsalidir. Kategori Auth ileticisinin sabitidir (`PlatformAuditForwarder.cs:28`, `:88`); `DataPrivacy` için iletici kategoriyi olaydan almalıdır, yani `PlatformAuditEvent`'e bir alan daha ve ileticide bir dal daha gerekir. Güvenli taraf: iki değer de Platform doğrulamasından geçer (`AuditService.cs:227-245`); daha küçük değişiklik IdentityAccess'tir | Entity Fields "Okuma satırının Platform değerleri"; AC-18; Dependencies (XMC-001); Ready-for-dev Checklist B |
| S-7 | F-12 | Çok belgeli değişiklik yazımında "kesin hata" sınıfı | Kesin hata → `false` yalnız tek belgeli yazımda. `RevokeRole`'un `DeleteManyAsync`'inde (`UserRoleRepository.cs:64-69`) her istisna `null` + `uncertainty = transport` ("sonuç bilinmiyor"); yeni değer yok | Yeni `uncertainty = partial` değeri; ekran, iletim kuralı ve sınıflandırıcıya yeni bir dal | Çok belgeli silmede yazma hatası kısmi silmeyle gelebilir; "uygulanmadı" demek yetkinin gerçekte kalktığını gizleyebilir. S-3'ün ilkesi "bilinmeyen hiçbir zaman `false` sayılmaz"dır; bu seçenek onu daraltır, yeni değer açmaz. Ekran "Sonuçlanmadı" der, Platform'a gitmez (S-3 dal 3) | Entity Fields; Owned Objects (sınıflandırıcı); Repo Scope DL-1 (`RbacMutationWrite`); AC-5, AC-14 |
