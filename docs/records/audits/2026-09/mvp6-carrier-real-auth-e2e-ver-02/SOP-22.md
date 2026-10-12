# MVP6-CARRIER-REAL-AUTH-E2E-VER-02 — SOP §22

Date: 2026-09-23  
Role: independent Carrier E2E verifier  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **BLOCKED — START PREREQUISITE FAILED; E2E NOT RUN**

## 1. Start-gate disposition

The required Auth successor independent runtime PASS handoff does not exist.

The successor writer completed after the earlier independent inspection. Its DEV
package is intact, but its own handoff keeps independent VER and CT acceptance open.
The newest independent successor record remains:

- `mvp6-carrier-auth-le-successor-ver-01/SOP-22.md`
- SHA-256 `674269a15842488f634acb65751b33ad3d2b550429ff2f74cab931c4bd2c8a09`
- verdict `REWORK — PHASE 2 NOT RUN`
- Carrier bounded E2E handoff `NOT ISSUED`

The later writer-complete record is:

- `mvp6-carrier-auth-le-successor-dev-01/SOP-22-DEV-HANDOFF.md`
- SHA-256 `ec31466889f9872de0f57357057dcf291fa90228aa59742f37c136243395e325`
- 22-path source manifest
  `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`
- source archive
  `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846`
- evidence archive
  `cb7d1c3ef8befe8e91265e151b596d085d6d57671ed61407ee2a724ed6ee33a2`

The DEV artifact manifest verifies 8/8, but writer evidence is not independent
runtime acceptance. No later independent runtime verifier package was found.

The writer applied the same exact 22-path source. Consequently the independent
findings on exact duplicate claim/audience cardinality and the
`iat`/`nbf`/current-time/skew relationship are not closed by a different source
hash or by an independent rerun.

Per the user's fail-closed start condition, no Web, Gateway, Auth, Platform, MDM,
SupplyChain or Mongo process was started. No JWT, login, Carrier request or browser
mutation was attempted.

## 2. Final UI identity

The controlling final UI manifest is
`mvp6-carrier-ver-environment-close-01/FINAL-UI-SOURCE.tsv`, SHA-256
`3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`.
It contains 21 paths and is byte-identical to
`carrier-writer/UI-SOURCE-MANIFEST-v3.tsv`.

The registered Carrier UI worktree at
`/Users/natig/.codex/worktrees/mvp6-carrier-ui-dev/ERP-vNext-recovery` was rehashed:
**21/21 PASS**. The exact Carrier UI patch remains
`57f90dadedd6e2304e3775fd1ea0bd7def29ad9d265ccbbeb892afdc04ba1adb`.

The historical `e96578d0…` identity is a preimage only and was not used as the
final source.

## 3. Preserved closed UI evidence

Because all 21 final UI paths are unchanged, the following hash-bound results are
preserved without unnecessary rerun:

| Evidence | SHA-256 | Preserved result |
|---|---|---|
| `BROWSER-ACCEPTANCE.tsv` | `700e37a3936f30dd0c3aad3a156c9f757503e49a95647b149be750d17f9cca94` | Original bounded rows only |
| `RESPONSIVE.tsv` | `c93a70b60bfe02a7f1e912d3febf971f719ba25c79089ec7fe9505e646ed709c` | 768/390 list/create measurements PASS |
| `L10N-RESOURCE-MATRIX.tsv` | `fbc83bfa5c9f4396c18abca42a4b997c2fea1fe1a039467e55b181618cb61f27` | Seven-language 70/70 PASS |

The responsive evidence records exact `window.innerWidth` 768 and 390, DPR 1 and
no document overflow. The final-v3 browser console had zero Carrier localization
warnings/errors. These results do not prove authenticated Carrier operations.

## 4. Single successor acceptance disposition

`SUCCESSOR-ACCEPTANCE.tsv` is controlling. Real Auth-issued
MVC→Gateway→Carrier list/create/replay/status, status offcanvas from a persisted
row, permission/tenant/LE isolation, persistence and fresh UAS remain **NOT RUN**.
They are not marked FAIL because the prerequisite failed before execution.

Auth DEV PASS is not Carrier PASS. A future Auth independent bounded PASS would
open the Carrier run; it would not itself close any Carrier acceptance row or grant
full-module, E5, G5 or rollout acceptance.

## 5. Durable PNG boundary

Durable PNG remains **OPEN**. The established browser integration exposes no
explicit permitted screenshot artifact-save/export operation. The previous data-URL
rejection was not bypassed with CDP, encoding, native capture or another export
mechanism. This boundary is independent of the Auth prerequisite.

## 6. Exact resume condition

Resume this E2E matrix only after a different verifier publishes an immutable,
hash-bound **PASS** for the exact Auth successor source after writer completion,
including the controlling negative token semantics. At that point:

1. recheck the 21 final UI hashes;
2. bind fresh source→binary→process identities;
3. use a real Auth-issued token;
4. execute only the still-open rows in `SUCCESSOR-ACCEPTANCE.tsv`;
5. keep durable PNG OPEN unless the browser tool exposes an explicitly permitted
   artifact-save/export capability.

## 7. Preservation

This lane added only this audit directory. It changed no product source, Auth
policy, UI, Gateway, Carrier, contract, pack, runtime configuration or Git state.
No commit, push or stash occurred.

