# Platform Modülleri — Eksik Geliştirme & Remediation + Yol Haritası

Öncelik: **P0** STOP-SHIP · **P1** REQUIRED · **P2** IMPORTANT · **P3** DEFERRED

## I. Eksik Geliştirme Listesi

| Gap ID | Module | Missing capability | Current evidence | Required implementation | Dependency | Severity |
|---|---|---|---|---|---|---|
| GAP-01 | MOD-0019 | Data masking + row/field policy engine | Yalnız `PiiMasking.cs` (log redaction) | Classification + masking rules + row-level security + query enforcement + response masking + policy test harness | MOD-0018 | **P0** (PV) |
| GAP-02 | MOD-0021 | Kriptografik tamper-evidence zinciri | `AuditEvent` hash alanı yok | Hash/previousHash zinciri veya WORM sink | MOD-0021 core | **P1** |
| GAP-03 | MOD-0021 | Authenticated runtime golden-flow smoke | Offline test var, closeout yok | Login→critical action→explorer→export smoke | Platform startup | **P1** |
| GAP-04 | MOD-0021 | Audit-writer failure davranışı | Outbox var; failure runtime doğrulanmadı | Fail-closed vs fail-open kararı + test | — | **P1** |
| GAP-05 | MOD-0018 | ABAC / real data-scope resolver | FU15 planned | Attribute policy + `DataScopeResolver` | MOD-0288 backing data | **P1** |
| GAP-06 | MOD-0018 | RBAC Admin UI | FU9 planned | Role/Permission Builder, Policy Console, Access Review Dashboard | RBAC core | **P2** |
| GAP-07 | MOD-0028 | FU06 Corporate unique index | `$ne` partial index Platform startup'ı çökertiyor | Index filtresini `$type/$lt`'e çevir (bkz. memory `mongo-partial-index-ne-crash`) | Mongo | **P1** |
| GAP-08 | MOD-0028 | Checksum / version-compare runtime | Controller var; runtime doğrulanmadı | Upload checksum + version diff smoke | FU06 fix | **P2** |
| GAP-09 | MOD-0023 | Uçtan-uca approval golden-flow smoke | "runtime mevcut" iddiası | approve/reject/request-info authenticated smoke + designer UI olgunluğu | RBAC+Audit | **P1** |
| GAP-10 | MOD-0031 | EvidenceLink SoR (tüm katmanlar) | Yalnız pack | Domain+persistence+API+gateway+UI panel | MOD-0028, MOD-0021 | **P1** |
| GAP-11 | MOD-0040 | Canonical/external ID mapping modeli | Correlation foundation var | ExternalReference model + ObjectId mapping + trace stitching contract | Interface Registry | **P1** |
| GAP-12 | MOD-0040 | Registry kimlik çelişkisi (CONF-01) | MOD-0040→MOD-0288 alias | EA reservation ile Blueprint'e hizala | — | **P1** |
| GAP-13 | MOD-0004 | Metric & semantic registry (tüm) | Hiç yok | Metric def + calc contract + certification + KPI catalog + UI | SoR/Data Contract Registry | **P2** |
| GAP-14 | MOD-0063 | Lakehouse (tüm) | Hiç yok | Dataset catalog + ingestion + storage + ACL + lineage | Data Contract Registry | **P2** |
| GAP-15 | Genel | Correlation servisler-arası tutarlılık contract testi | Kısmi | Cross-service correlation propagation testi | MOD-0040 | **P2** |

## K. Uygulama Yol Haritası (Fazlı)

### Faz 0 — Governance Düzeltmesi (S)
- **Modüller:** MOD-0040 kimlik (CONF-01).
- **Amaç:** Registry'yi Blueprint'e hizala; MOD-0040 = Canonical ID & Correlation için EA reservation + pack.
- **İşler:** registry düzeltme, pack authoring. **Frontend:** yok. **Permission/audit/event:** yok.
- **Exit gate:** `verify_module_id.py` PASS, registry-Blueprint tutarlı.

