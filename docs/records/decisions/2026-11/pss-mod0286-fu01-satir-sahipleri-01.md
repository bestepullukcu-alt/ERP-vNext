---
record_id: PSS-MOD0286-FU01-SATIR-SAHIPLERI-01
status: decided — sahip kararı `fu01-satir-sahipleri` ve sahip tablosu için "Evet, öneri uygun" (2026-11-01, PSS-UI CT sohbetinde sahibin kendi sözüyle)
prepared_at_local: 2026-11-01
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md (rev 04, d970554b…, main'de onaylı, PR #328)
queue: pss-ui
work_package: PSS-UI-FU01-01
---

# MOD-0286-FU01 — sayfa JS eşlemesinin borç satırları kararı

## Neden bir karar

Paketin açık kararı O-1: `FU-19` satırlarının karar adı ve her satırın sahibi sahibindir; dağıtımın ön koşuludur
(Owned Objects bölüm 6, AC-8). Ad sahibe önerilen öneriyle birlikte CT tarafından konuldu; paket yalnız
`pss-mod0286-taban-01` ve `tek-seviye-borc-01` adlarını yasaklar. Sahip öneri tablosunu kabul etti.

## Karar

1. **Satırlar:** iş paketinin taban commit'indeki `oracle satirlar` çıktısı, ölçüldüğü gibi (2026-11-01'de 22 satır,
   92 eşleşme). Hiçbir kırmızı elle çıkarılmaz ya da eklenmez; mevcut taban satırları değişmez.
2. **`fu`:** `FU-19`.
3. **`reason`:** her satırda `sahip kararı fu01-satir-sahipleri (2026-11-01)`.
4. **`owner`:** aşağıdaki tablo; makine okuması aynı klasördeki `pss-mod0286-fu01-satir-sahipleri-01.tsv`. Tablo bir
   sahiplik etiketidir, kod sahipliği değiştirmez. Borcu her alan kendi sayfasına sonraki dokunuşunda ya da kendi işiyle
   kapatır (FU-19, XMC-001); bu iş hiçbir kırmızıyı düzeltmez.

| Sayfa | Satır | Sahip |
|---|---|---|
| EnterpriseStrategyBusinessPerformance | 11 | enterprise-strategy-business-performance |
| EnterpriseStrategyBusinessPerformance/Components | 3 | enterprise-strategy-business-performance |
| DemandIdeas | 3 | portfolio-delivery |
| DeliveryExecutionManagement | 1 | portfolio-delivery |
| DecompositionTreeBuilder | 1 | portfolio-delivery |
| ManagementGovernance/ProcessModeling | 1 | management-governance |
| Meetings/Series | 1 | platform-shared-services |
| Meetings/MeetingTypes | 1 | platform-shared-services |

Taban commit'inde tabloda olmayan bir sayfa çıkarsa iş durur ve sahibe sorulur; CT sahip uydurmaz.
