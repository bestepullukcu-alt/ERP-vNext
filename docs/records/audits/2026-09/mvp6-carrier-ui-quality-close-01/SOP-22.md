# MVP6-CARRIER-UI-QUALITY-CLOSE-01 — SOP §22

Date: 2026-09-23  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Profile: Carrier-owned narrow rework plus browser evidence  
Targeted quality-close verdict: **PASS**  
Broader Carrier browser acceptance: **PARTIAL**

## Exact source binding

- Accepted preimage 21-path manifest: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`.
- Carrier-owned patch: `57f90dadedd6e2304e3775fd1ea0bd7def29ad9d265ccbbeb892afdc04ba1adb`.
- Final 21-path manifest: `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`.
- Only `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Carriers/index.l10n.js` and
  `frontend/Diten.Web.Tests/JavaScript/CarrierIndexBehaviorTests.cs` changed. The other 19 owned paths remained
  byte-identical. No shared/resource/backend/Auth/Gateway/permission source was changed.

## Missing-key disposition

The ten warnings were false positives. The Carrier bridge considered a value missing whenever the localized value
equalled its key. That is valid for nine English strings and for `Actions` in English and French. The predicate now
requires an own property with a non-empty string. The exact warning ownership and user effect are recorded in
`carrier-writer/WARNING-DISPOSITION.tsv`.

- Seven-language matrix: **70/70 PASS**.
- Old predicate: **10 warnings (RED)**.
- New predicate: **0 warnings (GREEN)**.
- Missing-`Save` mutation: **one exact warning**, proving the negative still detects a true omission.
- Focused tests: **14/14 PASS**.
- Writer build: **0 warnings / 0 errors**.
- Independent fresh browser console: **0 warnings / 0 errors** on the Arabic Carrier page.

## Browser and responsive evidence

The immutable v3 source was built and hosted on isolated ports Web/Gateway/Auth/Platform/SupplyChain
`5301/5300/5356/5357/5361`. The accepted five processes were hash-bound to the snapshot and all listeners were
stopped after capture. The initial sandbox-network-disabled, zero-log/no-listener attempt is retained as discarded.

| Check | Result | Exact observation |
|---|---:|---|
| 768 list | PASS | `innerWidth=768`, `DPR=1`, document/body scroll width `768`, RTL, 736 px table, no document overflow |
| 768 create | PASS | 400 px offcanvas; four approved fields; no document overflow |
| 390 list | PASS | `innerWidth=390`, `DPR=1`, document/body scroll width `390`, 358 px table; search/filter visible; Add reduced to 38.0078125 px icon control |
| 390 create | PASS | Offcanvas width exactly 390 px; four approved fields; no document overflow |
| UAS server surface | PASS | Hash-bound HTTP preflight returned denial/remediation with no DataTable, create, or status surface |
| UAS fresh browser rerun | OPEN | The no-role browser login did not complete; the prior browser PASS remains historical and was not relabelled as fresh |
| Status and mutations | OPEN | No real tenant/legal-entity Auth handoff; no diagnostic-token E2E claim |
| Persistent PNG | OPEN | No explicitly permitted save/export mechanism was available; prior rejection was preserved and no data URL/CDP/encoding workaround was attempted |

Measurements and acceptance boundaries are in `RESPONSIVE.tsv`, `BROWSER-ACCEPTANCE.tsv`,
`raw/browser-observations.tsv`, and `browser-runtime/`.

## Shared Search disposition

Arabic still renders `Search [CTRL + K]` because shared `main.js` hardcodes the placeholder. This is outside the
Carrier 21-path allowlist. No shared source was modified. A two-file, unapplied integration-owner candidate is
recorded as `shared-search/SHARED-SEARCH.patch`, SHA-256
`2abe82415f68870f8d714af093473f497bcb1f210f970e491c32d288b1440074`.

Required owner decision:

> Shared frontend integration owner'ın, yalnız kaydedilmiş iki preimage eşleşirse
> `2abe82415f68870f8d714af093473f497bcb1f210f970e491c32d288b1440074` SHA-256'lı exact patch'i
> uygulamasını; `main.js` için `1ae6b74ae5774865677d32b0d4b427b3b03e11175527f17adc0cc016295447fe`,
> `_LayoutTenantShell.cshtml` için `babcf035b3c7fc14eebd9442586debde2f78200d37d43423f0a5fadef17a7af4`
> hedeflerini üretmesini onaylıyorum. Kapsam yalnız tenant-shell global-search placeholder yerelleştirmesidir.
> Carrier source, global-search davranışı, Platform/archive shell, Auth, Gateway, permission veya başka shared
> kaynak değişikliği yetkisi vermiyorum.

## Boundary and writer state

The requested Carrier missing-key and 768/390 measurement gaps are closed. Shared Search, fresh browser UAS,
Auth-dependent list/create/replay/status, and durable PNG remain explicit open boundaries. No commit, push, stash,
canonical/guard, pack, gateway, Auth, backend, global layout, permission, or other-lane change was made. Carrier
writer and browser runtime are complete; runtime ports are clean.
