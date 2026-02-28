# ERP-vNext Architecture & Antigravity Kit

> Comprehensive AI Agent Capability Expansion for Diten Ecosystem

---

## 📋 Project Overview

ERP-vNext is a multi-tenant, micro-service based enterprise resource planning system.
- **Core Branding**: Diten
- **Architecture**: Micro-services with Ocelot Gateway
- **Backend Stack**: .NET 8, CQRS (MediatR), MongoDB
- **Frontend Stack**: ASP.NET Core MVC (Diten.Web), Sneat Bootstrap 5.3.3
- **Tenancy**: Single Database, Multi-Tenant (TenantId Filtered)

---

## 🏗️ Directory Structure

    ERP-vNext/
    ├── .antigravity/            # Central Intelligence Hub (Tek Yönetim Merkezi)
    │   ├── agents/              # Specialist Personas
    │   ├── skills/              # Domain Knowledge Modules
    │   ├── workflows/           # Automation Scripts (/commands)
    │   ├── rules/               # System Laws (Anayasa)
    │   └── scripts/             # Validation & Automation Scripts
    ├── frontend/
    │   └── Diten.Web/           # MVC Client Project (Port: 5001)
    │       ├── Views/
    │       │   ├── Shared/
    │       │   │   ├── _Layout.cshtml           # Legacy layout (FROZEN — Archive sayfaları kullanır)
    │       │   │   ├── _LayoutBackbone.cshtml    # Modern layout (MDM + yeni modüller kullanır)
    │       │   │   ├── _GlobalNotification.cshtml # Toast sistemi (paylaşımlı)
    │       │   │   ├── _GlobalConfirmation.cshtml # Modal sistemi (paylaşımlı)
    │       │   │   └── _SkeletonLoader.cshtml    # DataTable shimmer efekti
    │       │   ├── MDM/                          # Aktif modüller (_LayoutBackbone)
    │       │   └── Archive/                      # Legacy sayfalar (_Layout)
    │       ├── wwwroot/assets/
    │       │   ├── css/backbone-custom.css        # Modern CSS (16px rem baz)
    │       │   ├── js/dt-defaults.js              # Merkezi DataTable config
    │       │   └── js/MDM/                        # Modül bazlı JS dosyaları
    │       └── Resources/                         # L10n dosyaları (8 dil, SharedResource + sayfa bazlı)
    ├── gateway/
    │   └── DitenApiGateway/     # Ocelot Gateway (Port: 5000)
    └── services/
        └── DitenMdmService/     # Master Data Management (Port: 5050)

---

## 🔀 Dual-Layout Mimarisi (Production-Safe)

| Layout | Dosya | Kullanıcılar | Durum |
|---|---|---|---|
| **Legacy** | `_Layout.cshtml` | Archive/, Identity/ | 🔴 FROZEN — Dokunulmaz |
| **Modern** | `_LayoutBackbone.cshtml` | MDM/, yeni modüller | ✅ Aktif geliştirme |

`_ViewStart.cshtml` default olarak `_Layout`'u gösterir. Modern sayfalar `Layout = "_LayoutBackbone"` ile override eder.

---

## 🤖 Specialist Agents (Focus: ERP-vNext)

- **orchestrator**: System-wide task delegation and workflow management.
- **backend-specialist**: .NET 8, CQRS, Mongo & Multi-tenancy expert.
- **frontend-specialist**: Diten.Web MVC & DataTable v2 architecture expert.
- **explorer-agent**: Project-wide code analysis & discovery.
- **test-engineer**: Smoke tests, Integration tests, and tenant auditing.

---

## 🔄 Custom ERP Workflows (Slash Commands)

- **/fix-project-names**: Renames legacy namespaces to `Diten.*` and updates `.sln/.csproj`.
- **/add-endpoint-cqrs**: Generates Domain, DTO, Command, Handler, and Controller for MDM.
- **/tenant-audit**: Scans codebase for mandatory `TenantId` implementation.
- **/dev-up-and-smoke-test**: Starts Gateway/MDM and runs basic connectivity checks.
- **/add-gateway-route**: Automatically updates `ocelot.json` for new service endpoints.

---

## 📂 CQRS Klasör Yapısı Kuralları

- **Model vs Handler Ayrımı:** 
  - Handler sınıfları **kesinlikle** `Commands` veya `Queries` klasörlerinin içinde **OLMAYACAKTIR**.
  - Bunun yerine her feature altında ayrı bir `Handlers` klasörü oluşturulmalıdır.
  - O klasörün de altında `CommandHandlers` ve `QueryHandlers` klasörleri yer alacaktır.

---

## ⚖️ System Rules (Rules Directory)

Ajanların uyması gereken zorunlu anayasalar (`.antigravity/rules/`):
- **api-conventions.md**: RESTful route naming (lowercase) ve standard response tipleri.
- **erp-architecture.md**: Genel ERP mimari prensipleri.
- **multi-tenancy.md**: Guid TenantId zorunluluğu ve X-Tenant-Id header kuralları.
- **ports.md**: Frontend (5001), Gateway (5000) ve MDM (5050) port standartları.
- **mongo-indexing.md**: MongoDB için performans ve tenant bazlı index kuralları.
- **dev-runbook.md**: 3 tab geliştirme düzeni ve yerel çalışma kuralları.
- **frontend-standards.md**: CSS, JS, Asset, Build ve UI kuralları (MOD-0013 genişlemesi).
- **dynamic-localization-standard.md**: L10n bridge, resx sync ve çeviri kuralları.
- **views-organization.md**: Modül bazlı View gruplama ve Layout atama kuralları.