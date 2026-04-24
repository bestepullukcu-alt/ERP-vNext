---
id: MDM-073
name: Audit Pack Generator
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-073-audit-pack-generator
started: 2026-04-15
target: 2026-06-30
---

# MDM-073 — Audit Pack Generator

## Module Summary
- Source module: `MOD-0229`
- Suggested wave: `W2`
- Source stream: `SRC - Agreements & Cont; SRC - Inventory; SRC - Inventory Reserva; SRC - Logistics; SRC - Procurement-Purch; SRC - Sales O2C; SRC - Treasury Payments`
- Bootstrap note: Accelerates audit response with exportable packs.

## Ownership and Boundaries
- Bu paket, `Audit Pack Generator` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-073-audit-pack-generator.md`
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
- [ ] `Audit Pack Generator` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. Audit Pack Generator (Inventory) (MOD-0229 embed) Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Audit Pack Generator (Inventory) (MOD-0229 embed) Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Audit Pack Generator (Inventory) (MOD-0229 embed) Detail` (Header Summary / Overview) için kapsam netleştirildi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0229` → hedef paket kimlik: `MDM-073`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
