# Master Data Management (MDM)

**Kısaltma:** `MDM`
**Module ID prefix:** `MDM-NNN`
**Ana servis:** `services/Diten.MdmService/` (Port 5050)

## İş Tanımı

Master Data Management domain'i; kurum genelinde ortak kullanılan referans ve ana verilerin yaşam döngüsünü, sahipliğini ve tutarlılığını yönetir.

## Kapsam (Yüksek Seviye)

- Referans verilerin merkezi yönetimi
- Ürün, SKU, ülke, kategori gibi ana verilerin katalog yönetimi
- Modüller arası veri sahipliği ve sınırlarının netleştirilmesi
- Domain içi modül paketlerinin dalga (wave) planına göre yürütülmesi

## Kapsam Dışı

- Kimlik, yetki ve platform altyapısı (→ platform-shared-services)
- Strateji/KPI ve performans yönetimi (→ enterprise-strategy-business-performance)

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md)
- [module-packs/](module-packs/)

> Tarihsel `controls/` ve `decisions/` katmanları `archive/domains/master-data-management/` altına taşınmıştır; otorite `AGENTS.md` + `.antigravity/rules/` + `docs/platform/master-plan.md`.

## Yeni Modül Eklerken

1. `module-packs/MDM-{NNN}-{slug}.md` dosyası oluştur
2. Branch adını `feature/mdm/mdm-{nnn}-{slug}` formatında aç
3. Orchestrator ile `/add-module` workflow'u çalıştır
