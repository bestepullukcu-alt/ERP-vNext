# WP-0018-FIX-01 — Platform'dan eklenen ikinci yönetici rolsüz kalıyor (rol ataması sessizce düşüyor)

> **ALİ ONAYI GEREKİR (XMC-001).** Düzeltme `services/Diten.AuthService/**` içinde; Auth Ali'nin alanıdır. Bu paket Ali
> onaylamadan dağıtılmaz; Platform tarafında kod değişikliği gerekmez.

| Alan | Değer |
| :--- | :--- |
| Durum | **draft** |
| Onaylayıcı | Beste (merkezi ekip, Platform sahibi — işin açılışı) + **Ali (Auth sahibi — kod onayı, zorunlu)** |
| Açılış | 2026-11-25 — sahip onayı: "platform tarafından eklemek gerekiyordu" |
| Modül | **MOD-0018** RBAC / ABAC Authorization. Ön kontrol: `verify_module_id.py . --check-id MOD-0018 --name "RBAC / ABAC Authorization"` → `OK … proven against Blueprint/registry` (exit 0, 2026-11-25). Rol atama eki **MOD-0018-FU29** (RBAC Role Assignment Write-Ahead Audit; registry satırı, sahip kararları `docs/records/decisions/2026-11/mod-0018-fu29-owner-decisions-01…07.md`) en yakın FU'dur; pack dosyası bu checkout'ta yok (bkz. §9 soru 1) |
| Pack | `execution/domains/platform-shared-services/module-packs/MOD-0018-rbac-abac-authorization.md` (ready-for-dev). Not: pack :43/:112/:261 "AuthService Roles/Permissions CRUD" kapsam dışı der; kullanıcı-rol **ataması** FU29'un konusudur. MOD-0360 (Tenant User & Identity, eski CAND-CAP-0001) pack'i "Role assignment"ı açıkça dışlar (:53) |
| Servis | `Diten.AuthService` (Persistence + iç olay kapısı) |
| Kaynak gözlem | 2026-11-25 (dev): kiracı `62d31535-08af-41ed-9dda-844d7b928a98`, Platform "Tenant → Access → Add Admin" ile eklenen `b.pullukcu@grand-medical.com` iki kez "Invite" (200); giriş yapabiliyor, **hiç rolü yok** (menü boş, /Roles "yetkiniz yok") |
| Kod tabanı | Filo hat dalı `feature/sce/mod-0173-inventory-ledger-valuation` (`8c32f4d90`) = origin/main `82af5c24a` = HEAD `c815d95d0` — `InternalEventsController.cs`, `UserRoleRepository.cs`, `InviteTenantAdminUserCommandHandler.cs`, `AdminUserInvitationService.cs`, `CreateTenantAdminUserCommandHandler.cs` blob'ları **aynı**. Hat dalı ile main arasında bu davranışta fark yok |

## 1. Akış — Platform hangi kapıyı çağırıyor (ölçüldü, kod)

1. "Add Admin" → `CreateTenantAdminUserCommandHandler` yalnız Platform `tenants.AdminUsers`'a satır ekler; Auth'a gitmez
   (Platform zaman çizelgesi: `tenant.admin_user.added` 08:51:15 UTC).
2. "Invite" → `InviteTenantAdminUserCommandHandler.cs:116` `InviteAsync(…, AdminInvitationTrigger.Operator)` →
   `AdminUserInvitationService.cs:195-198` → Auth `POST /internal/events/tenant-admin-invited`, `trigger=operator-invite`.
3. Auth `InternalEventsController.cs:120-125` → `createOnly = false` (operatör kapısı). `:161` create-only kısa devresi bu
   yolda **çalışmaz**. Yeni hesap: `:194` `GrantTenantAdministratorAsync` → `:198` hesap yazılır. Mevcut hesap: `:227-244`
   sıfırlama → `:250` `GrantTenantAdministratorAsync`. Yani **her iki davette de** kapı rol vermeyi dener — kod beklentisi doğru.
4. `GrantTenantAdministratorAsync` (`:255-266`): üyelik yoksa yazar; `ExistsAsync` false ise `UserRoleRepository.AssignAsync`.

## 2. Kök neden — ölçüldü (dev, salt okuma)

