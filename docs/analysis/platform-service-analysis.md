# Diten Platform Servisi Analizi

Tarih: 2026-05-05  
Kapsam: `services/Diten.Platform`, `services/Diten.Platform.Common`, Platform ile doğrudan ilişkili gateway/frontend sözleşmeleri ve PSS domain tanımları.

## Yönetici Özeti

`Diten.Platform`, ERP vNext ekosisteminin platform yönetim servisidir. Servisin bugünkü sorumluluğu iş domain verisi üretmekten çok, tenant yönetimi, platform modül kataloğu, kullanıcı kişiselleştirmesi ve ortak tenant altyapısını sağlamaktır.

Platform tarafı şu üç ana işlevi taşır:

1. Tenant registry ve lifecycle yönetimi
2. Platform module catalog yönetimi
3. Tenant/user bazlı saved view kişiselleştirme altyapısı

Bunların yanında `Diten.Platform.Common`, tenant context, tenant resolution middleware ve MongoDB repository tabanlarını sağlayarak diğer servislerin ortak multi-tenant davranışını standartlaştırır.

## Domain Konumu

Platform servisi, `execution/domains/platform-shared-services/domain-config.md` dosyasındaki Platform & Shared Services domain kapsamına girer. Bu domain; kimlik, yetkilendirme, audit, workflow, event bus, API gateway, tenant mimarisi ve platform katalog gibi yatay kabiliyetleri sahiplenir.

Repo scope olarak PSS domaini şu alanlarla ilişkilidir:

