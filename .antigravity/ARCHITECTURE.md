# ERP-vNext Architecture & Antigravity Kit

> Comprehensive AI Agent Capability Expansion for Diten Ecosystem

---

## 📋 Proje Özeti (Project Overview)

ERP-vNext; çok kiracılı (multi-tenant), mikro hizmet tabanlı bir kurumsal kaynak planlama (ERP) sistemidir.
- **Marka**: Diten
- **Mimari**: Ocelot Gateway ile Mikro Servisler
- **Backend**: .NET 8, CQRS (MediatR), MongoDB
- **Frontend**: ASP.NET Core MVC (Diten.Web), Sneat Bootstrap 5.3.3
- **Kimlik & Yetki**: Custom Auth Service (JWT + BCrypt + Dynamic RBAC)
- **Tenancy**: Tek Veritabanı, Çoklu Kiracı (TenantId Filtreli - Guid)
- **Platform**: Platform Core Service (Port: 5057)
- **Localization**: 7 Dil (EN, FR, ES, ZH, AR, RU, TR) - Resx + JS Bridge (`_IndexL10n.cshtml` JSON payload + `index.l10n.js` -> `window.L10n`)

## 🥇 Golden Reference Standardı

DataTable modülleri için aktif referanslar `developer-enablement` domain'inde bulunan canlı modüllerdir:

| Referans | Kullanım | UI surface |
|---|---|---|
| `GoldenReferenceSlim` | `8 ve altı` create/edit form alanı | Index içinde `_CreateEditOffcanvas.cshtml` |
| `GoldenReferenceCompact` | `8'den fazla` create/edit form alanı | Ayrı `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` |

Eski `SampleModule`, `Products`, `Diten.MdmService` ve hardcoded `5050` anlatımları aktif golden kaynak değildir; yalnızca tarihsel örnek olarak görülürse module pack ve domain config önceliği uygulanır.

Yeni modül geliştirmesi iki aşamalıdır:

1. `/prepare-module-pack` veya `module-pack-author` ile `status: draft` module pack hazırlanır.
2. Kullanıcı onayı sonrası status `approved` veya `ready-for-dev` olur ve `@orchestrator` geliştirmeyi başlatır.

---

## 🏗️ Klasör Hiyerarşisi (Directory Structure)

    ERP-vNext/
    ├── .antigravity/            # Tek Yönetim Merkezi (Merkezi İstihbarat)
    │   ├── agents/              # Uzman Personalar (16 ajan)
    │   ├── skills/              # Teknik Yetenek Modülleri
    │   ├── workflows/           # Otomasyon Akışları (/komutlar)
    │   ├── rules/               # Sistem Anayasası (Kurallar)
    │   └── scripts/             # Doğrulama ve Otomasyon Scriptleri
    ├── execution/               # Domain + Module execution katmani
    │   ├── README.md            # Kullanim rehberi
    │   ├── scripts/
    │   │   └── generate-dashboard.py   # Module pack dashboard ureticisi
    │   └── domains/
    │       ├── master-data-management/
    │       ├── developer-enablement/
    │       ├── platform-shared-services/
    │       └── enterprise-strategy-business-performance/
    ├── frontend/
    │   └── Diten.Web/           # MVC Projesi (Port: 5001)
    │       ├── Controllers/
    │       │   ├── AccountController.cs      # Login/Logout (AuthService entegrasyonu)
    │       │   ├── GoldenReferenceSlimController.cs
    │       │   ├── GoldenReferenceCompactController.cs
    │       │   └── Archive/                   # Legacy controller'lar (FROZEN)
    │       ├── Views/
    │       │   ├── Shared/
    │       │   │   ├── _Layout.cshtml         # Legacy layout (FROZEN)
    │       │   │   ├── _LayoutBackbone.cshtml # Modern layout (yeni modüller)
    │       │   │   └── _SkeletonLoader.cshtml # DataTable shimmer efekti
    │       │   ├── Account/                   # Login sayfası (AuthService)
    │       │   ├── DevEnablement/             # Golden reference modülleri
    │       │   └── Archive/                   # Legacy sayfalar (_Layout)
    │       ├── wwwroot/assets/
    │       │   ├── css/backbone-custom.css    # Modern CSS (16px rem baz)
    │       │   ├── js/dt-defaults.js          # Merkezi DataTable config
    │       │   ├── js/Account/                # Login JS modülleri
    │       │   └── js/DevEnablement/          # Golden reference JS dosyaları
    │       └── Resources/                     # 7 Dil Resx Dosyaları
    ├── gateway/
    │   └── Diten.ApiGateway/    # Ocelot Gateway (Port: 5000)
    │       ├── ocelot.json      # Route tanımları
    │       └── Program.cs       # JWT Authentication + CORS
    └── services/
        ├── Diten.AuthService/              # Identity & Access Management (Port: 5056)
        ├── Diten.Platform/                 # Platform shared services (Port: 5057)
        ├── Diten.DevEnablementService/     # Golden references (Port: 5058)
        └── Diten.EnterpriseStrategyService/
                ├── Diten.AuthService.Persistence/  # MongoDB repos, Seed Data, Indexes
                └── Diten.AuthService.Infrastructure/ # JWT, BCrypt, HasPermission, TenantMiddleware

