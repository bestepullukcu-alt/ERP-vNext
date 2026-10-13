# Evidence checklist (process v1.0 §5) — verifier view

| §5 item | Status | Pointer |
|---|---|---|
| Exact source manifest and archive SHA-256; base HEAD; overlay list | PARTIAL — input hashes and HEAD verified; overlay composition NOT RUN | `raw/01-preflight-probe.txt`; `SOP-22.md` P1–P2 |
| Native .NET 8 SDK/runtime versions; effective config before start; no 27017 | NOT RUN — runtime unreachable; no Mongo started, so no 27017 connection | `raw/01-preflight-probe.txt` |
| Source → binary → process → browser binding | NOT RUN | `SOP-22.md` |
| DB before/after for every mutation and negative case | NOT RUN | `SOP-22.md` |
| Redacted raw evidence (no tokens/cookies/credentials) | MET — raw holds only hashes, paths and tool probe output | `raw/` |
| Cleanup record | MET | `CLEANUP.md` |
| Incomplete archive → HEAD-archive + overlay method before "not runnable" | NOT APPLICABLE — stop cause is executor environment, not archive completeness | `SOP-22.md` Blocker |
