# WP-0173-LE1 evidence — company (legal entity) selector, waiver W-0173-02, 2026-10-01

Base `3a3f048d9`. Raw outputs. No token, password or signing secret is written here: the REAL AuthService token of the
dev-runbook seed user was obtained by a real login and kept in a scratch folder; only its claim NAMES and the relevant
permission names are recorded (`d5-real-token-claims.json`).

| File | What it shows |
|---|---|
| `d1-build-inventory.log`, `d1-build-web.log` | InventoryService 0 errors / 0 warnings; Diten.Web 0 errors, the same 17 pre-existing warnings, 0 in Inventory files |
| `d2-inventory-tests.trx` + summary | 295/295, 0 skipped (282 + 13 `LegalEntitySelectorScopeTests`) |
| `d2-web-tests.trx` + summary | 391/391, 0 skipped (377 + 14: company selection, proxy header/short-circuit, selector render states) |
| `d3-architecture-tests.log` | `tests/architecture` 18/18 |
| `d4-sabotage.log` | (a) membership refusal removed → 5 red (four L4 cases + cache test) → restored → 13/13; (b) claim/header equality removed → L1 red → restored; SHA-256 identical, diff empty |
| `run-le1-env.ps1` | the runtime chain (copy of the UI1 script; Prism started with `--max-http-header-size=65536`, see note 1) |
| `d5-real-token-claims.json` | the real token: issuer `diten-auth-service`, **no `legal_entity_id`**, `inventory.stock.read`, `inventory.movements.create`, `mdm.legal-entities.read` present (695 permissions) |
| `d5-mdm-companies-real-token.json` | MDM `/api/legal-entities/lookup` through the Gateway with the real token: the 5 ACTIVE companies of the seed tenant |
| `d5-scope-probe-real-token.json` | real token through Gateway → InventoryService: no header / malformed → 403 "Select a company"; tenant company → 200; unknown company and another tenant's ACTIVE company → 403 `CROSS_LE_FORBIDDEN` |
| `d5-seed-real-token.json` | 3 OPENING_BALANCE postings with the real token + `X-Legal-Entity-Id` = LE-DITEN-HQ → 201 |
| `d5-l5-legal-entity-master-down.json` | own InventoryService restarted with the legal-entity master on a closed port: POST and GET → 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE` |
| `d5-mongo-transactions.json`, `d5-mongo-audit.json` | every row under LE-DITEN-HQ; audit `LeSource = header-unverified-membership`, actor = the seed user's real `sub`; INV-…0004 = the UI reversal of INV-…0001 |
| `screenshots/01…07` | real login → "Şirket seçin" (no list call) · HQ list · EU empty (partition) · S-2 keeps HQ + reversal dialog · reversal posted · Arabic RTL · "company cannot be confirmed" message |
| `d6-processes-after.json` | every process started is stopped; no listener left on 4010/4011/5066/5091; the user's services untouched (same PIDs as at the start) |

Notes
1. **Prism header limit.** The real token is 34.6 KB (one claim per permission). Node refuses headers above 16 KB, so the
   forwarded bearer never reached the PRODUCT-MASTER mock and the first seed attempt answered 503
   `PRODUCT_MASTER_UNAVAILABLE` (nothing written). Prism was restarted with `--max-http-header-size=65536`; the seed then
   posted. A mock-environment limit, not an InventoryService rule; real masters behind Ocelot/Kestrel need ≥ 64 KB too.
2. **One unplanned write.** A first L5 attempt ran while the normal service was still up (the restart command had been
   refused by a local safety check), so it posted `LOT-LE1-L5` (5 EA, INV-…0005) under HQ in the smoke database. It is
   a valid posting of the same user/company; the L5 measurement was then repeated against the restarted instance.
3. **FIX-4 adaptation (as UI1-11).** The seed tenant is `Provisioning` in the local Platform; the smoke Web instance ran
   without `Platform:InternalApiKey` (tenant-liveness unverifiable → the guard fails open, its documented behaviour).
4. **"Company list unavailable" state** is measured by the real partial render test (`InventoryAccessSurfaceTests`),
   not live: producing it live would need the user's MDM stopped or the seed role changed — both off limits.
