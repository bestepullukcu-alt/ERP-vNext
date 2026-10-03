# CT intake — Loads 3.1.0 publication + guard binding (Q25) — 2026-09-26

Recorded by CT 2026-09-26T11:26+0300. New record (K4). **Intake only — CT disposition after independent VER (Q32, EXECUTION-ORDER Step 5).**
Writer: AL-MVP6-LOADS-PUB01, Claude Code in Terminal on the Mac, done 11:24 (≈7 min). Evidence `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/`: SHA256SUMS **24/24 OK** (CT, from the folder).

| Write | File | sha256 now (CT) | Expected (lane report) |
|---|---|---|---|
| W1 | docs/analysis/contracts/shipment-bundle.openapi.yaml | 6dc1dd486375130d… | 6dc1dd48…96aa2 (was 5dfe7c1b…) |
| W2 | docs/analysis/contracts/loads-semantics-v3.1.0.md | 9d8a370664abb822… | 9d8a3706…f5034 |
| W3 | docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md | 84f07a6b6dde5395… | 84f07a6b…306f0a |
| W4 | docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json | 64db811693d3a251… | 64db8116…491234 |
| W5 | docs/reference/architecture/docs-path-authority.json | 65a8ccbdd09acb97… | 65a8ccbd…660a4e (was 6d3865f8…) |

Lane-reported results: preconditions PASS; baseline 52/56 (DocsPathGuard 19 expected offenders; JwtClockSkew ×2, MongoTestDatabase ×1 pre-existing); rehearsal PASS (39/39); repository write PASS; post-checks PASS (payload a77538b4…, W4↔W5 binding, 2 targets / 43 seals, v2 annex unchanged); production DocsPathGuardTests 39/39; full architecture 53/56 (same 3 pre-existing failures); secret scan last write PASS. No commit/push/stash/--index.
Deviations reported by the lane (to be judged in VER): `--results-directory` added to avoid overwriting the 23 Sep TRX; echoed exit codes wrong in one Step-3 line (TRX authoritative); W3–W5 written by a python3 script kept outside the repo (bytes saved as raw/w345.py.txt); rollback copies kept in /private/tmp/mvp6-q25-loadspub.4mTLMX until CT decides.
Tracked diff: 17 files (W1 was already in the tracked set); new untracked: W2, W4, evidence folder (W5 was already untracked). Pre-existing JwtClockSkew/MongoTestDatabase failures are outside this task; noted, not raised here.
Next: Q32 independent VER in a new Terminal session (not the writer session).
