# Q403 (ledger Q402) — what an "intent" is: four pack paragraphs amended, two acceptance rows proposed

- Lane: Q403, documentation-writer. Runs before R-4. Recorded 2026-10-04.
- Preflight: `Sun Oct  4 17:15:09 UTC 2026` · `feature/mvp6-logistics` · HEAD `ca9330392` · porcelain 6 · staged 0. The
  four packs were clean against HEAD.
- Step 0 (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `docs-organization.md` `096ed27bdd6c2349`
  - `documentation-writer.md` `d2c09df48e8f9bbb`
  - R-2 `REPORT.md` `7d69c72129471b07`, which is the live measurement this rests on: its T2 table at `:106-107` and
    `evidence/traps-browser.md` §T2 `:21-32`
  - `MODULE-RECIPE.md` `04925ba82d8c1f69` (line 3.1)
- Packs before: MOD-0186 `be669789f16d5963`, MOD-0187 `7a89b0d36cd08592`, MOD-0190 `fbcad3249ea4a845`, MOD-0192
  `f42c181c03f8696b`.
- **Changed:** four prose paragraphs, one in each of MOD-0186, MOD-0187, MOD-0190 and MOD-0192.
- **Not changed:** any acceptance row, error-table row, field list, operation, error code, heading or table, MOD-0183/0184/0185,
  any code. Nothing staged or committed. Agent verdict ≠ CT ACCEPTED.

## The six locations, verified by opening them

| location | text found | kind |
|---|---|---|
| MOD-0186:712 | "Per intent: one pending request; the same key and identical body on network/500/503 retry; an edited payload is a new intent." | prose → **amended** |
| MOD-0187:664-665 | the same, with "**identical body text**", wrapped across two lines | prose → **amended** |
| MOD-0190:394 | the same, "on network/503 retry" | prose → **amended** |
| MOD-0190:449 | `SU-14 … edited payload new intent; IDEMPOTENCY_KEY_REUSED stops \| BLOCKED (DN-01)` | acceptance row → **proposed only** |
| MOD-0192:411 | the same as MOD-0190:394 | prose → **amended** |
| MOD-0192:468 | `CP-14 …` (identical wording to SU-14) | acceptance row → **proposed only** |

All six are as CT measured. MOD-0187's sentence spans `:664-665`, not one line.

A sweep of the four packs for `edited payload|new intent|per intent|per-intent|new key` found no other per-payload
definition. The "per-intent key forwarded exactly" lines (MOD-0187:593, MOD-0190:333, MOD-0192:342, MOD-0186:643) only
say the browser's key is forwarded unchanged. They are consistent with the new definition and were left alone.

## What was wrong, and what the amendment does

The old sentence makes the **payload** the unit of intent. A lane that follows it faithfully re-mints the key whenever
the body changes, and **is right to**. That is exactly the code in all three remaining drafts.

That reading is correct for a user who deliberately wants a different thing. It is wrong for a user correcting a
submission whose outcome they do not know. R-2 measured both behaviours live on Returns
(`mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2):

| key policy | sequence | result |
|---|---|---|
| re-minted per payload (the pack's reading; the draft's code) | create commits, the response is lost, the user edits one field, saves | new key → 201 → **2 Returns from one intent** |
| one key per opened form | the same sequence | same key → **409 `IDEMPOTENCY_KEY_REUSED`** → **1** Return |

**The amendment separates the two cases by what the user does, not by what the body contains:**

- **A deliberate new intent is a newly opened form or panel.** That is the only event that mints a new key.
- **Everything inside one opened form is the same intent**: edits, failures, retries. A correction under an unknown outcome
  therefore cannot become a second record.

The sentence's first half (one pending request; same key and identical body on retry) was kept verbatim, including each
pack's own retry status set.

**What the UI must do, checkable:**

| # | rule | check |
|---|---|---|
| 1 | mint the `Idempotency-Key` when the create form / transition or command panel **opens** | key exists before the first submit; it is minted once per open |
| 2 | **reuse** it unchanged across edits, failures and retries until the form or panel closes | an edited resubmit carries the same key (as in R-2's T2 request log) |
| 3 | mint a new key **only** for a newly opened form or panel | close and reopen gives a different key |
| 4 | on 409 `IDEMPOTENCY_KEY_REUSED`: stop, keep the inputs, tell the user the request was already received with different values and that a different request needs a new form; never mint a key to get past it | no further request is sent from that form (R-2: a third click sent nothing) |

Rule 4's user message is the meaning of each pack's existing error row ("Stop retry; new user intent required", e.g.
MOD-0186:707). The row itself is unchanged.

## The four prose edits — before, after, reason

Full before/after strings are in `evidence/before-after.json`; the unified diff is in `evidence/packs.diff`.

**Shared after-text** (each pack names its own surfaces and retry statuses):

> Per intent: one pending request; the same key and identical body on {retry statuses} retry.
> **An intent is one opened {surfaces}, not one payload** (amended 2026-10-04, Q403). The UI mints the `Idempotency-Key`
> when that form or panel opens and keeps it unchanged across edits, failures and {retry statuses} retries until it closes;
> only a newly opened form or panel is a new intent with a new key. A user who edits while the outcome is unknown resends
> under the same key, so a committed first attempt answers 409 `IDEMPOTENCY_KEY_REUSED` instead of creating a second record.
> On that 409 the UI stops, keeps the inputs, tells the user the request was already received with different values and
> that a different request needs a new form, and never mints a key to get past it.
> {Measurement sentence citing R-2's `evidence/traps-browser.md` §T2.}

| pack | before | after (the variable parts) | reason |
|---|---|---|---|
| MOD-0186:712 | "…identical body on network/500/503 retry; an edited payload is a new intent." | surfaces "create form or transition panel"; network/500/503; "Measured by R-2 … two Returns from one intent; one key per opened form gave 409 and one Return." | this is the module R-2 measured: the per-payload reading produced the duplicate live |
| MOD-0187:664-665 | "…**identical body text** on network/500/503 retry; an edited payload is a new↵intent." | surfaces "create form or transition panel"; network/500/503; "Measured on Returns (MOD-0186), whose create and command surfaces have this shape, by R-2 …" | same create + transition shape as Returns; its draft carries the per-payload code (`index.js:541,679`) |
| MOD-0190:394 | "…**identical body text** on network/503 retry; an edited payload is a new intent." | surfaces "create form or command panel (snapshot capture, sign-off)"; network/503; measured-on-Returns sentence; **plus one sentence: "Acceptance row SU-14 below still reads 'edited payload new intent'; its replacement is proposed to the owner in this record."** | its draft carries the per-payload code (`index.js:198`, `details.js:510`); the pointer stops a reader taking SU-14 as overruling the new prose before the owner decides |
| MOD-0192:411 | the same as MOD-0190 | surfaces "create form or command panel (scenario, evaluation)"; network/503; measured-on-Returns sentence; the same pointer to CP-14 | its draft carries the per-payload code (`index.js:217`, `details.js:616`); same pointer reason for CP-14 |

### Proof that nothing else moved (`evidence/packs.diff`, and a check against `git show HEAD:`)

| pack | headings identical (count) | table lines identical (count) | malformed table rows | lines removed / added | any changed line a heading or table row |
|---|---|---|---|---|---|
| MOD-0186 | yes (55) | yes (195) | 0 | 1 / 8 | no |
| MOD-0187 | yes (55) | yes (172) | 0 | 2 / 8 | no |
| MOD-0190 | yes (48) | yes (147) | 0 | 1 / 8 | no |
| MOD-0192 | yes (48) | yes (151) | 0 | 1 / 8 | no |

The diff touches SU-14 and CP-14 only as the text of the new pointer sentence; the rows themselves are byte-identical. No
field list, operation, error code, status or acceptance row changed.

## Proposed acceptance-row replacements — for the owner to accept or reject verbatim

Not applied. Changing these changes what "accepted" means for MOD-0190 and MOD-0192. The status column is left as it is
(`BLOCKED (DN-01)`).

**MOD-0190:449, SU-14 — current:**

```
| SU-14 | Idempotency: same key and body on retry; replay shown as completed; edited payload new intent; `IDEMPOTENCY_KEY_REUSED` stops | BLOCKED (DN-01) |
```

**Proposed:**

```
| SU-14 | Idempotency: one key per opened form or command panel, minted on open and kept across edits, failures and retries; same key and body on network/503 retry; replay shown as completed; an edit resent under an unknown outcome uses the same key and a committed first attempt answers 409 `IDEMPOTENCY_KEY_REUSED`, which stops with inputs kept and no new key; only a newly opened form or panel is a new intent | BLOCKED (DN-01) |
```

**MOD-0192:468, CP-14 — current:**

```
| CP-14 | Idempotency: same key and body on retry; replay shown as completed; edited payload new intent; `IDEMPOTENCY_KEY_REUSED` stops | BLOCKED (DN-01) |
```

**Proposed:**

```
| CP-14 | Idempotency: one key per opened form or command panel, minted on open and kept across edits, failures and retries; same key and body on network/503 retry; replay shown as completed; an edit resent under an unknown outcome uses the same key and a committed first attempt answers 409 `IDEMPOTENCY_KEY_REUSED`, which stops with inputs kept and no new key; only a newly opened form or panel is a new intent | BLOCKED (DN-01) |
```

**Reason (both rows).** As written, "edited payload new intent" accepts the behaviour R-2 measured producing two records
from one intent. A lane that passes SU-14/CP-14 as written can ship the duplicate.

The proposed text:

- keeps every clause that is still right: same key and body on retry; replay shown as completed;
  `IDEMPOTENCY_KEY_REUSED` stops;
- replaces only the intent definition, with the one the prose now uses;
- is checkable by the same lost-response sequence R-2 ran.

If the owner rejects it, the prose and the rows disagree. The pointer sentences added at MOD-0190:394 and MOD-0192:411
then need to say which one governs.

## The drafts are not cleaned by this amendment

**Amending the packs does not change a byte of the drafts R-4 will build from.** R-3 measured statically that the latest
drafts already contain the per-payload key code:

- **Claims (v4):** `index.js:541` (create), `index.js:679` (transition: `transitionIntent.bodyText !== bodyText`).
- **Capacity (v3):** `index.js:217` (create), `details.js:616` (`current.bodyText !== bodyText`).
- **S&OP (v3):** `index.js:198` (create), `details.js:510` (`intents[kind].bodyText !== bodyText`).

The drafts sit as tarballs under `docs/records/audits/2026-09/mvp6-{claims,capacity,sop}-ui-draft-0N/`.

**R-4 must change each of these scripts to the one-key-per-opened-form rule before shipping, and must not ship any draft
script as it stands.** R-2 shows the size of the change: its Returns fix minted the key in `openCreate`/`openTransition`
and removed the body comparison. It also shows that each draft's own behaviour test may pin the old rule and must be
re-pinned (`ReturnIndexBehaviorTests`, R-2 F-R2-4; MODULE-RECIPE 8.3). None of this lane's edits touch a draft. The drafts
are records and are not corrected after writing (K4).

## Findings

- **F-Q403-1 — the evidence these packs now cite is not committed.** `ca9330392` committed R-2's code but no record:
  `mvp6-r2-returns-ui-01/` is untracked, as are the R-1, R-3, Q394 and Q403 record folders (`git status`). Each amended
  paragraph cites `mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2. **That record must be committed with, or before,
  these pack edits**, or the citation points at nothing in the repository.
- **F-Q403-2 — MOD-0187's sentence spans `:664-665`.** The dispatch cited one line. Recorded only.
- **F-Q403-3 — the prose and the acceptance rows now disagree in MOD-0190 and MOD-0192** until the owner rules on the
  proposals above. Each prose edit says so, pointing at this record.
- **F-Q403-4 — MOD-0186 and MOD-0187 have no row equivalent to SU-14/CP-14** that names "edited payload". Their
  idempotency acceptance rows say only "idempotency (BLOCKED DN-01)", without the per-payload clause:
  - `RU-15, RU-16` at MOD-0186:768;
  - `CU-18, CU-19` at MOD-0187:717.

  Nothing was proposed for them.
- **F-Q403-5 — the same rule lives in an owner-approved scope file outside this lane's writes.**
  `docs/roadmap/plans/mvp6-ui-pack-drafts-01/returns/SCOPE.md:112` (tracked) reads "Editing the payload starts a new intent."
  - The file is bound by MOD-0186 §32 through its SHA256SUMS.
  - The Claims side of that package has no such line (`grep -i 'payload starts|editing the payload|edited payload|new intent'`
    over the package and `mvp6-decision-prep-01/`: one hit).
  - Not changed here. CT should carry it with the two row proposals, because a lane reading the scope package instead of
    the pack would still build the defect.

Nothing committed, nothing pushed, nothing staged.
