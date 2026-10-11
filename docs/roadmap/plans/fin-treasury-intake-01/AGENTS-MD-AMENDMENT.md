# `AGENTS.md` amendment package — three edits, one pass

- date: 2026-10-11
- prepared by: Control Tower, for the owner to apply
- why one package: `AGENTS.md` is a shared seam and CT SOP §16.4 allows one writer on it. Three separate
  pieces of work need it, so they are batched rather than queued.
- CT does not edit `AGENTS.md`. Every change below is written out in full for review.

Each edit gives the exact current text and the exact replacement. Line numbers are from `origin/main`
`a814bad0f` and will shift as earlier edits are applied — match on the text, not the number.

---

## Edit 1 — §3: SupplyChain is on 5066, and CRM is missing from the table

**Why.** Three independent sources say 5066 and the table says 5061:

- `gateway/Diten.ApiGateway/ocelot.json` routes `/api/shipment-bundle` to **5066** and `/api/crm` to **5061**
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.json` sets
  `"Urls": "http://127.0.0.1:5066"`
- the runtime verification lane ran the whole golden flow on 5066 on 2026-10-11, having verified the routing
  itself first

§3 also contradicts itself today: the table says SupplyChain 5061, while the note directly beneath it says
*"Sonraki mikroservisler 5066+ kullanır."* And 5061 belongs to CRM, which has no row at all.

Starting SupplyChain on 5061 answers 503 on every page and collides with CRM. It happened on 2026-10-10.

**Current (two lines of the table):**

```
| SupplyChain Service | 5061 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api` |
| Procurement Service | 5065 | `services/Diten.ProcurementService/src/Diten.ProcurementService.Api` |
```

**Replace with:**

