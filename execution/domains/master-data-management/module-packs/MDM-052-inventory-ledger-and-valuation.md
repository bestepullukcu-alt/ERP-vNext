---
id: MDM-052
name: Inventory Ledger & Valuation
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-052-inventory-ledger-and-valuation
started: 2026-04-15
target: 2026-06-30
---

# MDM-052 — Inventory Ledger & Valuation

## Module Summary
- Source module: `MOD-0173`
- Suggested wave: `W2`
- Source stream: `SRC - Inventory; SRC - Inventory Reserva; SRC - Procurement-Purch`
- Bootstrap note: Ek not bulunmuyor.

## Ownership and Boundaries
- Bu paket, `Inventory Ledger & Valuation` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-052-inventory-ledger-and-valuation.md`
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
- [ ] `Inventory Ledger & Valuation` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. MOD-0173 Inventory Ledger & Valuation Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. MOD-0173 Inventory Ledger & Valuation Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. MOD-0173 Inventory Ledger & Valuation Detail` (Header Summary / Overview) için kapsam netleştirildi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0173` → hedef paket kimlik: `MDM-052`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
