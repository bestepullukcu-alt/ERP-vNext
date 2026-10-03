# SOP §22 — Independent verification of pack-text applies (MVP6-PACK-APPLY-INDEPENDENT-VER-01)

**Verdict: PASS on all six checks. One observation for CT (O3) falls outside the apply steps.**
Verifier: MVP6 Lane-3. I wrote none of the applies being checked. Repo `/Users/natig/Projects/ERP-vNext-recovery`,
`feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (checked at start and end). All Git commands ran read-only with
`GIT_OPTIONAL_LOCKS=0`. Rehearsals ran in temporary Git repos outside the repository. The only files written are the ones in this folder.
Start 2026-09-26T09:29:52+03:00 · end: see the final report (Europe/Istanbul).

Every command and its raw result is in [COMMANDS.tsv](COMMANDS.tsv), column `check` = the number below.

## Authority checked

| Record | sha256 (own run) |
|---|---|
| `mvp6-returns-pack-signoff-owner-decision-01.md` (MOD-0186: alignment → UI revision) | `649269c3…4611` |
| `mvp6-self-registration-patches-signoff-owner-decision-01.md` (patches 01, 02, 03, 04, 06; 05 not signed) | `7707329e…3e58` |
| `mvp6-claims-pack-signoff-owner-decision-01.md` (MOD-0187: alignment → UI revision) | `365de5be…827b` |

All three equal the hashes cited in the writer evidence.

## Results

| # | Check | Verdict | Evidence (own commands) |
|---|---|---|---|
| 1 | Every evidence checksum file verifies | **PASS** | q46-q47 `ARTIFACTS.sha256` 13/13 OK. Patch-05 rebase `SHA256SUMS` 3/3. Returns package 5/5 (file `64620aa1…` = record). Self-registration package 11/11 (file `2acd3241…` = record). UI-revision package 6/6 (file `6b7d770b…` = record). Claims alignment package 5/5 (repo-root paths). All 11 signed or referenced patch hashes and the three source `SIGN-OFF-DECISION.md` hashes equal the records. The claims `ARTIFACTS.sha256` is 7/8: its MOD-0187 line records `762ab533…`, the Q38 result at 01:34. The later signed patch 06 took the pack to `31cb35c3…` at 09:25:55, and check 3 reverses patch 06 back to exactly `762ab533…`. That line is superseded by a later approved apply, not broken (O1). |
| 2 | Current target sha256 = approved after-hash | **PASS** | MOD-0186 `6c8fbe28…a5a0`; MOD-0187 `31cb35c3…6626`; DCP-009 `e346043d…cec6`; MOD-0183 `8e269efa…eadf`; MOD-0184 `346288ab…fd1e`; MOD-0185 `45b5dd33…caa5`. |
| 3 | Reproduce from the approved before-text | **PASS** | For each target in a temp repo: (a) reverse-apply the signed chain from the live file. That lands exactly on the approved before-hash, with every intermediate hash as recorded (MOD-0186 → `f4396e8a` → `07a8a015`; MOD-0187 → `762ab533` → `f0e4d3bd` → `a342054c`). (b) Forward-apply each signed patch in order with `git apply --check` + `git apply`. Every step gives the approved hash and the final file is byte-identical to the live file (`cmp`). The base came from `git show 4a8d4d4:<path>` where that blob equals the before-hash (DCP-009, MOD-0183). For the other four it was the reverse-reconstructed text, because at HEAD those packs already differed from their approved before-hash (O2). |
| 4 | Only approved sections added; no other line changed; patch 05 NOT applied | **PASS** | Since before-text + signed patches = live file byte-for-byte (check 3), no line outside the signed patches changed. The self-registration patches 01/02/03/04/06 are pure additions with one level-2 heading each (DCP-009 §21, MOD-0183 §22, MOD-0184 §31, MOD-0185 §29, MOD-0187 §33). The alignment/UI patches replace only the front-matter/status lines they carry: MOD-0186 and MOD-0187 `status: draft` → `ready-for-dev`, `shell`/`golden_reference`/`form_field_count`, and one MOD-0187 allowlist paragraph. `git status -- execution/` shows exactly the six targets, with no untracked file. Patch 05: MOD-0186 = `6c8fbe28…`, not `a762305e…`; no `## 30./33. Self-registration` heading; `git apply -R --check` of the rebased 05 fails (exit 1), so it is not present. |
| 5 | Rebased patch 05 | **PASS** | `git apply --check --include=<MOD-0186>` of the rebased patch on the live file: exit 0, live hash unchanged. Applied in a temp copy it gives `a762305ec789f0b2547d5205135a0ff70b64e83f4a63b839a985432a46d8a552` (matches the writer's expected value). The original 05 fails `--check` on the live file (exit 1, base superseded). Comparing added lines, original vs rebased, exactly one line differs: `## 30.` → `## 33.`. Both patches remove 0 lines. The whole-patch diff is below: only the index line, the hunk header/position and context lines (now anchored after §32) and the heading number differ. |
| 6 | No forbidden change by this step | **PASS** | `git diff --stat`: 16 files, 6322+/223−. Six are the approved pack/DCP targets. The code files (Program.cs, 4 Shipment `.cs`, DocsPathGuardTests.cs), `.antigravity/rules/docs-organization.md` and both contract YAMLs were last modified 2026-09-18…23, before both apply steps (01:33 and 09:25 today). The untracked contract-semantics files are also older. `gateway/` and `execution/registries/`: 0 status entries. No file under gateway, `.antigravity`, `execution/registries`, `docs/analysis/contracts`, `services` or `frontend` is newer than 2026-09-26 01:33:29 +03:00 (`find -newermt`: 0). No `.git/index.lock`. |

## Rebased patch 05 — diff of original vs rebased (`diff original rebased`)

```
2c2
< index 2079277..2dc5077 100644
---
> index c6b82ca..fae58be 100644
5,8c5,8
< @@ -556,3 +556,80 @@ pack promotion and dispatch NOT AUTHORIZED. Manual/opaque proposal approval woul
<  Versioned [DEV](../../../../docs/roadmap/plans/mod-0186-prep-02/dev-prompt-v1.0-HELD.md) and
<  [VER](../../../../docs/roadmap/plans/mod-0186-prep-02/ver-prompt-v1.0-HELD.md) remain **HELD**;
<  CT must issue new released versions after exact missing gates close; do not edit HELD into READY silently.
---
> @@ -785,3 +785,80 @@ frontend 32/52/88; shared integration 8/16/28; independent UI VER 14/24/40; **to
>  
>  Not authorized by this section: UI code until an integrated target exists and a versioned UI dispatch is released; gateway, permission,
>  navigation, L10n or icon-map edits except by the single integration owner; contract changes; `done` status; commit or push.
10c10
< +## 30. Self-registration (PATCH PROPOSAL — NOT APPROVED until owner sign-off)
---
> +## 33. Self-registration (PATCH PROPOSAL — NOT APPROVED until owner sign-off)
```

The added lines are otherwise byte-identical, including the historical "Base note" paragraph.

## Observations for CT (no verdict change)

- **O1 — superseded checksum line.** `mvp6-claims-pack-apply-01/ARTIFACTS.sha256` still lists MOD-0187 at `762ab533…`. That was true when the file was written and was later superseded by signed patch 06. Records are not edited (K4). CT may note the chain `762ab533 → 31cb35c3` in its disposition.
- **O2 — approved bases differ from HEAD.** MOD-0184, MOD-0185, MOD-0186 and MOD-0187 were already modified in the working tree before these steps. Their approved before-hashes are those working-tree states, not the `4a8d4d4` blobs. Reproduction therefore used reverse-reconstruction, and the chain closes exactly. Their earlier (pre-sign-off) edits are not covered by this verification.
- **O3 — `docs/roadmap/backlog/product-backlog.md` changed today.** It was modified at 08:59:33 +03:00, after the claims apply and before the q46/q47 apply. It is not a pack, code, `.antigravity`, gateway, contract or registry file, and neither writer's evidence lists it. It is not attributed to either apply step, and its content was not verified here. CT should confirm who changed it.
- **O4 — stale headings in the applied text.** The applied self-registration sections keep their signed heading "(PATCH PROPOSAL — NOT APPROVED until owner sign-off)": one each in MOD-0183, MOD-0184, MOD-0185, MOD-0187 and DCP-009. MOD-0186 §31 keeps "(PACK-ALIGNMENT-03 proposal — NOT AP…". These are exactly the signed bytes. Removing a label would need its own patch and hash.

## Not done

No file outside this folder was written. No apply, commit, push, stash or index operation. CT decides.