**Veri:** `diten_auth_v3.users`: hesap `c3742df2-1978-4d88-b05e-0d770975a179` 08:51:36 UTC'de oluşmuş (ilk Invite, yeni-hesap
yolu), `IsActive=true`, kiracı doğru. `tenant_user_memberships`: üyelik **var** (08:51:36). `userRoles`: bu kullanıcı için
**0 satır**; kiracıda tek satır ilk yönetici `ebaf0412…` → Admin (`6f9443b8…`, 08:25:55). `authAuditLogs`: 08:55:52 ve
08:57:28 girişlerinde `reason = empty_role_set`. Yani üyelik yazıldı, rol yazılmadı — ve hiçbir hata dönmedi.

**Neden:** `userRoles` koleksiyonunda kodun **tanımlamadığı** bir benzersiz indeks var:

| İndeks | Benzersiz | Kısmi/seyrek | Kaynağı |
| :--- | :--- | :--- | :--- |
| `UserId_1_RoleId_1_TenantId_1` | evet | hayır | kod: `MongoDbIndexConfigurations.cs:203-208` |
| `UserId_1_TenantId_1` | hayır | — | kod: aynı yer |
| **`PrincipalType_1_PrincipalId_1_RoleId_1_TenantId_1`** | **evet** | **hayır** | **kodda yok** (repo geçmişinde `PrincipalType`/`PrincipalId` alanı `UserRole` varlığında hiç olmadı; `git log -G`) |
| `Status_1_TenantId_1` | hayır | — | kodda yok |

- `UserRole` varlığı (`Domain/Entities/UserRole.cs`) `PrincipalType`/`PrincipalId` taşımaz → bugünkü kodun yazdığı her
  satırda iki alan da **yok** → bu indekste anahtar `(null, null, RoleId, TenantId)`.
