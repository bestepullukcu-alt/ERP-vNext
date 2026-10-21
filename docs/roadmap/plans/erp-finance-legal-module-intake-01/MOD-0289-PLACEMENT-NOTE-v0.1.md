# MOD-0289 Bank Master & Bank Account Registry — destination placement (note v0.1)

    created  : 2026-10-21
    status   : PROPOSAL — placement for an owner decision (MDM owner); no pack, no code
    measured : destination origin/main 9d44e8a1f (read-only); source = ERP-vNext pack + Treasury worktree (read-only)
    batch    : producer batch 1 (MOD-0289 + MOD-0220), see DESTINATION-BRIEF-v1.0.md

## 1 · Facts

| | source (ERP-vNext) | destination (Recovery) |
|---|---|---|
| MOD-0289 pack | `execution/domains/platform-shared-services/module-packs/MOD-0289-bank-master-bank-account-registry.md` — `domain: platform-shared-services`, `status: draft` | none; no registry row; no code (no bank/account entity in `services/`) |
| MOD-0289 code | `services/Diten.Platform/.../Features/Mod0289/` (Platform service) | — |
| Platform domain scope | — | PSS `domain-config.md` **Out-of-Scope**: "MDM (master data) ana veri modelleri → `master-data-management`" and "Tenant-side ERP işlemleri (… Finance …)" |
| MDM domain scope | — | MDM `domain-config.md` Purpose: "owns ERP business master-data systems of record and the read-only contracts consumed by Platform Shared Services and tenant business modules"; already holds **MOD-0220** (Legal Entity foundation, `Diten.MdmService`, `ready-for-dev`) and MOD-0290 |
| Blueprint 8.8 | ERP Suite · Treasury & Cash · W-3 · aim: "Own bank master data and bank account registries for legal entities and suppliers while payment execution remains with treasury/payment gateway capabilities" | same workbook row |

## 2 · Proposal (not a decision)

**Place MOD-0289 in `master-data-management`, service `Diten.MdmService`** — not in Platform as the source did.

- The destination's own domain rules put master-data SoR in MDM and keep Finance out of PSS; a bank master is master
  data (the Blueprint aim says "own bank master data … while payment execution remains with treasury").
- MOD-0289's strongest dependency, MOD-0220 (bank accounts belong to legal entities), already lives in MDM — the LE
  check stays inside one service instead of a new cross-service call.
- No new service and no new port: MDM already has one (`ports.md`: 5059).
- Payment execution (MOD-0126, MOD-0269) stays out of MDM and still needs its own placement decision later.

Alternatives: (b) Platform, as in the source — contradicts PSS's out-of-scope list (needs a PSS domain-config change);
(c) a new finance/treasury domain + service — a sixth-domain-level decision and a new port, heavier than needed for a
master-data module.

## 3 · What this means for the producer packet

The packet describes the source's Platform implementation as **source fact**; the destination supplement will map it
to MDM (base classes, repository/tenant filter, permission keys `mdm.*`, audit, routes). The packet must not assume
Platform composition (Program/DI, audit pipeline) carries over.

## 4 · Decisions needed

1. MDM owner: accept MOD-0289 in `master-data-management` / `Diten.MdmService`? (registry row + pack follow only after this.)
2. Permission-key prefix for the destination (`mdm.bank-accounts.*`-style, per `permission-key-standard.md`) — set in the destination pack.
3. The unmet Blueprint gates (MOD-0126, MOD-0140 merge, MOD-0269) — decided before integration starts (brief §3).

## 5 · Side finding (not in scope)

`origin/main` tracks **743 files** under `services/**/.tmp-*/` (build outputs such as `.exe` / `.dll`, e.g.
`services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-ctcap/Debug/net8.0/`). Owner: CRM lane / repo hygiene — a
separate clean-up with its own approval.
