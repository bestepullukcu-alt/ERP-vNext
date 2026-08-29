# Commercial Suite (CRM) — Domain Foundation & MOD-0018 RBAC Integration Preflight

**Date:** 2026-07-14 · **Type:** governance / planning preflight (read-only + scaffold) · **Verdict:** PARTIAL

## Scope

CRM / Commercial Suite domain foundation + build-lane yapısı + MOD-0018 RBAC entegrasyon kararlarının hazırlığı.
Kod / runtime / migration / seed / ocelot / menü değişikliği YOK. Yeni RBAC sistemi kurulmadı; mevcut MOD-0018'e mapping.

## Read (authority order)

AGENTS.md · .antigravity/agents/module-pack-author.md · .antigravity/rules/permission-key-standard.md (PKS-001) ·
execution/registries/module-id-registry.md · execution/registries/module-implementation-status.md ·
execution/portfolio/master-development-plan.md · execution/domains/platform-shared-services/domain-config.md ·
execution/domains/master-data-management/README.md · docs/how-to-add-a-module.md ·
services/Diten.AuthService/.../DefaultRolePermissionTemplate.cs · Blueprint `Blueprint_Data` (xlsx, node ile parse).

## Key findings

1. **Domain yok:** `commercial-suite` / `crm` / `sales` / `customer` / `marketing` domain'i mevcut değil. Öneri:
   `execution/domains/commercial-suite/` (Blueprint suite adı = "Commercial Suite (CRM + O2C)").
2. **27 MOD ID Blueprint-canonical:** MOD-0149…0172, MOD-0282…0284 hepsi `Blueprint_Data`'da mevcut; canonical ad +
   wave çıkarıldı. **Registry'de hiçbiri kayıtlı değil, çakışma yok.** (Not: MOD-0169 registry'de "Platform Reference"
   RETIRED stub; Blueprint MOD-0169 = Billing & Invoicing gerçek yeteneği için ayrılmış — canonicalization uyumlu.)
3. **verify_module_id.py çalıştırılamadı:** ortamda Python yok. Komutlar module-packs/README.md'ye listelendi.
4. **RBAC mevcut ve olgun:** PKS-001 `module.resource.action` lowercase-dotted; `crm.*` namespace §4'te önceden
   ayrılmış; entitlement→permission köprüsü mevcut; DefaultRolePermissionTemplate (SuperAdmin/Admin/Viewer).
5. **ABAC boşluğu:** Real DataScopeResolver = MOD-0018-FU15 = `planned/reserved`. Territory/team scoping bloklu.
6. **Navigation:** genel modüller için dinamik loader yok; permission-guard'lı elle `<li>` gerekir (baseline modüller
   için DynamicModuleMenu var).

## Deliverables (created)

- execution/domains/commercial-suite/README.md
- execution/domains/commercial-suite/domain-config.md
- execution/domains/commercial-suite/module-packs/README.md
- execution/domains/commercial-suite/crm-build-lanes.md
- execution/domains/commercial-suite/crm-rbac-integration-plan.md
- execution/domains/commercial-suite/crm-sor-boundary.md
- execution/domains/commercial-suite/legacy-value-preservation.md
- docs/audits/commercial-suite-crm-domain-rbac-preflight-2026-07-14.md (bu dosya)

## Required follow-ups (propose-only, not changed here)

- **AGENTS.md §9** branch short-code listesine `crm` eklenmesi (EA onayı).
- **module-id-registry.md** 27 satır reservation (owner: commercial-suite, status: reserved/planned).
- **master-development-plan.md** Section 2 (inventory) + Section 12 (wave) Commercial Suite eklemesi.
- **PKS-001 §4** `commercial.*` namespace reservation (veya `crm.*` altında toplama kararı).

## Verdict: PARTIAL

Foundation güvenli şekilde hazırlanabilir; blocker'lar: (a) commercial-suite domain path + `crm` branch code EA onayı,
(b) MOD-0018-FU15 data-scope resolver eksik (field-force scoping), (c) `commercial.*` namespace reservation,
(d) HCP identity + O2C SoR EA-TBD.
