# Commercial Suite — Module Packs

Bu klasör Commercial Suite module pack'lerini tutar. **Henüz pack yok.** Her pack DCP-002 canonicalization gate'inden
geçmeden ve draft onaylanmadan authoring başlamaz. Pack authoring entry point: `/prepare-module-pack` veya
`module-pack-author`.

## Blueprint-canonical MOD ID + name (reservation adayları)

Tüm 27 ID `Blueprint_Data`'da doğrulandı (Suite: `Commercial Suite (CRM + O2C)`). Aşağıdaki adlar canonical'dır;
pack `name` alanı bunlarla eşleşmelidir (aksi halde DCP-002 canonical-name gate fail-close eder).

| MOD ID | Canonical Name (Blueprint) | Sub-area | Wave | Registry Status (öneri) |
|---|---|---|---|---|
| MOD-0149 | Customer 360 / Account Hierarchy | CRM Core | W-1 | reserved / planned |
| MOD-0150 | Contact & Relationship Management | CRM Core | W-3 | reserved / planned |
| MOD-0151 | Territory Management | CRM Core | W-4 | reserved / planned |
| MOD-0152 | Lead Management | Sales | W-3 | reserved / planned |
| MOD-0153 | Opportunity & Pipeline Management | Sales | W-3 | reserved / planned |
| MOD-0154 | Forecasting & Quotas | Sales | W-4 | reserved / planned |
| MOD-0155 | Field Sales / Visit Planning | Sales | W-4 | reserved / planned |
| MOD-0156 | Price Lists & Discount Guardrails | CPQ & Pricing | W-3 | reserved / planned |
| MOD-0157 | Quote Generation | CPQ & Pricing | W-3 | reserved / planned |
| MOD-0158 | Quote-to-Contract Handoff | CPQ & Pricing | W-3 | reserved / planned |
| MOD-0159 | Product Configuration | CPQ & Pricing | W-4 | reserved / planned |
| MOD-0160 | Case Management | Service | W-1 | reserved / planned |
| MOD-0161 | SLA Routing & Escalation | Service | W-2 | reserved / planned |
| MOD-0162 | Knowledge Base | Service | W-4 | reserved / planned |
| MOD-0163 | Customer Satisfaction Loop | Service | W-4 | reserved / planned |
| MOD-0164 | Consent & Preference Management | Marketing | W-2 | reserved / planned |
| MOD-0165 | Campaign Management | Marketing | W-4 | reserved / planned |
| MOD-0166 | Journeys & Automation | Marketing | W-4 | reserved / planned |
| MOD-0167 | Segmentation / CDP | Marketing | W-4 | reserved / planned |
| MOD-0168 | Order Capture | Order-to-Cash (Bridge) | W-2 | reserved / planned (SoR EA-TBD) |
| MOD-0169 | Billing & Invoicing | Order-to-Cash (Bridge) | W-2 | reserved / planned (SoR EA-TBD) |
| MOD-0170 | Returns (RMA) | Order-to-Cash (Bridge) | W-3 | reserved / planned (SoR EA-TBD) |
| MOD-0171 | Disputes / Claims | Order-to-Cash (Bridge) | W-3 | reserved / planned (SoR EA-TBD) |
| MOD-0172 | Allocation & ATP/CTP | Order-to-Cash (Bridge) | W-4 | reserved / planned (SoR EA-TBD) |
| MOD-0282 | Partner & Alliance Management | Business Development | W-4 | reserved / planned |
| MOD-0283 | Pursuit & Proposal Management (RFP/RFI) | Business Development | W-4 | reserved / planned |
| MOD-0284 | Deal Desk & Commercial Approvals | Business Development | W-4 | reserved / planned |

> **Not:** Task promptundaki bazı adlar Blueprint canonical'dan küçük farklıdır (örn. "Returns / RMA" → Blueprint
> **"Returns (RMA)"**; "Pursuit & Proposal Management" → Blueprint **"…(RFP/RFI)"**). Pack `name` **Blueprint** adını
> kullanmalıdır.

## DCP-002 preflight (her ID için, python ortamı gerektirir)

```
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0149 --name "Customer 360 / Account Hierarchy"
# … 27 ID için, yukarıdaki canonical ad tablosuna göre tekrarlanır.
```

İlk hazırlanacak pack: **MOD-0149** (`/prepare-module-pack` — golden reference alan sayısına göre slim/compact).
