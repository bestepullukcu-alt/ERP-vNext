# MOD-0018 ROLE1 — varsayılan rol şablonu yalnız rol oluşturulurken (WP-0018-ROLE1, CT-MVP1-0018-ROLE1 v1)

| alan | değer |
|---|---|
| Modül | MOD-0018 AuthService varsayılan roller, dilim ROLE1 (FU-0022-SEED-02; FUB2 kanıtı §2.1) |
| Dal / taban | `feature/sce/mod-0018-role1`, taban `03f7cc1ba` (temiz). Yalıtılmış worktree `C:\Users\Ali\erp-vnext-wt-0018-role1`. Push, PR ve merge yok |
| Commit'ler | 1) `bf9b99e91` kod + testler · 2) bu kanıt + betikler belgesi + MOD-0022 / MOD-0175 pack §19 ROLE1 notları |
| Durum | **ajan sonucu — CT bekliyor** |
| BLOCKED | Yok. Davet edilen yönetici var olan rollere dokunulmadan atanıyor (§4 S8, yoklama). "Kiracı yeni mi" kararı için yeni şema ya da koleksiyon gerekmedi: var olan benzersiz indeks yeterli (§2). Platform akışı bozulmadı: `tenant-activated` ve `tenant-admin-invited` 200 dönüyor ve aynı sırayla çalışıyor. |
| Sahip kararı (bağlayıcı) | "Varsayılan rol şablonu yalnız yeni kiracılarda uygulanır. Sonrasında rollerin içeriğine yalnız yönetici karar verir; sistem kendiliğinden yetki eklemez." |

## 1. Ne değişti

- **`DefaultRoleProvisioningRule`** (Domain, yeni). Tek karar noktası:
  - "Bu çağrı rolü oluşturduysa şablonun tamamı uygulanır, oluşturmadıysa hiçbir şey."
  - `GrantsFor(roleName, catalog, roleCreatedByThisCall)`.
  - Denetim olayı adı `default_role_provisioned`. Tetikleyiciler: `tenant-activated`, `tenant-admin-invited`, `register`, `seed`.
- **`IRoleRepository.CreateSystemRoleIfMissingAsync`** (Mongo `RoleRepository`) atomik "oluşturdu mu" bilgisini veriyor:
  - upsert'ün `UpsertedId` değeri varsa rol bu çağrıda oluşturulmuştur;
  - eşleşen var olan rol ya da `DuplicateKey` hatası → oluşturulmadı.
  - Bu yeni üye arayüzde varsayılan gövdeyle (hata fırlatan) tanımlı. Böylece hiç sağlama yapmayan 12 test sahtesi değişmedi.
- **`RoleProvisioningService.EnsureDefaultRolesAsync(tenantId, trigger)`** — Admin ve Viewer için:
  - Kiracıda olmayan rol oluşturulur, şablonun tamamı verilir, bir denetim satırı yazılır.
  - Var olan role **dokunulmaz**: anahtar eklenmez, çıkarılmaz.
  - Denetim `IRbacAuditRecorder` → `authAuditLogs` üzerinden yazılıyor. İçerik: kiracı, `roleId`, `roleName`, `permissionCount`, `trigger`, `actorId` (iç olaylarda null).
