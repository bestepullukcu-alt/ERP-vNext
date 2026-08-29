# PV — ERP-vNext Mevcut Durum & Gap Matrisi (D + I)

> Kural: ERP-vNext'te bir özellik yalnızca Blueprint/spec/registry satırında geçiyorsa **implemented sayılmaz**. Kod/test/runtime kanıtı yoksa "planned/contract only".

## D. ERP-vNext capability karşılaştırması

| PV Capability | ERP-vNext karşılığı | Module ID | Status (kanıt) | Legacy'yi karşılıyor mu? |
|---|---|---|---|---|
| Safety Case / AE intake | — | (yok) | **NOT_IMPLEMENTED** — registry'de 0 PV rezervasyonu | Hayır |
| Patient / Reporter / Event | — | (yok) | NOT_IMPLEMENTED | Hayır |
| Seriousness/Expectedness/Causality | — | (yok) | NOT_IMPLEMENTED | Hayır |
| Case narrative / versioning / follow-up | — | (yok) | NOT_IMPLEMENTED | Hayır |
| Signal / Literature / MedDRA / WHODrug | — | (yok) | NOT_IMPLEMENTED | Hayır (legacy'de de yok) |
| Authority reporting / reconciliation | — | (yok) | NOT_IMPLEMENTED | Hayır |
| PV Organization / MAH | Organization, Person & Position Directory | MOD-0288 | **done** (runtime Platform'da) | Kısmen (org/person seam) |
| Registration / Marketing Authorization | — (Regulatory/MAH domain yok) | (yok) | NOT_IMPLEMENTED | Hayır |
| Product / SKU (GlobalSku) | Product / Item / SKU Master | MOD-0290 | **draft**, `runtime_code_allowed:false` | Hayır (CONTRACT_ONLY) |
| Pharmaceutical form / Active ingredient | Reference Data Management | MOD-0048 | ready-for-dev (pack) | Kısmen (FOUNDATION) |
| Reference/lookup (country/authority) | Reference Data Management | MOD-0048 | ready-for-dev | PARTIALLY_AVAILABLE |
| RBAC / authorization | RBAC/ABAC + AuthService JWT | MOD-0018 | foundation + tenant-scoped token (runtime) | **PARTIALLY_AVAILABLE — legacy'den güçlü** |
| Audit trail | Audit Trail Service | MOD-0021 | ready-for-dev / implemented evidence | PARTIALLY_AVAILABLE (legacy'de yok) |
| Workflow / approval | Workflow Designer | MOD-0023 | review/planned (pack) | FOUNDATION/CONTRACT_ONLY |
| Task/checklist | Task & Checklist Engine | MOD-0024 | review/planned | CONTRACT_ONLY |
| Scheduler / job | Scheduler / Job Orchestration | MOD-0026 | **done** | PARTIALLY_AVAILABLE |
| Notification | Notification Service | MOD-0027 | approved (pack) | PARTIALLY_AVAILABLE (legacy'de sadece Gmail) |
| Document / evidence | Documentation & Evidence Mgmt | MOD-0028 | review | PARTIALLY_AVAILABLE |
| Controlled documents / SOP | Controlled Documents | MOD-0029 | planned (runtime kanıtı repo'da) | PARTIALLY_AVAILABLE |
| Tenant / environment | Tenant / Environment Mgmt | MOD-0009 | in-progress | PARTIALLY_AVAILABLE |
| E-signature | — | (yok) | NOT_IMPLEMENTED | Hayır (legacy'de de yok) |

**Mevcut implemented servisler:** `Diten.AuthService`, `Diten.CrmService`, `Diten.DevEnablementService`, `Diten.EnterpriseStrategyService`, `Diten.HcmService`, `Diten.MdmService`, `Diten.Platform` (+ Common/Contracts/Building.Blocks). **PV/Safety/Regulatory servisi yok.**

## I. Gap analizi

| Gap ID | Capability | Current state (ERP-vNext) | Required state | Severity | Dependency | Recommendation |
|---|---|---|---|---|---|---|
| PVG-01 | PV domain boundary | Registry'de 0 rezervasyon | Safety/Regulatory domain + module ID'ler | **P0** | Governance | DCP + module-id-registry rezervasyonu aç |
| PVG-02 | Safety Case aggregate | Yok | Case (intake→lifecycle) aggregate | **P0** | PVG-01, MOD-0290, MOD-0048 | Greenfield inşa (god-entity DEĞİL) |
| PVG-03 | Endpoint auth | (legacy) Yok | JWT + tenant-scoped + RBAC | **P0** | MOD-0018, AuthService | Platform auth zorunlu tüket |
| PVG-04 | Tenant isolation | (legacy) host-based | Identity/tenant-claim based | **P0** | MOD-0009/0018 | Host-based middleware RETIRE |
| PVG-05 | Audit trail | MOD-0021 hazır ama PV bağlı değil | Her PV mutasyonu MOD-0021'e | **P0** | MOD-0021 | Case olaylarını audit'e bağla |
| PVG-06 | E-signature | Yok | Onay/imza (GxP) | **P0** | Workflow/Audit | Yeni yetenek gerek |
| PVG-07 | Validation evidence | Yok | IQ/OQ/PQ + controlled release | **P0** | QMS | Doküman + kanıt paketi |
| PVG-08 | Product/SKU master | MOD-0290 draft | Runtime SoR (GlobalSku karşılığı) | **P1** | MDM | MOD-0290-FU01+ | 
| PVG-09 | MedDRA coding | Yok | Reaksiyon kodlama sözlüğü | **P1** | Reference/dictionary | Lisans + entegrasyon |
| PVG-10 | WHO Drug coding | Yok | Ürün kodlama | **P1** | Reference/dictionary | Lisans + entegrasyon |
| PVG-11 | Case versioning | (legacy) string | Gerçek versiyon/geçmiş | **P1** | Case aggregate | Event-sourced/versioned |
| PVG-12 | Structured follow-up | (legacy) string | Follow-up koleksiyonu | **P1** | Case aggregate | Yeniden modelle |
| PVG-13 | Duplicate detection | Yok | Otomatik tespit | **P1** | Case aggregate | Kural + dedupe |
| PVG-14 | Authority reporting/E2B | (legacy) link/özet | Otorite gönderim entegrasyonu | **P1** | Integration | Yeni yetenek |
| PVG-15 | Workflow bağlama | MOD-0023 review | Case lifecycle MOD-0023 ile | **P2** | MOD-0023 | Entegre et |
| PVG-16 | Notification bağlama | MOD-0027 approved | Deadline/submission uyarıları | **P2** | MOD-0027, MOD-0026 | Entegre et |
| PVG-17 | Evidence/attachment | MOD-0028 review | Ekler DocMgmt'te (disk DEĞİL) | **P2** | MOD-0028 | Local-disk RETIRE |
| PVG-18 | Reporting/KPI | Yok | PV read-model/KPI | **P2** | Case aggregate | Read model |
| PVG-19 | Data retention/archive | Yok | Politika + arşiv | **P2** | Platform | Politika tanımı |
| PVG-20 | Migration tooling | Yok | Idempotent Mongo→ERP migrasyon | **P2** | Case aggregate | Bkz. data-migration-assessment |
| PVG-21 | Observability | Kısmi (MOD-0041) | PV metrik/log/trace | **P3** | Platform | Sonra |
| PVG-22 | Signal management | (legacy) bool | Gerçek sinyal yönetimi | **P3** | Case aggregate + analytics | İleri faz |
