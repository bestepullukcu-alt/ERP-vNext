# Platform & Shared Services (PSS)

**Kısaltma:** `PSS`
**Module ID prefix:** `PSS-NNN` (yeni paketler) · tarihsel `MOD-NNNN` kayıtları korunur
**Ana servisler:** [services/Diten.Platform/](../../../services/Diten.Platform/) (port 5057), [services/Diten.AuthService/](../../../services/Diten.AuthService/) (port 5056)

## İş Tanımı

Platform & Shared Services domain'i; tenant yönetimi, subscription, RBAC/ABAC, audit, secrets, workflow ve diğer ERP modüllerinin tükettiği yatay platform yeteneklerini sahiplenir. Tenant-side iş modülleri (MDM/ESBP/ERP) bu domain'in çıktılarını consume eder; PSS hiçbir iş anlamı (goal, invoice, KPI, vb.) sahiplenmez.

## Kapsam (Yüksek Seviye)

- Tenant lifecycle (provisioning, suspend, cancel)
- Subscription plan + feature mgmt
- Platform admin / partner admin yönetimi
- RBAC + ABAC enforcement, module entitlement
- Audit trail, evidence linking, document mgmt
- Secrets/configuration, internal event bus, observability seam

## Kapsam Dışı

- MDM (master data) — `→ master-data-management`
- ESBP (strateji/KPI) — `→ enterprise-strategy-business-performance`
- Reference modules (Golden Reference vb.) — `→ developer-enablement`
- Tenant-side iş süreçleri (HR, Finance, CRM)

## Domain-Specific Belgeler

- [domain-config.md](domain-config.md) — sınırlar, repo scope, runtime kararları
- [module-packs/](module-packs/) — her aktif modülün sözleşme dosyası

> Tarihsel `controls/`, `decisions/` ve `batches/` katmanları [archive/domains/platform-shared-services/](../../../archive/domains/platform-shared-services/) altına taşınmıştır; otorite değildir.

## Otorite Hiyerarşisi (Yeni Modül Yazarken)

1. **Module Pack** — [module-packs/{ID}.md](module-packs/)
2. **Domain Config** — [domain-config.md](domain-config.md)
3. **AGENTS.md** — repo kontratı
4. **`.antigravity/rules/`** — engineering NASIL (`response-envelope`, `handler-design`, `views-organization`, ...)
5. **`docs/platform/master-plan.md`** — modül envanteri, MVP scope, cross-cutting standartlar (§7)

## Yeni Modül Eklerken

Tam akış için: [docs/agent-usage-guide.md](../../../docs/agent-usage-guide.md). Kısa hâli:

1. `/prepare-module-pack` çağır (modül adı + alan sayısı + iş kuralları)
2. Üretilen `PSS-NNN-{slug}.md` pack'ini incele, gerekirse düzelt
3. `status: approved` yap
4. Branch aç: `feature/pss/pss-NNN-{slug}`
5. `@orchestrator {pack-yolu}` çağır
6. Test + `status: done` + PR
