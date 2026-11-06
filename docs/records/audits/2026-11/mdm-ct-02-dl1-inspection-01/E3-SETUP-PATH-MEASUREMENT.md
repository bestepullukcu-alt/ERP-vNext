# E3 kurulum yolu ölçümü — MOD-0290-FU06 DL-1 (`kurulum()`)

- **Tarih:** 2026-11-06 · **Dal:** `chore/ct/mdm-ct-02` @ `e70ea72ba` · **Yöntem:** salt kod okuması. Servis, dotnet, mongod başlatılmadı; hiçbir şey koşulmadı.
- **Hedef:** `execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py` `kurulum()` (bugün `OwnerStop` ile durur) için boş veritabanında platform yöneticisi → kiracı → kullanıcı/rol → kiracı girişi yolunu kaynakla göstermek. Paket: `MOD-0290-FU06-product-master-read-permission-declaration.md` §18 E3 Y1–Y6.
- **Etiketler:** "kod okuması" = kaynakta okundu, koşulmadı. "ölçülmedi" = kaynakta bulunamadı ya da açılmadı.
- Bu notta jeton, parola, anahtar değeri yoktur.

## 1. İlk platform yöneticisi ve jetonu

### 1a. Önerilen yol: `FirstPlatformAdministrator` aracı → set-password → platform girişi (otomatikleştirilebilir: **evet**, kod okuması)

