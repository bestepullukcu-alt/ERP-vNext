# SOP-22 — Browser Runtime Handoff

The v3 Carrier UI was copied into an immutable snapshot by overlaying only the 21 manifest-owned paths onto the
previous exact integration snapshot. All 21 SHA-256 and byte-size pairs matched before build and after runtime.

`Diten.Web` was built fresh from that snapshot. The accepted processes used separate 53xx ports and disposable
Mongo database names. Gateway used a temporary Ocelot file rather than changing repository configuration.
Platform's Auth callback and SupplyChain's module registration endpoints were overridden to the isolated ports.

The accepted run established source-to-binary-to-process evidence in `raw/source-binary-runtime.sha256`,
`PROCESS-START.tsv`, and `raw/processes-p53.txt`. The admin and no-role HTTP probes kept tokens in memory only.

Root browser execution completed the admin viewport, console, RTL, DataTable, toolbar, filter, Add, and offcanvas
checks. The subsequent fresh no-role browser login did not complete, so UAS browser acceptance remains OPEN even
though the same hash-bound runtime produced a denial-only UAS response through the HTTP preflight.

All runtime listeners were stopped after capture. No commit, push, stash, source edit, operational database use,
or backend behavior change occurred.
