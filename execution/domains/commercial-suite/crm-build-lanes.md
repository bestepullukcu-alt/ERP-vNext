# Commercial Suite — Build Lane Plan

> Build lane = production öncesi teslimat şeridi (bir veya birden çok modülün sıralı hazırlığı). Bu bir runtime
> yapı değildir; module pack ve capability pack sırasını organize eder. Wave sütunu **Blueprint_Data**'daki canonical
> W-x atamasından gelir (doğrulandı). Öncelik sütunu domain önerisidir, EA onayına tabidir.

## Wave taxonomy (Blueprint canonical)

Blueprint tüm 27 modülü `Commercial Suite (CRM + O2C)` suite'i altında W-1…W-4 dalgalarına atamıştır:

- **W-1:** MOD-0149 (Customer 360), MOD-0160 (Case Management)
- **W-2:** MOD-0164 (Consent), MOD-0161 (SLA Routing), MOD-0168 (Order Capture), MOD-0169 (Billing & Invoicing)
- **W-3:** MOD-0150 (Contact), MOD-0152 (Lead), MOD-0153 (Opportunity), MOD-0156 (Price Lists), MOD-0157 (Quote), MOD-0158 (Quote-to-Contract), MOD-0170 (Returns), MOD-0171 (Disputes)
- **W-4:** MOD-0151 (Territory), MOD-0154 (Forecast), MOD-0155 (Field Sales), MOD-0159 (Product Config), MOD-0162 (Knowledge Base), MOD-0163 (CSAT), MOD-0165 (Campaign), MOD-0166 (Journey), MOD-0167 (Segmentation), MOD-0172 (Allocation/ATP), MOD-0282 (Partner), MOD-0283 (Pursuit/Proposal), MOD-0284 (Deal Desk)

> **Uyarı:** Aşağıdaki lane sırası "hazırlık/pack authoring" mantıksal sırasıdır ve Blueprint wave'i ile **her yerde
> birebir örtüşmez**. Örn. Field Sales (MOD-0155) legacy değeri en yüksek alan olsa da Blueprint'te W-4'tür; bu yüzden
> pack/preservation hazırlığı erken yapılır ama **implementation W-4 sırasına göre sonra** gelir.

## Lane 0 — crm-platform-readiness (ön koşul, runtime değil)

| Alan | Karar |
|---|---|
| Kapsam | MOD-0018 permission/RBAC entegrasyonu · MOD-0048 reference-data readiness · navigation readiness · tenant isolation · audit readiness · **MOD-0018-FU15 Real DataScopeResolver** |
| Amaç | CRM'in tükettiği tüm platform ön koşullarını (permission seed, entitlement bridge, data-scope) hazır etmek |
| Not | Bu bir module implementation değildir; CRM foundation'ı **bloklayan** ön koşul lane'idir. FU15 karşılanmadan territory/field-force scoping yapılamaz. |

## Build Lane Matrix

| Build Lane | Modules | Purpose | Wave (Blueprint) | Legacy Dependency | Priority (öneri) |
|---|---|---|---|---|---|
| **crm-platform-readiness** | MOD-0018, MOD-0048, MOD-0288, MOD-0018-FU15 (tümü mevcut/planlı) | RBAC + reference + data-scope ön koşulu | — (ön koşul) | — | P0 (blocker) |
| **crm-foundation** | MOD-0149 | Customer 360, Account, WorkPlace, account hierarchy | W-1 | DitenCRM Client/WorkPlace/Property/ClientCategory (referans) | P0 |
| **crm-service-core** | MOD-0160 | Case Management (Blueprint W-1) | W-1 | Greenfield | P1 |
| **crm-consent-core** | MOD-0164 | Consent & Preference (erken; W-2) | W-2 | Greenfield | P0 |
| **crm-relationship-core** | MOD-0150 | Contact, relationship, affiliation | W-3 | DitenCRM Contact/affiliation (referans) | P1 |
| **crm-sales-core** | MOD-0152, MOD-0153 | Lead → Opportunity → Pipeline | W-3 | Greenfield (generic CRM) | P1 |
| **crm-territory-core** | MOD-0151 | Territory, Zone, MicroZone, rep assignment | W-4 | MR zone / micro-zone (referans) | P1 (FU15 bağımlı) |
| **crm-field-sales-extension** | MOD-0155 | MicroTarget, MR visit planning, route planning, ActivityReport | W-4 | **En yüksek legacy değer** (MicroTarget/Activity/Visit/schedule engine) | P1 (pack erken, impl geç) |
| **crm-forecasting** | MOD-0154 | Forecasting & Quotas | W-4 | Greenfield | P2 |
| **crm-targeting-core** | MOD-0167 | Segment, TargetCustomer, UCLN, SubjectList, StrategyTemplate | W-4 | CrmV2 TargetCustomer/UCLN/StrategyTemplate (referans) | P2 |
| **crm-campaign-core** | MOD-0165 | Campaign, CyclePeriod, execution, results | W-4 | Campaign/PromoCampaign/CyclePeriod (referans) | P2 |
| **crm-automation** | MOD-0166 | Journeys & automation | W-4 | Greenfield | P3 |
| **commercial-cpq** | MOD-0156, MOD-0157, MOD-0158, MOD-0159 | Price lists, quote, quote-to-contract, product config | W-3/W-4 | Greenfield | P2 |
| **commercial-service** | MOD-0161, MOD-0162, MOD-0163 | SLA routing, knowledge base, CSAT | W-2/W-4 | Greenfield | P2 |
| **commercial-o2c** | MOD-0168, MOD-0169, MOD-0170, MOD-0171, MOD-0172 | Order capture, billing, returns, disputes, allocation/ATP | W-2/W-3/W-4 | Greenfield; **SoR EA-TBD (Finance overlap)** | P2 (SoR onayı bekler) |
| **commercial-bizdev** | MOD-0282, MOD-0283, MOD-0284 | Partner, pursuit/proposal, deal desk | W-4 | Greenfield | P3 |

## Reservation önerisi (registry)

27 MOD ID Blueprint'te canonical; registry'de kayıt yok. Her biri için — pack authoring'ten önce — DCP-002 preflight:

```
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0149 --name "Customer 360 / Account Hierarchy"
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0150 --name "Contact & Relationship Management"
... (27 ID için, aşağıdaki module-packs/README.md canonical ad tablosuna göre)
```

Önerilen registry status: **reserved / planned** (owner domain: `commercial-suite`). `ready-for-pack` yalnız
crm-platform-readiness ön koşulları karşılandıktan ve draft pack onaylandıktan sonra.
