# MISSING OR CONFLICTING INFORMATION

## A. What the author of this package could and could not see

**Could see:** the repository at `/Users/natig/Projects/ERP-vNext-recovery` (HEAD `5e9f2b724`), the
full committed record set under `docs/`, the nine module packs, `CT-QUEUE.tsv` (595 rows), the
development plans v9.0–v9.5, `origin/main` after a fresh fetch, and the filesystem state of the
registered worktrees.

**Could see of this conversation:** only its own Control Tower session, and that session had
**already been summarized at least once** before this package was written — context was compacted,
so earlier turns exist as summary rather than verbatim text.

**Could NOT see:**

| | |
|---|---|
| other lane chat sessions | **not reachable.** Their repository handoffs were used instead — `SOP-22-DEV-HANDOFF.md`, `INDEPENDENT-VER-HANDOFF.md` and the sealed lane `REPORT.md` files. Whatever was decided in a lane chat and never written to the repository is not in this package. |
| verbatim owner messages | mostly **not reachable.** See §B. |
| any running process | nothing was running when this was written. No `RUNNING` state is claimed anywhere in this package. |
| CI run history | not consulted. `.github/workflows/` was read as source; no run results were fetched. |

**Therefore: this package does not claim to be the complete history.** It is a measured snapshot of
the tree plus the committed record set, as of 2026-10-16 13:34 +0300.

## B. Owner approval text — what is and is not verbatim

Of the twelve decision records in `docs/records/decisions/2026-10/`, **exactly one** carries a
verbatim owner sentence reachable from here:

- `mvp6-carrier-loads-ui-scope-owner-decision-01.md` → `decision_source:` owner answer
  **"Approve both scopes, build"**, 2026-10-04.

Every other record is attributed (`owner: ny@gmgroup.ch`, `status: ACCEPTED`) and **paraphrases**.
The merge decision is explicit about this — it says *"Rationale, in the owner's words"* and then
summarises. That is a decision record, which is good evidence, but it is **not** the owner's text.

Two records point at archived raw authority that was **not opened** while writing this package:

- `docs/records/audits/2026-09/mvp6-mod0186-patch-disposition-02/authority-source.md`
  (SHA-256 `447fb83b5418104430ab0d5be59dd26443cf692719b1cba8b980d6d43ad6667d`), user message
  2026-09-20T11:44:45Z
- `docs/records/audits/2026-09/mvp6-mod0186-http-composition-01/authority-source.md` item 2
  (SHA-256 `a57efad020a7070d3b83f0504489e4ebd8bcd5266ee38afe889318568005f979`)

If Codex needs exact owner wording for MOD-0186/0187 authority, those two files are where it lives.

**Nothing in this package treats a CT recommendation, a draft decision or a technical PASS as owner
approval.** Q484 is the clearest case: CT *refused* the re-pin the release included and returned it
to the owner with a recommendation. That recommendation is not a decision.

## C. Direct conflicts between records and the tree

### C-1. Plan v9.5 §6.1 says the SupplyChain `JwtSettings` fix landed. The tree says otherwise. (F2)

v9.5 §6.1: *"Fixed, +5 lines, digest matching the siblings; no value in any record (Q495)."*

Measured 2026-10-16 on HEAD `5e9f2b724`:

- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.Development.json`
  contains six keys — four `*.ReferenceBaseUrl` plus two `PlatformRegistration.*` — and **no
  `JwtSettings` section**.
- `Program.cs:46` is unchanged: `var secret = Required("JwtSettings:Secret");` still throws.
- The fix exists as an **uncommitted modification** in
  `/Users/natig/mvp6-env/bs-0190-0192-20261011-1411` (3 keys: `Secret`, `Issuer` `diten-auth-service`,
  `Audience` `diten-erp`).

**Controlling record: the tree.** v9.5 describes work that was done but never committed. The plan is
not lying — it is reporting a working-tree state that was lost to the disk failure before it was
secured.

### C-2. v9.5 §8 counts "10 sealed lanes today". Nine are findable.

Findable: eight directories under `docs/records/audits/2026-10/` (`mvp6-q484-capacity-branch-01`,
`mvp6-stage-r-gateway-contract-01`, `-readiness-01`, `-row1-loads-config-01`, `-row13-01`,
`-row13-02-returns-claims`, `-row5-returns-01`, `-stack-up-01`) plus
`docs/roadmap/plans/mvp6-ui-pack-wave-q322-01/`. The tenth is **not identified**. Low consequence —
no gate cell depends on it — but the count is unreconciled.

### C-3. v9.5 §8 says "commits on the MVP-6 branch: 1 (`baa35a7bf`), nothing since". Superseded.

Since v9.5 was written, `956d6d097` ("wip(mvp6): 0190/0192 bounded slice — uncommitted work
secured") landed, and the branch took two `origin/main` merges plus a merge of
`origin/feature/sce/mod-0190-0192-bounded-slice`. v9.5's §8 is accurate for 2026-10-13 and stale now.

### C-4. `GATE-18-0.tsv` rows 10 for MOD-0186/0187 read NOT MET; they were waived 2026-10-05. (F10)

**Deliberate, not an error.** The TSV sits inside its own lane's `ARTIFACTS.sha256`; editing it would
break a dated audit seal (K4). Movements are recorded in separate `MOVED.tsv` files. **The decision
record governs, the TSV does not.** v9.5 §3 states this. Any tool that reads `GATE-18-0.tsv` alone
will under-report by those two cells and by the six in the four `MOVED.tsv` files.

### C-5. `CT-QUEUE.tsv` is not reliably machine-parseable.

Nominally TSV with 8 columns, but item text contains embedded tabs and runs to thousands of
characters, so column 3 (`state`) frequently holds prose instead of a state token. A naive
`awk -F'\t' '{print $3}'` yields ~40 distinct "states", most of them sentences. **Read it by row id,
not by column.** The state histogram in this package was built that way and the unparseable rows are
why no aggregate queue-state count is given.

### C-6. 39 of 48 sealed artifacts are not in Git, though their seals are. (F1)

Not a contradiction in the records — a gap between record and repository.

| lane | artifacts hashed | not tracked in git | hash-verified in worktree |
|---|---:|---:|---:|
| `mvp6-stage-r-row5-returns-01` | 17 | 15 | 15 |
| `mvp6-stage-r-row1-loads-config-01` | 10 | 8 | 7 |
| `mvp6-q484-capacity-branch-01` | 8 | 7 | 5 |
| `mvp6-stage-r-row13-02-returns-claims` | 7 | 5 | 4 |
| `mvp6-stage-r-row13-01` | 6 | 4 | 4 |
| **total** | **48** | **39** | **35** |

**0 hash mismatches.** The remaining 4 of the 39 are tracked under a different path or are the
`REPORT.md`/`MOVED.tsv` files themselves, found in the primary tree rather than the worktree. The
audit trail is **intact and recoverable**, and it is recoverable from exactly one place.

## D. Not found — recorded as MISSING, not as absent

| | |
|---|---|
| **Phase 5 / Phase 6 / release CI gates** | `.github/workflows/` contains **only** `phase1-gates.yml` and `service-tests.yml`. The SOP's Phase 5/6 language was not traced to any workflow. Either they are run by hand, or they live somewhere this package did not look. **Do not read this as "Phase 5/6 do not exist."** |
| **A post-merge completion-gate record** | nothing under `docs/records/audits/2026-10/` records the 7-item gate being run after the 2026-10-14/15 merges. Absent from the record set; may have been run without a record. |
| **The 2026-09 decision set** | 53 files in `docs/records/decisions/2026-09/` were **not** individually read. Only the 2026-10 set was. MOD-0186/0187 §31 authority chains reach into 2026-09 and were taken from the pack `status_note` lines rather than from source. |
| **The 10th sealed lane** | see C-2. |
| **Verbatim owner messages** | see §B. |
| **MOD-0147 / MOD-0148 DC-01…DC-05** | `mvp6-ct-ruling-dc01-dc05-concurrence-01.md` was read only at header depth. DC-01 (domain ownership) and DC-02 (authority to consume SUPPLIER-BASE) are both recorded OPEN in the pack `status_note`; the ruling's full reasoning was not extracted. |
| **CI run results** | no workflow run history was fetched. |

## E. Figures carried from the session's own earlier measurement

One figure in this package was measured earlier in the CT session and **re-verified** while writing
it, because the session's own context had been compacted and the claim could no longer be read
verbatim:

- "61 open acceptance rows across seven MVP-6 packages, MOD-0190 = 1, MOD-0192 = 4."
  **Re-measured 2026-10-16** by counting `[ ]` checkboxes per pack: 0183:12, 0184:7, 0185:12,
  0186:12, 0187:13, 0190:**1**, 0192:**4** → **61**. Confirmed, including both named modules.

No other figure in this package rests on recalled session state. Everything else was measured
against the tree on 2026-10-16.

## F. Final verification of this package

Run 2026-10-16 after all files were written.

**Link check.** Every repo-relative path mentioned in any file of this package was extracted and
tested for existence: **89 distinct paths, 12 did not resolve.** All twelve were examined and none is
a broken reference:

| not resolving | why that is correct |
|---|---|
| `.antigravity/scripts/sync_adapters.py`, `.claude/agents/`, `.claude/rules/localization-standard.md`, `.claude/skills/resx-check`, `.github/workflows/agent-adapters.yml`, `services/Diten.PlanningService/src`, `services/Diten.PlanningService/src/Diten.PlanningService.Api/appsettings.json`, `scripts/test-env/mod0188-ci-mongo.sh` | **incoming on `origin/main`**, described in this package as incoming. They do not exist on this branch yet, which is the point. |
| `docs/records/audits/2026-10/mvp6-stage-r-`, `execution/domains/supply-chain-execution/module-packs/MOD-01`, `services/.../Program.cs` | **artifacts of the extraction regex** reading a brace list (`mvp6-stage-r-{row13-01,…}`) or an abbreviation as a path. Not references. |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Common/ReturnsClaimsHttpHost.cs` | **this is finding F1/F3 itself** — the file is untracked and exists only in the worktree. Its absence from the tree is the defect being reported. |

**Hash integrity.** `SOURCES.tsv` holds 46 in-repository sources, every one read and hashed with
`shasum -a 256`: **0 unreadable, 0 `NOT-READABLE` entries.** Two further sources are listed as
`IN-GIT-NOT-OPENED` with the SHA-256 published in the MOD-0186 pack rather than computed here, and
are labelled that way because they were not opened. Four external absolute paths are labelled
`EXTERNAL-ABSOLUTE`; directories are marked `DIRECTORY-NOT-HASHED` rather than given a fake hash.

**Worktree still present.** `/Users/natig/mvp6-env/bs-0190-0192-20261011-1411` was confirmed to exist
at the moment this package was sealed, with its five evidence directories intact.

**What is not verified here:** no build was run, no test was executed, no service was started, and no
CI run history was fetched. Every 2026-10-16 figure in this package is a static read of the tree or a
`git`/`shasum` measurement. Anything requiring execution is marked NOT RUN.

**Excluded from the delivered archive by instruction:** no full source archive, no database dump, no
raw log, no `.env`, no private key, no password, no cookie and no bearer token. Large evidence sets
are referenced by path and hash instead — see `SOURCES.tsv` and finding F1.
