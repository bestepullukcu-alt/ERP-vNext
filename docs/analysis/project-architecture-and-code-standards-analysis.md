# Proje Yapısı, Katmanlar ve Kod Standartları Analizi

Tarih: 2026-05-05  
Kapsam: Repo kök kontratı, servis yapısı, frontend/gateway yerleşimi, Platform servisi üzerinden gözlenen mimari uygulamalar ve test/dokümantasyon düzeni.

## Yönetici Özeti

Diten ERP vNext, .NET 8 mikroservisleri, Razor MVC frontend'i, Ocelot API Gateway'i ve MongoDB tabanlı multi-tenant veri modelini kullanan modüler bir ERP mimarisidir. Repo standartları `AGENTS.md` ile güçlü şekilde tanımlanmıştır: yeni modül geliştirme module pack onayına bağlıdır, frontend-gateway-servis port ayrımı nettir ve domain sınırları korunur.

Mevcut yapı, özellikle Platform tarafında 5 katmanlı mimari ve CQRS/MediatR desenini büyük ölçüde uygular. Bununla birlikte bazı eski/ara dönem desenleri de yan yana durmaktadır: response envelope çeşitliliği, SavedView'daki eski base entity modeli ve bazı architecture testlerinde güncel olmayan path beklentileri gibi.

## Repo Üst Yapısı

Kök yapı:

- `AGENTS.md`: Repo genel execution contract
- `.antigravity/**`: Global engineering system ve kurallar
- `execution/domains/**`: Domain config, module pack ve control çıktıları
- `services/**`: .NET servisleri
- `frontend/Diten.Web`: Razor MVC frontend
- `gateway/Diten.ApiGateway`: Ocelot gateway
- `docs/**`: Ürün, API, audit ve analiz dokümantasyonu
- `events/**`: Event schema ve event dokümanları
- `tests/**`: Architecture ve tenancy testleri

Repo bu projede `src/Backend` veya `src/Frontend` yapısı kullanmaz. Gerçek yapı `services`, `frontend`, `gateway`, `execution` ve `docs` klasörleri üzerinden ilerler.

## Domain Modeli ve Execution Katmanı

`execution/domains` altında domain bazlı planlama ve yetki sınırları bulunur:

- `master-data-management`
- `developer-enablement`
- `platform-shared-services`
- `enterprise-strategy-business-performance`

Her domain şu sözleşmeye bağlanır:

- `domain-config.md`: Domain kapsamı, ownership, runtime kararları
- `module-packs/*.md`: Modül bazlı geliştirme kontratı
- `controls/*.md`: Domain kontrol ve karar çıktıları

Yeni modül veya büyük feature geliştirme module pack olmadan yapılmaz. `draft` module pack sadece planlama dokümanıdır; kod üretimi için `approved` veya `ready-for-dev` gerekir.

## Servis ve Port Şeması

Standart portlar:

- Gateway: `5000`
- Frontend: `5001`
- Auth Service: `5056`
- Platform Service: `5057`
- DevEnablement Service: `5058`
- MongoDB: `27017`

Kritik kural: frontend doğrudan servis portlarına istek atmaz. Frontend istekleri Gateway portu `5000` üzerinden akar.

## Katmanlı Mimari

Repo standardı 5 katmanlı mimariyi hedefler:

1. API
2. Application
3. Domain
4. Persistence
5. Infrastructure

Platform servisinde bu yapı şu şekilde uygulanmıştır:

- `Diten.Platform.API`
  - Controller, middleware, API model ve host bootstrap
- `Diten.Platform.Application`
  - CQRS command/query, handler, validator, DTO, behavior
- `Diten.Platform.Domain`
  - Entity, enum, repository interface
- `Diten.Platform.Infrastructure`
  - MongoDB context, repository implementation, settings, external/service implementations
- `Diten.Platform.Common`
  - Ortak tenant ve persistence altyapısı

Bu ayrım genel olarak temizdir. API katmanı MediatR üzerinden Application katmanına gider; Infrastructure katmanı repository interface'lerini uygular; Domain katmanı MongoDB attribute bağımlılığından çoğunlukla kaçınır, fakat bazı eski entitylerde Mongo attribute kullanımı vardır.

## CQRS ve Pipeline Standardı

Application katmanında MediatR kullanılır. Platform `DependencyInjection.cs` içinde şu pipeline behavior'ları kayıtlıdır:

- `ValidationBehavior`
- `LoggingBehavior`
- `ExceptionBehavior`
- `PerformanceBehavior`

Bu AGENTS kontratındaki 4 zorunlu behavior kararıyla uyumludur.

Feature klasörleme örneği:

