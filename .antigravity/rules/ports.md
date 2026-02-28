# Port Registry (Single Source of Truth)

## Amaç
Local development ve ileride environment’larda port çakışmalarını önlemek.
Yeni servis açarken “rastgele port” seçilmez.

## Port Bandları
- **5000**: Gateway (Ocelot) — dev
- **5001**: Frontend (Diten.Web) — dev
- **5011–5056**: Microservice bandı (backend servis portları)
- **5050**: Preferred “new service” başlangıç portu (band içinde uygunsa)
- **7000+**: Dev tools / özel (mümkünse kullanılmaz; bazı tool’lar kapabilir)

## Aktif Kullanımlar (Şu an)
### Frontend
- **Diten.Web**: `http://localhost:5001`

### Gateway
- **Diten.ApiGateway (Ocelot)**: `http://localhost:5000`

### MDM
- **Diten.MdmService.Api**: `http://localhost:5050`
  - Health: `/health`
  - API: `/api/...`
  - PublicBaseUrl: `http://localhost:5000/services/mdm`

## Ayrılmış/Mevcut Sistem Portları (Legacy)
> Bu liste sistemden gelen ocelot config’e göre “ayrılmış band” olarak kabul edilir.
- 5011 Daywork
- 5012 Country
- 5013 VisitMix
- 5014 HR
- 5015 TaskManagement
- 5016 Settings
- 5017 Pages
- 5018 Budget
- 5019 Material
- 5020 Physician
- 5021 SurveySystem
- 5022 AdminPanel
- 5023 ExternalAPIs
- 5024 Organization
- 5025 CRM
- 5026 Production
- 5027 Finance
- 5028 AuthorizationSystem
- 5029 InventoryManagement
- 5030 _cache
- 5031 Company
- 5035 Notification
- 5036 FRR
- 5037 Purchasing
- 5038 Campaign
- 5039 ProjectSettings
- 5040 Content
- 5041 Marketing
- 5042 CrmV2
- 5043 Territory
- 5044 DitenPPM
- 5052 PvTenant
- 5053 PvOrganization
- 5054 PvDocumentManagement
- 5056 PvSurvey
- 5002 product (legacy)

## Boş Port Seçme Kuralı (Yeni Servis Açarken)
1) Yeni servis microservice bandından seçilir: **5011–5056**.
2) Seçmeden önce kontrol:
   - `lsof -nP -iTCP:<PORT> | grep LISTEN`
3) Port boşsa bu dosyaya eklenir (aktif kullanımlar listesine).
4) Servis portu ile gateway upstream route birlikte eklenir (routes.md).

## Çakışma Çözümü
- Port doluysa PID bulunur:
  - `lsof -nP -iTCP:<PORT> | grep LISTEN`
- PID kapat:
  - `kill -9 <PID>`