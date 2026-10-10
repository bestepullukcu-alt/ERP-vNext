# Terminal runtime lane — Loads 3.1.0 publication + guard binding (queue Q25)

Written by CT 2026-09-26 for Claude Code in Terminal on the Mac. Your full task is DISPATCH v1.1:
`docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.1.md` — read it and carry out the block inside it exactly.
🤖 Applying knowledge of @integration-agent and @testing-agent.

Additions from CT (they do not widen the dispatch):
1. Git: always prefix with `GIT_OPTIONAL_LOCKS=0`. `git apply` is allowed only for the W1/W2 patch named in EXECUTION-ORDER.md; no other git write.
2. Local security settings (`.claude/settings.local.json`, owner-approved) block the Edit/Write tools on `docs/analysis/**`, `docs/records/decisions/**`, `docs/reference/**` and other product paths. Do NOT change these settings. Write W1–W5 only by the exact methods in EXECUTION-ORDER.md run from Bash (`git apply` for W1/W2; byte copy for W3; exact string replacement with a short python3 script for W4/W5), each followed by a sha256 check against the expected value. Your evidence folder is writable with the normal tools.
3. You are the only writer in the repository during this run. If `git status` shows a change you did not make, STOP and report.
4. Record served/executed file hashes for every test you run; keep one unique file name per attempt (never overwrite); run the secret scan after your last write.
5. On any failure rule in EXECUTION-ORDER.md: restore exactly as it says, record it, STOP. Do not improvise fixes to product, tests, rules or guards.