| # | Adım | Uç / komut | Gövde / girdi | İzin / jeton | Kaynak |
|---|---|---|---|---|---|
| 1 | Araç | `dotnet exec …/Diten.AuthService.FirstPlatformAdministrator.dll --email <adres> --frontend-base-url http://localhost:5001 --link-file <YENİ dosya, var olan dizinde>` (`--user-name`, `--display-name` isteğe bağlı) | Ortam: `DITEN_AUTH_MONGO_CONNECTION`, `DITEN_AUTH_MONGO_DATABASE`, `DITEN_PLATFORM_MONGO_CONNECTION`, `DITEN_PLATFORM_MONGO_DATABASE`, `JwtSettings__Secret` (Auth'unkiyle **aynı**: bağlantının özeti bu sırla hesaplanır) | Doğrudan Mongo yazar, HTTP yok. Çıkış: 0 oluştu · 1 bağlantı · 2 kullanım · 3 reddedildi (platform hesabı ya da kayıt zaten var) · 4 yarıda | `services/Diten.AuthService/tools/Diten.AuthService.FirstPlatformAdministrator/FirstPlatformAdministratorCommand.cs` · `RunAsync`, `TryParse` |
| 1→ | Ne yazar | Auth `users`: kiracı `00000000-…-0001`, `PlatformActorType=platform_admin`, **pasif**, parola kullanılamaz, `RequirePasswordChange`, yalnız bağlantı özeti (24 saat). Platform `PlatformAdministrators`: aynı id, `ActorType=PlatformAdmin`, `Status=Active`, `Roles=[SuperAdmin]`, `InvitationStatus=PendingInvitation` | — | Aynı ortamda platform hesabı/kaydı varsa hiçbir şey yazmaz (çıkış 3) | `services/Diten.AuthService/src/Diten.AuthService.Persistence/Operations/FirstPlatformAdministratorAccount.cs` · `FindBlockerAsync`, `TryCreateAsync`; `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/FirstPlatformAdministratorRecord.cs` · `TryCreateAsync` |
| 1→ | Bağlantı biçimi | Dosyada tek satır: `{base}/platform/reset-password?email=<kaçışlı>&token=<kaçışlı>` | Betik `urllib.parse.parse_qs` ile `token`'ı okur, dosyayı hemen siler; `--out` altına yazılmaz | — | `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformAuthEmailService.cs` · `BuildPlatformPasswordResetUrl` |
| 2 | Parola belirleme | `POST http://localhost:5056/api/platform-auth/reset-password` (Gateway'de `/api/platform-auth/{everything}` → 5056 da var) | `{"email","token","newPassword"}` (`PlatformResetPasswordRequest`) | Anonim. Bağlantı özeti eşleşmeli + Platform kaydı `Active` (Auth → Platform `GET api/internal/platform-administrators/status`, iç anahtar). Parola politikası: platform kiracısının giriş ayarları; varsayılan en az 10, büyük harf, özel karakter (`TenantLoginSettings`). Başarı: 204; hesap etkin, e-posta onaylı, zorunlu değişim temizlenir | `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs` · `ResetPassword`; `…/Api/Models/RequestModels.cs`; `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalPlatformAdministratorsController.cs` · `GetStatus` |
| 3 | Platform girişi | `POST http://localhost:5056/api/platform-auth/login` | `{"email","password","rememberMe":false}` (`PlatformLoginRequest`; e-posta yerine kullanıcı adı da olur) | Anonim. Platform kaydı `Confirmed` olmalı (yoksa 401/503). Roller kayıttan eşlenir (`ConvergeAtMintAsync` → `SuperAdmin`); SuperAdmin/Admin/BillingAdmin/SupportAdmin/ReadOnly yoksa 403. Yanıt `data.accessToken`, `actor_type=platform_admin`, **ömür 15 dk** | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/PlatformLoginCommandHandler.cs` · `Handle` |

**Jetondaki izinler:** girişte, platform kiracısındaki `SuperAdmin` rolünün izinlerinden okunur. Platform'un `platform.*` anahtarları, Platform açılışında Auth'a gönderilir ve `SuperAdmin`'e verilir: `PlatformPermissionAutoRegistrationWorker.ExecuteAsync` → `/internal/permissions/sync` → `FullCatalogPermissionGrantService.GrantToFullCatalogRolesAsync`. Bu yüzden platform girişi Platform günlüğünde `Permission auto-registration complete.` satırından **sonra** yapılmalı (kod okuması). `SuperAdmin` rolünün boş veritabanında `DataSeeder` ile oluştuğu, `AdminAccountSeed` belgesinden çıkarım ("system seed … every environment"); `DataSeeder.cs` satırı açılmadı, **kısmi inceleme**.

**Platform API yetkisi:** `PlatformAdminOnly` politikası = `actor_type=platform_admin` + kayıt onayı (`IPlatformActorTenantScope.IsUnrestricted`): `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` (≈satır 966). `[HasPermission]`, platform aktöründe kaydı okur (`Active`, silinmemiş) ve ilk istekte `InvitationStatus=Accepted` yazar; ardından jetondaki `permission` iddiasına bakar: `services/Diten.Platform/src/Diten.Platform.API/Security/HasPermissionAttribute.cs` · `OnAuthorizationAsync`.

### 1b. Alternatifler

| Yol | Sonuç | Otomatik mi |
|---|---|---|
| Geliştirme tohumu: `DevSeeds__PlatformAdmin=true` (Auth `DevAccountSeed.SeedPlatformAdministratorAsync` + Platform `PlatformAdministratorSeed.EnsureSeededAsync`, ikisi de yalnız `Development` + bu bayrak) | `admin@diten.com`, sabit id `11111111-…`, depodaki dev parola **özeti**. Parolanın kendisi depoda yok ("local setup note"). Ayrıca araç bu hesabı görünce reddeder (çıkış 3) | **hayır**: parola bilinmiyor; depo dev özetini atılabilir ortama taşır |
| `POST /api/platform-auth/platform-admins/provision` (iç anahtar, `AllowAnonymous`) | Yalnız Auth hesabı; Development'ta yanıtta `setupUrl`. Platform kaydı yazılmaz, giriş kayıt onayı ister | **hayır** (tek başına) |
| `DevSeeds__TenantAdmin` | `kiraci.admin@diten.com` platform kiracısına (`0…01`) yazılır; yeni kiracı için işe yaramaz | hayır |

## 2. Kiracı, plan ve modül hakkı

| # | Adım | Uç | Gövde | İzin | Kaynak |
|---|---|---|---|---|---|
| 1 | Plan id | `GET http://localhost:5057/api/platform/subscription-plans` | — | `PlatformActor` + `platform.subscription-plans.read` | `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionPlansController.cs` |
| 1→ | Plan tohumu | Platform açılışında `FREE` (deneme 14 gün; `users.max`=5, `modules.max`=3), `STARTER` (25 / 10), `PROFESSIONAL` (100 / 25), `ENTERPRISE` (1000 / 100); hepsinde `IncludedModuleKeys` boş, yani ürün modülü planda değil | — | — | `…/Infrastructure/Persistence/Configurations/SubscriptionPlanStartupInitializer.cs` · `StartAsync`; `…/Application/Features/SubscriptionPlans/Handlers/CommandHandlers/SeedDefaultSubscriptionPlansCommandHandler.cs` · `Handle`, `BuildQuotas` |
| 2 | Kiracı | `POST http://localhost:5057/api/admin/tenants` (Gateway'de `/api/admin/tenants` → 5057; `X-Tenant-Id` başlığı **gönderilmez**, admin yolunda 400) | Zorunlu: `name`, `domain` (nokta içermeli, ör. `e3-t1.example.test`), `displayName`, `planId`, `tenantType` (**sayı**: Customer=0, Demo=1, Internal=4; Trial ve Paid reddedilir; Platform'da genel string enum dönüştürücü bulunamadı), `initialAdmin` `{firstName,lastName,email}`. Slug addan türetilir; `{slug}.ditenteknoloji.com` benzersiz olmalı | `PlatformActor` + `PlatformAdminOnly` | `…/API/Controllers/Admin/TenantsController.cs` · `RegisterTenant`; `…/Application/Features/Tenants/Commands/RegisterTenantCommand.cs`; `…/Validators/RegisterTenantCommandValidator.cs`; `services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantType.cs`, `InitialAdminInfo.cs` |
| 2→ | Kiracı oluşunca | Abonelik + kota satırları (`InitializeSubscriptionQuotasAsync`, `CurrentValue=0`), kiracı `Active`, `TenantCreatedV1` yayımlanır, sonra **eşzamanlı HTTP** `tenant.activated` → Auth. Yanıt 201, `data` = kiracı id | — | — | `…/Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs` · `Handle`, `TryCompleteProvisioningAsync` |
| 3 | Yönetici satırı id | `GET http://localhost:5057/api/admin/tenants/{id}/admin-users` | — | `PlatformActor` | `TenantsController.GetAdminUsers` |
| 4 | Modül hakkı | `POST http://localhost:5057/api/platform/tenants/{tenantId}/commercial/module-entitlements` | `{"moduleCode","source","isEnabled","expiryDateUtc","reason","rowVersion":null}` (`TenantModuleEntitlementRequest`). `source`: `EntitlementSource` üzerinde `JsonStringEnumConverter` var, `"Addon"` da `1` de kabul edilir (betiğin "string enum converter yok" yorumu bu enum için yanlış). `reason` yalnız ManualOverride ya da `isEnabled=false` için zorunlu; bitiş gelecekte olmalı | `platform.tenants.commercial.subscription.assign` + `PlatformAdminOnly`. Okuma: `…subscription.read` | `…/API/Controllers/Platform/TenantModuleEntitlementsController.cs` · `Add`; `…/Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs` · `Handle`; `…/Validators/TenantModuleEntitlementValidators.cs`; `services/Diten.Platform/src/Diten.Platform.Domain/Enums/EntitlementSource.cs` |

**`modules.max` yeni kiracıda (kod okuması, koşulmadı): geçer.** Ekleme işlemi önce `RecalculateEntitlementAsync` ile sayacı gerçek etkin hak sayısına (yeni kiracıda 0) getirir, sonra `TryConsumeEntitlementAsync(+1)` çalışır. `modules.max` sayılan anahtar değildir: `TryConsumeAtomicAsync` sınırı plandan alır (STARTER 10). Koşullar şunlar: abonelik `Active` ya da süresi geçmemiş `Trialing` (`IsSubscriptionEligible`), kota satırı kiracı kaydında yazılmış. Ret olursa kod `QUOTA_LIMIT_EXCEEDED` (409) ya da `QUOTA_USAGE_NOT_FOUND` (404) döner; betiğin `"quota"` araması ikisini de yakalar. Kaynak: `…/Application/Features/Quotas/Services/QuotaService.cs` · `TryConsumeCoreAsync`, `ComputeCurrentUsageAsync`, `IsCountedKey`; `…/Features/Quotas/QuotaContracts.cs`.

**Plan seçimi `STARTER` olmalı (FREE olmamalı):** `users.max` sayılan anahtardır (Auth'taki Active + Invited). FREE'de sınır 5'tir; kiracı 1'in 6 kullanıcısının sonuncusu `CreateUserCommandHandler` → `TryConsumeUserSeatAsync` ile reddedilir. Kaynak: `QuotaService.CountSeatedUsersAsync`, `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs` · `Handle`.

## 3. Kiracıda roller, ilk yönetici, kullanıcılar

### 3a. Varsayılan roller (kod okuması)
- `tenant.activated`, kiracı oluşturma isteğinin içinde eşzamanlı HTTP çağrısıdır (RabbitMQ değildir): `POST http://localhost:5056/internal/events/tenant-activated`, `X-Internal-Api-Key`. Auth yalnız `Admin` ve `Viewer` rollerini oluşturur (`RoleProvisioningService.DefaultRoles`), ardından `SyncEntitledModulesBestEffortAsync` çalışır. Planda modül yoksa boş liste döner ve hiçbir şey yapılmaz. Hata olursa yalnız uyarı yazılır, kiracı yine oluşur. Kaynak: `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/AuthServiceTenantActivationNotifier.cs` · `NotifyActivatedAsync`; `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs` · `TenantActivated`; `…/Application/Common/Services/RoleProvisioningService.cs` · `EnsureDefaultRolesAsync`.
- Platform tarafının ayarları: `AuthService:BaseUrl` (Development'ta `http://localhost:5056`) ve `AuthService:InternalApiKey`. Betik ikincisini veriyor. Auth bu anahtarı `InternalEventAuth:ApiKey` ile karşılaştırır.
- **Adanmış üç rol** (`ProductDataSteward`, `ProductIdentityApprover`, `ProductIdentityRetirementSteward`) varsayılan rol değildir. Ürün modülü hakkı eşitlenince `EntitlementPermissionSyncService` → `ResolveDedicatedRolesAsync(createMissing: true)` ile **sistem rolü** olarak oluşurlar. Yani ancak Y2a'daki `entitlement.sync.applied` satırından **sonra** atanabilirler. Kaynak: `…/Application/Common/Services/EntitlementPermissionSyncService.cs` (≈satır 639, 796–860); `…/ProductIdentityLifecycleEntitlementGrantProfile.cs` · `StewardRole`, `ApproverRole`, `RetirementStewardRole`.

### 3b. İlk kiracı yöneticisi

| Yol | Akış | SMTP gerekir mi | Otomatik mi |
|---|---|---|---|
| **A (önerilen):** Auth iç kapısı | `POST http://localhost:5056/internal/events/tenant-admin-invited`, başlık `X-Internal-Api-Key`, gövde `{"tenantId","adminUserId","tenantCode","tenantName","email","name","trigger":"operator-invite"}` (`TenantAdminInvitationProvisioningRequest`). Yanıt `{userProvisioned, setupToken, setupExpiresAtUtc, message}`; **`setupToken` açık metindir** (Platform'a dönmesi için). Hesap yoksa oluşturur (pasif, Admin rolü + üyelik); hesap varsa `operator-invite` onu sıfırlar ve yeni bağlantı verir. Auth aynı adımda `EnsureDefaultRolesAsync` ve hak eşitlemesini de çalıştırır | hayır | **evet** |
| B: Platform "Davet" | `POST http://localhost:5057/api/admin/tenants/{id}/admin-users/{adminUserId}/invite` (`PlatformActor`). Bağlantı yanıtta (`loginUrl`) **yalnız** Development'ta ve e-posta kuyruğa giremediyse döner | dolaylı | **kısmi**: bildirim hattının sonucuna bağlı |
| C: `TenantCreatedV1` olayı | Platform `TenantLifecycleNotificationConsumer` → Auth `tenant-admin-created` (yalnız oluşturur) → bağlantı yalnız e-postada (`tenant.invite.email`) | evet (Mailpit 1025) | hayır: bağlantı betiğe ulaşmaz; paylaşılan Mailpit'e yazar |

A ile C'nin sırası fark etmez. C önce çalışırsa A hesabı sıfırlar; A önce çalışırsa C (yalnız oluşturur) `account_exists` döner ve hiçbir şeyi değiştirmez. İkisi aynı anda yazarsa A `409 … try again` döner, yeniden denenir. Kaynak: `InternalEventsController.TenantAdminInvited`, `ProvisionTenantAdministratorAsync`; `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/AdminUserInvitationService.cs` · `InviteAsync`; `…/Application/Features/Tenants/Handlers/InviteTenantAdminUserCommandHandler.cs` · `Handle` (satır ≈194); `…/Infrastructure/Eventing/TenantLifecycleNotificationConsumer.cs`.

Not: `adminUserId` ile `tenantCode` Auth'ta okunmaz (`ProvisionTenantAdministratorAsync` yalnız `TenantId`, `Email`, `Name`, `Trigger` kullanır), yine de 3. adımdaki gerçek değerler gönderilir.

### 3c. Parola, kullanıcı, rol

| # | Adım | Uç | Gövde | İzin | Kaynak |
|---|---|---|---|---|---|
| 1 | Yöneticinin parolası | `POST http://localhost:5000/api/users/set-password` (Gateway ve Auth'ta genel yol; başlık gerekmez) | `{"email","token","newPassword"}` (`SetTenantPasswordRequest`) | Anonim, hız sınırlı. Hesap etkinleşir, e-posta onaylanır, zorunlu değişim temizlenir. Politika kiracının giriş ayarlarından (varsayılan en az 10) | `…/AuthService.Api/Controllers/UsersController.cs` · `SetPassword`; `…/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs` |
| 2 | Yönetici girişi | §4 | | | |
| 3 | Kullanıcı oluşturma | `POST http://localhost:5000/api/users` (Bearer kiracı yöneticisi; Gateway `X-Tenant-Id`'yi JWT'den koyar) | `{"email","password","firstName","lastName"}`; `password` verilirse hesap **hemen etkin** olur, e-posta gitmez; `accountKind` gönderilmez (ayrı izin ister) | `auth.users.create`: `access-governance` modülünde; tenant `Admin` rolü bu modülü tam alır (`DefaultRolePermissionTemplate.AdminModules`) | `UsersController.Create`; `CreateUserCommandHandler.CreateSelfServiceAsync`; `…/Persistence/Seed/DataSeeder.cs` satır 447–451 |
| 4 | Rol listesi | `GET http://localhost:5000/api/roles` | — | `auth.roles.read` | `…/Api/Controllers/RolesController.cs` · `GetAll` |
| 5 | Rol verme | `POST http://localhost:5000/api/users/{id}/roles` | `{"roleId"}` (`AssignRoleRequest`) | `auth.users.assign-role`. Kiracıdaki her rol atanabilir; sistem rolü için ayrı ret bulunamadı. 204 | `UsersController.AssignRole`; `…/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs` · `Handle` |

Rolsüz kullanıcının girişi başarılıdır: "Login produced empty role set" uyarısı ve denetim satırı yazılır, jetonda izin olmaz (`LoginCommandHandler.HandleLoginSuccess`). Bu, "anahtarsız kullanıcı" için yeterlidir.

## 4. Kiracı kullanıcısının girişi

- `POST http://localhost:5000/api/tenant-auth/login`, gövde `{"email","password","rememberMe"}` (`TenantLoginRequest`). **Gövdede kiracı alanı yok.** Kaynak: `…/Api/Controllers/TenantAuthController.cs` · `Login`; `…/Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs` (`_tenantContext.TenantId`).
- Kiracı bilgisi **`X-Tenant-Id: <kiracı guid>` başlığıyla** gelir. Gateway sırası: JWT > başlık > alt alan; alt alan ancak ilk etiket **guid** ise çalışır (`ReadSubdomainTenant`), slug ile çalışmaz. Hiçbiri yoksa Development'ta `TenantResolution:DevBypassTenantId` = `00000000-…-0001` (platform kiracısı) uygulanır. Bu durumda giriş yanlış kiracıda aranır ve sessizce 401 alınır. Auth da aynı sırayı izler (JWT > başlık > dev bypass). Kaynak: `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` · `InvokeAsync`, `ReadSubdomainTenant`, `IsPublicEndpoint`; `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs`.
- Giriş, kiracının giriş ayarlarını Platform'dan okur (`ITenantLoginSettingsClient`); okunamazsa 401 `LoginUnavailable`. Varsayılan `MfaRequired=false`, `EmailLoginEnabled=true` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantLoginSettings.cs`).
- **Betik bulgusu:** `mdm_fu06_dl1_e3.py` · `login()` bu başlığı göndermiyor; `User` sınıfında kiracı alanı yok. Değiştirilmezse dev bypass yüzünden her ölçüm "kullanıcı girişi jeton vermedi" diye `OwnerStop` ile durur.
- Kiracı jetonunun ömrü `JwtSettings:AccessTokenExpirationMinutes` = 120 (`appsettings.json`); platform jetonununki 15 dk.

## 5. Ortam ayarları ve olay sırası

| Ayar | Servis | Neden | Durum |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Development` | hepsi | Betikte zaten var | — |
| `DevSeeds__*` **verilmez** | Auth, Platform | Varsayılan false. Verilirse araç reddeder (çıkış 3) | kod okuması |
| `TenantResolution__DevBypassEnabled=false` | Gateway, Auth | Başlık unutulursa platform kiracısına sessizce düşmek yerine 400 "Missing Tenant" | öneri; etkisi kod okuması |
| `Smtp__Enabled=false` | Auth, Platform | Önerilen yol e-posta istemez. Development ayarları `localhost:1025` (paylaşılan Mailpit); `TenantCreatedV1` davet e-postası oraya gidebilir | öneri; bildirim hattının bu bayrakla davranışı **ölçülmedi** (hata yumuşak: `AdminUserInvitationService.TryQueueInvitationEmailAsync` yakalar) |
| Araç ortamı | araç | `DITEN_AUTH_MONGO_CONNECTION`=betiğin `uri`, `DITEN_AUTH_MONGO_DATABASE`=`DB_AUTH`, `DITEN_PLATFORM_MONGO_CONNECTION`=`uri`, `DITEN_PLATFORM_MONGO_DATABASE`=`DB_PLATFORM`, `JwtSettings__Secret`=`E3_JWT_SECRET` | kod okuması |
| Derleme | — | `BUILDS` listesine `services/Diten.AuthService/tools/Diten.AuthService.FirstPlatformAdministrator/Diten.AuthService.FirstPlatformAdministrator.csproj` (net8.0, Exe) eklenmeli | eksik |
| Platform iç anahtar karşılaştırması | Platform | Auth → Platform iç çağrılarında (`status`, giriş ayarları, hak listesi) Platform'un hangi ayarı karşılaştırdığı açılmadı; betik Y3b'de de buna güveniyor | **ölçülmedi** |

**Olay sırası (kod okuması):**
1. Platform açılışı: plan tohumu, ardından `Permission auto-registration complete.` (Auth'ta `SuperAdmin` `platform.*` izinlerini alır). MDM öz-kaydı HTTP ile yapılır, RabbitMQ'dan geçmez; çıkan `GlobalApplicabilityChangedV1`'in Auth'ta tüketicisi yoktur (paket §23).
2. `POST /api/admin/tenants`: (a) aynı istek içinde HTTP `tenant.activated` → Auth `Admin`, `Viewer`; (b) `TenantCreatedV1` → RabbitMQ → Platform `TenantLifecycleNotificationConsumer` → Auth `tenant-admin-created` + davet e-postası. Auth `TenantCreatedV1` tüketmez.
3. `POST …/module-entitlements`: `TenantEntitlementAddedV1` işlem içi outbox'a yazılır → `OutboxPublisherWorker` (`Eventing__WorkerEnabled=true`) → RabbitMQ → Auth `EntitlementSyncConsumer` (Grant) → `entitlement.sync.applied` → Admin/Viewer izinleri + adanmış üç rol oluşur.
4. `PATCH …/expiry`: `TenantEntitlementExpiryUpdatedV1` → Reconcile → `entitlement.sync.applied`. Kaynak: `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs` · `ConsumeAsync`.

## 6. Ölçülen / ölçülemeyen özeti

| Madde | Durum | Otomatik |
|---|---|---|
| 1 Platform yöneticisi → jeton | ölçüldü (kod) | **evet** (araç + reset-password + login) |
| 1 Dev tohum yöneticisi | ölçüldü (kod) | hayır (parola depoda yok) |
| 2 Kiracı + plan | ölçüldü (kod) | evet |
| 2 Hak satırı + `modules.max` | ölçüldü (kod): STARTER'da geçer; koşulmadı | evet |
| 3 Varsayılan roller / ilk yönetici / kullanıcı + rol | ölçüldü (kod) | evet (yol A) |
| 3 Adanmış üç rol | ölçüldü (kod): ancak hak eşitlemesinden sonra var | evet, sıra şartıyla |
| 4 Kiracı girişi | ölçüldü (kod): `X-Tenant-Id` başlığı | evet; betikte değişiklik gerekir |
| 5 `SuperAdmin` boş veritabanında | **kısmi** (`DataSeeder` satırı açılmadı) | — |
| 5 Platform iç anahtar adı, `Smtp__Enabled=false` etkisi | **ölçülmedi** | — |

## 7. `kurulum()` için önerilen sıra

Betikte yazılacak adımlar, sırayla. Parola ve jetonlar yalnız bellekte tutulur; `--out` altına ve çıktıya yazılmaz (betikteki `JWT_LIKE` süzgeci korunur).

0. **Önkoşul değişiklikleri:** `BUILDS` listesine aracın csproj'u eklenir. `service_env`: Auth ve Platform'a `Smtp__Enabled=false`, Gateway ve Auth'a `TenantResolution__DevBypassEnabled=false` verilir (öneri). `User` sınıfına `tenant: str` eklenir. `login()` `headers={"X-Tenant-Id": user.tenant}` gönderir.
1. **Hazır bekle:** Platform günlüğünde `Permission auto-registration complete.` görülür (≤ `START_WAIT_SECONDS`); görülmezse kuleye dur.
2. **Platform yöneticisi:** `secrets.token_urlsafe` ile güçlü parola üretilir (büyük harf + küçük harf + rakam + özel karakter, ≥16). Geçici dizinde yeni bir `link` yolu belirlenir. Araç çalıştırılır (`dotnet exec <tools bin>/Diten.AuthService.FirstPlatformAdministrator.dll --email e3-platform-admin@e3.example.test --frontend-base-url http://localhost:5001 --link-file <yol>`, §5 ortamı); çıkış 0 değilse dur. `token` `parse_qs` ile okunur, dosya silinir.
3. `POST :5056/api/platform-auth/reset-password` → 204 beklenir.
4. `POST :5056/api/platform-auth/login` → `accessToken`. Kontrol: `token_permissions` içinde `platform.tenants.commercial.subscription.assign` var. Yoksa dur (A1 eşitlemesi eksik). Kimlik bilgisi `Setup`'ta tutulur; `platform_token()` yardımcısı her çağrıda yeniden girer (**15 dk ömür**: Y4a `PATCH` ve Y5 `POST` öncesi şarttır).
5. `GET :5057/api/platform/subscription-plans` → `code == "STARTER"` satırının `id`'si.
6. **Kiracı 1 ve kiracı 2:** `POST :5057/api/admin/tenants` `{name:"E3 Tenant 1", domain:"e3-t1.example.test", displayName, planId, tenantType:0, initialAdmin:{firstName,lastName,email}}` → 201, `data` = kiracı id. Hemen ardından Auth veritabanında bu kiracının `Admin` ve `Viewer` rolleri sayılır (`own_query`, `roles`); 2 değilse dur (`tenant.activated` HTTP'si düşmüş demektir).
7. Her kiracı için `GET :5057/api/admin/tenants/{id}/admin-users` → `id`. Ardından `POST :5056/internal/events/tenant-admin-invited` (`X-Internal-Api-Key`, `trigger:"operator-invite"`) → `setupToken`. 409 gelirse en çok 3 kez yeniden denenir.
8. `POST :5000/api/users/set-password` `{email, token, newPassword}` → başarı.
9. Yönetici girişi: `POST :5000/api/tenant-auth/login` + `X-Tenant-Id` → kiracı yönetici jetonu.
10. **Kullanıcılar:** kiracı 1'de `viewer`, `nokey`, `steward`, `approver`, `retirement`; kiracı 2'de `viewer`. `POST :5000/api/users` `{email,password,firstName,lastName}` → 201, `data.id`.
11. `GET :5000/api/roles` → `Viewer` id. Her iki kiracıda `POST :5000/api/users/{viewerId}/roles {roleId}`. `admin` zaten `Admin` rolündedir (yol A); `nokey` rolsüz kalır.
12. `Setup` döner (kiracı 1, kiracı 2, kullanıcılar ve kiracıları, platform kimliği).
13. **Y2a'nın içinde, `entitlement.sync.applied` taban sayacından sonra** ayrı bir `adanmis_roller(setup)` çalışır: kiracı 1 yönetici jetonuyla `GET /api/roles` üç adı bulur (biri yoksa dur), sonra `steward` → `ProductDataSteward`, `approver` → `ProductIdentityApprover`, `retirement` → `ProductIdentityRetirementSteward` atanır. Kiracı 2'ye adanmış rol verilmez.
14. `add_entitlement` gövdesindeki `source: 1` değişmeden kalabilir; `"Addon"` da geçerlidir.

**En büyük engel (betikte):** kiracı girişi `X-Tenant-Id` ister; bugünkü `login()` bunu göndermiyor ve Development'taki dev bypass hatayı gizliyor. Bunun ardından, adanmış rollerin hak eşitlemesinden önce var olmaması geliyor: `kurulum()` ile Y2a arasında bölünmüş bir adım gerekir.
