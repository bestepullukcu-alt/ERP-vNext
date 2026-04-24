# Enterprise Strategy & Business Performance — Domain Config

> **İskelet dosya.** Aktif ESBP çalışması başladığında detaylı doldurulacak.

## Purpose
Kurumsal stratejinin modellenmesi ve iş performansının ölçümü.

## In-Scope Modules
(ESBP altındaki modüller — ihtiyaç doğdukça eklenir)

## Out-of-Scope
- Master veri yönetimi (→ master-data-management)
- Kimlik/erişim (→ platform-shared-services)

## Domain-Level Repo Scope
- `services/Diten.EnterpriseStrategyService/**`
- `frontend/Diten.Web/Views/EnterpriseStrategy/**` (varsa)
- `frontend/Diten.Web/Resources/Views/EnterpriseStrategy/**`
- `gateway/Diten.ApiGateway/.../ocelot.json` (sadece ESBP rotaları)

## Protected Paths
(Detay ESBP çalışması başladığında)

## Runtime Decisions
(ESBP-spesifik kararlar — ihtiyaç doğdukça)

## Shared Dependencies
- `.antigravity/` global standartları
- Gateway (Ocelot)
- MDM'den alınan master veri referansları (read-only)