- `Features/Tenants/Commands`
- `Features/Tenants/Queries`
- `Features/Tenants/Handlers`
- `Features/Tenants/Validators`
- `Features/ModuleCatalog/Commands`
- `Features/ModuleCatalog/Handlers/CommandHandlers`
- `Features/ModuleCatalog/Handlers/QueryHandlers`
- `Features/SavedViews/...`

Bu yapı yeni feature'lar için iyi bir referans noktasıdır.

## Multi-Tenancy Standardı

Repo standardı MongoDB tek DB, multi-tenant modeldir. Tenant izolasyonu `TenantId` üzerinden yapılır; cross-tenant erişim bulunamadı/erişilemez davranışı göstermelidir.

Ortak altyapı:

- `ITenantContext`
- `TenantContext`
- `TenantResolutionMiddleware`
- `TenantRepository`
- `GlobalRepository`
- `HybridRepository`

Tenant resolution kuralı hem Gateway hem servis tarafında uygulanır. Gateway request'i edge katmanında doğrular ve downstream servise `X-Tenant-Id` aktarır. Servis tarafı ise tenant context'i uygulama içinde kullanılabilir hale getirir.

Repository standardı:

- Tenant-scoped kayıtlar `TenantId` + `IsDeleted=false`
- Global kayıtlar `IsDeleted=false`
- Hybrid kayıtlar global veya aktif tenant kaydı + `IsDeleted=false`

Bu desen yeni servisler için tekrar kullanılabilir niteliktedir.

## Persistence ve MongoDB Standardı

MongoDB bağlantısı servislerin Infrastructure katmanında kurulmalıdır. Platform servisinde:

- `MongoClient` Infrastructure DI içinde oluşturulur.
- `IPlatformDbContext` Mongo database ve collection erişimini soyutlar.
- Repository implementasyonları Infrastructure/Persistence altında yer alır.
- Indexler `MongoDbIndexConfigurations` ile uygulama başlangıcında oluşturulur.

Bu model Mongo bağlantısının controller/handler gibi üst katmanlara sızmasını engeller.

## API Standardı

Repo standardı `Response<T>` envelope + `CustomBaseController` kullanımını zorunlu kabul eder.

Mevcut gözlem:

- Tenant endpoints `API/Models/Response<T>` ve `CustomBaseController` kullanır.
- Module Catalog `Application/Common/Response<T>` kullanır ve controller içinde HTTP payload'a map eder.
- Saved Views doğrudan DTO ve status code döner.

Bu, işlevsel ama tam standartlaşmamış bir durumdur. Yeni geliştirmelerde tek response envelope seçilmeli ve mevcut endpointler aşamalı olarak uyumlu hale getirilmelidir.

## Security Standardı

Genel standart:

- JWT authentication
- RBAC permission modeli
- `[HasPermission]` tabanlı endpoint koruması
- Tenant context ile actor/tenant ayrımı

Platform servisindeki durum:

- JWT Bearer validation Infrastructure DI içinde yapılır.
- `PlatformActor` policy `actor_type` claim'ini `platform_admin` veya `partner_admin` olarak bekler.
- Module Catalog endpointleri permission stringleri ile işaretlenmiştir.
- Gateway cookie'den access token okuyup Authorization header'a taşıyabilir.

Yeni endpointler route tipine göre doğru actor modelini seçmelidir:

- Platform/admin endpointleri: platform admin veya partner admin
- Tenant endpointleri: tenant user
- Public endpointler: açıkça bypass listesinde olmalı

## Frontend Standardı

Frontend `frontend/Diten.Web` altında Razor MVC yapısıdır. Program konfigürasyonunda:

- Localization resources aktif
- View location pattern'leri MDM, Platform, genel Views ve Archive için tanımlı
- Cookie authentication kullanılıyor
- JWT access/refresh token cookie bridge var
- Gateway URL üzerinden auth gateway client bağlanıyor

Layout standardı:

- Yeni modüller `_LayoutBackbone.cshtml` kullanmalıdır.
- `Views/Shared/_Layout.cshtml` archive için frozen alandır.
- Archive controller/view klasörleri protected path kapsamındadır.

Localization standardı:

