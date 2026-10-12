# Terminal runtime lane — A12 independent runtime VER, attempt 2 (queue Q04)

Written by CT 2026-09-26 for Claude Code in Terminal on the Mac. Follow it exactly.

Role: MVP6 Terminal runtime lane — independent A12 runtime VER, attempt 2 (queue Q04). 🤖 Applying knowledge of @testing-agent and @devops-agent.
You did not write the A12 patch. No access to earlier conversations. Attempt 1 (docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-01/) stopped at preflight because it ran in a Linux VM; you run natively on the Mac.

Repo: /Users/natig/Projects/ERP-vNext-recovery (expected feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c; STOP if different).
Git: read-only, always GIT_OPTIONAL_LOCKS=0. NO add/commit/push/stash/reset/clean/rm/mv; no edits to repository files outside your output folder.
Process: docs/guides/operations/mvp6-development-process-v1.0.md (§5 evidence checklist, §6 verification by impact); plan docs/roadmap/plans/mvp6-development-plan-v9.2.md §2 steps 8 and 10.

Authority (owner, 2026-09-25, CEO Natig Yusubov): existing records suffice —
docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/INDEPENDENT-VER-HANDOFF.md lines 8–24 and
docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/INDEPENDENT-VER-HANDOFF.md. Scope: disposable, isolated, evidence-only run of the A12 successor.
Copy this authority verbatim into AUTHORITY.md. NO source repair, common checkout, gateway/.antigravity/contract edits, A10 proxy.

Read first: AGENTS.md, CLAUDE.md, MOD-0183 pack; docs/roadmap/plans/mvp6-claude-development-handoff-01/{ENVIRONMENT,AUTHORITY-AND-GATES}.md;
both handoffs above; mvp6-shipment-a12-safe404-rework-01/SOP-22.md; mvp6-shipment-a12-safe404-independent-ver-01/SOP-22.md; template run
mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/ (SOP-22.md, raw/runtime-and-input-hashes.txt, COMMANDS.tsv, scripts/);
mvp6-shipment-shared-ui-independent-ver-01/SOP-22-VER.md (HEAD archive + overlay method); the attempt-1 folder; docs/roadmap/plans/mvp6-process-pilot-01/EVIDENCE-REUSE.tsv;
evidence kit proposal docs/roadmap/plans/mvp6-evidence-kit-proposal-01/ (use its scripts where they fit; record which).

Preflight:
1. Hashes: A12 patch b3c7dcbb…ecefb, archive 7b6a0d1a…314d, manifest 8ffa6c96…0d36; Auth overlay mvp6-carrier-numericdate-exec-01/final-source.tar.gz f50350b8…e2cd and FINAL-22-SOURCE-MANIFEST.tsv b9713185…1731 (full hashes in the records).
2. New /private/tmp dir: git archive 4a8d4d4 → A12 360 overlay → Auth 22 overlay. Prove zero path overlap, 360/360 and 22/22. No dirty-checkout source.
3. Generate a disposable gateway route config from HEAD ocelot.json for lane ports only; never edit the repo ocelot.json; record diff and hash.
4. BUILD-INPUT-MANIFEST.tsv separate from the owned 360 manifest. STOP and report any missing dependency.

Run: native /Users/natig/.dotnet/dotnet (verify SDK 8.0.417 / runtime 8.0.23; .NET 10 not acceptable). Release binaries, fresh isolated ports,
DB-010 isolated Mongo replica set (never 27017; verify resolved config before start). Fresh real-Auth identities: actor-a T1/LE-A, actor-b T1/LE-A,
actor-le-b T1/LE-B; separate browser profiles; no cookie swapping. Secrets generated locally, never written to files.

Early vertical slice first: one normal authorized detail load end-to-end before the negative matrix.
Test: normal detail (summary/lines/POD/actions); cross-LE, unknown, soft-deleted detail show only localized safe-not-found + support reference;
hidden surfaces hidden/inert and not keyboard-reachable; late async responses do not re-expose stale surfaces; backend 404 SHIPMENT_NOT_FOUND
+ correlation unchanged; DB before/after zero writes; add negative controls.
Impact regression: A12 changes load() in details.js (refresh path of A08/A09) → rerun the A08 stale-transition and both A09 stale-POD browser flows.
Accessibility and A03/PRES-183-02 stay inherited (no impact).
PNG: use Playwright's supported screenshot save (page.screenshot to file) if available; save PNGs with sha256. If not possible, record the blocker. No base64/CDP/tunnel workaround.
Forbidden: counting static or inherited evidence as PASS; storing tokens/credentials.

Output: docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02/ → AUTHORITY.md, SOP-22.md (PASS/FAIL/NOT RUN per criterion incl. A08/A09 regression and PNG),
BUILD-INPUT-MANIFEST.tsv, EVIDENCE-CHECKLIST.md (process §5, each item with pointer), raw/ (redacted; source→binary→process→browser), png/,
CLEANUP.md (processes, ports, Mongo data removed), ARTIFACTS.sha256.
Final report: start/end timestamps (Istanbul), agent run time, waits. Return to CT; CT decides.
