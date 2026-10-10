# Q449 — Returns UI scope: "an intent is one opened form", carried into the approved scope file

- Lane: Q449, `documentation-writer`. Target: `docs/roadmap/plans/mvp6-ui-pack-drafts-01/returns/SCOPE.md`.
- Placement: Claude app → Code tab → Local, `uname -s` = Darwin. G2 placement waiver applied — Cowork withdrawn by owner.
- Preflight 2026-10-05 01:08:44 +03: `feature/mvp6-logistics` · HEAD `cea01354e` · `git status --short` 70 · 0 staged · no
  `index.lock`. Target clean (equal to HEAD, sha256 `6ed52afb…`).
- Read: `documentation-writer.md`, commit `cea01354e` (message and file list), Q403 `REPORT.md`, MOD-0186:713-718 (the amended
  text), R-2 `evidence/traps-browser.md` §T2 (exists, `:21`).
- **Nothing staged or committed. Only the one sentence changed.** Agent verdict ≠ CT ACCEPTED.

## The change (`evidence/SCOPE.md.diff`, 9 insertions / 1 deletion, all inside item 7 of §6)

`SCOPE.md:112` read **"Editing the payload starts a new intent."** — the per-payload rule `cea01354e` removed from MOD-0186,
0187, 0190 and 0192. It is replaced by the Q403 definition **verbatim** as MOD-0186:713-718 carries it, for this file's
surfaces ("create form or transition panel", retries "network/500/503"), with a parenthesis that cites Q449, Q403 and
`cea01354e` and quotes the removed sentence, and R-2's measurement sentence (two Returns from one intent per-payload; one
key per opened form gave 409 and one Return).

Checked mechanically: with whitespace normalised, the text before and after MOD-0186's "(amended 2026-10-04, Q403)" both
occur unchanged in the new `SCOPE.md`. The rest of item 7 ("409 `IDEMPOTENCY_KEY_REUSED` stops retry. 201 …") is
unchanged; it agrees with the new definition, as do `:111` ("per-intent `Idempotency-Key`") and `:147` ("Stop retry;
require a new user intent"). sha256 after: `8ef88492…`.

## "The only scope file among the drafts carrying it" — verified, with a qualification

- **True as worded.** `find docs/roadmap/plans -iname '*scope*.md'` → 7 files; the per-payload grep hits only
  `mvp6-ui-pack-drafts-01/returns/SCOPE.md:112` (`evidence/scope-sweep.txt`).
- **Not the only scope document carrying it.** Three tracked files in other UI scope packages still state the rule
  (not in this lane's write scope; not changed):
  - `docs/roadmap/plans/mvp6-carrier-ui-scope-01/ACCEPTANCE.md:10` — UI184-A06 "Editing the payload creates a new request intent/key." (an acceptance row);
  - `docs/roadmap/plans/mvp6-carrier-ui-scope-01/SCREEN-FLOW.md:24` — "a user-edited payload begins a new intent and therefore a new key";
  - `docs/roadmap/plans/mvp6-shipment-pod-ui-scope-01/SOP-22.md:81` — "A changed payload is a new intent".
  The packs these scopes fed are already correct: MOD-0184:529 carries the opened-form definition citing R-2, and
  `mvp6-process-pilot-01/MODULE-RECIPE.md:56` states one key per form instance. The remaining hits are historical
  `.patch` files (pack revisions already applied or superseded).

## Findings

- **F-Q449-1 — the edit breaks the package's checksum line for this file, and with it what the approval bound.**
  `mvp6-ui-pack-drafts-01/SHA256SUMS` lists `./returns/SCOPE.md` as `6ed52afb…`; it now hashes `8ef88492…`, and
  `shasum -c SHA256SUMS` reports `./returns/SCOPE.md: FAILED` (`evidence/package-sha256sums-check.txt`). MOD-0186:599 and
  MOD-0187:548 pin that `SHA256SUMS` file (`b568c94f…`), whose own bytes are unchanged, so the packs' pin still matches —
  but the checksum it carries no longer verifies the scope text the owner approved
  (`mvp6-returns-claims-ui-scope-owner-decision-01`). Regenerating `SHA256SUMS` would change `b568c94f…` and require editing
  both packs. Not done (outside this lane). **Owner/CT decision:** record this amendment against the approval, or re-approve
  the amended scope and re-pin.
- **F-Q449-2** — three tracked scope-package files outside `*SCOPE*.md` still carry the per-payload rule (list above), one
  of them an acceptance row (UI184-A06). A lane reading those packages instead of MOD-0183/0184 would build the defect from
  an approved source — the same K6 shape this lane closed for Returns.

## Not done

No other file in the package, no `SHA256SUMS`, no pack, no acceptance row.
