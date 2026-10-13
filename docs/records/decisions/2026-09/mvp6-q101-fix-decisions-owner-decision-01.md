# Owner decision — Q101 fix decisions D1–D6 — 2026-09-26

Recorded by the Q102 lane (WP-MVP6-GOV-102 · Prompt Q102 v1 · AL-MVP6-GOV-102; Claude Code on the Mac; single ledger writer)
at 2026-09-26T22:04+03:00 on CT instruction (CT writes no files).
The owner gave these decisions through the question tool, ~21:50–21:58 +03:00, in the CT conversation.

Source audit: `docs/records/audits/2026-09/mvp6-q101-control-audit-01/` (FINDINGS.tsv `8c37547d16fe8ea64533386d7c35727f9e0ddeac90f9ab830667343d7708d886`).
CT verdict: `docs/records/audits/2026-09/mvp6-ct-verdict-q101-2026-09-26.md`.

## Decisions

| # | Finding | Choice | Decision | Fix WP |
|---|---|---|---|---|
| **D1** | F01 working tree ≠ accepted source | **A** | Compose the accepted source now, by exact hash, **only in `~/mvp6-env/`**: HEAD + BC-SOURCE + A12-360 + Auth 22. That composition is the common base for every Mac build. The working tree stays unchanged until the final integration. | Q103 |
| **D2** | F09 module-local permission attributes | **A** | Move to `[HasPermission]` and keep the contract 403 body. security-agent reviews the change. Run a 401/403 regression on the Mac. | Q109 |
| **D3** | F11 archives without a CT/VER record | **A** | The 6 unrecorded archives stay as evidence and are marked "superseded/unrecorded evidence". They are held from commit. security-agent scans them for secrets. | Q110 |
| **D4** | F12 TRX build output in the repo | **A** | Copy the `TestResults` TRX files into their evidence records. Add `TestResults/` to `.gitignore`. **Delete nothing.** | Q111 |
| **D5** | F14 port 5061 missing from ports.md | **A** | A rule patch adds port 5061 and the lane test-port convention to `.antigravity/rules/ports.md`. Order: sign-off → exact apply → VER. | Q113 |
| **D6** | F15 architecture guards scan draft .cs under docs/ | **A** | DocsPathGuard is **unchanged**. From now on, draft code in records is kept only as `.tar.gz` archives. Open `overlay/` folders are held from commit. | — (standing rule) |

## D1 recipe (exact-hash inputs, as in accepted records)

- HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- BC-SOURCE `ebd5d80c…`
- A12 360 overlay `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d`
- Auth 22 `f50350b8…`

The prefixes above come from the Q65/Q84b/Q88b recipes (CT-QUEUE; `mvp6-ct-verdict-q88-2026-09-26.md`).
Q103 must pin every input by its full hash before it composes anything. The target is `SUCCESSOR-360-SOURCE-MANIFEST.tsv`; for example,
SupplyChain `Program.cs` must be `33027bcd65b7274eda322578da15ef7fa9b9ce6d6d9ef75fe01eb8bc25b29752`.

## Scope

- These decisions set the fix direction for F01, F09, F11, F12, F14 and F15. They are not applies.
- Each code, rule or `.gitignore` change goes through its own WP (Q103–Q113) with its own sign-off and VER where the SOP requires one.
- Q03a still applies: no commit (`docs/records/decisions/2026-09/mvp6-commit-strategy-owner-decision-q03a-01.md`).
- The decisions grant no authority to push, stash, delete or change the working tree. The final integration is the one exception, and
  only for D1.
