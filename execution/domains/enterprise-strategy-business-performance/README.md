# Enterprise Strategy & Business Performance (ESBP)

**Kısaltma:** `ESBP`
**Module ID prefix:** `ESBP-NNN`
**Servis:** `services/Diten.EnterpriseStrategyService/`

## İş Tanımı

Enterprise Strategy & Business Performance domain'i, kurumsal stratejik hedeflerin **modellenmesi**, **iş performansının ölçülmesi** ve **KPI/OKR yönetimi** yeteneklerini sağlar.

## Kapsam (Yüksek Seviye)

- Strateji modelleme (vision, mission, goals, objectives)
- KPI / OKR yönetimi
- İş performans ölçümü ve raporlama
- Stratejik hedeflerin modüller arası referanslanması

## Kapsam Dışı

- Master veri yönetimi (→ MDM)
- Kullanıcı/kimlik yönetimi (→ PSS)

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md)
- [decisions/](decisions/)
- [controls/](controls/)
- [module-packs/](module-packs/)

## Yeni Modül Eklerken

1. `module-packs/ESBP-{NNN}-{slug}.md` oluştur
2. Branch: `feature/esbp/esbp-{nnn}-{slug}`
3. Orchestrator çağır, `/add-module` workflow çalışır