- Genel hedef 7 dil: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`
- Platform context için mevcut Program.cs davranışı `en` ve `tr` setini özel ele alır.

DataTable standardı:

- DataTables v2 kontratı gerekir.
- `data-dt-standard="v2"` beklenir.
- 8 ve altı form alanı için GoldenReferenceSlim
- 8'den fazla form alanı için GoldenReferenceCompact

## Gateway Standardı

Gateway `gateway/Diten.ApiGateway` altında Ocelot kullanır. `ocelot.json` protected path olarak tanımlıdır ve normal ajanlar tarafından değiştirilmemelidir.

Gateway sorumlulukları:

- Downstream route yönetimi
- JWT token okuma ve doğrulama
- Cookie token'ı Authorization header'a taşıma
- Tenant resolution
- Host/path kombinasyonu koruması
- CORS

Gateway tenant middleware'i admin host, tenant host, personalization path, admin path ve public endpoint ayrımlarını yapar. Tenant çözüldüğünde `X-Tenant-Id` header'ını downstream için set eder.

## Test ve Kalite Kapıları

AGENTS kontratındaki temel komutlar:

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`

Frontend DataTable doğrulaması:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName} --reference slim|compact
```

Mevcut test gözlemi:

- Platform Application testleri feature bazlı bulunur.
- `tests/architecture` ve `tests/tenancy` altında repo geneli testler vardır.
- Bazı architecture/tenancy testleri güncel klasör yapısıyla uyumsuz eski path veya constructor beklentileri içeriyor. Bu testler güncellenmeden güvenilir kalite kapısı sayılmamalıdır.

## Protected Paths ve Çalışma Disiplini

Aşağıdaki alanlar kullanıcı açıkça istemedikçe değiştirilmemelidir:

- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/.../ocelot.json`
- İlgili module pack dışında kalan diğer domain servisleri

Yeni modül geliştirme module pack onayına bağlıdır. Fix/refactor işlerinde kod düzeltmesi önce yapılır; `.antigravity` etkisi gerekiyorsa ayrıca onay alınır.

## Branch ve Module Pack Standardı

Yeni modül branch formatı:

```text
feature/{domain-kısa}/{module-id}-{slug}
```

Örnek:

```text
feature/pss/pss-005-tenant-module-catalog
```

Module pack minimum içeriği:

- YAML frontmatter
- Owned objects
- Repo scope
- Protected paths
- Acceptance criteria
- Test expectations
- DataTable modülleri için field count ve golden reference kararı

## Güçlü Yanlar

- Repo kontratı açık ve domain sınırları iyi tanımlı.
- Platform servisinde katmanlı mimari, CQRS ve pipeline behavior standardı uygulanmış.
- Tenant isolation hem gateway hem servis içinde savunmalı uygulanıyor.
- MongoDB repository tabanları yeni servisler için tekrar kullanılabilir.
- Module pack yaklaşımı büyük feature'larda kapsam kontrolü sağlıyor.
- Frontend-gateway-servis port ayrımı net.

## Standart Sapmaları ve Riskler

- Platform API response envelope kullanımı tek tip değil.
- SavedView eski base entity ve `Status` soft delete modelini kullanıyor; repo standardındaki `IsDeleted/DeletedAt` beklentisiyle birebir uyumlu değil.
- Bazı testler eski repo path'lerine göre yazılmış görünüyor.
- `HasPermission` attribute kullanımının Platform API tarafında policy provider ile tam runtime entegrasyonu doğrulanmalı.
- Event/outbox contract'ları mevcut, ancak bazı akışlarda dispatch implementasyonu henüz yok.
- Frontend Platform localization özel olarak `en/tr` ile sınırlı; genel 7 dil standardıyla bilinçli bir fark mı yoksa geçici durum mu netleştirilmeli.

## Yeni Geliştirme İçin Pratik Kontrol Listesi

1. İlgili domain-config ve module pack'i oku.
2. Module pack status `approved` veya `ready-for-dev` değilse kod yazma.
3. Branch adını module pack'e uygun aç.
4. API/Application/Domain/Infrastructure ayrımını koru.
5. Command/query, handler ve validator dosyalarını feature altında grupla.
6. Tenant-scoped veri için `TenantId` ve tenant repository filtresini zorunlu tut.
7. Mongo erişimini yalnızca Infrastructure/Persistence içinde yap.
8. Endpointlerde JWT, actor type ve permission gereksinimini açık tanımla.
9. Response envelope standardından sapma yaratma.
10. Frontend gerekiyorsa Gateway üzerinden çağrı yap ve DataTable v2 kontratını doğrula.
11. İlgili build/test komutlarını çalıştır.
12. Dokümantasyon ve audit notlarını `docs/**` altında güncelle.

## Sonuç

Proje yapısı güçlü bir domain execution modeli üzerine kuruludur. Platform servisi bu mimarinin en net uygulandığı alanlardan biridir: tenant yönetimi, module catalog, saved views ve ortak multi-tenant altyapı aynı servis ailesi içinde yer alır. Bir sonraki kalite adımı, mevcut ara dönem farklılıklarını azaltıp response, soft delete, permission policy ve test path standartlarını tek çizgiye taşımaktır.
