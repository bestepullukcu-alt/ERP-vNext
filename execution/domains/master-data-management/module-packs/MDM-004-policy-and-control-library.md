---
id: MDM-004
name: Policy & Control Library
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-004-policy-and-control-library
started: 2026-04-15
target: 2026-06-30
---

# MDM-004 — Policy & Control Library

## Module Summary
- Source module: `MOD-0005`
- Suggested wave: `W1`
- Source stream: `SRC - Back Bone`
- Bootstrap note: Policy-driven approvals for PPM/SRC; BPM consumes later as needed.

## Ownership and Boundaries
- Bu paket, `Policy & Control Library` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-004-policy-and-control-library.md`
- `services/Diten.MdmService/**` (uygulama başladığında ilgili modül altı)
- `frontend/Diten.Web/Areas/MDM/**` (gerektiğinde)
- `frontend/Diten.Web/Views/MDM/**` (gerektiğinde)

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/**`
- `services/Diten.EnterpriseStrategyService/**`

## Dependencies
- Tenant/Env Mgmt (MOD-0009/0010)
- RBAC/ABAC (MOD-0018)
- Audit Trail (MOD-0021)
- Platform Standards (MOD-0013)
- Module Boundary (MOD-0014)

## Runtime Constraints
- Domain/Module otorite sırası korunur: `Module Pack > Domain Config > AGENTS.md > .antigravity/`.
- Bu pakette yalnızca modül-seviyesi kapsam ve kabul kriterleri tutulur.

## Acceptance Criteria
- [ ] `Policy & Control Library` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. Overview` (Overview / Filters) için kapsam netleştirildi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0005` → hedef paket kimlik: `MDM-004`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
