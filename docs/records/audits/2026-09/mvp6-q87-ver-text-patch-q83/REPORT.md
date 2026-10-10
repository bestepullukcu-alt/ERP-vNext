# Q87 — Independent VER of the Q86 apply (Q83 text patches P1–P5)

Independent verifier (not the Q86 writer). READ-ONLY run on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`.
Start 2026-09-26 19:47:04 +03 · end 2026-09-26 19:50 +03 (Istanbul). Scratch only in VM `/tmp` (`/tmp/q87v3`, isolated scratch repo with `GIT_CEILING_DIRECTORIES=/tmp`). The only write is this file.

**Gate:** `MILESTONE-EVENTS.tsv` line 125 (2026-09-26T19:16+03:00) holds the Q86 writer hand-off ("… Q87 VER may start now"). The run started.

## Result: **PASS** (9/9)

| # | Check | Result | Evidence |
|---|---|---|---|
| V1 | Decision record exists, names A for P1–P5, and binds full hashes equal to SIGN-OFF.md | PASS | `docs/records/decisions/2026-09/mvp6-text-patch-q83-signoff-owner-decision-01.md` (`d27458e19cda13c7453bd3c5378c22286078b3551f081ae5d91f0db1aff381da`): Decisions 1–5 are "A — approve". All 5 preimages, 5 patch hashes and 5 single-patch postimages, plus the combined MOD-0190 `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913`, are equal to SIGN-OFF.md. The record binds SIGN-OFF.md `37bf248d…6797` and SHA256SUMS `9b156f75…b817`, and both match the files. |
| V2 | Current target sha256 = the postimages | PASS | MOD-0190 `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913` (P1+P4); MOD-0192 `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` (P2); DCP-009 `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf` (P3); MOD-0187 `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` (P5). Rechecked at the end: unchanged. |
| V3 | Reverse-apply on /tmp copies returns the preimages | PASS | `git apply -R --check` + `-R`: MOD-0190 P4 → `44a377c2…9ac3` (the P1-alone postimage), then P1 → `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f`; MOD-0192 P2 → `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f`; DCP-009 P3 → `6b12ce685114248e89b78729546f43e86836d52f337599cb399de2993920046b`; MOD-0187 P5 → `8ed42fad66b7739b8c56778a57e0ead84cc8ce3678ea4868306263ac501a94a2`. The forward `--check` of P1 and P4 on the restored copy also exits 0. |
| V4 | Patch folder SHA256SUMS 9/9 | PASS | `docs/roadmap/plans/mvp6-text-patch-q83-01/SHA256SUMS`: `sha256sum -c` gives 9 OK, rc 0. |
| V5 | Packs 0183–0186 unchanged | PASS | `2a65ce1d516c1850bef74f9d26e5d54dfc6042acae45ad5199730ef43ddf2c83` / `35bead97350624074d5d67e4119eaafba59a9eb68dd15f8e1f5729564291dc21` / `9ec4ef1bc1db21e07e01d5e8c0671ea4809840e6245d8749fb4da0a8b75abd2e` / `fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27`. |
| V6 | Path set unchanged, no lock, HEAD unchanged, no stray files | PASS (see A1) | `git diff --name-only HEAD` = 19 paths, the same count Q83-CT and the Q86 hand-off recorded before and after the run. The HEAD blobs of all 4 targets (`f8d864e8…`, `637690f3…`, `edd550b8…`, `ce30e922…`) differ from their preimages, so the targets were already in the set before Q86. No `.git/index.lock`; HEAD `4a8d4d4b…` at start and end. No `*.orig`, `*.rej`, `*~`, `*.bak` or `.#*` in `module-packs/` or `delivery-capability-packs/`, and porcelain there lists only the 8 known ` M` files. |
| V7 | `verify_module_id.py` exit 0 (name from frontmatter) | PASS | MOD-0187 "Claims Management", MOD-0190 "S&OP Workflow & Sign-offs", MOD-0192 "Capacity Planning": each "proven against Blueprint/registry", exit 0. |
| V8 | Content checks | PASS | (a) The string "proposal until" occurs 0 times in 0190 and 0192. §22 now reads "The owner decision prepared in `…/PROMOTION-DECISION.md` is recorded…". (b) DCP-009 §21.1 ModuleCodes include `sop-workflow-signoffs` (MOD-0190) and `capacity-planning` (MOD-0192); the Excluded row reads "None." and cites `mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md` `37f3ff0d…0bea`, which matches the file. (c) MOD-0190: §7 "`SANDOP-CAPACITY` 3.0.0 / wire `v1`"; §16 "published SANDOP-CAPACITY 3.0.0 YAML/annex"; §18 "re-pinned to 3.0.0 after the six operations were proven identical". The historical binding stays: §21 YAML `9543e3f2…2ff3` / annex `eb1df138…1bda`, and §22 "Contract at acceptance: SANDOP-CAPACITY 2.0.0". Canonical YAML is `5213b535…adab` (3.0.0). (d) MOD-0187 §32.14 effort block (lines 730–735, O/M/P 4/8/16, 30/50/84, 8/16/28, 14/24/40, total 56/98/168, 68/118/200) hashes the same before and after (`716177ccad8f7e06…`). The diff against the preimage covers only line 552 and lines 736–744. The new wording cites PH15-UI-187 `95a5c4f4…f74e`, modules-first `63e8601e…df32` and draft overlays `05033624…a67c`, and all 3 hashes match the files. |
| V9 | Ledger states (read only) | PASS | CT-QUEUE: Q85 "DONE (owner A×5 ~19:15)"; Q86 "DONE (writer hand-off 2026-09-26T19:16; 4 files at postimage; VER pending Q87)"; Q87 "READY". Not edited. |

## Findings

- **O-Q87-1 (info, no action in this scope):** MOD-0190 §22 still carries the note "(open, not decided here) … evidence remains bound to 2.0.0 until a CT/owner disposition re-pins it". This is consistent with P4, which re-pins the module and leaves the §21/§22 acceptance evidence at 2.0.0 by design. CT may choose to fold the wording into the Q89/Q90 text revision.
- **O-Q87-2 (info):** SIGN-OFF.md still says "STATUS: NOT DECIDED". The file is hash-bound by SHA256SUMS (V4), so it cannot be updated. The decision record supersedes it.
- The known F-Q83-2 (stale §24 DCP note in 0190/0192) is tracked as Q89 and is not re-raised here.

## ASSUMPTIONs

- **A1:** Q86 did not write an explicit list of paths before its run; it recorded only "19 tracked paths" (Q83 CT verdict, Q86 hand-off). V6 compares that count and shows the 4 targets were already in the set, since each preimage ≠ HEAD. An earlier explicit porcelain from 08:22 (`mvp6-loads-publication-guard-01/raw/porcelain-status-post-step2.txt`, 17 paths) predates the Q81 apply to MOD-0190/0192, so it is not the Q86 baseline.
- **A2:** Q90 was running in parallel during this VER and writes only drafts under `mvp6-text-patch-q90-01/`. Target hashes were rechecked at the end and had not changed.
- **A3:** "Effort numbers byte-identical" is taken as the §32.14 effort paragraph (lines 730–735) being byte-equal to the preimage.

## To do

- CT: give the Q86/Q87 disposition.
- Ledger writer (not Q87): set Q87 → DONE with this report's hash.
- Q89/Q90: optionally cover O-Q87-1 wording.

Agent PASS ≠ CT ACCEPTED — returning to CT.