- **`InternalEventsController`** (`tenant-activated`, `tenant-admin-invited`) ve **`RegisterCommandHandler`** kendi tetikleyicilerini geçiyor. Davet akışı değişmedi: Admin rolü yoksa oluşturuluyor, davet edilen kişi Admin'e atanıyor.
- **`DataSeeder`** (tohum kiracı `00000000-…-0001`):
  - `EnsureRole` artık "bu açılış oluşturdu mu" bilgisini döndürüyor. Ekleme `uq_roles_tenant_name_active` ile korunuyor; eşzamanlı ikinci açılış rolü "oluşturulmadı" olarak okur.
  - `AssignBaselineAsync` aynı kuralı kullanıyor. SuperAdmin, Admin ve Viewer şablonu **yalnız ilk tohumlamada** alıyor; bu sırada denetim satırı yazılıyor.
  - Sonraki açılışlar rolleri tamamlamıyor.
  - **Tohum belgesi** (`DataSeeder` sınıf açıklaması): geliştirme ortamında şablona ya da kataloğa sonradan eklenen anahtarlar artık **elle verilir** (rol ekranı ya da rol-izin API'si). Taze bir geliştirme veritabanı yine şablonun tamamıyla başlar.
- **Değişmeyenler (yalnız ölçüldü, §5):**
  - Admin self-servis uzlaştırıcısı;
  - tohum dosyasındaki MOD-0251, 97c5 ve yetkilendirilmiş modül grant yolları;
  - olaylardaki yetkilendirme senkronu;
  - şablonun içeriği (`DefaultRolePermissionTemplate`).

## 2. "Kiracı yeni mi" — tasarım (madde 2)

**Ölçüm (taban):**
- Kararın tek bir yeri yoktu.
- `RegisterCommandHandler` "Viewer rolü yoksa" diye bakıyordu.
- Olaylar her seferinde şablonu "eksikleri ekle" diyerek yeniden uyguluyordu.
- `DataSeeder` her açılışta tamamlıyordu.
- `tenant-activated`'ın çift kayıt koruması kiracıya değil olay kimliğine (inbox `EventId`) bağlıydı.
- Aynı anda iki olay geldiğinde bunlardan biri `rolePermissions` benzersiz indeksinde **DuplicateKey** ile 500 dönüyordu (önce-kırmızı S7).

**Karar:** "yeni"lik kiracı düzeyinde değil **rol düzeyinde** ve **tek bir atomik olaya** bağlı: rolü bu çağrı mı ekledi?
- Kaynak, var olan benzersiz kısmi indeks `uq_roles_tenant_name_active` (TenantId + Name, `IsDeleted = false`). Eşzamanlı çağrılardan **yalnız biri** ekler.
- Kural kodu tek yerde: `DefaultRoleProvisioningRule.GrantsFor`. Hem çalışma zamanı hem tohum aynı kuralı çağırıyor.
- İşaret, sürüm alanı ya da yeni koleksiyon yok. **Rolün varlığı işaretin kendisi.**

**Sonuçları:**
- Şablon bir role en fazla bir kez uygulanır.
- Var olan bir rol hiçbir olayda değişmez.
- Bir varsayılan rolü eksik olan eski kiracı yalnız o rolü şablondan alır.
- Davet akışı Admin rolünü bulur.

**Bilinen sınır (O-ROLE1-3):** rol eklendikten sonra grant'ler yazılırken süreç çökerse rol eksik kalır ve bir sonraki olay onu **tamamlamaz**. Bu güvenli yönde: daha az yetki demek. Bu durumda denetim satırı da yazılmaz, eksiklik buradan görülebilir; yönetici tamamlar.

## 3. Testler — önce / sonra

| süit | taban (03f7cc1ba) | sonra (bf9b99e91) | uyarı (`--no-incremental`) |
|---|---|---|---|
| AuthService Application (`DITEN_ITEST_MONGOD_BIN_DIR`) | 1063/1063 | **1072/1072** (+9) | 6 → 6 |
| Web | 699/699 | 699/699 | 21 → 21 |
| Mimari | 18/18 | 18/18 | — |

- `AccountKindAcceptanceHost.Tests` koşulmadı.
- Platform, Inventory ve gateway koduna dokunulmadı.
- Ara koşuda mimari testi `MongoTestDatabaseGuardTests.NoTestBuildsThePlatformSchema` kırmızı oldu ve uyarı sayısı 7'ye çıktı. İkisi de yeni testtendi:
  - test atılık bir veritabanına bütün şemayı kuruyordu;
  - `Assert.Equal(…, true)` kullanıyordu.
  - Düzeltme: olay testleri artık host'un üretim indeksli paylaşılan veritabanını kullanıyor (DB-010), yalıtım taze kiracı kimliğiyle. Önce-kırmızı ve sabotajlar bu son testlerle **yeniden** ölçüldü.

## 4. Senaryolar (security-agent, önce-kırmızı)

Yeni testler (`Roles/DefaultRoleTemplateOnceTests.cs`) üretim giriş noktalarından geçiyor:
- `InternalEventsController` + `RoleProvisioningService` + Mongo depoları;
- `DataSeeder.SeedAsync`;
- gerçek Mongo (`AuthTestHost`'un kendi geçici mongod'u).

Yalnız dış kenarlar sahte: iç anahtar kontrolü ve Platform istemcileri.

Önce-kırmızı **ayrı bir taban worktree'sinde** koşuldu (03f7cc1ba + yeni test dosyası). Tek dolgu: test yardımcısında tabanın 3 parametreli kurucusu. Sonuç: **8 kırmızı, 1 yeşil.**

| # | senaryo | tabanda | ROLE1'de |
|---|---|---|---|
| S1 | Mevcut kiracıda Viewer'dan anahtar çıkarıldı → `tenant-admin-invited` | **geri eklendi** | eklenmedi |
| S2 | Aynısı, ikinci `tenant-activated` (yeni EventId) ile | **geri eklendi** | eklenmedi |
| S3 | Tohum kiracıda Viewer ve SuperAdmin'den anahtar çıkarıldı → yeniden açılış (`SeedAsync` ikinci kez) | **geri eklendi** | eklenmedi |
| S4 | Şablona sonradan giren anahtar → mevcut kiracı, iki olay | **mevcut kiracıya gitti** | gitmedi; Admin ve Viewer aynı |
| S5 | Yeni kiracı → tüm şablon rolleri, tüm anahtarlar, rol başına bir denetim satırı | roller tam, **denetim yok** | tam + 2 satır (`permissionCount`, `trigger`) |
| S6 | Mevcut kiracıda HİÇ olmayan Viewer → davet | Viewer oluştu ama **Admin'e çıkarılan anahtar geri eklendi** | yalnız Viewer oluştu (şablon + 1 denetim satırı); Admin'e dokunulmadı |
| S7 | Aynı anda üç olay (2× activated + invited), yeni kiracı | **DuplicateKey ile çöktü** | her rol bir kez; anahtar sayısı = şablon; rol başına bir denetim satırı |
| S8 | Davet edilen yönetici, yeni ve mevcut kiracıda Admin'e atanıyor | yeşil (koruyucu) | yeşil |
| S9 | İlk tohum açılışı şablonu uygular; tohum rolü başına bir denetim satırı; ikinci açılış yeni satır yazmaz | **denetim yok** | 3 satır (`trigger: seed`) |

## 5. Admin uzlaştırıcısı ve diğer otomatik yollar — ölçüm (madde 4, DEĞİŞTİRİLMEDİ)

| mekanizma | nerede / ne zaman | ne yapıyor | mevcut kiracılara anahtar ekliyor mu |
|---|---|---|---|
| **Admin self-servis uzlaştırıcısı** `ReconcileTenantAdminSelfServiceGrantsAsync` → `TenantAdminSelfServiceReconciler.PlanMissingGrants` | `DataSeeder.SeedAsync`, **her AuthService açılışında**, **bütün kiracılar** | Adı tam "Admin" olan her role `TenantSelfServicePermissions` kümesinden eksik olanları ekliyor: `platform.tenant-security.read`, `platform.tenant-security.manage`, `platform.tenant-navigation.manage`, `auth.users.lookup`. Yalnız ekliyor, çıkarmıyor. Etkilenen kiracının rol sürümünü artırıyor | **Evet.** Yoklamada ölçüldü: yönetici Admin'den `auth.users.lookup`'ı çıkardı → yeniden açılışta **geri geldi** (§6). Yani bu dört anahtar için "yönetici karar verir" kuralı bugün geçerli değil |
| Yetkilendirilmiş modül senkronu `SyncEntitledModulesBestEffortAsync` → `EntitlementPermissionSyncService` | `tenant-activated`, `tenant-admin-invited` ve yetkilendirme olayları (`EntitlementSyncConsumer`) | Kiracının lisanslı modüllerinin anahtarlarını Admin / Viewer'a `GrantSource.Module` olarak veriyor; lisansı biten modülünkileri geri alıyor | Evet, modül lisansına bağlı. Şablondan ayrı bir iş kuralı |
| Tohum dosyasındaki diğer grant'ler: `AssignMod0251ModuleGrantsAsync`, `…PlatformIntegrationGrantsAsync`, 97c5 grant'leri, `SeedDefaultTenantEntitledModuleGrantsAsync` | her açılışta, tohum kiracı ve 97c5 | Belirli anahtarları eksikse ekliyor | Evet, yalnız tohum kiracı ve 97c5. Yoklamada tohum Admin'inde 76 anahtar var; şablon 43, kalan 33 bu yollardan |

Bunların hepsi şablon dışı. Bu WP bunları değiştirmedi; karar CT'nin ve sahibin (O-ROLE1-1, O-ROLE1-2).

## 6. Sabotajlar (K3)

Her sabotaj: tek metin değişikliği → derle → `DefaultRoleTemplateOnceTests` + `RoleProvisioningServiceTests` (17 test) → geri al. Geri almadan sonra 17/17 yeşil ve `git diff` özeti aynı.

| # | sabotaj | kırmızı | testler |
|---|---|---|---|
| (a) | `tenant-admin-invited`'da "eksikleri tamamla" geri geldi (`created \|\| trigger == TenantAdminInvited`) | **4** | S1, S4, S6, S7 |
| (b) | aynısı `tenant-activated` için | **3** | S2, S4, S7 |
| (c) | tohum açılışında tamamlama geri geldi (`GrantsFor(…, true)` DataSeeder'da) | **1** | S3 |
| (d) | "yeni kiracı" kararı hep "yeni" (`true ? …` kuralda) | **6** | S1, S2, S3, S4, S6, S7 |

## 7. Çalışma zamanı yoklaması

**Kurulum:**
- Kendi AuthService'im (15056, bu dal, Release) ve yalıtılmış mongod (EphemeralMongo, rastgele port).
- Davetin ihtiyaç duyduğu tek Platform okuması (kiracı giriş ayarları) için küçük bir taklit (14057). Tabanda da davet bu okumaya bağlı; taklit olmadan 500 döndü.
- Filoya hiç istek gönderilmedi. İç anahtar ve JWT sırrı bellekte, yazdırılmadı. Davet yanıtındaki geçici şifre okunmadan atıldı.

Sonuç `probe-results.json`, **9/9 OK**:

| adım | sonuç |
|---|---|
| İlk açılış (taze veritabanı) | Tohum kiracıda SuperAdmin 272 / Admin 43 / Viewer 30 anahtar; 3 `default_role_provisioned` satırı (`trigger: seed`) |
| Yeni kiracı → `tenant-activated` | 200; Admin 43, Viewer 30 anahtar; 2 denetim satırı (`trigger: tenant-activated`) |
| Yönetici Viewer'dan `auth.roles.read`'i çıkardı → `tenant-admin-invited` | 200 `processed`; anahtar **geri gelmedi** (Viewer 29); davet edilen Admin'e atandı (1 satır); denetim satırı hâlâ 2 |
| İkinci `tenant-activated` | 200; hâlâ geri gelmedi |
| Açılıştan önce: tohum Viewer'dan `auth.roles.read` ve yoklama kiracısı Admin'inden `auth.users.lookup` çıkarıldı → AuthService **yeniden başlatıldı** | yoklama kiracısının Viewer anahtarı gelmedi (29); tohum Viewer 33 → **32**; yeni denetim satırı yok (tohum 3, kiracı 2) |
| **Ölçüm (madde 4):** Admin'den çıkarılan `auth.users.lookup` | yeniden açılışta **geri geldi** (uzlaştırıcı) |

Mongo okuması (`db-read.json`):
- Roller: tohum SuperAdmin / Admin / Viewer, yoklama kiracısı Admin / Viewer; hepsi `IsSystem`.
- Anahtar sayıları: yoklama Admin 43, Viewer 29; tohum SuperAdmin 272, Admin 76, Viewer 32.
- 5 denetim satırı, rol başına bir tane.

## 8. Süreçler (PID)

**Sahibin filosu** — istek gönderilmedi, hiçbir sürece dokunulmadı.

| port | önce (13:20) | sonra (13:54) |
|---|---|---|
| 4010 | 19992 | 19992 |
| 5000 | 38952 | 38952 |
| 5001 | 32364 | 32364 |
| 5056 | 30208 | 30208 |
| 5057 | 23172 | 23172 |
| 5058 | 31240 | 31240 |
| 5059 | 37040 | 37040 |
| 5060 | 44056 | 44056 |
| 5061 | 20552 | 20552 |
| 5066 | 16424 | 16424 |
| 27017 | 4904 | 4904 |

**Kendi süreçlerim:**
- 1. yoklama denemesi (Platform taklidi yokken): AuthService 30248.
- 2. deneme: Platform taklidi 34016, AuthService 42988 (1. açılış) ve 36540 (yeniden başlatma).
- Hepsi kapandı (0 canlı). Yoklama mongod'u kapatıldı.
- Test süitleri kendi geçici mongod'larını açıp kapattı.
- Önce-kırmızı worktree'si `git worktree remove` ile kaldırıldı.

## 9. Açık noktalar ve varsayımlar

| kimlik | konu |
|---|---|
| **O-ROLE1-1** | Admin self-servis uzlaştırıcısı her açılışta bütün kiracıların Admin rolüne dört anahtarı geri koyuyor (§5, yoklamada ölçüldü). Sahip kararıyla ("sistem kendiliğinden yetki eklemez") çelişiyor. Değiştirilmedi; karar CT / sahip |
| **O-ROLE1-2** | Yetkilendirilmiş modül senkronu ve tohum dosyasındaki modül / 97c5 grant'leri de var olan rollere anahtar ekliyor. Bunlar lisans ve geliştirme yolları, şablon değil; değiştirilmedi |
| O-ROLE1-3 | Rol oluşturulduktan sonra grant'ler yazılırken çökme olursa rol eksik kalır ve tamamlanmaz (güvenli yön). Denetim satırının yokluğu bunu gösterir |
| O-ROLE1-4 | `docs/guides/operations/how-to-add-a-module.md` 3. adım: "Baseline rol grant'ları: `DefaultRolePermissionTemplate.cs`". Artık şablon değişikliği yalnız **yeni** kiracılara ve yeni tohum veritabanına ulaşıyor; mevcut geliştirme veritabanında elle verilir. Rehber bu WP'nin diff kapsamı dışında; güncellenmesi önerilir |
| O-ROLE1-5 | Davet ucu Platform'un giriş ayarları okumasına bağlı. Platform yoksa 500 dönüyor ve **rol sağlama bu hatadan önce çalışıyor**. Tabandan gelen davranış, değişmedi |
| ASSUMPTION-0018-ROLE1-01 | "Yeni" rol düzeyinde ölçülüyor (bu çağrı rolü ekledi mi). Kiracı düzeyinde ayrı bir işaret yok. Eksik bir varsayılan rolü olan eski kiracı o rolü şablondan alıyor (NE-1) |
| ASSUMPTION-0018-ROLE1-02 | SuperAdmin de tohum kiracıda şablon rolü sayıldı: ilk tohumlamadan sonra kataloğa giren anahtarlar ona da elle verilir (NE-1, "tohum kiracı") |

**Kapananlar:** FU-0022-SEED-02 (`tenant-admin-invited` / `tenant-activated` mevcut kiracıyı tamamlamıyor); FU-0022-SEED-01 (tohum kiracı da artık yalnız ilk tohumlamada). CT-D10'daki "mevcut kiracılara otomatik grant yok" kuralı şablon yolları için gerçekten geçerli. İstisna: O-ROLE1-1'deki self-servis anahtarları.

**Push öncesi:** `git fetch origin main` → `git merge origin/main --no-ff` → derleme + süitler → push yalnız `git rev-list --count HEAD..origin/main` = 0 iken.
