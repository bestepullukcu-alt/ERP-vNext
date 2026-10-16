---
decision_id: CT-I4-AC-TRACEABILITY-PROPOSED-01
status: proposed — not decided; the owner chooses whether, and at which scope
proposed_at_local: 2026-10-16T11:33+03:00
revised_at_local: 2026-10-16 — after Codex's review: the pack-version binding, the v1/v2 case, the standard's own example, the 163 packs as an expectation
proposed_by: the AI session and Codex, under CT-COMPLETION-DELEGATION-OWNER-DECISION-01 (a proposal, not an approval)
scope: two İ4 items — the pack "Teslimatlar" section and the delivery check — read as traceability from a pack's acceptance criteria to acceptance records; new Control Towers only
relates_to: ct-new-ct-standards-owner-decision-01.md §1 (İ4); CT-I4-RECORD-CODE-LOCK-OPEN-01; CT-OPS-03-ACCEPTANCE-CONTRACT-JOINT-DECISION-01; SOP §29.2
recorded_by: the AI session (Claude Code) on 2026-10-16
---

# İ4 — linking a pack's acceptance criteria to acceptance records (PROPOSED)

**Nothing here is decided or built.** The İ4 list names "a Teslimatlar section" and "the delivery check script" only
by name; reading them as acceptance-criteria traceability is this proposal's reading — NOT VERIFIED as what the names
meant.

## The gap, measured on 2026-10-16

- SOP §29.2 lists, for Module Done: all mandatory module-pack AC, required WPs accepted, golden flows, module-level
  regression, the pack's status update, and no open blocker (or an approved deferral).
- `.antigravity/rules/module-pack-standard.md` §7 gives acceptance criteria neither an identity nor a mandatory mark
  (the same at this branch and the local `origin/main`). In 144 of 163 packs they are unnumbered checkboxes; "mandatory"
  appears in 5; no pack has a "Teslimatlar" section; no tool checks a pack's delivery.
- So which mandatory criterion an accepted record closed cannot be traced mechanically. A person — the independent
  verifier and the Control Tower — can still trace it by reading.

## The option

For new Control Towers only; the 163 existing packs are not converted.

1. **Fixed identities.** In a new pack, each acceptance criterion starts `AC-<n>` and carries `(zorunlu)` or
   `(isteğe bağlı)`. An identity is never removed or renumbered.
2. **An approved pack version.** The check reads the pack at a named commit (`yol@commit`) where its single `status` is
   `approved` or `ready-for-dev`; criteria are read only from that version, and a record counts only when its bound
   pack version is exactly that `yol@commit`.
3. **The record side.** In an acceptance list (REP-001 §6.2) the `ID` column may carry the pack's `AC-<n>`. The ID
   alone is not enough: the same module's other approved pack version may give `AC-<n>` another meaning. So the
   acceptance record binds, explicitly, the approved pack version it covers — `yol@<40-hex commit>`. Today's
   `required_evidence_source` does not always give it (it may point to a work-package file), so S2 adds a binding to
   REP-001 §6.1 — backward compatible, mandatory only for this check; its exact format is designed separately.
4. **The check.** A read-only `acceptance_record.py module-check --queue <Q> --module <M> --pack <yol>@<commit>`.
   Exit 0 means only: every mandatory `AC-<n>` of that pack version is `geçti` (or `N/A — <gerekçe>`) in an
   authoritative, accepted record of that module. **It never means Module Done** — the other SOP §29.2 conditions are
   not checked. Exit 1: a mandatory criterion is not covered (named). Exit 2: not measurable.

## Acceptance criteria of the option

| Case | Expected |
|---|---|
| A synthetic new pack: AC-1..3 mandatory, AC-4 optional; records cover AC-1 and AC-2 | exit 1, AC-3 named |
| The same, records cover AC-1..3 | exit 0; AC-4 missing does not block |
| A record of another module (an FU is its own module value) | not counted |
| A pack version that is not approved / ready-for-dev, or criteria read from another version | not measurable (exit 2) |
| A record bound to another version of the same pack, or bound to none | not counted |
| The same module and the same `AC-<n>`, but v1 and v2 give it different meanings | a record bound to v2 does not close v1's coverage, nor the reverse |
| A conditional or rejected record; a record that is not its work's authoritative head | not counted |
| A newer revision `-R<n>` over the work that is not ACCEPTED | that work's rows not counted (current-revision rule, as CT-OPS-04) |
| `geçti` without evidence | not counted (REP-001 §6.2) |
| A repeated `AC-<n>` in the pack | refused |
| An old pack without identities | exit 2 "ölçülemez", never 0 |
| The 163 existing packs | expected once the check exists: all "ölçülemez", none changed — not measured today, no check exists yet |
| Each guard removed in turn | at least one named test red |

## The smallest scopes to choose from

| Scope | Files | Note |
|---|---|---|
| S0 — keep open | none | the gap stays; people trace by reading |
| S1 — rules only | `module-pack-standard.md`: §7 (two bullets) and the standard's own example pack, whose `## Acceptance Criteria` block shows the new form; REP-001 §6.2 (one sentence) | no tool: the rule is checked only by people |
| S2 — rules and check | S1 + REP-001 §6.1 (the pack-version binding: backward compatible, mandatory only for `module-check`) + `scripts/acceptance_record.py` (the binding in its strict reader, `module-check` and their self-tests) + SOP §29.2 (one line); the exact format is designed before any code | the shared files need the ControlTower (Legacy) session's single-writer windows |

The AI session's view: S2 if the owner wants the AC coverage of new Control Towers measured; otherwise S0. Priority 🟡 —
no new Control Tower declares Module Done today.

## The record-code lock is not part of this

RCS-001 (`.antigravity/rules/record-coding-standard.md`) holds rules on a code's permanence (§2, §5.2, §6; orchestrator
gate 12; the pack template's section 22). Whether İ4's "record-code lock" meant them is NOT VERIFIED; the owner's
answer stands — the item stays open (`CT-I4-RECORD-CODE-LOCK-OPEN-01`).