- Aynı kiracıda aynı rol için **ikinci** alanı-boş satır = aynı anahtar → **DuplicateKey**. Ölçüm: 20 satırın 16'sında
  `PrincipalId` dolu (2026-06-05'e kadar eski şema ve elle yazılmış `codex-role-assignment` satırları), 4'ünde boş; boş olan
  4 satır 4 ayrı (kiracı, rol) çiftinde — **hiçbir çiftte ikinci boş satır yok**: indeks her (kiracı, rol) için tek bir yeni
  atamaya izin veriyor.
- `UserRoleRepository.cs:54-60` `AssignAsync` upsert'ü, `:56` `catch (MongoWriteException) when DuplicateKey` ile **her**
  benzersiz indeks ihlalini "eşzamanlı bir atama kazandı, zaten verilmiş" sayıp yutar; gerçekten verilmiş mi diye bakmaz.
  Sonuç: kapı 200 döner, Platform "Invitation sent" yazar, kullanıcı rolsüz kalır.

**Hat dalı / main farkı:** yok (blob'lar aynı). Kusur verinin (yabancı indeks) ve kodun (körlemesine yutma) birleşimi;
temiz bir veritabanında (testler, yeni kurulum) yabancı indeks olmadığı için görünmez.

## 3. Etki

- **Davet kapısı** (`InternalEventsController.cs:265`), **Roller/Kullanıcılar ekranından rol atama**
  (`AssignRoleCommandHandler.cs:69` — `:66` ön kontrolü geçer, `:79` 204 döner), **kayıt** (`RegisterCommandHandler.cs:103`),
  **platform yöneticisi rol yakınsaması** (`PlatformAdministratorRoleConvergence.cs:127`) — dördü de aynı `AssignAsync`'i
  kullanır; dev'de bir kiracıda bir role ikinci kişi **hiçbir yoldan** eklenemez ve her yol başarı söyler.
- Dev ölçümü: rolsüz etkin kiracı kullanıcısı 2 (`c3742df2…` bu vaka; `0196b2dc…` kiracı `00000000-…-0001`, ayrıca incelenmeli).
  Engellenmiş (kiracı, rol) çiftleri: test kiracısı Admin + kiracı `97c59330…`'da 3 rol.
- Güvenlik yönü: yetki **fazlası** yok (fail-closed: rolsüz kullanıcı hiçbir şey göremez); risk işlevsel + yanıltıcı başarı.
- Diğer ortamlar (varsa test/canlı) bu indeksi taşıyor mu — ölçülmedi (§9 soru 3).

## 4. Çözüm seçenekleri

| # | Seçenek | Karar |
| :--- | :--- | :--- |
| A | Yalnız dev'de yabancı indeksi düşür | Gerekli ama yetmez: körlemesine yutma kalır, başka bir yabancı/yanlış indeks aynı sessizliği üretir |
| B | `AssignAsync` DuplicateKey'de **doğrula**: `(UserId, RoleId, TenantId)` satırı gerçekten varsa başarı, yoksa adlı hata | **Önerilen (kod)** |
| C | `UserRole`'a `PrincipalType`/`PrincipalId` ekleyip eski şemayı doldurmak | Reddedildi: eski, kodda olmayan bir şemayı canlandırır; MOD-0018 sözleşme değişikliği |
| D | Açılışta tanınmayan benzersiz indeksleri **otomatik düşürmek** | Reddedildi: veri yapısını sahipsiz değiştirir; yerine adlı uyarı (F-3) |

## 5. Önerilen tek çözüm: **B + çağıranlarda son koşul + indeks uyarısı + dev adımı (A)**

**F-1 · `UserRoleRepository.AssignAsync` (Auth Persistence).** `:56` yakalamasında: aynı filtreyle (`UserId`, `RoleId`,
`TenantId`, `IsDeleted == false`) satır var mı bak. Varsa (gerçek eşzamanlı atama) bugünkü gibi başarı. Yoksa
`RoleAssignmentNotWrittenException` (yeni, Application arayüzünde) fırlat; log: `auth.role_assignment.not_written
TenantId RoleId UserId Index=<yazma hatasındaki indeks adı>` (e-posta/token yok).

**F-2 · Çağıranlar adlı hata döner, başarı söylemez.**
- `InternalEventsController.GrantTenantAdministratorAsync` (`:255-266`): hata → yeni-hesap yolunda hesap **yazılmadan**
  (`:194` hesap yazımından önce olduğu için zaten öyle) `409 { code: "ROLE_GRANT_FAILED" }`; mevcut-hesap yolunda (`:250`)
  aynı kod. Platform tarafı değişmeden bunu 502 "Tenant admin invitation could not be completed." olarak gösterir
  (`InviteTenantAdminUserCommandHandler.cs:118-127`); adlı kod eşlemesi isteğe bağlı Platform eki (§9 soru 4).
- `AssignRoleCommandHandler.cs:69`: hata → `Response.Fail(…, 409, "ROLE_GRANT_FAILED")`, 204 değil.
- `RegisterCommandHandler.cs:103`, `PlatformAdministratorRoleConvergence.cs:127`: istisna yukarı taşınır (yutulmaz); davranış
  test ile sabitlenir.

**F-3 · İndeks uyarısı (Auth, açılış).** `MongoDbIndexConfigurations` `userRoles` indekslerini listeler; kodun tanımladıkları
dışındaki **benzersiz** her indeks için `auth.index.unexpected_unique Collection=userRoles Index=<ad>` hata logu. Düşürmez.

**F-4 · Dev tek seferlik (kod değil; Ali onayıyla, CT adımı).** `diten_auth_v3.userRoles` üzerinde
`PrincipalType_1_PrincipalId_1_RoleId_1_TenantId_1` indeksinin düşürülmesi (veri satırı silinmez). Ardından
`b.pullukcu@…` için Admin rolü **Roller ekranından** atanır (denetlenen yol; yeniden "Invite" parolayı ve oturumları sıfırlar,
gerekmez). `0196b2dc…` ayrıca incelenir. Doğrudan Mongo'ya satır yazılmaz.

## 6. KORU / YAPMA

- Gerçek eşzamanlı aynı atamanın idempotent başarısı (BL-521 FIX5 4(c)) korunur.
- Davet kapısının yazma sırası (önce üyelik + rol, en son hesap — BL-454 stage D FIX2 (1)) ve create-only kapısının
  "mevcut hesaba dokunma" kuralı (`:161`) değişmez.
- `UserRole` şeması, `MongoDbIndexConfigurations`'ın oluşturduğu indeksler, Platform kodu, Gateway: değişmez.
- Hiçbir log/yanıt e-posta, token ya da parola içermez.
- **DUR:** yabancı indeks eski bir sözleşmenin (ör. dış bir okuyucunun) dayanağıysa (`PrincipalId` dolu satırları okuyan bir
  tüketici bulunursa) → F-4 yapılmaz, raporla.

## 7. Kabul ölçütleri (AC)

1. AC-1 — Koleksiyonda `(PrincipalType, PrincipalId, RoleId, TenantId)` benzersiz indeksi ve aynı kiracı+rol için alanları boş
   bir satır varken, başka kullanıcıya `AssignAsync` → `RoleAssignmentNotWrittenException`; satır yok; adlı log.
2. AC-2 — Aynı kullanıcı+rol+kiracı için iki eşzamanlı `AssignAsync` → ikisi de başarı, tek satır (bugünkü davranış).
3. AC-3 — Davet kapısı, AC-1 koşulunda: yeni-hesap yolunda `409 ROLE_GRANT_FAILED` ve **hesap oluşmaz**; mevcut-hesap yolunda
   `409 ROLE_GRANT_FAILED`. Platform "Invite" 200 dönmez.
4. AC-4 — Rol atama ekranı (`AssignRoleCommandHandler`) AC-1 koşulunda 409 + kod; 204 dönmez.
5. AC-5 — Açılışta yabancı benzersiz indeks için adlı hata logu; indeks düşürülmez.
6. AC-6 (E4, CT, F-4 sonrası) — test kiracısında ikinci yönetici Admin rolüyle giriş yapar, menü dolu; yeni bir üçüncü
   yönetici "Add Admin" + "Invite" ile Admin alır (`userRoles`'ta satır, `empty_role_set` yok).

## 8. Testler ve sabotaj

| Test | Yer | Sabotaj (kırmızı beklenir) |
| :--- | :--- | :--- |
| Yabancı benzersiz indeksli gerçek Mongo koleksiyonu + ikinci kullanıcı ataması → istisna, satır yok | `Diten.AuthService` Persistence/Application testleri (gerçek Mongo, mevcut test Mongo deseni) | F-1 doğrulamasını kaldır (eski yutma) |
| Eşzamanlı aynı atama → başarı, tek satır | aynı | Doğrulamayı "her DuplicateKey'de fırlat"a çevir |
| Davet kapısı yeni-hesap yolu, yabancı indeks → 409 + `users`'ta hesap yok | `InternalEventsController` testleri (mevcut BL-454 test sınıfları yanında) | `GrantTenantAdministratorAsync`'te hatayı yut |
| Davet kapısı mevcut-hesap yolu → 409 | aynı | aynı |
| `AssignRoleCommandHandler` → 409 + `ROLE_GRANT_FAILED` | `Users` handler testleri | Handler'da istisnayı yakalayıp 204 dön |
| Açılış indeks uyarısı adlı log | `MongoDbIndexConfigurations` testi | Uyarı döngüsünü kaldır |

Her sabotaj: kırmızı → bayt-eşit geri al → yeşil. Auth testleri filtresiz; Platform takımı çalıştırılmaz (kod değişmiyor),
Platform "Invite" uçtan uca E4'te CT tarafından.

## 9. Açık sorular

1. **Ali:** Bu iş MOD-0018-FU29 (rol atama eki) altında mı yürüsün, yoksa MOD-0018 ana kimliğiyle ayrı FIX mi? FU29 pack
   dosyası bu checkout'ta yok; FU29 `AssignRoleCommandHandler`'ı değiştiriyorsa çakışma sırası.
2. **Ali:** F-4 — dev'de yabancı indeksin düşürülmesine onay; indeksi kim/ne zaman oluşturdu biliniyor mu (repo geçmişinde yok;
   `userRoles_guid_migration_backup_20260728` göçüyle ilişkili olabilir — ölçülmedi)?
3. **Ali/CT:** Diğer ortamların `userRoles` indeksleri salt-okuma ile sayılsın mı?
4. **Beste:** Platform "Invite" Auth'tan gelen `ROLE_GRANT_FAILED`'ı adıyla göstersin mi (küçük Platform eki: `AdminInvitationRefusals`
   + `details.js` metni, en/tr)? Bu pakette yok; öneri: evet, ayrı küçük paket.
5. **Beste/Ali:** `0196b2dc…` (kiracı `00000000-…-0001`, rolsüz etkin kullanıcı) bu kusurdan mı, bilinçli mi?
