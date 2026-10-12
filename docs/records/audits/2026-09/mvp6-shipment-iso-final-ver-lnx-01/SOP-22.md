# SOP §22 — MVP6-WP-183-ISO-FINAL-VER (Q61 v1.1, Linux chat lane)

**Verdict: NOT RUNNABLE ON LINUX CHAT LANE — Step 0 FAIL; Steps 1–8 NOT RUN.**

- Lane: AL-MVP6-183-ISOVER-LNX-01 (VER, independent). Repo `/Users/natig/Projects/ERP-vNext-recovery` via the device bridge (Linux VM, aarch64), `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched).
- Start 2026-09-26T12:11:01+03:00; end in the final report (Europe/Istanbul).
- **PROVISIONAL-LINUX note:** the approved environment rule names native macOS .NET. Even a successful Linux run could only be PROVISIONAL, pending a separate owner decision to accept Linux runtime evidence or a later Mac rerun. No run happened, so nothing is provisional or accepted.

## Step 0

See [FEASIBILITY.md](FEASIBILITY.md) and [COMMANDS.tsv](COMMANDS.tsv). Failing checks: `/tmp` free space 4.2 GB (< 6 GB); .NET, MongoDB and Playwright-browser download hosts refused by the egress proxy (HTTP 403 on CONNECT). No workaround attempted.

## Rows

| Row | Result |
|---|---|
| UI183-A01, A02, A03 (missing-read), A04, A05, A06, A07, A15 | **NOT RUN** (Step 0 fail). No static or inherited evidence counted. |
| UI183-A10, A13, A14 | OPEN (owner decisions DN-01, DN-02, PC rows) — unchanged |

## Evidence not produced (Step 0 stop)

BUILD-INPUT-MANIFEST.tsv, SOURCE-BINARY-PROCESS.tsv, EVIDENCE-CHECKLIST.md, raw/, png/ and PNG-INDEX.tsv were not produced, because Steps 1–7 did not start. CLEANUP.md records that nothing needed cleaning.

## ASSUMPTIONs

1. The `/tmp` disk requirement is read as a Step 0 feasibility criterion (prompt §0a, §0e "no disk"). The session volume has 7.7 GB free, but the prompt names `/tmp`; the network failure alone is already a hard stop either way.
2. Reachability was probed with HTTP HEAD requests only; no file was downloaded.

## Repository state

Only this folder was written. **Uncommitted — to be committed from a Mac session.** No git writes; no `.git/index.lock`.

Agent PASS ≠ CT ACCEPTED — returning to CT.