---

## 🧭 Execution Katmani ve Yetki Modeli

`execution/` klasoru domain ve module bazli calisma baglamini tasir:
- `domain-config.md`: domain sinirlari ve runtime kararlar
- `module-packs/*.md`: tek modulun kimligi, scope'u, acceptance criteria'si

Yetki hiyerarsisi:

```text
Module Pack > Domain Config > AGENTS.md > .antigravity/
```

Notlar:
- `batches/` katmani bu repoda kullanilmaz.
- `snapshots/` katmani bu repoda kullanilmaz.
- Orkestrasyon asamasi `.antigravity/workflows/add-module.md` uzerinden ilerler.

---

## 🔀 Çift Layout Mimarisi (Dual-Layout)

| Layout | Dosya | Kullanıcılar | Durum |
|---|---|---|---|
| **Legacy** | `_Layout.cshtml` | Archive/ | 🔴 FROZEN — Dokunulmaz |
| **Modern** | `_LayoutBackbone.cshtml` | MDM/, Account/, Yeni Modüller | ✅ Aktif (Layout = "_LayoutBackbone") |

---

## 🔐 Auth & RBAC Mimarisi

    Login Flow:
    Browser → Gateway (5000) → AuthService (5056)
                                  ├── JWT Access Token (15dk) + Refresh Token (7gün)
                                  ├── Claims: sub, email, tenant_id, roles[], permissions[]
                                  └── Seed: admin@diten.com / Admin123! (SuperAdmin)

    Permission Model:
    {module}.{resource}.{action}
    Örn: mdm.legal-entities.create, auth.users.assign-role

    RBAC Hierarchy:
    SuperAdmin → Tüm permissions
    Admin      → auth.* + mdm.*
    Viewer     → *.*.read

    MongoDB Collections (diten_auth):
    users, roles, permissions, userRoles, rolePermissions, refreshTokens

---

## 🤖 Uzman Ajan Kadrosu (Full Orchestra)

### Teknik Geliştirme (10 Ajan)
| # | Ajan | Sorumluluk |
|---|---|---|
| 1 | **orchestrator** | Ana şef — görev dağıtımı ve 5 fazlı iş akışı yönetimi |
| 2 | **backend-architect** | .NET 8, CQRS (MediatR), Repository, Domain, Controller |
| 3 | **frontend-ui-ux** | Razor View, Sneat PRO, DataTables v2, Statik Şablonlar |
| 4 | **security-agent** | Zero Trust, JWT, RBAC, HasPermission, Tenant Shield |
| 5 | **data-agent** | MongoDB Index, Collection tasarımı, Idempotent Seed Data |
| 6 | **l10n-agent** | 7 dil .resx senkronizasyonu, `window.L10n` bridge (partial + loader JS) |
| 7 | **testing-agent** | xUnit, Moq, FluentAssertions, Tenant isolation testleri |
| 8 | **integration-agent** | Ocelot Gateway routing, JWT pass-through, servis iletişimi |
| 9 | **debugger** | Katmanlı izolasyon (FE→GW→Auth→Service→DB), 4 fazlı araştırma |
| 10 | **explorer-agent** | Mimari keşif, Sokratik protokol, standart kıyas denetimi |

### Performans & Optimizasyon (1 Ajan)
| # | Ajan | Sorumluluk |
|---|---|---|
| 11 | **performance-optimizer** | CQRS Handler profili, MongoDB explain(), UI render KPI'ları |

### Analiz & Dokümantasyon (5 Ajan)
| # | Ajan | Sorumluluk |
|---|---|---|
| 12 | **business-analyst** | PRD/BRD, IFRS/KVKK uyumluluk, User Story ve iş kuralları |
| 13 | **product-manager** | Ürün stratejisi, MoSCoW önceliklendirme, sistem etki analizi |
| 14 | **product-owner** | Backlog yönetimi, Gherkin AC, MVP/scope kontrolü |
| 15 | **documentation-writer** | API Spec (Swagger), ADR, CHANGELOG, llms.txt |
| 16 | **user-manual-generator** | Son kullanıcı kılavuzları, ekran rehberleri, onboarding |

---

## 🔄 Workflow Komutları (Slash Commands)

### Ana Senaryolar
| Komut | Açıklama |
|---|---|
| **/add-module** | ✅ **ANA SENARYO** — Yeni modülü sıfırdan (Entity → UI) tüm orkestra ile oluşturur |
| **/add-endpoint-cqrs** | Mevcut modüle yeni API ucu, Handler, Validator ve Controller ekler |

