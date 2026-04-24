---
id: MDM-046
name: Customer 360 / Account Hierarchy
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-046-customer-360-account-hierarchy
started: 2026-04-15
target: 2026-06-30
---

# MDM-046 — Customer 360 / Account Hierarchy

## Module Summary
- Source module: `MOD-0149`
- Suggested wave: `W2`
- Source stream: `SRC - Agreements & Cont; SRC - Logistics; SRC - Sales O2C`
- Bootstrap note: Ek not bulunmuyor.

## Ownership and Boundaries
- Bu paket, `Customer 360 / Account Hierarchy` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-046-customer-360-account-hierarchy.md`
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
- [ ] `Customer 360 / Account Hierarchy` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. Standards Pack` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Status Model` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Merge/Dedupe Policy` (Policy List / Approval Flow) için kapsam netleştirildi.
- [ ] Sayfa planı `4. Evidence Policy` (Policy List / Approval Flow) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Term Binding Console` (Workspace / Validation) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Term Impact View` (Context Panel / Zoom/Expand) için kapsam netleştirildi.
- [ ] Sayfa planı `3. CRM Field Dictionary` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Schema Viewer` (Context Panel / Zoom/Expand) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Relationship Types` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Reason Code Catalog` (Search & Filters / Columns Config) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Boundary Card` (Header Summary / Overview) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Owned Objects` (Overview / Filters) için kapsam netleştirildi.
- [ ] Kalan `46` sayfa maddesi sprint planına bölünerek module backlog'una işlendi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0149` → hedef paket kimlik: `MDM-046`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
