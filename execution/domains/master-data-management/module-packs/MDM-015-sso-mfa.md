---
id: MDM-015
name: SSO / MFA
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-015-sso-mfa
started: 2026-04-15
target: 2026-06-30
---

# MDM-015 — SSO / MFA

## Module Summary
- Source module: `MOD-0017`
- Suggested wave: `W1`
- Source stream: `SRC - Back Bone`
- Bootstrap note: Identity baseline for all governance apps.

## Ownership and Boundaries
- Bu paket, `SSO / MFA` modülünün domain içi kapsamını tanımlar.
- Domain dışı yetenek ihtiyaçları bağımlılık olarak takip edilir; sahiplik transferi yapılmaz.
- Paket dışı iş kalemleri ayrı module pack'e taşınır.

## Repo Scope
- `execution/domains/master-data-management/module-packs/MDM-015-sso-mfa.md`
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
- Secrets Vault (MOD-0012)
- Audit Trail (MOD-0021)

## Runtime Constraints
- Domain/Module otorite sırası korunur: `Module Pack > Domain Config > AGENTS.md > .antigravity/`.
- Bu pakette yalnızca modül-seviyesi kapsam ve kabul kriterleri tutulur.

## Acceptance Criteria
- [ ] `SSO / MFA` için modül sınırları ve sahiplik kapsamı netleştirildi.
- [ ] Bağımlılık kapıları (`dependency_gates`) planlama board'una işlendi.
- [ ] Sayfa planı `1. Overview` (Overview / Filters) için kapsam netleştirildi.

## Test Expectations
- Tenant bağlamında modülün yalnızca kendi kapsamındaki kayıtları işlemesi doğrulanır.
- Modül için tanımlanan ana akışlarda smoke test senaryoları hazırlanır.
- Bağımlılık içeren akışlarda entegrasyon temas noktaları doğrulanır.

## Implementation Notes
- Bootstrap kaynağı: `python3 .antigravity/scripts/excel_parser.py --domain master-data-management`.
- Kaynak kimlik: `MOD-0017` → hedef paket kimlik: `MDM-015`.

## Follow-up Items
- [ ] Module owner ataması netleştirilecek.
- [ ] Sprint hedef tarihi modül önceliğine göre güncellenecek.
