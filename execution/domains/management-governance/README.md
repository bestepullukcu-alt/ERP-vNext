# Management & Governance

**Kısa kod:** `mg`

**Aktif orchestration:** [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)

**Planlanan servis:** `services/Diten.ManagementGovernanceService` — mevcut değildir

## Domain kimliği

Bu governance scaffold iki geçici candidate capability'nin sahibidir:

- `MOD-0354` — Decomposition & Work Structuring Engine
- `MOD-0355` — Business Process Architecture & Modeling

Candidate kimlikler yalnız governance içindir; runtime code, route, permission, collection, event, job veya
configuration literal'ı olamaz.

## Sınır özeti

- DWS Wave 1 yalnız hierarchy, ordering, pure structural dependency ve immutable
  baseline/version/compare mechanics sahibidir. Task execution lifecycle veya local approval yazmaz.
- BPM process architecture, model, version, activity ve control point sahibidir. Workflow run, approval,
  operational task, SLA veya escalation motoru yazmaz.
- MOD-0024 generic task, MOD-0023 workflow/approval, MOD-0031 evidence ve MOD-0018 effective-permission
  sahibidir.
- DWS ve BPM aynı planlanan servis içinde birbirinden bağımsız `Dws` ve `ProcessModeling` internal
  module'larıdır. Architecture testleri bu ayrımı mekanik olarak koruyamazsa servis scaffold'u bloklanır ve
  ayrı servis kararı uygulanır.

Bu scaffold module pack, servis veya production-code yetkisi vermez. Port ve gateway route kararı içermez.

## Mevcut code-reality

`frontend/Diten.Web` içindeki `ManagementGovernance`, `DeliveryExecutionManagement` ve ilişkili ESBP/DWS
yüzeyleri önceden yazılmış mock/prototype/legacy code-reality evidence'tır. Production baseline,
tamamlanmış capability, module lifecycle durumu veya implementation authority değildir. Registry'deki
`Active` / `Monitor` etiketleri gerçek module status sayılmaz.

Mevcut `/management-governance` kabuğu aktif DCP-006 kapsamı dışındaki 1.1, 1.2, 1.5, 1.7, 1.8, 1.9 ve
1.10 alanlarını da gösterir; bunlar aktif delivery scope değildir. `Approve selected`, `Bulk approve`,
`Bulk assign`, `Bulk escalate` ve hard-coded `CanApprove` / `CanAssign` / `CanEscalate = true` yüzeyleri
`QUARANTINE` hazard evidence'tır. Gate 2 alınmadan bu kontroller kaldırılamaz, değiştirilemez veya çalışır
hale getirilemez.

DWS içindeki FS dependency ile due date/owner/overdue/status bileşimi structural Wave 1 değildir ve
`QUARANTINE` kalır. Yalnız pure hierarchy/order/structural dependency reference olarak korunabilir. BPM
placeholder'ları implementation kanıtı değildir. Global `_ViewStart` üzerinden kullanılan FROZEN
`_Layout` production temeli değildir; gelecekteki tenant module pack'leri `_LayoutTenantShell` kullanır ve
`_Layout.cshtml` değiştirilmez.

## Yeni modül kapısı

1. DCP-006 `approved` olmalıdır.
2. OD-02 ve OD-08 `CLOSED` olmalıdır.
3. DWS ve BPM için ayrı `draft` module pack hazırlanmalıdır.
4. İlgili pack insan incelemesiyle `approved` / `ready-for-dev` olmadan kod yazılamaz.
5. Service scaffold ayrıca açık kullanıcı onayı gerektirir.

Belgeler:

- [domain-config.md](domain-config.md)
- [module-packs/README.md](module-packs/README.md)
