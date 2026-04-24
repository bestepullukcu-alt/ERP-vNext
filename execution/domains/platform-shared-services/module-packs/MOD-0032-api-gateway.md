# MOD-0032-api-gateway — API Gateway

## 1. Module Summary
- **Module ID:** MOD-0032-api-gateway
- **Module Name:** API Gateway
- **Domain:** Platform & Shared Services
- **Subdomain:** Integration & Interoperability
- **Planned Wave:** W1
- **UI:** YES (Admin)
- **Purpose:** Diten ERP mikroservis mimarisinde Ocelot tabanlı API Gateway (Port 5000) yapılandırmasını, rota yönetimini ve servis sınırlarını tanımlar.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- ApiService
- ApiRoute (ocelot.json konfigürasyonu)
- RateLimitPolicy

### In-scope
- Ocelot `ocelot.json` dosyasındaki rotaların yönetimi
- Kimlik doğrulama (Auth) yönlendirme politikaları
- Gateway üzerinden geçecek servis kontratlarının takibi

### Out-of-scope
- Servislerin iş mantığı (Service Logic)
- Karmaşık yük dengeleme (load balancing) stratejileri (başlangıç aşamasında)

### Current MVP execution status
- **Aktif:** Ocelot Gateway projenin ana giriş noktasıdır. `gateway/Diten.ApiGateway` bu modülün birincil yürütme alanıdır.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization (Kimlik doğrulama için)
- `gateway/Diten.ApiGateway` Ocelot projesi

### Primary consumers
- Tüm frontend uygulamaları (Diten.Web vb.)
- Entegrasyon yöneticileri

### Interface stubs
- Gateway Port: 5000
- Authentication: JWT Bearer (AuthService üzerinden)

## 4. Repo Scope

### Recommended backend scope
- `gateway/Diten.ApiGateway/`
- `gateway/Diten.ApiGateway/ocelot.json`

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0032-api-gatewayController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0032-api-gateway/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0032.js`

### Protected paths
- `gateway/Diten.ApiGateway/.../ocelot.json` (Sadece yetkili ajanlar/adminler tarafından değiştirilebilir)

## 5. UI Surfaces
- API Gateway Yönetimi — Rota listesi ve servis sağlık durumu izleme (Admin paneli)

## 6. Runtime Constraints
- Tüm dış istekler Port 5000 (Gateway) üzerinden geçmelidir.
- Servis portlarına doğrudan erişim engellenmelidir.

## 7. Acceptance Criteria
- Yeni bir mikroservis eklendiğinde Gateway rotası tanımlanmış ve doğrulanmış olmalıdır.
- Kimlik doğrulama gerektiren rotalar unauthorized isteklere 401 dönmelidir.

## 8. Testing Notes
- Run targeted gateway build: `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj`
- Rota doğrulama testleri (Postman/Curl üzerinden Port 5000 testi)

## 9. Implementation Notes
- Ocelot konfigürasyonu domain-independent tutulmalıdır.
- Gateway mimarisi `AGENTS.md` Madde 3 ile hizalıdır.
