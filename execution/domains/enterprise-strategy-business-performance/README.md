# Enterprise Strategy & Business Performance (ESBP)

**Kısaltma:** `ESBP`
**Kısa kod (branch):** `esbp`
**Mevcut Strategy servisi:** [services/Diten.EnterpriseStrategyService/](../../../services/Diten.EnterpriseStrategyService/)
**Tenant shell:** `_LayoutTenantShell`
**Canonical local API port:** `5102`

## İş Tanımı

Enterprise Strategy & Business Performance domain'i strateji hedefleri, objectives/cascade, planlama
dönemleri, KPI/target ve performans bağlamını yönetir.

Domain governance scaffold'ı [DCP-005 Management & Governance Core](../../portfolio/delivery-capability-packs/DCP-005-management-governance-core.md)
sınırlarını materialize eder. Scaffold production kodu veya module pack oluşturmaz ve tek başına
implementation yetkisi vermez.

## Kapsam

- `MOD-0352` — Enterprise Strategy Management (historical approved alias: `CAND-CAP-0007` / “Enterprise Strategy & Performance Management”); Blueprint subdomain 1.1 and outside DCP-006 active 1.3/1.4/1.6 implementation scope
- `CAND-CAP-0007-FU01` — Security, Tenancy & Data Migration Foundation
- `MOD-0117` Demand bağlantısı — yalnız consumer/transition sınırı

Candidate kimlikler geçici governance kimlikleridir. Runtime code, route, permission, collection, event,
job veya configuration literal'ı olarak kullanılamaz.

## Kapsam Dışı

- Demand/idea lifecycle SoR'u — `MOD-0117`
- Generic task/checklist lifecycle — `MOD-0024`
- Workflow run, approval decision, SLA ve escalation — `MOD-0023`
- WorkCenter aggregation/projection — DCP-004 / `CAND-CAP-0006`
- Person/position/organization master records — `MOD-0288`
- Document/evidence payload ownership — ilgili shared modules
- DWS Wave 1 node execution lifecycle
- BPM içinde ikinci workflow/approval motoru
- `MOD-0354` ve `MOD-0355` — [Management & Governance](../management-governance/) domain'ine taşındı

`Diten.EnterpriseStrategyService` içindeki mevcut DWS kodu yalnız legacy/hazard/migration reference'tır;
production baseline veya Management & Governance service'i değildir.

## Mevcut code-reality

`frontend/Diten.Web` içindeki Management Governance, Delivery Execution ve ilişkili ESBP/DWS yüzeyleri
önceden yazılmış mock/prototype/legacy evidence'tır; production baseline, tamamlanmış capability, module
status veya implementation authority değildir. Registry `Active` / `Monitor` etiketleri gerçek lifecycle
status değildir ve 1.1/1.2/1.5/1.7/1.8/1.9/1.10 yüzeyleri aktif DCP-006 scope'u sayılmaz.

Approve/assign/escalate kontrolleri ile hard-coded permission sonuçları ve DWS FS +
due-date/owner/overdue/status davranışı `QUARANTINE` hazard evidence'tır. Gate 2 alınmadan bunlar
kaldırılamaz, değiştirilemez veya etkinleştirilemez. Pure hierarchy/order/structural dependency yalnız
reference olarak korunabilir; BPM placeholder'ları implementation kanıtı değildir.

Global `_ViewStart` üzerinden kullanılan FROZEN `_Layout` production temeli değildir. Gelecekteki tenant
module pack'leri `_LayoutTenantShell` kullanır; `_Layout.cshtml` değiştirilmez.

## Governance Durumu

- DCP-005 historical governance kaynağıdır; aktif 1.3/1.4/1.6 orchestration DCP-006'dır.
- Domain governance scaffold mevcuttur.
- Henüz ESBP module pack'i oluşturulmamıştır.
- Production değişikliği için hem DCP-005 execution kapısı hem de sıradaki module pack'in
  `approved` / `ready-for-dev` kapısı gerekir.
- DCP-005 Gate 2 tehlikeleri ilk production değişikliğinden önce ayrıca kontrol edilir.

## Domain Belgeleri

- [domain-config.md](domain-config.md) — domain sınırları, repo scope, sahiplik ve Gate 2 kontratı
- [module-packs/](module-packs/) — gelecekteki onaylı modül sözleşmeleri
- [DCP-005](../../portfolio/delivery-capability-packs/DCP-005-management-governance-core.md) — ordered delivery ve üst seviye gate'ler
- [Control Tower master plan](../../../docs/enterprise-strategy-control-tower-master-plan.md)

## Yeni Modül Akışı

1. DCP-005 ordered sequence ve ilgili gate'leri doğrula.
2. Candidate veya parent/FU kimliğini DCP-002 fail-closed preflight ile doğrula.
3. `/prepare-module-pack` ile yalnız `draft` module pack hazırla.
4. İnsan incelemesi ve açık onay sonrası pack'i `approved` / `ready-for-dev` yap.
5. Yalnız bundan sonra `@orchestrator` ile production implementation başlat.
