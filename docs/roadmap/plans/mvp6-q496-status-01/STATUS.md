---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Q496 — not a product gap: a lane test-fixture authorization, browser-only verification pending

    item      : Q496 (v9.6 §5 item 2) · owner: repository owner · CT role: measurement
    author    : CT (gmg-10)
    measured  : 2026-10-18 from tracked source (no running stack)
    verdict   : Q496 is NOT a product defect or a code/seed gap. It is a one-write authorization in the
                lane's DISPOSABLE auth DB, and it gates only the BROWSER halves of nine GATE-18-0 cells.

## What Q496 is

A single write into the lane's disposable auth database that links the lane sign-in account to a role
carrying the 25 `supplychain.*` permission keys (the account was registered as `Viewer`, 0/25, so every
module page renders the denied state). It blocks the browser halves of nine GATE-18-0 cells — rows 2 (×4),
7 (×2), 12 (×3). The harness refused CT this write with reason `[Permission Grant]`, and refused even
counting the keys (`CT-QUEUE.tsv` Q496; `mvp6-stage-r-stack-up-01/REPORT.md`).

## Measured — the product side is complete

- The `supplychain.*` permission keys **exist in source** and **self-register** with Platform through the
  module manifest providers (`services/Diten.SupplyChainService/src/.../ModuleRegistration/*ManifestProvider.cs`).
  There is no missing key and no seed to change — by design the keys arrive via self-registration, not a
  shared catalogue/seed (OD-12, `mvp6-q221-q209-prework-01`).
- The lane's auth catalogue holds them ("25 supplychain keys among 652", `mvp6-stage-r-stack-up-01`), and a
  privileged role (SuperAdmin/Admin, ~759 permissions) already carries them. (An earlier CT measurement
  that reported "0 rows hold the keys" was a field-name error — it queried `Module` where the field is
  `Key` — and was corrected: the role holds all of them.)
- Rows **7 and 12 already have their server halves green in the suites**; Q496 gates only their **browser**
  halves. Row 2's server half is viable and measured.

## Conclusion

Q496 needs no code, no seed, and no contract change. It is a disposable-lane-DB fixture authorization
(link the `Viewer` account to the privileged role, or add the keys to its role). It is the **same frontier
as the browser rerun**: establishing a signed-in actor that holds the keys. That could not be completed in
CT's session — the credential/permission-grant writes it requires are harness-refused, and a local tenant
cannot be fully born for a clean browser sign-in (see `memory` and `mvp6-golden-flows-http-01`, which
already proves the five flows at the service boundary).

So the nine cells are **browser-verification-pending, not product-blocked**. Closing Q496 as a product
item needs one of: the owner provides a working sign-in that holds the keys, or the owner runs the lane
fixture write, and then a browser pass records the nine cells. The service-boundary evidence already
stands independently of that pass.

## Files

- `STATUS.md` — this measurement.
