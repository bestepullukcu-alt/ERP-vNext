---
id: MDM-069
name: Third-Party Risk Management (TPRM)
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-069-third-party-risk-management-tprm
started: 2026-04-15
target: 2026-06-30
---

# MDM-069 — Third-Party Risk Management (TPRM)

## Module Summary
- Source module: `MOD-0216`
- Suggested wave: `W2`
- Source stream: `SRC - Agreements & Cont; SRC - Logistics; SRC - Procurement-Purch; SRC - Treasury Payments`
- Bootstrap note: HARD if Procurement/Logistics/Treasury SRCs involve third parties.

## Ownership and Boundaries
- Bu paket, `Third-Party Risk Management (TPRM)` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-069-third-party-risk-management-tprm.md`
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
- RBAC/ABAC (MOD-0018)
- Audit Trail (MOD-0021)
- Workflow Designer (MOD-0023)
- Evidence Linking (MOD-0031)
- Interface Registry (MOD-0002)
- Data Contract Registry (MOD-0003)
- API Gateway (MOD-0032)

## Runtime Constraints
- Domain/Module otorite sırası korunur: `Module Pack > Domain Config > AGENTS.md > .antigravity/`.
- Bu pakette yalnızca modül-seviyesi kapsam ve kabul kriterleri tutulur.

## Acceptance Criteria
- [ ] `Third-Party Risk Management (TPRM)` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. TPRM Embed for Suppliers/3PL/Carriers (MOD-0216) Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. TPRM Embed for Suppliers/3PL/Carriers (MOD-0216) Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. TPRM Embed for Suppliers/3PL/Carriers (MOD-0216) Detail` (Header Summary / Overview) için kapsam netleştirildi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0216` → hedef paket kimlik: `MDM-069`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
