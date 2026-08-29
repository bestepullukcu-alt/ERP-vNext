# Platform Modülleri — Capability & Yüzdesel Tamamlanma Matrisi

> Yüzdeler **dosya sayısıyla değil**, capability ağırlığıyla (E bölümü) hesaplandı. Kanıt yetersizse aralık + confidence verildi.
> Ağırlıklar: Domain/SoR %15 · Persistence %10 · Application %15 · API/contracts %10 · Security/RBAC %10 · Audit/evidence %10 · Frontend %10 · Integration/events %5 · Tests %10 · Runtime golden-flow %5.

## 1. Boyut Bazında Yüzdeler

| Modül | Spec readiness | Backend impl | Frontend impl | Integration | Test coverage | Runtime usability | **Overall (ağırlıklı)** | Confidence |
|---|---|---|---|---|---|---|---|---|
| MOD-0004 Metric & Semantic Registry | %60 (blueprint net) | %0 | %0 | %0 | %0 | %0 | **%5–10** | Low |
| MOD-0018 RBAC/ABAC | %85 | %75 (RBAC) / %20 (ABAC) | %15 (admin UI yok) | %70 | %70 | %70 (login+enforcement) | **%62–75** | Med |
| MOD-0019 Data Masking | %70 (blueprint) | %10 | %0 | %5 | %5 | %0 | **%8–15** | Med |
| MOD-0021 Audit Trail | %90 | %85 | %55 (AuditLog/Retention views) | %70 | %80 | %40 (adanmış smoke yok) | **%70–80** | Med |
| MOD-0023 Workflow Designer | %85 | %80 | %55 | %60 | %70 | %45 | **%60–72** | Med |
| MOD-0028 Documentation Mgmt | %90 | %80 | %75 | %65 | %70 | %55 (FU06 blocked) | **%68–78** | Med |
| MOD-0031 Evidence Linking | %75 (pack detaylı) | %0 | %0 | %0 | %0 | %0 | **%8–12** | High |
| MOD-0040 Canonical ID & Correlation | %55 | %30 (correlation) / %0 (canonical ID) | %0 | %40 | %20 | %25 | **%25–35** | Med |
| MOD-0063 Data Warehouse/Lakehouse | %55 (blueprint) | %0 | %0 | %0 | %0 | %0 | **%5** | High |

## 2. Functional Completion vs Production Readiness vs PV Reuse Readiness

| Modül | Functional completion % | Production readiness % | PV reuse readiness % | Not |
|---|---|---|---|---|
| MOD-0004 | %5 | %0 | %0 | Hiç kod yok |
| MOD-0018 | %68 | %55 | %50 | RBAC prod-ready; ABAC/data-scope + admin UI eksik; PV için `pv.*` catalog + data-scope gerek |
| MOD-0019 | %10 | %0 | %0 | Policy engine yok; PV hasta PII maskeleme **P0 blocker** |
| MOD-0021 | %78 | %55 | %55 | Tamper-evidence zinciri + authenticated smoke + non-repudiation (e-sig) eksik |
| MOD-0023 | %70 | %50 | %50 | Backend tam; adanmış golden-flow smoke + designer UI olgunluğu eksik |
| MOD-0028 | %75 | %55 | %55 | ControlledDocuments çalışıyor; FU06 corporate blocked; checksum/version-compare kısmi |
| MOD-0031 | %8 | %0 | %0 | Yalnız pack |
| MOD-0040 | %30 | %25 | %20 | Correlation foundation var; canonical/external ID mapping (PV migration) yok |
| MOD-0063 | %5 | %0 | %0 | Hiç kod yok |

## 3. Durum Sınıflandırması (özet)

| Modül | Status |
|---|---|
| MOD-0004 | SPECIFICATION_ONLY |
| MOD-0018 | PARTIALLY_IMPLEMENTED (RBAC core → FULLY_IMPLEMENTED_AND_RUNTIME_PROVEN; ABAC/admin-UI → NOT_IMPLEMENTED) |
| MOD-0019 | NOT_IMPLEMENTED |
| MOD-0021 | IMPLEMENTED_BUT_RUNTIME_NOT_PROVEN |
| MOD-0023 | PARTIALLY_IMPLEMENTED |
| MOD-0028 | PARTIALLY_IMPLEMENTED |
| MOD-0031 | SPECIFICATION_ONLY |
| MOD-0040 | FOUNDATION_ONLY (+ registry WRONG_BOUNDARY: MOD-0040 kimliği MOD-0288'e kaymış) |
| MOD-0063 | SPECIFICATION_ONLY |