```
| CRM Service | 5061 | `services/Diten.CrmService/src/Diten.CrmService.Api` |
| Procurement Service | 5065 | `services/Diten.ProcurementService/src/Diten.ProcurementService.Api` |
| SupplyChain Service | 5066 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api` |
```

**And append one note** after the existing 2026-09-17 note:

```
> **Not (2026-10-11, düzeltme):** `SupplyChain Service` **5066**'dadır, 5061 değil. 5061 CRM'in rotasıdır
> (`ocelot.json`: `/api/shipment-bundle` → 5066, `/api/crm` → 5061) ve servisin kendi `appsettings.json`'ı
> `"Urls": "http://127.0.0.1:5066"` der. Tablo 5061 derken hemen altındaki 2026-09-17 notu "sonraki
> mikroservisler 5066+" diyordu; çelişki bu satırla kapatıldı. Runtime VER (AL-SCE-LOGISTICS-RUNTIME-VER,
> 2026-10-11) golden flow'un tamamını 5066'da koştu. `CRM Service` satırı da bu düzeltmeyle tabloya eklendi;
> daha önce hiç yoktu.
```

> If you prefer to apply only part of this package, **apply this edit**. The other two can wait; this one is a
> live trap that has already cost a day.

---

## Edit 2 — §2: the tree lists 7 domains and there are 11, soon 13

**Why.** `execution/domains/` holds eleven directories. The §2 tree shows seven. Four existing domains are
absent — `commercial-suite`, `human-capital-management`, `procurement`, `supply-chain-execution` — and the
Treasury intake adds two more.

**Current (the `domains/` block of the tree):**

```
│       ├── developer-enablement/        (DEVEN — mevcut; Diten.DevEnablementService golden references)
│       ├── enterprise-strategy-business-performance/ (ESBP — governance scaffold mevcut; Diten.EnterpriseStrategyService)
│       ├── management-governance/       (MG — governance scaffold; DWS + BPM; production service yok)
│       ├── pharmacovigilance/           (PVG — governance scaffold mevcut; DCP-004; production service yok)
│       ├── master-data-management/       (MDM — governance scaffold mevcut; production service yok)
│       ├── platform-shared-services/    (PSS — mevcut; Diten.Platform + Diten.AuthService)
│       └── portfolio-delivery/          (PPM — governance scaffold mevcut; MOD-0117; production service yok)
```

**Replace with:**

```
│       ├── commercial-suite/            (CRM — mevcut; Diten.CrmService)
│       ├── developer-enablement/        (DEVEN — mevcut; Diten.DevEnablementService golden references)
│       ├── enterprise-strategy-business-performance/ (ESBP — governance scaffold mevcut; Diten.EnterpriseStrategyService)
│       ├── human-capital-management/    (HCM — mevcut; Diten.HcmService)
│       ├── management-governance/       (MG — governance scaffold; DWS + BPM; production service yok)
│       ├── master-data-management/      (MDM — governance scaffold mevcut; production service yok)
│       ├── pharmacovigilance/           (PVG — governance scaffold mevcut; DCP-004; production service yok)
│       ├── platform-shared-services/    (PSS — mevcut; Diten.Platform + Diten.AuthService)
│       ├── portfolio-delivery/          (PPM — governance scaffold mevcut; MOD-0117; production service yok)
│       ├── procurement/                 (PROC — governance scaffold; DCP-010; production service yok)
│       └── supply-chain-execution/      (SCE — mevcut; Diten.SupplyChainService; DCP-009)
```

**Then, only once the Treasury intake's registry rows exist**, add the two new ones in alphabetical place:

```
│       ├── erp-suite-financial-management/  (FIN — MOD-0120 AR; DCP-011; production service yok)
│       ├── erp-suite-treasury-cash/         (TRY — MOD-0126 Payments & Approvals; DCP-011; production service yok)
```

Both names come from the Blueprint's own suite/capability-group strings, so the domain folder names match the
authority rather than inventing a shorter one.

---

## Edit 3 — §9: four prefixes are in daily use and none is declared

**Why, measured on the remote branches.** The declared list is
`mdm | pss | deven | esbp | mg | ppm | pvg | crm` — eight codes. Twelve distinct prefixes are actually in use:

| prefix | branches | declared? |
|---|---|---|
| `pss` | 24 | yes |
| `governance` | 10 | **no** |
| `pvg` | 9 | yes |
| `ppm` | 7 | yes |
| `mg` | 7 | yes |
| **`sce`** | **5** | **no** |
| `dm` | 3 | **no** |
| `mdm` | 2 | yes |
| `infra` | 2 | **no** |
| `workcenter-ux`, `workcenter-kanban-board`, `workcenter-frontend` | 1 each | **no** |

CT contributed to this: every MVP-6 branch it created today is `feature/sce/…`, against a rule that does not
list `sce`. That is CT's drift to own, and it is named here rather than quietly fixed.

**Current:**

```
- `domain-kısa`: `mdm` | `pss` | `deven` | `esbp` | `mg` | `ppm` | `pvg` | `crm`
```

**Replace with:**

```
- `domain-kısa`: `mdm` | `pss` | `deven` | `esbp` | `mg` | `ppm` | `pvg` | `crm` | `sce` | `proc` | `fin` | `try`
```

**And append two example lines:**

```
- `feature/sce/mod-0183-shipment-tracking-pod`
- `feature/fin/mod-0120-accounts-receivable`
```

`fin` = `erp-suite-financial-management`, `try` = `erp-suite-treasury-cash`, `sce` =
`supply-chain-execution`, `proc` = `procurement`.

**Not proposed here, and deliberately so:** `governance`, `dm`, `infra` and the three `workcenter-*` prefixes
are in use and undeclared. Whether each is legitimised, renamed or retired is a call about work CT has not
looked at, so CT is not proposing text for them. They are listed above so the decision is visible rather than
inherited.

---

## What each edit unblocks

| edit | unblocks |
|---|---|
| 1 | removes a live trap; lets the MVP-6 packs and `AGENTS.md` agree on the port for the first time |
| 2 + 3 | Treasury intake step 2, which gates steps 3 through 7 |

Edit 1 stands alone and is worth applying on its own. Edits 2 and 3 are only useful together, and only if the
Treasury intake proceeds.