### Faz 1 — Platform Güvenlik & Denetim Zemini (L)
- **Modüller:** MOD-0018 (ABAC/data-scope), MOD-0021 (tamper-evidence + runtime smoke).
- **Golden flow:** permission→role→login→decision→403; critical action→AuditEvent→explorer→export.
- **Failure path:** cross-tenant/data-scope leakage 404; audit-writer failure fail-closed.
- **Bağımlılıklar:** MOD-0288 backing data (data-scope).
- **Backend:** `DataScopeResolver`, ABAC policy eval, audit hash zinciri, fail-closed writer. **Frontend:** Access Review (P2, ertelenebilir). **Permission/audit/event:** data-scope perms, meta-audit.
- **Test:** ABAC decision + data-scope leakage + audit tamper + writer-failure.
- **Exit gate:** authenticated smoke yeşil; leakage testi geçer.
- **Büyüklük:** L.

### Faz 2 — Correlation & Canonical ID (M)
- **Modüller:** MOD-0040.
- **Golden flow:** request→correlation→downstream aynı ID→canonical mapping→trace stitching.
- **Failure path:** correlation kayıp guard; aynı external ID→tek canonical (dedup).
- **Bağımlılıklar:** Faz 0.
- **Backend:** ExternalReference model, ObjectId mapping, trace stitching contract, cross-service test. **Frontend:** yok.
- **Exit gate:** cross-service correlation propagation testi geçer; PV migration mapping hazır.
- **Büyüklük:** M.

### Faz 3 — Content & Evidence (L)
- **Modüller:** MOD-0028 (FU06 fix + hardening), MOD-0031 (yeni).
- **Golden flow:** document create/version/permission/audit; object↔evidence link→provenance→reopen→export.
- **Failure path:** metadata/content atomikliği; silinmiş/yetkisiz evidence sızıntı guard.
- **Bağımlılıklar:** Faz 1 (RBAC+Audit).
- **Backend:** FU06 index fix, checksum/version-compare, EvidenceLink SoR + API + gateway. **Frontend:** Evidence Panel + register. **Permission/audit/event:** evidence perms + audit.
- **Test:** version reload + evidence link + provenance + cross-tenant.
- **Exit gate:** FU06 authenticated smoke yeşil; evidence link golden-flow yeşil.
- **Büyüklük:** L.

### Faz 4 — Workflow Olgunlaştırma (M)
- **Modüller:** MOD-0023.
- **Golden flow:** definition→publish→instance→task inbox→approve/reject/request-info→history/audit.
- **Failure path:** invalid transition / unauthorized approval / expired row-version.
- **Bağımlılıklar:** Faz 1.
- **Backend:** SLA/escalation runtime doğrulama. **Frontend:** designer/SLA console olgunluğu. **Permission/audit/event:** workflow audit derinliği.
- **Exit gate:** uçtan-uca approval smoke yeşil.
- **Büyüklük:** M.

### Faz 5 — Data Masking (M–L) — **PV için Faz 1'e çekilir**
- **Modüller:** MOD-0019.
- **Golden flow:** classification→masking policy→role bind→maskeli/tam görünüm→API'de aynı kural.
- **Failure path:** UI-maskeli/API-ham engellenir (server-side enforcement).
- **Bağımlılıklar:** Faz 1 (RBAC).
- **Backend:** policy engine + query/serialization enforcement + test harness. **Frontend:** Masking Rules Studio + Data Access Matrix.
- **Exit gate:** API-level masking testi geçer (UI-only değil).
- **Büyüklük:** M–L.

### Faz 6 — Semantic & Analytics (XL)
- **Modüller:** MOD-0004, MOD-0063.
- **Bağımlılıklar:** SoR/Data Contract/Data Dictionary registry'leri (henüz yok) — bu faz onlar olmadan başlamaz.
- **Exit gate:** metric certify + dataset ingestion/lineage golden-flow.
- **Büyüklük:** XL.

### Paralellik / Çakışma Uyarısı
- Faz 1 (0018-sec & 0021) ile Faz 5 (0019) **aynı security service** yüzeyine dokunur → decision/policy contract dondurulmadan paralel yapılmamalı.
- Faz 3 içi 0028→0031 **sıralı** (aynı document/evidence runtime).
- Faz 2 (0040) izole (Building.Blocks) → diğerleriyle paralel güvenli.
