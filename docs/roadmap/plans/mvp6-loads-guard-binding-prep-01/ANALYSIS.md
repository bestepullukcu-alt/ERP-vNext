# Q26 analysis — which DocsPathGuard inputs change when the Loads 3.1.0 publication lands

Source of truth: `tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs` (SHA-256 `da5ec0cc0f180d7237bdf599c7d71eb1fb96962ed14496427a1ea789b18c5050`, byte-identical to the reader used by the 23 Sep Capacity activation). Line numbers below refer to that file.

## 1. What the guard checks (from the code)

| Check | Code | Consequence for publication |
|---|---|---|
| Authority `status` = APPROVED; `decision` is non-null, under `docs/records/`, and hash-pinned | 208–212 | A new decision record is needed, because the payload changes (below). |
| Decision `kind` = `DOCS_PATH_OWNER_DECISION`, `status` = APPROVED, non-empty `decisionId`/`approvedBy` | 215–219 | The new decision record must meet all of these. |
| `payloadSha256` = SHA-256(UTF-8(raw `canonicalTargets` + "\n" + raw `sealedInputs`)) | 220–222 | Any change to a target or seal hash changes the payload → new decision. |
| Every canonical target file matches its pinned SHA-256 | 226–230 | **The YAML target must be re-pinned from `5dfe7c1b…d21c` to `6dc1dd48…96aa2`.** |
| Every sealed input, provenance file and provenance line matches (`` `path:N`; baseline SHA256 `hash` ``) | 233–250 | Unchanged by publication; relevant to §3. |
| historical-data = `.json`, historical-tool = `.py`, both with empty `targets`; active-tool = `.py` whose `targets` are canonical targets | 251–267 | Constrains the new seals (§3) and the annex question (§2). |
| Every canonical target is used by at least one active-tool (`unused canonical target`) | 269 | **The new annex must NOT be added as a target** (§2). |
| Scan: every code-extension file (`.cs .cshtml .js .py .sh .ps1 .json .csproj .css .html .yaml .yml .xml .resx`) naming `docs/<x>/` outside the five folders is an offender unless the file is the authority record or a sealed input | 103–127, 54–58 | Checked for the 3.1.0 YAML (§2) and for the current tree (§3). |
| Every authority path (authority file + each seal) must itself contain at least one such hit (`consumed`) | 138 | Each new seal must contain a non-five `docs/` hit (all 8 do). |

## 2. Inputs that change because of the two canonical files

1. **canonicalTargets[0] SHA-256:** `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` → `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`. This is required, because an unchanged pin fails line 229 once the YAML is published.
2. **New annex as a target: no.** No sealed active-tool lists it (grep of all 6 active tools: 0 mentions of `loads-semantics`), so adding it fails line 269 (emulated: `NEG annex as target` → FAIL). The annex is `.md`, which is not a scanned extension, so it can never be an offender either. The existing v2 annex is not a target today and stays that way.
3. **sealedInputs: unchanged by publication itself.** The 6 active tools name the YAML path but pin no YAML hash (grep for `5dfe7c1b`: 0 in all 6); their own bytes do not change. No provenance file changes.
4. **3.1.0 YAML scan hits:** 0 non-five hits, the same as the current YAML (emulated both). Publication adds no offender.
5. **Decision reference:** it must move to a new decision record, because the payload hash changes. The payload excludes `status` and `decision`, so the payload hash can be fixed before approval.

## 3. Pre-existing red (outside publication, blocks a green run)

Emulated on the current working tree on 2026-09-26 between 00:21 and 00:33 +03:00, the guard is already RED: **19 offender hits in 8 files**, all Loads amendment evidence created 24 Sep (after the last production PASS at 23 Sep 11:20 +03:00), all untracked:

