# MVP6 decision preparation 02 (Q70, WP MVP6-WP-DECISION-PREP-02)

🤖 Applying knowledge of @product-owner + @module-pack-author + @read-only-auditor.

Lane AL-MVP6-DECPREP-02 (INS), a chat lane on the linked Mac folder. Repo `feature/mvp6-logistics` @
`4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Start 2026-09-26T14:18:32+03:00. Text only: no git writes, no ledger edits,
nothing applied or approved.

**Everything here is prepared text — NOT APPROVED.** CT puts the decisions to the owner one at a time, in the order below.

## Presentation order (dependencies first)

| Order | ID | File | Owner question (short) | Recommended | Unblocks |
|---|---|---|---|---|---|
| 1 | Q27 | `Q27.md` | Promote the shared MOD-0190 S&OP pack to the owner-promoted bytes + §22? | **A — approve as written** | S&OP UI-scope preparation (chat), then UI build (Mac) |
| 2 | Q28 | `Q28.md` | Promote the shared MOD-0192 Capacity pack the same way? | **A — approve as written** | Capacity UI-scope preparation |
| 3 | DN-02 | `DN-02.md` | Approve the bounded same-origin DataTable profile for the Shipment UI, naming the 4 MD-03 checks? | **A — refreshed text** | Frame for PC-02/03/04/28; UI183-A14 |
| 4 | PC-02 | `PC-02.md` | Accept the resolved `_Filter` partial instead of the literal token (profile only)? | **A — semantic resolution** | A02/A14 profile check |
| 5 | PC-03 | `PC-03.md` | Verify create (13 inputs) and detail sections separately instead of forcing one section map? | **A — Shipment-only exception** | A05/A06/A14; settles F-183-2 |
| 6 | PC-04 | `PC-04.md` | Accept the resolved `_IndexL10n` partial checked by rendered 7-language output? | **A — semantic resolution** | A13/A14 |
| 7 | PC-28 | `PC-28.md` | Verify Shipment under the existing `proxy-profile` (same-origin)? | **A — select proxy-profile** | A01/A14 |
| 8 | DN-01 | `DN-01.md` | Authorise the hash-bound loopback fault proxy for the A10 VER? | **A — refreshed text** | UI183-A10 (Mac run after Q24) |

**Why this order:**
- Q27 and Q28 depend on nothing else and are hash-gated quick yeses, so they go first. They unblock the two largest
  remaining module scopes (S&OP 136 h M, Capacity 144 h M), which can then be prepared in parallel chat lanes.
- DN-02 sets up the profile that PC-02, PC-03, PC-04 and PC-28 rule inside, so it comes before them. The four PCs follow
  directly, because A14 needs all of them.
- DN-01 is independent of everything else. Its run also waits for the kit v1.2 validation (Q24b), so it can come last
  without delaying anything.

## Summary of today's checks

- **All eight decisions are still needed.** CT-QUEUE Q10, Q11, Q12, Q27 and Q28 are all `DECISION-REQUIRED`. The CT
  A12 disposition (11:14) leaves A10 OPEN (unauthorised) and A13/A14 unchanged.
- **The generic DataTable gate was reproduced on the exact accepted A12 composition.** HEAD + successor `7b6a0d1a…` +
  final-source `f50350b8…`, the same order as A12 VER-02, gives 49 PASS / 35 FAIL, identical to history. The
  rule/verifier line references in the proposal still hold.
- **New finding for PC-28.** The quality-gate workflow already defines `proxy-profile` (same-origin
  `/{Area}/{Module}/api`) and requires the profile to be chosen explicitly. With `--api-profile proxy`, the same source
  gives 51 PASS / 34 FAIL: FAIL 28 disappears and both proxy checks pass. The proposal's recommended ruling needs no
  verifier change.
- **MD-03 is folded into DN-02.** The refreshed text names SCR-07 `Unknown`, SCR-14 `AreYouSure`, SCR-22 `ShowAll`
  and SCR-34 `reloadWithToast`, so no check is waived silently.
- **A13 is no longer blocked by PNG.** The method is decided (Q13 READY). A13 now needs a fresh seven-culture VER plus
  PC-04.
- **DN-01 is refreshed for kit v1.2**, approved at 13:22. The proxy runs inside a v1.2 lane and holds no credential.
- **The Q27/Q28 deltas are still exact.**
  - The shared packs equal the preimages `637690f3…` and `edd550b8…`.
  - `git apply --check` passes alone and together in VM `/tmp` copies.
  - The results equal the targets `8403d8f4…` and `de81a0e2…`.
  - The package SHA256SUMS verifies 9/9.
  - Finding: the new §22 heading still carries "proposal — NOT APPROVED" (and MOD-0192 §21 "proposed draft delta").
    This belongs with the Q50 heading fix, not with a new option.
- **S&OP and Capacity after promotion:**
  - the backend is `ready-for-dev`;
  - the UI needs a UI scope first (UI-SCOPE-190/192, not prepared), then a pack UI delta, then an approved Phase 1.5
    nine-row table, then the SR-D4 self-registration overlay with the UI.

## Not included (CT decisions, not owner decisions, or out of scope)

- MD-01 (A07 disposition), MD-02 (EVIDENCE-REUSE rows) and MD-04 (parent A09) are Control Tower decisions.
- Q50 is the heading-label correction.
- PH15-UI-183 is already prepared in `mvp6-decision-prep-01`.

## ASSUMPTIONS

1. **Order.** Q27 and Q28 go before the Shipment decisions. Readiness `BLOCKING-DECISIONS.tsv` also lists them first
   (orders 4–5 before 6–8), and nothing depends on them. The PCs follow DN-02 directly, instead of the readiness order
   Q11→Q10→Q12, so that the A14 set is decided together.
2. **Language.** Exact decision texts keep the language of the original prepared text: Turkish for DN-01, DN-02 and the
   PCs, English for Q27 and Q28. The Q27/Q28 texts are unchanged because their hashes are still exact.
3. **Refreshes.** The DN-01, DN-02 and PC texts are refreshed only where today's state requires it:
   - the MD-03 names;
   - the kit v1.2 context;
   - the verifier hash;
   - PC-28 as a profile selection.

   None of them adds a new behaviour or option. The PCs are prepared one per file, matching the owner's
   one-at-a-time preference. The proposal's combined PC form stays valid as an alternative vehicle.
4. **Effort.** No per-decision O/M/P exists in the ledger. The files cite the covering ledger rows (effort-update-08) and
   mark source-changing alternatives as UNESTIMATED.
5. **Verifier reruns.** They used the repository's own `verify_datatable_page.py` (read-only, `python3 -B`) on
   compositions in the VM's `/tmp`. They are static checks only; no browser, service or Mongo ran.
6. **Pack apply-check.** It ran outside any git repository (`GIT_CEILING_DIRECTORIES=/tmp`), on copies in `/tmp`.
7. **"Where it runs"** follows the working mode: decisions are recorded and texts or scripts prepared in chat lanes;
   runtime and browser work runs on the local Mac; commits happen only in Mac Terminal sessions.

## Files

- The eight decision files: `Q27.md`, `Q28.md`, `DN-02.md`, `PC-02.md`, `PC-03.md`, `PC-04.md`, `PC-28.md`, `DN-01.md`.
- `evidence/`: the two verifier outputs and the command notes.
- This `README.md`, and `SHA256SUMS` (relative paths; verify from this folder).

Uncommitted — to be committed by a Mac Terminal session (chat lanes cannot commit).
