---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Independent VER of the six MVP-6 UIs — hand-off (Phase A read-only-auditor → Phase B documentation-writer)

    prepared  : 2026-10-18 by CT (gmg-10)
    target    : AL-MVP6-6UI-VER, VER, HIGH
    repo      : /Users/natig/Projects/ERP-vNext-recovery · branch `feature/mvp6-logistics`
    base/freeze: HEAD `651a0b5f06b1f56b070b198b198f0764c0631ba5` — re-freeze fresh hashes of every file in
                scope before dispatch; fail the run if the tree differs at execution time.
    releases  : the owner's **MOD-0184 `:376` runtime-write** reservation (pack §23 owned paths accepted
                "as design; no runtime write"); `:376` withholds runtime *write*, not observation. A clean
                independent VER is the owner's precondition to release it. VER does not release it — it
                produces the evidence the owner releases on.

## AGENT CONTRACT (G3)

- **Phase A — `read-only-auditor`** (mode **strict**: no fetch/pull, no `.git` write, no Edit/Write, no
  fixes). `Read` FIRST, in order: `AGENTS.md` (full) · `.antigravity/agents/read-only-auditor.md` ·
  `.antigravity/workflows/read-only-audit.md` · `.antigravity/rules/git-safety.md` (GIT-002, full) ·
  `.antigravity/rules/frontend-standards.md` + `.antigravity/rules/frontend-js-standard.md` ·
  `.antigravity/rules/localization-standard.md` + `.antigravity/rules/dynamic-localization-standard.md` ·
  `.antigravity/rules/unauthorized-surface-standard.md` (UAS-001).
  **Write-scope check:** `tools: Read, Grep, Glob, Bash` — read-only, covers an audit, owns no write. PASS.
- **Phase B — `documentation-writer`** (`tools: …, Edit, Write`) writes the VER report **into this folder
  only** (`docs/roadmap/plans/mvp6-6ui-ver-handoff-01/REPORT.md` + `SHA256SUMS`). It persists the Phase-A
  findings; it makes no source/fixture/report repair.
- **G3 note:** `read-only-auditor` has a §17.4 entry; `documentation-writer` does **not** — its fields are
  supplied here **by analogy** (§17.4 has no documentation-writer row), per the standing rule. The pairing
  (auditor cannot Write → documentation-writer persists) is the §17.4-permitted lane composition.

## Scope — the six UIs at HEAD (frozen inventory)

| module | views (tracked) | resx (7 langs) |
|---|---|---|
| MOD-0183 Shipments | 10 | 7 |
| MOD-0184 Carriers | 6 | 7 |
| MOD-0185 Loads | 5 | 7 |
| MOD-0186 Returns | 6 | 7 |
| MOD-0187 Claims | 6 | 7 |
| MOD-0188 DemandPlanning | 4 | 7 |

Include this session's UI changes in the audit target: the l10n glossary **Part A** (key renames +
A2/A3/A4 value convergence) and **Part A2** (error-key `Err*` convergence, incl. the Carriers JS dispatcher
rewrite to `ERROR_KEY`/`t()`), and the Returns `index.js:348` **F-R2-5** fix. Records:
`mvp6-supplychain-l10n-partA-pass-01/`, `mvp6-supplychain-l10n-partA2-pass-01/`.

## NE (what to verify — independently, with `path:line` evidence)

1. **l10n completeness & parity.** Every module's resx has **7 languages** with an **identical key-name
   set** (measure by `<data name>` MATCH count, not line count — files are inline-packed); `en` is the wire
   source; no empty/placeholder/English-leak values; no key the JS/bridge references is missing.
2. **Err* convergence correctness (Part A2).** For each module, every wire `failure.code` the service emits
   maps to an existing `Err*` resx key (Carriers via `ERROR_KEY`+`STATUS_FALLBACK_CODE`+`t()`; others via
   their dispatcher/bridge); unknown → `ErrInternalError`; **0 plain infra keys** (`AccessDenied`,
   `ValidationError`, `InternalError`, …) remain as resx keys; bridge props resolve to live keys.
3. **UAS-001.** No page skeleton or data is drawn for an unauthorized user; 403 → the denied card, no
   redirect; the list/first-load path fails closed.
4. **Tenant isolation at the UI seam.** Every data call is tenant/LE-scoped (legalEntityId on the wire); no
   cross-tenant leakage path; no module calls another module's endpoint.
5. **No secrets / no PII in URLs.** No token, key or personal data in query strings or logs.
6. **Build/syntax.** `node --check` on each `index.js`; the Razor views compile; the `_IndexL10n` bridges
   are well-formed.

## NEDEN

A DEV/lane PASS and CT's own in-session verification are **not** an independent VER. MOD-0184 `:376` stays
held until a strict read-only auditor, re-running the checks on a frozen tree, confirms them — so the owner
can release runtime-write on evidence, not on the implementing lane's word.

## NASIL

Read-only repo; for any build/`node --check`/grep sweep use a **hash-identical disposable copy**, never the
tree; all evidence paths `path:line`; reports land outside the audited source (Phase B, this folder).

## YAPMA

No source/resx/JS/bridge repair, no key rename, no git mutation, no owner acceptance, no `:376` release
claim, no "verified" stamp on another lane's run. Any defect found is a **finding** → a separate DEV rework
lane, not a fix here.

## DOĞRULA (gate)

- Re-measure parity and the dangling-key / plain-infra-key scans **independently** (do not trust CT's
  numbers): expect 7/7 parity per module and **0** dangling/plain-infra keys.
- Independently confirm the Carriers `ERROR_KEY` targets all exist as resx keys 7/7 and the fallback is
  `ErrInternalError`.
- Fail the VER if any parity gap, dangling key, English-leak, UAS-001 breach, cross-tenant path, secret in
  URL, or a vacuous/mis-scoped check is found. SOP §22 verdict with evidence, required rework, and an
  **unchanged-repo proof** (`git status` clean, `git diff --name-only` empty after the run).

## Relationship to the other open steps

Independent of Q151 (integrated run) — this VER is the **static/observational** bar on the UI layer. It can
run now on the branch; it does not need a tenant sign-in (that blocker is Q151's UI-driven run, not this
audit). Parts B/C of the l10n pass (native terms, SharedResource) are out of scope — flag, don't verify.

## Files

- `VER-HANDOFF.md` — this. Phase B writes `REPORT.md` + `SHA256SUMS` beside it.