- `services/Diten.Platform/**`
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**`
- `gateway/Diten.ApiGateway/**`
- `frontend/Diten.Web/**` içindeki Platform modülleri
- `execution/domains/platform-shared-services/**`

## Runtime Rolü

Platform servisi port `5057` üzerinde çalışan .NET 8 Web API servisidir. Normal kullanımda frontend doğrudan bu porta gitmez; trafik Gateway portu `5000` üzerinden akar.

`services/Diten.Platform/src/Diten.Platform.API/Program.cs` içinde:

- Application ve Infrastructure katmanları DI ile bağlanır.
- JWT Bearer authentication aktif edilir.
- `PlatformActor` authorization policy kullanılır.
- `UseTenantResolution()` ile tenant context middleware'i çalıştırılır.
- Swagger, global exception handler ve controller routing açılır.

## Ana Özellikler

### 1. Tenant Registry ve Lifecycle Yönetimi

Controller: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/TenantsController.cs`  
Route: `/api/admin/tenants`  
Yetki: `PlatformActor` policy

Bu alan platform admin veya partner admin aktörleri için tenant yönetim API'sidir.

Sunulan kabiliyetler:

- Tenant listeleme, arama, filtreleme, sayfalama ve sıralama
- Tenant istatistiklerini alma
- Tenant detayını görüntüleme
- Yeni tenant kaydı oluşturma
- Tenant suspend/reactivate lifecycle işlemleri
- Tenant modül özetini alma
- Tenant kullanıcı özetini alma
- Tenant genel ayarlarını okuma/güncelleme
- Tenant login/security ayarlarını okuma/güncelleme

İlgili domain nesneleri:

- `Tenant`
- `TenantDomain`
- `TenantLoginSettings`
- `InitialAdminInfo`
- `TenantType`

Yeni tenant kaydı `RegisterTenantCommandHandler` içinde şu davranışlarla oluşturulur:

- Slug oluşturma veya normalize etme
- Slug ve domain tekillik kontrolü
- Tenant code üretimi
- Platform domain ataması: `{slug}.ditenteknoloji.com`
- Default region, environment, tier, language, timezone ve currency ataması
- Provisioning step ve activity timeline başlangıç kayıtları
- Default `TenantDomain` kaydı
- Default `TenantLoginSettings` kaydı
- Initial admin varsa davet olayı için future outbox notu ve timeline adımı

Not: Integration event contract'ları tanımlıdır, fakat tenant admin invitation dispatch/outbox akışı mevcut kodda henüz uygulanmamıştır.

### 2. Platform Module Catalog

Controller: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/ModuleCatalogController.cs`  
Route: `/api/platform/module-catalog`  
Yetki: `PlatformActor` policy + `HasPermission`

Module Catalog, ERP içindeki modüllerin platform seviyesinde kayıt altına alındığı katalogdur. Bu yapı tenant'a atanabilir modülleri, core modülleri, domain/service sınıflandırmasını ve lifecycle statüsünü yönetir.

Sunulan kabiliyetler:

- Modül kataloğu listeleme
- Search/domain/service/category/status/core/assignable filtreleri
- Sayfalama ve sıralama
- İstatistik alma
- Tenant'a atanabilir aktif modülleri listeleme
- Id veya module code ile detay alma
- Modül oluşturma
- Modül güncelleme
- Activate/deactivate
- Tekil delete
- Bulk delete

Yetki kodları:

- `Modules.ModuleCatalog.Read`
- `Modules.ModuleCatalog.Create`
- `Modules.ModuleCatalog.Update`
- `Modules.ModuleCatalog.Delete`
- `Modules.ModuleCatalog.BulkDelete`

Ana entity: `ModuleCatalogItem`

Alanlar:

- `ModuleCode`
- `ModuleName`
- `DisplayName`
- `Description`
- `Domain`
- `Service`
- `Category`
- `Status`
- `ModuleVersion`
- `IsCoreModule`
- `IsTenantAssignable`
- `SortOrder`

Önemli iş kuralları:

- `ModuleCode` canonical forma normalize edilir.
- Aynı `ModuleCode` tekrar oluşturulamaz.
- Core modüller silinemez.
- Assignable endpoint yalnızca `Active` ve `IsTenantAssignable=true` kayıtları döner.
- MongoDB tarafında `ModuleCode` unique index ile korunur.

Bu modül için mevcut kullanıcı ve API dokümanları şurada bulunur:

- `docs/platform/module-catalog/api.md`
- `docs/platform/module-catalog/user-manual.md`

### 3. Saved Views / Personalization

Controller: `services/Diten.Platform/src/Diten.Platform.API/Controllers/SavedViewsController.cs`  
Route: `/api/personalization/views`  
Yetki: `[Authorize]`

Saved Views, kullanıcının belirli module/page kombinasyonu için kaydettiği kişisel görünüm tanımlarını saklar.

Sunulan kabiliyetler:

- `moduleKey` ve `pageKey` ile view listeleme
- Yeni view oluşturma
- View güncelleme
- View silme
- Default view tekilleştirme

Ana entity: `SavedView`

Alanlar:

- `TenantId`
- `UserId`
- `ModuleKey`
- `PageKey`
- `ViewName`
- `ViewDefinitionJson`
- `IsDefault`
- `Visibility`

Tenant izolasyonu repository seviyesinde `TenantContext.TenantId` ile uygulanır. Delete işlemi fiziksel silme yerine `Status=false` yapar. Bu entity eski `Domain/Common/BaseEntity` tabanını kullandığı için `IsDeleted` yerine `Status` alanı ile soft delete davranışı gösterir.

### 4. Lookup ve Health Endpointleri

`HealthController`:

- `GET /health`
- Anonymous erişime açık
- Service adı, status ve timestamp döner

`LookupsController`:

- `GET /api/lookups/countries`
- `GET /api/lookups/currencies`
- `GET /api/lookups/timezones`
- Anonymous erişime açık

Bu endpointler tenant middleware bypass path kapsamındadır.

## Ortak Platform Altyapısı

### Tenant Context

`services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy` altında ortak tenant altyapısı vardır.

Temel tipler:

- `ITenantContext`
- `TenantContext`
- `TenantResolutionMiddleware`
- `TenantResolutionExtensions`

Tenant resolution davranışı:

- Admin/platform path'lerinde `X-Tenant-Id` kabul edilmez.
- Platform admin ve partner admin aktörleri platform context olarak işlenir.
- Tenant endpointlerinde JWT `tenant_id` claim'i veya `X-Tenant-Id` gerekir.
- JWT tenant ve header tenant çakışırsa JWT önceliklidir.
- Personalization path için platform aktörleri tenant header kullanamaz; tenant user için tenant context zorunludur.
- Development ortamında konfigüre edilmişse dev bypass tenant kullanılabilir.

Gateway tarafındaki `TenantResolutionMiddleware` benzer kural setini edge katmanında uygular ve çözülen tenant'ı downstream servislere `X-Tenant-Id` olarak taşır.

### Repository Tabanları

`Diten.Platform.Common.Persistence` içinde üç ana repository yaklaşımı bulunur:

- `GlobalRepository<TEntity>`: Tenant bağımsız, yalnızca `IsDeleted=false` filtreler.
- `TenantRepository<TEntity>`: `TenantId == TenantContext.TenantId` ve `IsDeleted=false` filtreler.
- `HybridRepository<TEntity>`: Global kayıtları ve mevcut tenant override kayıtlarını birlikte döndürür.

Platform servisindeki tenant registry ve module catalog global repository modelini kullanır. Saved views ise kendi repository'sinde tenant bazlı filtreleme yapar.

### MongoDB Saklama Modeli

Platform servisi MongoDB kullanır. `Infrastructure/DependencyInjection.cs` içinde Mongo client ve database singleton olarak hazırlanır.

Koleksiyonlar:

- `tenants`
- `tenant_domains`
- `tenant_login_settings`
- `platform_module_catalog`
- `saved_views`

Indexler `MongoDbIndexConfigurations.EnsureIndexesAsync` ile uygulama başlangıcında oluşturulur.

Öne çıkan indexler:

- `tenants.Code`, `tenants.Slug`, `tenants.Domain` unique
- `tenant_domains.DomainName` unique
- `tenant_login_settings.TenantRefId` unique
- `platform_module_catalog.ModuleCode` unique
- saved views için tenant/user/module/page/status birleşik indexleri

## Güvenlik ve Yetkilendirme

Platform API JWT Bearer auth kullanır. `PlatformActor` policy, authenticated user yanında `actor_type` claim'inin `platform_admin` veya `partner_admin` olmasını ister.

Module Catalog ek olarak permission attribute kullanır. Bu attribute mevcut kodda policy adını permission string olarak ayarlayan ince bir attribute'tur. Permission policy provider/handler tarafı AuthService içinde daha gelişmiş şekilde bulunur; Platform API tarafında bu permission policy'lerin runtime kaydı ayrıca doğrulanmalıdır.

## API Yanıt Deseni

Platform API'de iki response deseni birlikte görülür:

- `API/Models/Response<T>` + `CustomBaseController`
- `Application/Common/Response<T>` + controller içinde `CreateActionResult`

Tenant controller ilk deseni kullanır. Module Catalog ikinci deseni kullanır ve frontend uyumluluğu için `isSuccessful` yanında `succeeded` alanını da döner. Saved Views ise doğrudan DTO/HTTP status döner.

Bu durum işlevsel olsa da standartlaşma açısından tek response envelope kullanımına yaklaştırılmalıdır.

## Frontend ve Gateway Bağlantısı

Frontend `frontend/Diten.Web/Program.cs` içinde gateway base URL kullanır. Varsayılan `GatewayUrl` yoksa `http://localhost:5000` alınır. Bu, AGENTS kontratındaki "frontend servis portlarına doğrudan gitmez, gateway üzerinden gider" kuralıyla uyumludur.

Frontend tarafında Platform context için:

- `/Platform/...` view location pattern'i vardır.
- Admin host veya `/platform` path için platform localization davranışı ayrı ele alınır.
- JWT cookie bridge ile access token cookie'den okunur, doğrulanır ve gerektiğinde refresh edilir.

## Test Durumu

Platform altında Application testleri bulunur:

- `TenantValidatorsTests`
- `TenantHandlersTests`
- `ModuleCatalogRulesTests`

Repo genelinde architecture/tenancy test klasörleri de vardır. Ancak bu testlerde bazı path ve constructor beklentileri mevcut repo yapısıyla uyumsuz görünüyor. Örneğin architecture test içinde eski `gateway/DitenApiGateway/...` ve `services/DitenMdmService/...` path beklentileri var. Bu testler çalıştırılmadan önce güncellenmelidir.

## Güçlü Yanlar

- Platform domain sorumluluğu net: tenant, katalog, personalization ve ortak tenant altyapısı.
- .NET 8, MediatR, FluentValidation, AutoMapper ve MongoDB ile tutarlı modern stack kullanılıyor.
- Tenant resolution hem gateway hem servis katmanında uygulanmış.
- Repository tabanları global, tenant-scoped ve hybrid veri modellerini açıkça ayırıyor.
- Module Catalog için iş kuralları, permission kodları ve dokümantasyon mevcut.
- MongoDB indexleri başlangıçta programatik olarak kuruluyor.

## İyileştirme Alanları

- Response envelope standardı Platform içinde tekleştirilmeli.
- SavedView entity eski `Status` tabanlı soft delete modelinden ortak `BaseEntity/IsDeleted` standardına taşınabilir.
- `HasPermission` attribute için Platform API tarafında permission policy provider kaydı doğrulanmalı.
- Tenant lifecycle event/outbox davranışı contract seviyesinden uygulama seviyesine indirilmeli.
- Architecture/tenancy testleri mevcut repo path'lerine ve middleware constructor'larına göre güncellenmeli.
- Tenant admin invitation akışı AuthService entegrasyonuna bağlanmalı.

## Sonuç

Platform servisi bugün Diten ERP vNext'in yatay kontrol düzlemi olarak çalışıyor. İş domainlerinin veri üretimini üstlenmiyor; bunun yerine tenant kimliği, tenant yaşam döngüsü, modül kataloğu, kişiselleştirme ve ortak multi-tenant altyapısını sağlıyor. Bu haliyle PSS domaininin temel runtime çekirdeği konumunda.
