# CT disposition — Q40 self-registration prep and Q41 backlog entries (2026-09-26)

Recorded by: Control Tower, 2026-09-26 09:10 (Istanbul). New record (K4: earlier records are not edited).
Base: feature/mvp6-logistics @ 4a8d4d4b; no commit, push or stash.

## Q41 — MVP6 deferred items in product-backlog.md — DONE (accepted)

| Check | Result |
|---|---|
| Authority | `docs/records/decisions/2026-09/mvp6-backlog-entries-owner-decision-01.md` (append-only) |
| Change | `docs/roadmap/backlog/product-backlog.md` +187 / −0 lines; entries BL-372 … BL-384 appended (headers at lines 5079–5252) |
| Evidence | `mvp6-backlog-entries-apply-01/` — ARTIFACTS.sha256 3/3 verified by CT (file hash `712328ce…6bca36`) |
| Finding closed | AG-02 (compliance check 2026-09-26) |
| Effort | No product effort credit (governance record). |

## Q40 — Supply Chain self-registration preparation — DONE (prepared, not approved)

| Check | Result |
|---|---|
| Output | `docs/roadmap/plans/mvp6-self-registration-prep-01/` — SHA256SUMS 8/8 verified by CT (file hash `c2fa03f6…61e356`) |
| Content | Capability-level foundation (DCP-009 follow-up) + per-module manifest sections for MOD-0183…0187; S&OP/Capacity excluded; 11 Nav keys × 7 languages; test plan |
| Estimate | 38/70/127 h (D2=A); D2=B +4/8/14; D5=B +1/2/4 — **unestimated scope removed from the ledger only after owner decision** |
| Decisions | D1–D5 in OWNER-DECISION-TEXT.md → new queue item Q43 (DECISION-REQUIRED) |
| Recorded gaps | UI source not in common checkout; Returns/Claims permission classes; single-key action model; Platform credential (D2=B); new domain `Nav.Domain.SUPPLYCHAINEXECUTION`; `NavManifestL10nGuardTests` coupling; GOLDENSLIM case inconsistency |
| Finding | AG-01 stays OPEN (blocker) until code lands via the integration owner after Q14/Q15. |
| Effort | No credit (preparation). |

## Environment note

A CT `git status` in the device shell left an empty `.git/index.lock` (the shell cannot delete). The owner removes it; CT uses
`GIT_OPTIONAL_LOCKS=0` for all further read-only Git calls.

## Runtime lanes

Q04 (A12 VER-02), Q24 (kit validation), Q25 (Loads publication): no output folder exists at 09:10. State unchanged.
