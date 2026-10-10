# Owner decision — the merge secret gate covers source configuration only

- date: 2026-10-05
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q433, Q444, Q460
- amends: `docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md` §"Secret gate"

## Decision

The merge gate's wording — *"the merge must not complete with known secret material in the
resulting tree"* — is **scoped to source configuration**: tracked `appsettings*.json` under
`services/**`, `frontend/**` and `gateway/**`.

**Committed `.tmp-*` build output is explicitly out of scope** and remains Q341's separate
decision.

## Why the gate could not be satisfied as written

CT measured ten committed `.tmp-*` `appsettings` files carrying `ff4555d1` — **in both
parents**. Neither our branch nor `origin/main` removes them, so no merge resolution can
produce a tree without them. A gate that cannot be satisfied by any correct action is not a
gate; it is a blocker with no exit.

The alternative was to bring them into scope, which makes Q341's 797-file untracking part of
the merge. That materially widens an integration event already carrying 845 commits, and the
two decisions have nothing to do with each other.

## What CT got wrong, recorded because it changed the scope

Q433 described `ff4555d1` as *"a digest this programme has never seen"*, found on
`origin/main` in twelve Development configs. False: ten copies of the same digest have been in
**our own tree** all along, inside the `.tmp-*` build output. It is not an upstream digest. The
correction is what exposed the gate's wording problem.

## The gate, as it now stands

| class | decision |
|---|---|
| **A** — the two base configs we sanitised in `8f60dc6d3` (`d8b34f90`) | our sanitised version survives the merge |
| **B** — twelve upstream Development configs (`ff4555d1`) | sanitised in the merge result, with upstream provenance recorded |
| **C** — anything credential-like that cannot be classified | quarantine, BLOCKED pending security classification |
| **out of scope** | committed `.tmp-*` build output — Q341 |

## What this does not change

**Sanitation is still not rotation.** Seven retired digests remain valid signing keys until
Q293 rotates them, and `ff4555d1` joins that list. Scoping the gate narrows what the *merge*
must achieve; it does not narrow what is still owed.
