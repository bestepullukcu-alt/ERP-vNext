# ERP-vNext Architecture & Antigravity Kit

> Comprehensive AI Agent Capability Expansion for Diten Ecosystem

---

## 📋 Proje Özeti (Project Overview)

ERP-vNext; çok kiracılı (multi-tenant), mikro hizmet tabanlı bir kurumsal kaynak planlama (ERP) sistemidir.
- **Marka**: Diten
- **Mimari**: Ocelot Gateway ile Mikro Servisler
- **Backend**: .NET 8, CQRS (MediatR), MongoDB
- **Frontend**: ASP.NET Core MVC (Diten.Web), Sneat Bootstrap 5.3.3
- **Tenancy**: Tek Veritabanı, Çoklu Kiracı (TenantId Filtreli - Guid)
- **Localization**: 8 Dil (TR, EN, RU, ES, KA, KK, UK, UZ) - Resx + JS Bridge (window.L10n)

---

## 🏗️ Klasör Hiyerarşisi (Directory Structure)

    ERP-vNext/
    ├── .antigravity/            # Tek Yönetim Merkezi (Merkezi İstihbarat)
    │   ├── agents/              # Uzman Personalar
    │   ├── skills/              # Teknik Yetenek Modülleri
    │   ├── workflows/           # Otomasyon Akışları (/komutlar)
    │   ├── rules/               # Sistem Anayasası (Kurallar)
    │   └── scripts/             # Doğrulama ve Otomasyon Scriptleri
    ├── frontend/
    │   └── Diten.Web/           # MVC Projesi (Port: 5001)
    │       ├── Views/
    │       │   ├── Shared/
    │       │   │   ├── _LayoutBackbone.cshtml    # Modern layout (MDM + Yeni modüller)
    │       │   │   └── _SkeletonLoader.cshtml    # DataTable shimmer efekti
    │       │   ├── MDM/                          # Aktif modüller
    │       │   └── Archive/                      # Legacy sayfalar (_Layout)
    │       ├── Resources/                         # 8 Dil Resx Dosyaları
    ├── gateway/
    │   └── DitenApiGateway/     # Ocelot Gateway (Port: 5000)
    └── services/
        └── DitenMdmService/     # MDM Servisi (Port: 5050)

---

## 🔀 Çift Layout Mimarisi (Dual-Layout)

| Layout | Dosya | Kullanıcılar | Durum |
|---|---|---|---|
| **Legacy** | `_Layout.cshtml` | Archive/, Identity/ | 🔴 FROZEN — Dokunulmaz |
| **Modern** | `_LayoutBackbone.cshtml` | MDM/, Yeni Modüller | ✅ Aktif (Layout = "_LayoutBackbone") |

---

## 🤖 Uzman Ajanlar (Specialist Agents)

- **orchestrator**: Sistem geneli görev dağıtımı ve iş akışı yönetimi.
- **backend-specialist**: .NET 8, CQRS, Mongo & Multi-tenancy uzmanı.
- **frontend-specialist**: Diten.Web MVC, DataTable v2 & Sneat UI uzmanı.
- **explorer-agent**: Proje geneli kod analizi ve keşif.
- **test-engineer**: Tenant güvenlik denetimi ve entegrasyon testleri uzmanı.

---

## 🔄 Özel ERP Workflow Komutları (Slash Commands)

- **/fix-project-names**: Legacy namespace'leri `Diten.*` olarak günceller.
- **/add-endpoint-cqrs**: MDM için Domain, DTO, Command, Handler ve Controller üretir.
- **/tenant-audit**: Kod tabanını zorunlu `TenantId` uygulaması için tarar.
- **/dev-up-and-smoke-test**: Gateway/MDM servislerini başlatır ve bağlantı testi yapar.
- **/add-gateway-route**: Yeni servisler için `ocelot.json` dosyasını günceller.

---

## 📂 CQRS & Tenant Kesin Kuralları

- **Handler Ayrımı:** Handler sınıfları **kesinlikle** `Commands` veya `Queries` klasörlerinin içinde **OLMAYACAKTIR**. Feature altında `Handlers/CommandHandlers` ve `Handlers/QueryHandlers` olarak ayrılmalıdır.
- **Tenant Güvenliği:** - `X-Tenant-Id` header kullanımı zorunludur.
  - DTO'lar asla `TenantId` alanı içermez.
  - Veritabanı katmanı (Persistence) dışında `MongoDB.Driver` kullanımı yasaktır.

---

## ⚖️ Sistem Anayasası (Rules Directory)

Ajanların uyması gereken zorunlu dosyalar (`.antigravity/rules/`):
- **api-conventions.md**: RESTful route isimlendirme (lowercase) kuralları.
- **erp-architecture.md**: Genel mimari prensipler.
- **multi-tenancy.md**: Guid TenantId ve izolasyon kuralları.
- **mongo-indexing.md**: Performans ve tenant bazlı index kuralları.
- **frontend-standards.md**: CSS, JS, UI ve window.L10n (L10n Bridge) kuralları.
- **views-organization.md**: Modül bazlı View gruplama ve Layout atama kuralları.
- **details-page-rules.md**: Salt okunur detay sayfası UI standartları.
- **configuration-safety.md**: Kod içinde asla "mongodb://..." gibi bağlantı adresi (connection string) yazılamaz. Ayarlar mutlaka `appsettings.json` üzerinden okunmalı, ayar eksikse uygulama hata fırlatarak (Fail-fast) durmalıdır.