### Altyapı & Güvenlik
| Komut | Açıklama |
|---|---|
| **/add-mongo-collection** | Yeni MongoDB koleksiyonu, index ve Seed Data oluşturur |
| **/backend-specialist-bootstrap** | Yeni mikroservis iskeletini 5 katmanlı olarak kurar |
| **/tenant-audit** | TenantId izolasyonu ve Soft Delete uygulaması için kod taraması |

### Kalite & Denetim
| Komut | Açıklama |
|---|---|
| **/release-checklist** | Canlıya alım öncesi 4 fazlı kalite kapısı (Güvenlik, L10n, DB, Test) |
| **/debug** | Diten-specific sistematik hata ayıklama (4 pillar check) |
| **/test** | xUnit test oluşturma/çalıştırma, Tenant safety testi |
| **/details-page-rules** | Detay sayfası UI kuralları (Offcanvas vs Full Page) |

---

## 📂 CQRS, Tenant & Güvenlik Kesin Kuralları

- **Handler Ayrımı**: Handler sınıfları **ASLA** `Commands` veya `Queries` içinde olmayacaktır. Modül altında `Handlers/CommandHandlers` ve `Handlers/QueryHandlers` olarak ayrılmalıdır.
- **Layout Kuralı**: Tüm yeni MDM ve iş modülü sayfaları `_LayoutBackbone.cshtml` kullanmalıdır. `_Layout.cshtml` sadece Archive içindir.
- **Tenant Güvenliği**:
  - `X-Tenant-Id` header kullanımı zorunludur (GUID formatında: `00000000-0000-0000-0000-000000000001`).
  - DTO'lar `TenantId` alanı içeremez; TenantId sunucu tarafında middleware ile çözülür.
  - Veri silme işlemleri **Soft Delete** (`IsDeleted = true`) olarak yapılmalıdır.
  - Başka bir kiracıya ait ID ile erişim denemesinde `404 Not Found` dönülmelidir.
- **Yapılandırma Güvenliği**: `appsettings.json` dışında kod içinde asla bağlantı adresi yazılamaz. Ayar eksikse uygulama Fail-fast ile durmalıdır. (Bkz: `configuration-safety.md`)
- **Auth & RBAC**: Her endpoint `[Authorize]` + `[HasPermission("module.resource.action")]` ile korunmalıdır. Login/Register/Health hariç.

---

## ⚖️ Sistem Anayasası (Rules Directory)

Ajanların uyması gereken zorunlu dosyalar (`.antigravity/rules/`):

### Mimari & Güvenlik
- **erp-architecture.md**: 5 katmanlı mimari, bağımlılık kuralları, CQRS disiplini
- **security-jwt.md**: JWT standartları, Permission-based erişim, Token Passthrough
- **multi-tenancy.md**: GUID TenantId, Soft Delete, izolasyon kuralları
- **configuration-safety.md**: Fail-fast yapılandırma, hardcoded bağlantı yasağı

### API & Networking
- **api-conventions.md**: RESTful naming (/api/v1/), ProblemDetails, HTTP status kodları
- **routes.md**: Gateway Upstream/Downstream standardı, Header kuralları
- **ports.md**: Port registry (5000 GW, 5001 FE, 5056 Auth, 5057 Platform, 5058 DevEnablement)

### Frontend & UI
- **frontend-standards.md**: Sneat UI, DataTable v2, CSS/JS kuralları (MOD-0013/22/23/24)
- **dynamic-localization-standard.md**: 7 dil Resx senkronizasyonu, L10n bridge
- **views-organization.md**: Modül bazlı View hiyerarşisi, Dual-Layout yönetimi
- **details-page-rules.md**: Detay sayfası UI standardı (Offcanvas vs Full Page)

### Operasyonel
- **dev-runbook.md**: 5-servis yerel geliştirme düzeni (Auth → MDM → Platform → GW → FE)
- **logging-observability.md**: Structured logging, CorrelationId, PII koruması
- **mongo-indexing.md**: Tenant-First compound index, ESR kuralı
- **git-backup-policy.md**: Branch naming convention (backup/YYYYMMDD-HHmm_ozet)
- **Git backup standardı:** Varsayılan güvenli yedek yöntemi `.git-backups/` altında `bundle + working-tree.patch + untracked.tar.gz` artefact üçlüsüdür. Branch/commit tabanlı backup yalnız kullanıcı bunu açıkça istediğinde zorunlu hale gelir.
- **Frontend CSS standardı:** Reusable DataTable toolbar / inline filter / Select2 stilleri page-level View içine gömülmez; merkezi olarak `frontend/Diten.Web/wwwroot/assets/css/backbone-custom.css` içinde tutulur.

---

© 2024 Diten Teknoloji — ERP vNext Architecture Standard
