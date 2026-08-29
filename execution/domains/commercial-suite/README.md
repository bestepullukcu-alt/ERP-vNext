# Commercial Suite (CRM + O2C)

**Short code (öneri):** `crm` — AGENTS.md §9 branch listesine eklenmesi EA onayına tabi (bkz. Required Follow-up).
**Blueprint suite:** `Commercial Suite (CRM + O2C)` (Blueprint_Data, `docs/System Capability & Implementation Blueprint - master 7.xlsx`).
**Module ID policy:** yeni ERP product module pack'leri registry-controlled `MOD-NNNN-{slug}` ID kullanır; her ID DCP-002 canonicalization gate'inden geçer.
**Production service:** `services/Diten.CrmService/` **henüz yok**. Bu scaffold hiçbir runtime servis oluşturmaz.

## Purpose

Commercial Suite domain'i, ERP-vNext için müşteri/ticari yaşam döngüsünü sahiplenir: Customer 360 / Account,
Contact & Relationship, Territory, Lead → Opportunity → Pipeline, Forecast/Quota, Field Sales / Visit Planning,
Consent & Preference, Campaign, Journey, Segmentation/CDP ve ticari komşu alanlar (CPQ, Service, Order-to-Cash bridge,
Business Development). Strateji: **generic kurumsal CRM çekirdeği (Salesforce benzeri) + pharma field-force uzantısı**.

Bu governance scaffold, production implementation'dan **önce** domain sahipliğini, sınırlarını, build lane sırasını ve
MOD-0018 RBAC entegrasyon kararlarını açık hale getirmek için vardır. Kod yazmaz.

## Current Governance Status

- Domain governance scaffold **öneri** aşamasında (bu task ile oluşturuldu).
- Production service scaffold **yok** ve bu scaffold tarafından yetkilendirilmez.
- 27 Blueprint-canonical MOD ID (MOD-0149…MOD-0172, MOD-0282…MOD-0284) doğrulandı; **registry'de henüz kayıt yok** —
  reservation önerisi [crm-build-lanes.md](crm-build-lanes.md) ve [module-packs/README.md](module-packs/README.md) içinde.
- İlk hedef modül: `MOD-0149 Customer 360 / Account Hierarchy` (Blueprint W-1).
- En değerli legacy alan: `MOD-0155 Field Sales / Visit Planning` (bkz. [legacy-value-preservation.md](legacy-value-preservation.md)).

## Authority Order

1. Module Pack — `module-packs/{ID}-{slug}.md`
2. Domain Config — `domain-config.md`
3. `AGENTS.md`
4. `.antigravity/rules/`
5. Archive / external references (otorite değil)

## New Module Flow

1. DCP-002 canonicalization gate'i çalıştır (`verify_module_id.py --check-id … --name …`).
2. Draft module pack hazırla (`/prepare-module-pack`).
3. Kullanıcı draft'ı inceler.
4. Yalnız açık onaydan sonra pack `approved` / `ready-for-dev` olur.
5. `@orchestrator` yalnız approved/ready-for-dev pack için çağrılır.

## Out Of Scope (özet — tam liste [crm-sor-boundary.md](crm-sor-boundary.md))

- Country / City / District ve generic lookup/reference set → MOD-0048 Reference Data.
- Employee / Sales Rep master, Business Unit master → HR / Organization (MOD-0288).
- Brand / Product / SKU → MDM / Product.
- Auth / Role / Permission engine → MOD-0018 / AuthService.
- Navigation engine, Gateway global routing policy, Archive layout/controllers → ilgili sahipler / FROZEN.

## Governance Documents

- [domain-config.md](domain-config.md) — domain sınırları ve kararları
- [crm-build-lanes.md](crm-build-lanes.md) — build lane sırası + wave + legacy bağımlılık
- [crm-rbac-integration-plan.md](crm-rbac-integration-plan.md) — MOD-0018 permission/role/ABAC/audit entegrasyonu
- [crm-sor-boundary.md](crm-sor-boundary.md) — System-of-Record sınır matrisi
- [legacy-value-preservation.md](legacy-value-preservation.md) — legacy business-rule hafızası koruma planı
- [module-packs/](module-packs/) — module pack'ler (henüz yok)

## Related Governance Sources

- [Module ID Registry](../../registries/module-id-registry.md)
- [Module Implementation Status](../../registries/module-implementation-status.md)
- [Master Development Plan](../../portfolio/master-development-plan.md)
- [Permission-Key Standard PKS-001](../../../.antigravity/rules/permission-key-standard.md)
- [DCP-002 Module Identity Canonicalization](../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md)
