# MVP6 UI pack revisions — MOD-0187 Claims (ready) and MOD-0186 Returns (blocked) (CT queue Q35)

2026-09-26 · Module-pack author lane, documents only. **PREPARED PROPOSALS — NOT APPROVED, NOT APPLIED.**
Repo `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Authority: `docs/records/decisions/2026-09/mvp6-returns-claims-ui-scope-owner-decision-01.md` (`30931917…aee9`), which approves preparing
patches only. Its bound hashes were rechecked: drafts SHA256SUMS `b568c94f…` (13/13 OK) and both APPROVAL-DECISION files match.

## Result

| Module | Outcome | Chain |
|---|---|---|
| MOD-0187 Claims | **UI revision patch prepared and proven** | shared `a342054c…` → alignment-02 `00dee2b1…` → `f0e4d3bd…` → UI revision `5644479a…` → **`762ab533…`** (`ready-for-dev`) |
| MOD-0186 Returns | **BLOCKED — no patch** (owner chose "Claims now, Returns waits") | The named alignment `mvp6-final-pack-delta-01/proposed-pack.patch` is a non-applicable pseudo-patch with no target hash; no shared alignment reproduces the `ready-for-dev` pack `1c80cca7…` |

## Files

| Path | Purpose |
|---|---|
| `claims/ui-revision.patch` | Unified patch, preimage `f0e4d3bd…` → target `762ab533…`; header lines start with `#` |
| `claims/SOP-22-PACK-REVISION.md` | Gates, hash chain, proofs (git apply / patch --fuzz=0 / combined order / negative), content summary, 10 honest gaps |
| `claims/SIGN-OFF-DECISION.md` | Exact owner text binding both patches in order — **NOT APPROVED** — with options |
| `returns/SOP-22-PACK-REVISION.md` | Why Returns is blocked, what was verified, what unblocks it |
| `returns/SIGN-OFF-DECISION.md` | Nothing to sign; proposed dispatch of a Returns pack-alignment-02 |
| `SHA256SUMS` | Checksums of every file above (paths relative to this directory) |

## Key gaps (details in the SOP files)

The Claims approved-amount list gap (contract, not authorized) · G-MODAL (shared `showConfirm` may not host transition inputs) ·
G-ICONMAP (shared icon map owned by the integration owner) · G-DATETIME (no known offset date-time component) · the conflict between the verifier
PASS expectation and the approved OUT rows · the alignment-02 patch is itself not yet approved · no integrated target ·
the Returns alignment is missing · a timestamp inconsistency in the decision record (noted, not edited).

## Not changed

Packs, contracts, product code, `.antigravity`, gateway, existing records and the drafts. No commit, push or stash. All patch tests ran on scratch copies outside the repo.
