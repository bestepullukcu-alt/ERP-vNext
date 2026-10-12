# HELD — owner approval for the exact B-target delta is missing

## SOP §17 metadata

- WP: `MVP6-BC-INTEGRATION-SUCCESSOR-APPLY-01`
- Prompt version: `1.0`
- Lane: `INT`, single integration writer
- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch: `feature/mvp6-logistics`
- Expected HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Required B preimage manifest: `cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c`
- Exact C patch: `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f`
- Expected successor manifest: `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`
- Owned paths: the three paths in `BC-DELTA.tsv`
- Protected paths: every other path, especially `Program.cs`, canonical contracts, guard, gateway and historical evidence
- Gate: **HELD** until the exact text in `OWNER-DECISION-TEXT.md` is approved

## NE

Apply the exact three-file C uptake to the exact B 422-row integration source
set and produce bounded build/runtime evidence for the successor.

## NEDEN

B and C each have independent technical PASS evidence, but no existing owner
decision authorizes applying C to B. Disposable composition proves that the
change is three replacements and yields one deterministic successor manifest.

## NASIL

1. Verify the registered integration checkout contains all 422 B hashes.
2. Verify the three preimages in `BC-DELTA.tsv`; stop on any mismatch.
3. Apply only patch `f4fc82b4…`; do not overlay the Capacity directory.
4. Verify all 422 successor hashes and the three preservation hashes.
5. Perform a fresh build from the successor source.
6. Run the minimum affected controls:
   - exact duplicate-name HTTP status/code/message;
   - replay and error precedence;
   - deterministic duplicate and unique-index race convergence;
   - X01/X07 and hosted/read-fault tests retained from B;
   - affected Capacity composition, persistence and restart regression.
7. Record overlapping test sets instead of adding `36` and `34`.
8. Produce a source-to-build-to-runtime handoff for an independent verifier.

## YAPMA

Do not change `Program.cs`, contract, guard, gateway, permissions or business
rules. Do not overlay a full folder, resolve conflicts by guesswork, run an
unrelated full suite, overwrite the common checkout, or commit/push/stash.

## DOĞRULA

- 422/422 successor hashes match `dec28b6…`.
- The three wire/test targets match `BC-DELTA.tsv`.
- `Program.cs`, hosted/read-fault test and S&OP test match `PRESERVATION.tsv`.
- Runtime proves the exact duplicate-name message and does not regress replay,
  precedence, race, X01/X07, hosted evidence or restart behavior.
- Independent VER reproduces the affected controls.
- Agent PASS remains separate from CT acceptance and E5/G5.

