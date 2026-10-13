# MOD-0184 DEV-03 — evidence-only rework v1.0

WP MVP6-MOD0184-DEV03; lane AL-MVP6-MOD0184-DEV03; target testing-agent;
risk HIGH; evidence E4 recording; approved bounded Carrier scope remains unchanged.
Repository /Users/natig/Projects/ERP-vNext-recovery; branch feature/mvp6-logistics;
HEAD 4a8d4d4b339528a88e6220fb8402e5a2c771136c. Sole writer; capture fresh dirty baseline.

NE: Fix VER-184-01, the outage recorder substituting a space for an absent HTTP body.
NEDEN: Independent VER02 found two mismatches among nine requests; functional checks passed,
but their recording cannot be accepted. Report: /private/tmp/mod0184-runtime-ver02/verification-report.md.
NASIL: Preserve exact zero bytes and present JSON bytes. Add meaningful RED-before/GREEN-after
regressions for both. Regenerate the affected live outage/actual-commit-uncertainty evidence with
transport-boundary observation and exact mismatch comparison. Reuse the safely isolated test Mongo
or create a separate isolated fixture. Stop owned HTTP/proxy processes and disable failpoints afterward.
YAPMA: No production changes, contract/shared-guard/old-evidence edits, git mutations, waiver,
acceptance or other-module work. Old DEV02 evidence remains immutable historical failed handoff.
Owned paths: services/Diten.SupplyChainService/tests/carriers/restart_probe.py;
new capture regression under that same carriers directory;
new docs/records/audits/2026-09/mod-0184-dev-03/ evidence/report/inventory only.
DOĞRULA: Regression RED→GREEN, fresh live requests with zero byte mismatches, unchanged production
and prior inputs except named recorder; complete changed-file hashes and SOP §22 handoff.
No broad test repetition unless a new change/failure warrants it. Independent VER follows frozen handoff.
Architecture14/4 remains explicitly blocked; this rework does not alter its provenance or grant waiver.
