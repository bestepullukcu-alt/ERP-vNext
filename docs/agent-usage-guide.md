# ERP-vNext Agent Kullanım Rehberi

Bu rehber yeni modül geliştirme akışını iki güvenli aşamaya ayırır: önce module pack hazırlanır, sonra orchestrator onaylı pack üzerinden geliştirme yapar.

## Yeni Modül Akışı

1. Kullanıcı modül fikrini verir.
2. `module-pack-author` veya `/prepare-module-pack` çalışır.
3. Module pack `execution/domains/{domain}/module-packs/` altında `status: draft` olarak hazırlanır.
4. Kullanıcı module pack'i inceler, alanları/scope'u/acceptance criteria'yı düzeltir.
5. Onay sonrası status `approved` veya `ready-for-dev` yapılır.
6. Kullanıcı `@orchestrator {module-pack}` çağırır.
7. Orchestrator backend, frontend, gateway, l10n, test ve dokümantasyon ajanlarını aynı module pack'e göre yönetir.

`@orchestrator` module pack oluşturmaz. Module pack yoksa veya `draft` ise geliştirme başlatmaz.

## Hangi Agent Ne Zaman Kullanılır?

| İş | Agent / Workflow |
|---|---|
| Çoklu-servis stratejik etki analizi (yeni domain / büyük feature) | `product-manager` (module pack hazırlığı öncesinde) |
| User Story + Gherkin Acceptance Criteria + MVP/MoSCoW kapsamı | `product-owner` (module pack içeriği için) |
| Tek modül için PRD/BRD + IFRS/KVKK iş kuralı + L10n anahtar listesi | `business-analyst` (module pack içeriği için) |
| Yeni module pack hazırlama (sözleşme dosyası) | `module-pack-author` veya `/prepare-module-pack` |
| Onaylı module pack ile uçtan uca geliştirme | `@orchestrator` / `/add-module` |
| Backend endpoint/CQRS ekleme | `backend-architect` / `/add-endpoint-cqrs` |
| Frontend DataTable veya form düzenleme | `frontend-ui-ux` / `/add-page` |
| Gateway route ekleme | `integration-agent` |
| 7 dil RESX ve JS L10n bridge | `l10n-agent` |
| Test senaryoları | `testing-agent` |
| Güvenlik/tenant/RBAC denetimi | `security-agent` |
| Hata analizi | `debugger` |
| Teknik dokümantasyon | `documentation-writer` |
| Son kullanıcı kılavuzu | `user-manual-generator` |

> **Planlama kadrosu sırası:** `product-manager` (yalnız stratejik scope), `product-owner` (AC/MVP), `business-analyst` (iş kuralı/L10n) **opsiyonel** ön adımlardır; çıktıları **her zaman** `module-pack-author`'a girdi olur. Sıradan tek modül geliştirmesi için `module-pack-author` doğrudan çağrılabilir.

## Slim / Compact Seçimi

DataTable modüllerinde create/edit formundaki kullanıcı alanları sayılır.

Sayılmayanlar: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, audit alanları, DataTable checkbox/action kolonları.

| Form alan sayısı | Golden reference | Frontend yapı |
|---|---|---|
| `8 ve altı` | `GoldenReferenceSlim` | Index içinde `_CreateEditOffcanvas.cshtml` |
| `8'den fazla` | `GoldenReferenceCompact` | `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` |

Module pack içinde `form_field_count` ve `golden_reference: slim|compact` açık yazılır.

## Backend Klasör Standardı

Her feature altında CQRS ayrımı korunur:

- `Commands/`
- `Queries/`
- `Handlers/CommandHandlers/`
- `Handlers/QueryHandlers/`
- `Validators/`

Her command, query, handler ve validator ayrı dosyada olur. Controller ince kalır; MediatR'a gönderir ve `CustomBaseController` response envelope döner.

## Frontend Partial Standardı

Her DataTable modülünde ortak zorunlu yapı:

- `Index.cshtml`
- `_Filter.cshtml`
- `_DataTable.cshtml`
- `_IndexL10n.cshtml`
- `{ModuleName}Index.cs`
- `index.l10n.js`
- `index.js`

Slim ek dosyası:

- `_CreateEditOffcanvas.cshtml`

Compact ek dosyaları:

- `Create.cshtml`
- `Edit.cshtml`
- `Details.cshtml`
- `_Form.cshtml`

## Doğrulama Komutları

Slim:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim
```

Compact:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceCompact --reference compact
```

Build:

```bash
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
```

RESX:

```bash
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .
```

## Legal Entity Örneği

1. `Legal Entity module pack hazırla.`
2. `module-pack-author` domain'i, alanları, form alan sayısını ve Slim/Compact kararını yazar.
3. Kullanıcı `draft` module pack'i inceler.
4. Status `approved` veya `ready-for-dev` yapılır.
5. `@orchestrator Legal Entity module pack'e göre geliştir.`
6. Orchestrator onaylı pack üzerinden geliştirmeyi yürütür.
