# MVP6 Carrier UI Quality Close — Browser Runtime Evidence

Verdict: **PARTIAL**

This lane prepared and exercised a disposable, isolated runtime for the v3 Carrier UI source. The authorized
admin browser checks completed. A fresh browser UAS login did not complete after logout, so this lane does not
claim a fresh browser UAS PASS. The hash-bound HTTP UAS surface probe remains evidence that the server rendered
only the UAS-001 denial card for the seeded no-role user; it is not substituted for the missing browser result.

## Immutable source binding

- Registered source worktree: `/Users/natig/.codex/worktrees/mvp6-carrier-ui-dev/ERP-vNext-recovery`
- Immutable runtime snapshot: `/private/tmp/mvp6-carrier-ui-quality-close-01/snapshot-v3`
- v3 manifest SHA-256: `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`
- Carrier patch SHA-256: `57f90dadedd6e2304e3775fd1ea0bd7def29ad9d265ccbbeb892afdc04ba1adb`
- Pre-runtime manifest verification: 21/21 PASS
- Post-runtime manifest verification: 21/21 PASS
- Fresh Web build: PASS, 0 errors, 16 warnings; warnings are recorded in `raw/frontend-build.log`.

No source file was changed by this lane. Only this audit directory and lane-specific `/private/tmp` content were
written.

## Runtime topology

| Service | Port | Disposable database/config |
|---|---:|---|
| Gateway | 5300 | runtime-only Ocelot copy |
| Web | 5301 | `DitenWeb_mvp6_carrier_ui_quality_close_01_p53` |
| Auth | 5356 | `DitenAuth_mvp6_carrier_ui_quality_close_01_p53` |
| Platform | 5357 | `DitenPlatform_mvp6_carrier_ui_quality_close_01_p53` |
| SupplyChain | 5361 | `DitenSupplyChain_mvp6_carrier_ui_quality_close_01_p53` |

The runtime-only Ocelot copy maps 7 Auth routes to 5356, 64 Platform routes to 5357, and 37 SupplyChain routes to
5361. Platform used `AuthService__BaseUrl=http://127.0.0.1:5356`. Browser traffic used Web 5301 and the Web
adapter used Gateway 5300.

Gateway, Auth, and SupplyChain health returned 200. Web root returned the expected 302 login redirect. Platform
health returned 503 because the optional local RabbitMQ dependency was unavailable; the Platform API was
listening and SupplyChain self-registration plus Carrier permission sync completed successfully.

## Browser and preflight results

- Admin browser: 768 px and 390 px checks completed; console had 0 warnings; Arabic RTL, table, toolbar, filter,
  Add action, and create/status offcanvas surfaces were observed.
- Admin hash-bound HTTP preflight: login 200, page 200, DataTable present, create/status surfaces present, no
  UAS denial card.
- No-role hash-bound HTTP preflight: login 200, page 200, no DataTable/create/status surfaces, exactly one
  `diten-access-denied` card.
- Fresh no-role browser login after admin logout: not completed; fresh browser UAS remains OPEN and is not marked
  PASS.
- Persistent PNG export is outside this runtime lane and remains OPEN unless supplied by the root browser lane.

The login probes held access and refresh cookies only in process memory. No credential, cookie, token, or raw
login response was archived.

## Discarded attempt and cleanup

The first launch attempt ran inside the Codex network-disabled sandbox. Every process remained before managed
network startup, produced zero-byte runtime logs, and opened no listener. Those PIDs were terminated and the
attempt is recorded as DISCARDED. The accepted run used the already-authorized local runtime path.

After browser capture, all five accepted runtime processes were stopped. `raw/listeners-after-cleanup.tsv`
records that ports 5300, 5301, 5356, 5357, and 5361 have no listener.