| File (under `docs/records/audits/2026-09/`) | SHA-256 | Hit lines | Proposed kind |
|---|---|---|---|
| `mvp6-loads-root-amendment-independent-ver-01/verify_candidate.py` | `3e30d1bb8ea75d212632133ff55c28ce48f072d927030a4c85a9b5e73522709d` | 58, 59, 71 | historical-tool |
| `mvp6-loads-root-amendment-independent-ver-01/raw/raw-results.json` | `8255831b4510abdcc60a29ebc3d860df7a37e2d9f77305a1da00df180f82aa9c` | 5, 11 | historical-data |
| `mvp6-loads-root-amendment-independent-ver-01/raw/verifier.stdout.json` | `8255831b4510abdcc60a29ebc3d860df7a37e2d9f77305a1da00df180f82aa9c` | 5, 11 | historical-data |
| `mvp6-loads-root-amendment-independent-rever-01/verify_rever.py` | `1a1960f82e052c4cd41923e5b95c4bc7834fab19d462f7e73d55b6bf4db553c1` | 28, 29, 60 | historical-tool |
| `mvp6-loads-root-amendment-independent-rever-01/raw/results.json` | `77c89743329fc9bfe7837b2fa7a8de35e42f50917c29f1ca3bbc339ba427bf17` | 4, 10 | historical-data |
| `mvp6-loads-root-amendment-release-prep-01/verify_release_prep.py` | `b359cfdec8579b610fcb63774b184eb91d9d06dabd0a852eb84cbd517b074572` | 58, 59, 75 | historical-tool |
| `mvp6-loads-root-amendment-release-prep-01/raw/validation-results.json` | `a9c20c25e05117c24e473594f33bc7b3cd4ba0e2fffe9a598e859355895532eb` | 5, 11 | historical-data |
| `mvp6-loads-root-amendment-release-prep-01/raw/validator.stdout.json` | `a9c20c25e05117c24e473594f33bc7b3cd4ba0e2fffe9a598e859355895532eb` | 5, 11 | historical-data |

These are completed candidate/verifier scripts and their outputs. They pin the pre-publication preimage hashes, so after publication they describe history, not current canonical content. That is exactly the `historical-tool` / `historical-data` definition in `.antigravity/rules/docs-organization.md` (candidate rule, SHA-256 `0eaff863…e2a9`), which needs no rule, schema or reader change. Two pairs share a hash but have different paths; the reader allows this (existing seals already share `2c045364…`).

**Scope decision (owner, this session, 2026-09-26, answered via the option question after the current-tree emulation, before 00:33 +03:00):** "Seal the 8 files too (Recommended)". The candidate therefore re-pins the YAML and appends these 8 seals in one authority change and one decision.

## 4. Candidate content (smallest that turns the guard green with publication)

- `canonicalTargets`: YAML hash re-pinned; carrier target unchanged; no new target.
- `sealedInputs`: the 35 existing seals are byte-identical, and the 8 seals above are appended at the end, each with `provenancePath` `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` (SHA-256 `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a`, lines 3–10) and `targets: []`.
- Candidate form `status: UNAPPROVED`, `decision: null` (Capacity/SANDOP precedent). Activation changes only those two fields. Full line diff: `authority-candidate.diff.txt`.

## 5. Emulation evidence (not a substitute for the .NET run)

`guard-emulator.py.txt` is a Python transcription of the reader and scanner. Calibration: on today's authority it reproduces the approved payload `3bd20e26…8dfb` exactly, and every pin, seal and provenance line checks OK. Results (`EMULATION-RESULTS.txt`):

| Scenario | Result |
|---|---|
| Candidate activated + 3.1.0 YAML + annex + provenance | **PASS** (43 seals, 2 targets, 0 offenders, all consumed) |
| Current authority after publication | FAIL: target hash + 19 offenders |
| Re-pin only (no seals) | FAIL: 19 offenders |
| Candidate + annex added as target | FAIL: unused canonical target |
| Candidate activated without publication | FAIL: target hash |

The last two FAIL rows prove that the binding and the publication must land in the same step. The emulation ran in a disposable scratch area outside the repo; no repository file was modified. The authoritative check is the production .NET 8 test run in EXECUTION-ORDER.md.
