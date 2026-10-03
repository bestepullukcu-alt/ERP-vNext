# MVP6 evidence kit — proposal 03 (v1.2, queue Q66)

**Status: PROPOSAL, nothing active. NOT APPROVED.** Prepared by AL-MVP6-KIT-V12-01 (chat lane) for Control Tower on
2026-09-26. Repository `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, HEAD
`4a8d4d4b339528a88e6220fb8402e5a2c771136c`.

Basis:
- the Q63 independent review of v1.1 (`docs/records/audits/2026-09/mvp6-evidence-kit-v1-1-independent-review-01/SOP-22-VER.md`,
  findings F1–F14);
- the owner decision V2 (revise), recorded in `docs/records/audits/2026-09/mvp6-ct-verdict-q63-kit-v2-2026-09-26.md`.

**Commit pending — to be committed by the next Mac Terminal session (chat lanes cannot commit).** Ledger lines are
left to the ledger writer (Q64); this lane edited no ledger.

## What changed and why

The review found one HIGH defect. v1.1 paired Platform `ModuleRegistrationCredentials:Mdm:ActiveSecret` with MDM
`PlatformRegistration:InternalApiKey`, and K04 reported PASS because it compared its own group labels. In the product
the pairs are different:
- MDM registers with `ModuleRegistrationCredentialSecret`, checked against `Mdm:ActiveSecret`.
- MDM's `InternalApiKey` is checked against Platform `AuthService:InternalApiKey`.

So every v1.1 lane would have rejected MDM registration and returned 401 to MDM's audit calls.

v1.2 changes three things:
- It moves the key pairs into `kit/key-pairing.tsv`. Each of the 30 keys is cited from product source as
  `path:line=token`: 57 citations, all resolving at the cited line in the kit's reference source.
- It changes K04 to compare the *effective* value each service will actually use.
- It adds a unit test in which the v1.1 table must FAIL.

It also fixes the four MEDIUM findings:
- **F2:** the supervisor runs only an allow-list of tasks, gives each task only the values it needs, and `reveal`
  only writes to the caller's own terminal.
- **F3:** the final scan opens zip (including Playwright traces), gzip and tar files, and treats anything it cannot open
  as a failure.
- **F4:** a failed `up` stops what it started, clears the values and removes the socket; a second `up` with a live
  supervisor is refused.
- **F5:** git is read-only everywhere.

Of the lower findings, F6–F10, F12, F13 and F14 are fixed. F11 and the single-threaded part of F10 are accepted with a
reason.

`CHANGES.tsv` has one row per finding, with the product and kit file:line evidence and the test that shows it. The
phase-level view is in `KIT-SPEC-DELTA.md`.

**Checks** (`STATIC-CHECKS.txt`, all OK):
- bash -n, Python syntax in memory, node --check, and shellcheck (0 at warning level).
- Scanner self-tests: 3/3 plain and 1/1 in a zip. v1.1 misses the zip case.
- Pairing unit tests: 4/4.
- K04 on the real composed a08 source: v1.2 PASS; the v1.1 and v1.0 maps FAIL.
- Supervisor tests (17), driver tests (7), K01 tests (4) and K10 tests.

Nothing ran against .NET, Mongo or a browser; that is Q24.

## Files

- `proposed/` is the full v1.2 set of 28 files. `PLACEMENT.tsv` keeps the v1.1 install paths and adds two:
  `scripts/evidence-kit/key-pairing.tsv` and `scripts/evidence-kit/tests/test_k04_pairing.py`.
- `CHANGES.tsv`, `KIT-SPEC-DELTA.md`, `ADOPTION-DECISION-v1.2.md` (NOT APPROVED text), `STATIC-CHECKS.txt`.
- `SHA256SUMS`: verify from this folder with `shasum -a 256 -c SHA256SUMS`.

## ASSUMPTIONS (no-question policy)

1. **Reference source for citations.** Line numbers are those of the composed source the kit is templated on: HEAD plus
   the two sealed a08 overlays in `templates/overlays.tsv` (the `layer` column says which one). Most cited files come
   from the overlays, and SupplyChain's `PlatformRegistration` exists only in the shared-UI overlay. With other overlays,
   a moved line is recorded as "moved", and a missing token is NOT FOUND and fails.
2. **Pairs beyond the prompt.** The registration identifier and the service-identity `KeyId`, `Issuer`, `Audience`,
   `CallerId` and `Enabled` are compared as non-secret pairs, because the same product code compares them
   (ModuleRegistrationCredentialAuthenticator.cs:39; PlatformServiceTokenValidator.cs:49–63).
3. **Reveal.** "Enabled per call by the terminal user" is implemented as a one-time code that the supervisor writes to
   the caller's tty, which the user types back. The password is then written to that tty only. This is enforced in the
   supervisor through kernel peer credentials and the caller's tty device.
4. **Task scopes.** Harness scripts receive actor passwords only; `scan`/`seal` receive every value; `k07` receives actor
   passwords only. A lane that needs a service secret in a harness needs a kit change.
5. **Supervisor lifetime.** The defaults are 12 h in total and 4 h idle, both set by flags.
6. **F13.** The wrapper decision uses the overlay's own sealed manifest together with the pristine HEAD top level. This
   goes further than the review's "pristine HEAD listing" alone, because that would still strip a genuinely new
   top-level folder.
7. **Accepted items.** F11 (row order) and the single-threaded part of F10 are accepted with the reasons given in
   CHANGES.tsv.
8. **Guide file name.** It stays `mvp6-evidence-kit-v1.0.md` (content v1.2), so the A1 §5 line stays valid.
9. **Q24 conditions.** Two are added: no NOT FOUND citation, and both MDM calls succeed live (registration accepted,
   and one audit append returns 2xx).
10. **Scratch and test inputs.** Scratch was used only outside the repository: the VM's `/tmp`, and the cloud
    workspace's `/tmp` for tests. The real-input K04 test read the two sealed overlay archives and four HEAD-identical
    files (read-only). It had no user-secrets, since the Mac's user-secrets are not visible here; Q24 covers them.
11. **Python checks.** They use `ast` + `compile` in memory, and the unit test sets `dont_write_bytecode`, so no
    `__pycache__` is written.
