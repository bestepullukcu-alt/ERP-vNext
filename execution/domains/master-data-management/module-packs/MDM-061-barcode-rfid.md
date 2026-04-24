---
id: MDM-061
name: Barcode/RFID
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-061-barcode-rfid
started: 2026-04-15
target: 2026-06-30
---

# MDM-061 — Barcode/RFID

## Module Summary
- Source module: `MOD-0182`
- Suggested wave: `W2`
- Source stream: `SRC - Logistics`
- Bootstrap note: Ek not bulunmuyor.

## Ownership and Boundaries
- Bu paket, `Barcode/RFID` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-061-barcode-rfid.md`
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
- [ ] `Barcode/RFID` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. Auto-ID Standards Pack Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Auto-ID Standards Pack Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Auto-ID Standards Pack Detail` (Header Summary / Overview) için kapsam netleştirildi.
- [ ] Sayfa planı `4. Auto-ID Standards Pack Page 4` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Term/Header Binding (Auto-ID UI) Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Term/Header Binding (Auto-ID UI) Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Term/Header Binding (Auto-ID UI) Detail` (Header Summary / Overview) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Scan & Label Core Model Overview` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `2. Scan & Label Core Model Catalog` (Search & Filters / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `3. Scan & Label Core Model Detail` (Header Summary / Overview) için kapsam netleştirildi.
- [ ] Sayfa planı `4. Scan & Label Core Model Page 4` (Overview / Filters) için kapsam netleştirildi.
- [ ] Sayfa planı `1. Boundary Card (Auto-ID) Overview` (Header Summary / Overview) için kapsam netleştirildi.
- [ ] Kalan `39` sayfa maddesi sprint planına bölünerek module backlog'una işlendi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0182` → hedef paket kimlik: `MDM-061`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
