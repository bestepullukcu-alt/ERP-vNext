---
id: MOD-0286-FU01
name: UI Compliance Page JS Reference Mapping
slug: page-js-reference-mapping
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: draft
build_gate: closed
operational_runtime_gate: closed
owner: K3
branch: chore/ct/pss-ui-ct-01
started: 2026-10-31
target: 2026-11-07
form_field_count: 0
slice: "Reference-based mapping of the 106 page JS files the UI compliance selector cannot check today. One selector change and the baseline rows it needs. No runtime page, entity, endpoint, permission or Ocelot route."
---

# MOD-0286-FU01 - UI Compliance Page JS Reference Mapping

> **Draft, revision 01, 2026-10-31; not a go-ahead.** Owner's word in the PSS-UI tower chat, 2026-10-31: "Evet". Header values follow the parent's reasons
> (`MOD-0286-ui-design-system-page-pattern-governance.md:32-36`). DCP-002 at `0504c96ea`: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286-FU01
> --name "UI Compliance Page JS Reference Mapping"` -> `OK  MOD-0286-FU01: proven against Blueprint/registry.`

## Why a separate pack, and what it supersedes

Accepting work binds to the pack's bytes on main (`scripts/prompt_gate.py:736`). A parent revision would make the three accepted jobs (documents, CI, single-level)
rebind and rerun, so the parent stays byte-identical. An FU id is its own module for the tools (`scripts/ct_queue.py:1359-1364`, `scripts/acceptance_record.py:75`).

For **this pack's work only** it supersedes these parent statements (lines at `0504c96ea`):

| Parent statement | Line | Here |
|---|---|---|
| Owned Objects, DL-3 part 1, "Not mapped in rev 15" (1)(2)(2b)(3) | `:336-346` | The 106 files map by reference; their own lines change |
| "DL-3 tek seviye sayfalar": "Not mapped in rev 15", "`pages/` decision" | `:555-578` | Same; reference reading is chosen, moving JS is not |
| Follow-up row FU-18 | `:966` | Closed by this pack |
| CI-4 tail on `Meetings/series/index.js`; "left out by name" on the 106 files | `:488`, `:516-518` | These files are page JS of the pages that reference them |
| CI table "page JS" cells; `fu` lists of Validation Rules and AC-19 | `:488-494`, `:739-757`, `:823` | Widened only by this pack's `FU-19` row |

AC-21 (b)'s count of 106 and AC-21 (g)'s FU-18 cases stay true for the commit they were accepted at and are not re-judged. Everything else in the parent binds, above all
DL-3 part 2's count rule and the pattern block the selector reads (`:611-644`).

## Open Decisions (Owner Decision Pending)

| # | Decision | Who | Proposed answer | Status |
|---|---|---|---|---|
| O-1 | Name of the owner decision for the `FU-19` rows, and each row's owner | Owner | Tower asks; proposal in Owned Objects, part 6 | Precondition of dispatch; the pack holds only a placeholder |
| O-2 | `Follow-up` row for MOD-0286-FU01 in `execution/registries/module-id-registry.md` | Owner (registry owner: Lead Architect) | Add in its own approved commit; the rule says before a pack is created, this draft comes first on the tower's instruction | Precondition of approval |
| O-3 | The 2 unreferenced files and the missing file named at `StrategyPeriodDetail.cshtml:132` | Owning domains (XMC-001) | Not here: FU-20, FU-21 | Does not block this pack |

## Module Summary

The selector prints `eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) <path>` for 106 JS files, so a new violation in them is never red. At `0504c96ea` 102 of the 106 map by a
`<script src>` in a view: 95 to one page, 7 to several, 22 pages, 113 (file, page) pairs. Debt that surfaces: 17 files, 21 file-check rows, 89 matches = 22 page-keyed rows, 92 matches, about 1% on the 2153-row baseline.

## Ownership and Boundaries

- In-scope: mapping those 106 files by reference; the baseline reader change; the `FU-19` rows; self-test cases.
- Out-of-scope: moving any JS; FU-17 (area-less verifier); name tables; fixing any old red; runtime code; `.github/`; CI-3 and CI-9's `savedView` half, which stay on the page's own folder.

## Owned Objects

**1. The class.** A "class file" is a path `map_path` returns as `fu18` today (`scripts/uic_changed_pages.py:892`, `:897`, `:902`); the path rule does not change. A reference brings in nothing else: `js/shared/**`, top-level files and another page's own folder stay as they are.

**2. Reference rule, and which view counts (Decision 1).** Binding raw strings, Python `re`: `STRIP` with `re.S` is removed first, `SCRIPT` runs with `re.I | re.S`, text as `text_of` (`:227`); capture `c` names `frontend/Diten.Web/wwwroot/` + `c`.

```text
STRIP	@\*.*?\*@|<!--.*?-->
SCRIPT	<script\b[^>]*?\ssrc\s*=\s*["']\s*(?:@Url\.Content\(\s*["'])?[^"']*?(assets/js/[^"'?]+?\.js)
```

**Only a `.cshtml` whose own path belongs to the page counts**, by the rule the page's checks read (two-level: `Views/{Area}/{Module}/**`; single-level: files directly in `Views/{Module}/`). `Views/Shared/**`, `Views/Archive/**` and the two root files belong to no page.
**The partial chain is not followed:** a partial name is a Razor expression, and following it means parsing every view whenever a page is selected, against Decision 7. What it misses, measured: 8 mapped files are referenced only from `EnterpriseStrategyBusinessPerformance/Components/_SubdomainShell.cshtml` or `_Scripts.cshtml`; 59 files name `_SubdomainShell`, and these callers are neither named nor checked when such a file changes. The violation is still red, under page `EnterpriseStrategyBusinessPerformance/Components`, because the check runs on the file.
A capture holding `@`, `{` or `(` is not a path: `ölçülmedi <page> değişken script yolu <view> <capture>`, never red (0 today).

**3. Scope of a selected page.** The JS of its line checks (CI-4, CI-5, CI-6 JS, CI-7, CI-8, CI-9 `api/personalization`, CI-10) = its own-folder JS as today + every class file its own views reference at HEAD; `evaluate_page` reads only `js/{page}/` today (`:514`), so mapping alone would run no check. A referenced path absent at HEAD is not read (`Tree.read` gives `None`, `text_of` would fail): `başvurulan JS dosyası yok <page> <instance>`, never red. One exists today: `Views/EnterpriseStrategyBusinessPerformance/StrategyPeriodDetail.cshtml:132`.

**4. Own lines (Decision 2).** A changed class file with no page reference at HEAD keeps its own line, with the full repo path:

- `eşlenmemiş sayfa JS'i, yalnız Shared görünümü yüklüyor, sayfa kontrolü yok (MOD-0286-FU01) <path>`: today `pages/enterprise-strategy/esbp-horizon-dates.js` (`_Layout`), `pages/governance/permissions.js` (`_PermissionBootstrap`).
- `eşlenmemiş sayfa JS'i, hiçbir görünüm yüklemiyor, sayfa kontrolü yok (MOD-0286-FU01) <path>`: today `pages/systemStatus.js`, `pages/enterprise-strategy/project-ppm-form-init.js`.

The `(FU-18)` text (`:84`) does **not** stay: it cites a row this pack closes. A page reference wins over a Shared one. A Shared file does not select pages: that is all pages, against parent AC-20; the 4 files carry no debt today (0 matches of the seven JS keys).

**5. Several pages (Decision 3): one row per page + check + file.** Each owning page is evaluated once, with one line per check for the file however many of its views reference it. `n` is the file's count; each row's `b` is compared alone (`:763`), so one added match is one `YENİ KIRMIZI <page> <check> <instance> <n> +1` per owning page. **No double-count trap:** rows are never pooled and no row's budget absorbs another page's match; a stale high row on one page leaves the others red. Cost: the number of `YENİ KIRMIZI` lines counts owning pages, not violations (the exit reads only that one exists, `:870`); `--check-baseline`'s `eşleşme` (`:953`) sums rows, so it grows by 92 for 89 real matches, the 3 extra being `modal-form-utils.js` on two pages; a lowered count is edited in each row. Not one row per file: `self.rows` is keyed by page + check (`:494`), and a page-less row could not close when one page drops the file.

**6. Rows (Decision 6).** `fu` = `FU-19`, defined in Follow-up Items (the parent's list ends at FU-18, `:966`; a later parent revision starts at FU-23). `reason` = `sahip kararı <sahip karar adı> (<tarih>)`, filled from O-1, never `pss-mod0286-taban-01` or `tek-seviye-borc-01`. Reader, mapping, draft-mode change and rows land in **one pull request**, as parent AC-21 (a): mapping first makes each pull request on these pages red on old debt (inference from the 22 rows); rows first gives exit 2 on every pull request (measured on a scratch copy: `--check-baseline` exit 2, `HATA: taban satır 2155: CI-4 satır kontrolü instance'ı sayfanın dosya yolu değil`). In draft mode a class-file row gets `FU-19` and owner `belirsiz — sahip belirleyecek`, before the single-level branch of `owner_of` (`:394`), else an ESBP row would be `FU-16`.
Proposal for O-1, from each page's present rows: EnterpriseStrategyBusinessPerformance 11 and `.../Components` 3 (no present row, by area) enterprise-strategy-business-performance; DemandIdeas 3, DeliveryExecutionManagement 1, DecompositionTreeBuilder 1 portfolio-delivery; ManagementGovernance/ProcessModeling 1 management-governance; Meetings/Series 1, Meetings/MeetingTypes 1 platform-shared-services.

**7. A reference that changes (Decision 5).** The two sides are computed only for a page with a changed view in the diff; its own views are read at HEAD and at the base. For a view of page P and class file J:

| Case | J read under P | Output | Exit effect |
|---|---|---|---|
| P gains J, `n = 0` | yes | `başvuru eklendi P J`, `PASS P <check> J 0` | none |
| P gains J, `n > 0`, no row | yes | `başvuru eklendi P J`, `YENİ KIRMIZI P <check> J <n> +<n>` | 1: taking on debt needs an owner decision |
| A view drops J, another view of P keeps it | yes | check lines as usual, no `başvuru` line | none |
| P's last reference to J goes | no | `başvuru kalktı P J`; each row of P on J `kapandı - satır silinmeli P <check> J` | none: closed, never dropped |
| J deleted, P still references it | no | `başvurulan JS dosyası yok P J`; rows closed as above | none |
| J renamed | new path | new path has no row (red when `n > 0`); old row closed | parent's rename rule |

Closed and not red because dropping a load adds no violation; a removed page keeps the parent's `sayfa kaldırıldı` closures. **Limit, on purpose:** moving a reference to `Views/Shared/**` prints `başvuru kalktı`, and J then has only the Shared line.

**8. Baseline reader (Decision 4).** A row with `fu` other than `FU-19` takes today's path unchanged, so no present refusal loosens. A `FU-19` row:

- *Form, every run, no git:* `check` in CI-4, 5, 6, 7, 8, 9, 10; `instance` under `wwwroot/assets/js/<folder>/`, ending `.js`, no `:`, normal form (`_norm_ok`), top folder outside `NOT_PAGE_AREAS`, **not** under `js/{page}/`; `page` a page key outside `NOT_PAGE_AREAS`; `count` an integer >= 1. Else exit 2.
- *Class and reference, CI:* only for rows **added or changed against the base's baseline** (`git show`): the path is a class file present at HEAD and a view of the row's own page references it, reading only those pages' views. Else exit 2.
- *Class and reference, `--check-baseline`:* the same test on all `FU-19` rows, reading every view; CI never runs it.

CI does not test unchanged rows because a stale row must not stop a pull request that did not make it stale; it prints `kapandı` when its page is selected, as the parent's stale rows do.

**9. CI (Decision 7).** Changed pages only: no all-pages mode, flag, variable or value (parent AC-20). All views are read **only** when the diff names a class file, at HEAD and at the base, to find the pages that reference it. Two lines always print before `süre:`: `başvuru taraması yok` or `başvuru taraması: HEAD <n> görünüm, taban <m> görünüm, <s> s`; and `okunan görünüm metni: HEAD <a>, taban <b>`, counted from the `.cshtml` paths held in each `Tree.cache`. Cost at `0504c96ea`: 1530 views, 7.46 MB, 0.043 s to read + 0.059 s to scan one tree; checks with view text already read: all 22 pages 1.21 s, the 5 of `trace-ui.js` 0.30 s, the 3 of `strategy-apis.js` 0.008 s. **The workflow does not change:** `.github/workflows/ui-compliance.yml` already filters on `Views/**` (`:10`), `wwwroot/assets/js/**` (`:11`), the selector (`:16`) and the baseline (`:17`), keeps full history (`:30`) and runs `--base "$BASE_SHA"` (`:38`).

## Entity Fields

None. The deliverables are one script change and baseline rows.

## Repo Scope

Worker writes only: `scripts/uic_changed_pages.py` (with `--self-test`), `tests/ui-compliance-baseline.tsv`. Tower only: the owner decision record under `docs/records/decisions/`; the evidence script and oracle under `docs/records/audits/<YYYY-MM>/mod-0286-fu01-01/`. The worker never changes the evidence script.

## Protected Paths

`.antigravity/**`, `execution/registries/**`, `.github/**`, `frontend/Diten.Web/**` (read only), `services/**`, `gateway/**`, the parent pack (byte-identical). Other domains' fixes go to their owner (XMC-001).

## Dependencies

- The parent's selector and baseline on main at `0504c96ea` or later; the parent's pattern block, read at run time.
- **Registry row (O-2):** `execution/registries/module-id-registry.md:30` "requires reserving an ID here before creating a Module Pack". No tool measured here enforces it: `verify_module_id.py` proves the id from the parent, `ct_queue.py:251` accepts it from a pack.
- **Before dispatch:** owner decision and row owners (O-1); the tower's evidence script and oracle committed.
- One slice needs `whole_pack=<WP>` on the approval row (`scripts/pack_approval.py:387-388`), which counts HIGH (`:365`). One slice because reader, mapping and rows must be one pull request.

## Runtime Constraints

Local temporary clone and the CI job only. Python standard library + git; no service, port, network or secret. Reads, never writes.

## Layout & Shell Contract

`shell: none`: no Razor view and no layout. CI-1 checks other pages' explicit layout and is not changed here.

## Backend File Convention

None: no backend.

## Frontend File Contract

None: no view and no `data_mode`. `frontend/Diten.Web/**` is only read.

## Validation Rules

No field. Baseline: the parent's rules (`:739-757`) stand; the `FU-19` row is defined in Owned Objects, part 8. Line texts are those of Owned Objects, parts 2, 3, 4, 7, 9, byte for byte; instances are relative to `frontend/Diten.Web/`, as baseline rows.

## Failure Path to Verify

- **Duplicate** - a repeated page + check + instance: exit 2 (existing rule). A view naming J twice, or several views of a page: one line per page and check. **Missing** - a referenced file absent at HEAD: named line, no exception. A class file with no reference: its own line.
- **Malformed or unreferenced row** - SB-13: exit 2. A stale row unchanged in a pull request: `kapandı`, exit unaffected; in `--check-baseline`: exit 2.
- **Hidden violation** - a match on a counted line: `n > b` (SB-3, SB-4). One removed and one added in a file: `TABAN`, the parent's accepted limit.
- **Silent loss** - last reference gone: lines of part 7 (SB-6). A commented-out tag is no reference (SB-12).
- **Wrong order** - rows without the reader: exit 2; mapping without rows: old debt red (part 6).
- **Tool error** - `git show` of the base's baseline or `cat-file` fails: exit 2, never "0 pages".
- **Class changes under a row** - a new `Views/{X}/{Sub}/` makes J a normal page's file: old row closed, new page red until the owner adds a row (as the parent's rename rule).
- **Unauthorized** - no endpoint; the CI token stays read-only. **Concurrency** - two pull requests that each pass may together make a page red; the next one that selects it shows it, as for any page today.

## Authorization Convention

None: no endpoint, page or permission. The job keeps `permissions: contents: read`.

## Audited Events

This pack owns **no write command**; the table has no command row and claims no exception class. The baseline's trace is its git history, with the owner decision in each row's `reason`.

## Gateway / API Routing Decision

Karar: Gateway değişikliği gereksiz; endpoint yok.

## Acceptance Criteria

Each is measured by the tower's script in a temporary clone at the work's commit; SB ids are the sabotage table below.

- [ ] AC-1 (zorunlu) — Complete mapping: one commit appends one empty line to each of the 106 class files; `python3 scripts/uic_changed_pages.py --base <its parent>` exits 0, `grep -c '^YENİ KIRMIZI '` is 0, every (page, file) pair of the oracle has exactly one `CI-4` line `PASS` or `TABAN FU-19` with that page and instance (113 pairs, 22 pages at `0504c96ea`), `grep -c '^TABAN FU-19 '` equals the oracle's row count (22), `grep -c '(FU-18)'` is 0.
- [ ] AC-2 (zorunlu) — The 4 unmapped files: SB-11 gives exit 0, no `YENİ KIRMIZI`, and each file's line of Owned Objects, part 4; `grep -c 'MOD-0286-FU01'` is 4.
- [ ] AC-3 (zorunlu) — New violations: SB-1, SB-2, SB-3, SB-4, SB-8, SB-9 each exit 1 with exactly the table's lines, each owning page named once, no other `YENİ KIRMIZI`; each revert exits 0. SB-1 on the parent's selector and baseline exits 0 with its `(FU-18)` line, so the red comes from this work.
- [ ] AC-4 (zorunlu) — Baseline holds, references change: SB-5, SB-6, SB-7, SB-12 exit 0 with exactly the table's lines; after SB-6, a further commit touching page `DecompositionTreeBuilder` exits 0 with the `kapandı` line while `--check-baseline` exits 2.
- [ ] AC-5 (zorunlu) — Regression: SB-10 (a) stdout equals stdout of the same edit on the parent's selector and baseline, after dropping lines starting `süre:`, `başvuru taraması`, `okunan görünüm metni`; SB-10 (b), (c) exit 1 with the named lines.
- [ ] AC-6 (zorunlu) — Reader: each row of SB-13, appended alone to the work's baseline, gives `--check-baseline` exit 2 with `taban satır <n>`; R-1 and R-5, committed, give exit 2 under `--base <parent>`; the unchanged baseline gives `--check-baseline` exit 0.
- [ ] AC-7 (zorunlu) — Row set by an independent oracle: the tower's script, importing no selector code, computes (page, check, instance, count) from the git tree, Owned Objects parts 1-2 and the parent's pattern block; it equals the `FU-19` rows both ways, and the `FU-19` rows of `--draft-baseline`. A planted extra row and a planted lowered count each make it fail, recorded. Its `STRIP` and `SCRIPT` equal part 2's code block byte for byte.
- [ ] AC-8 (zorunlu) — One pull request, old rows untouched: `git diff --numstat <base> -- tests/ui-compliance-baseline.tsv` shows only added lines, as many as the oracle's rows; `awk -F'\t' '$6 == "FU-19"' tests/ui-compliance-baseline.tsv` holds no `taban-01`, `tek-seviye-borc-01` or `belirsiz`, and each row's `reason` names the O-1 decision, given to the script as a parameter; the other `fu` values keep their row counts (at `0504c96ea`: FU-14 1953, FU-16 134, FU-4 64, FU-12 1, FU-15 1).
- [ ] AC-9 (zorunlu) — Changed pages only: SB-10 (a) prints `başvuru taraması yok` and `okunan görünüm metni: HEAD 19, taban 0`; the AC-1 run prints `başvuru taraması: HEAD 1530 görünüm, taban 1530 görünüm, <s> s` (counts at `0504c96ea`) and `süre:`; `--help` lists the same four modes as today.
- [ ] AC-10 (zorunlu) — Self-test: `python3 scripts/uic_changed_pages.py --self-test` exits 0 with every case of Test Expectations; the positive assertions of AC-21g03 (first case), g06, g07, g08 name the new `hiçbir görünüm` line, the negative `FU18` assertions (`:1351`) cover both new lines, with no other change to an existing case.
- [ ] AC-11 (zorunlu) — Scope: `git diff --name-only <base> <work>` lists only the two Repo Scope worker files; the parent pack and `.github/workflows/ui-compliance.yml` have the same blob at both commits.
- [ ] AC-12 (zorunlu) — CI: the `ui-compliance` job on the pull request's head exits 0; the acceptance record names run id, head SHA and the artefact's SHA-256.

## Test Expectations

**Today at `0504c96ea` (origin/main when written):** class files 106 = pages 80 + deeper 17 + area 9; one page / several / Shared only / none: 95 / 7 / 2 / 2 (pages 73 / 3 / 2 / 2, deeper 17 / 0 / 0 / 0, area 5 / 4 / 0 / 0). Page-keyed debt 22 rows / 92 matches: CI-4 2 / 2, CI-5 4 / 4, CI-6 11 / 77, CI-7 4 / 6, CI-10 1 / 3. Baseline 2153 rows, 858 matches; after the work at this commit 2175, 950. `--draft-baseline` here equals the baseline's 2153 keys and counts, so no other red surfaces on the 22 pages. If main moves, the oracle's numbers at the work's base replace these. No .NET build or browser run: nothing runtime changes. Runs are kept with `scripts/run_record.py`.

Sabotage set: commits on a scratch branch of the clone, never pushed. A Commit path is under `frontend/Diten.Web/wwwroot/assets/js/` unless it starts `Views/`; `<J>` = that file's instance. Each run `--base <parent>`; each revert `--base <the sabotage commit>`, exit 0.

| Id | Commit | Exit | Named lines |
|---|---|---|---|
| SB-1 | `alert('x');` appended to `Platform/PersonReferences/person-reference-picker.js` | 1 | `YENİ KIRMIZI HCM/Employees CI-7 <J> 1 +1` |
| SB-2 | `alert('x');` appended to `Traceability/trace-ui.js` | 1 | `YENİ KIRMIZI Traceability/<P> CI-7 <J> 1 +1` for each P of LotMigration, LotMovements, LotTrace, Lots, Serials |
| SB-3 | ` a.style.b;` appended to the first `\.style\.` line of `pages/enterprise-strategy/modal-form-utils.js` (`:113`) | 1 | `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance CI-6 <J> 4 +1`; `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance/Components CI-6 <J> 4 +1` |
| SB-4 | Same on `pages/enterprise-strategy/goals.js` (`:3372`) | 1 | `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance CI-6 <J> 12 +1` |
| SB-5 | Empty line appended to `pages/enterprise-strategy/goals.js` | 0 | `TABAN FU-19 EnterpriseStrategyBusinessPerformance CI-6 <J> 11`; `TABAN FU-19 EnterpriseStrategyBusinessPerformance CI-5 <J> 1` |
| SB-6 | Tag of `pages/decomposition/decompositionPage.js` deleted at `Views/DecompositionTreeBuilder/Index.cshtml:179` | 0 | `başvuru kalktı DecompositionTreeBuilder <J>`; `kapandı - satır silinmeli DecompositionTreeBuilder CI-7 <J>` |
| SB-7 | Tag of `CRM/cycle-dates.js` deleted from `Views/CRM/CyclePeriods/Details.cshtml` (`Index` keeps it) | 0 | `PASS CRM/CyclePeriods CI-4 <J> 0`; no `başvuru kalktı` |
| SB-8 | Tag of `pages/enterprise-strategy/goals.js` added to `Views/CRM/CyclePeriods/Index.cshtml` | 1 | `başvuru eklendi CRM/CyclePeriods <J>`; `YENİ KIRMIZI CRM/CyclePeriods CI-5 <J> 1 +1`; `YENİ KIRMIZI CRM/CyclePeriods CI-6 <J> 11 +11` |
| SB-9 | Tag of `Traceability/trace-ui.js` deleted from `Views/Traceability/Serials/Index.cshtml`, `alert('x');` appended to the file | 1 | SB-2's line for the other 4 pages; `başvuru kalktı Traceability/Serials <J>`; no CI-7 line of Serials on J |
| SB-10 | (a) Empty line on `CRM/Accounts/index.js`; (b) `alert('x');` there; (c) `alert('x');` on `Recall/cases.js` | 0, 1, 1 | (a) AC-5, AC-9; (b) `YENİ KIRMIZI CRM/Accounts CI-7 <J> 1 +1`; (c) `YENİ KIRMIZI Recall CI-7 <J> 1 +1` |
| SB-11 | `alert('x');` appended to the 4 files of Owned Objects, part 4 | 0 | The 4 lines; none of those paths on a check line |
| SB-12 | `@* <script src="~/assets/js/pages/enterprise-strategy/goals.js"></script> *@` added to `Views/CRM/CyclePeriods/Index.cshtml` | 0 | No `başvuru eklendi`; no goals.js line of that page |
| SB-13 | Rows, one at a time (AC-6) | 2 | R-1 `FU-14` `HCM/Employees · CI-7 · wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js · 1`; `FU-19`: R-2 with `CI-2`; R-3 a `Views/...cshtml` instance; R-4 under `js/{page}/`; R-5 `Home · CI-7 · wwwroot/assets/js/pages/systemStatus.js`; R-6 `Home · CI-6 · wwwroot/assets/js/pages/governance/permissions.js`; R-7 `Home · CI-6 · wwwroot/assets/js/shared/diten-tags.js`; R-8 `HCM/Employees · CI-4 · wwwroot/assets/js/CRM/Accounts/index.js`; R-9 `wwwroot/assets/js/dt-defaults.js`, and a path with `..`; R-10 the missing `wwwroot/assets/js/pages/enterprise-strategy/strategy-initiatives-api.js` |

Self-test cases to add, in throw-away repositories, prefix `FU01`: a class file referenced by one page; one case per JS key on it (CI-4, CI-5, CI-6, CI-7, CI-8, CI-9 `api/personalization`, CI-10); two owning pages (both red, once each); two views of one page (one line); a two-level and a single-level owner; `Url.Content` and `?v=` forms; a tag in `@* *@` and in `<!-- -->`; `data-src` not read; a variable path (`ölçülmedi`); Shared only and unreferenced (the two lines); a path missing at HEAD (named line, no exception); each row of part 7's table; a page selected through another file reads its class files; no other page's view read when no class file changed (`okunan` line); `FU-19` rows accepted and refused as R-1...R-10; an added unreferenced row exit 2, the same row unchanged `kapandı`; the draft gives `FU-19` for a class file on a single-level page and `FU-16` for a file of its own folder.

## Page Help (UXH-001)

Not applicable: no page. Tur yok.

## Ready-for-dev Checklist

- [ ] Before approval: sections complete (authorization, Gateway, lookup, audit, help, tour stated as not applicable); DCP-002 preflight OK; registry `Follow-up` row added (O-2).
- [ ] Before approval: independent review of this version UYGUN, no open ENGEL; `status: ready-for-dev` on the approved bytes; approval row with `whole_pack=<WP>`.
- [ ] Before dispatch: O-1 recorded; row count re-measured at the expected base; evidence script and oracle committed, the oracle's own sabotage recorded.

## Implementation Notes

- Measured at `0504c96eaf9251ea03ccb731c932823d964c4291`, clean tree: Python importing the selector read only (`map_path` over `git ls-tree`, `load_patterns`, `read_baseline`, `execute`), session scratch `fu01_measure.py`, `fu01_measure2.py`, `fu01_measure3.py`; the tower puts them in the review ZIP. The 106 and the debt match an earlier run at `1ee773e90` of a script that did not import the selector.
- Reference rule over 1530 views: 1305 captures, all prefix `~/`, 0 variable; the 14 other `assets/js/` literals are `<link ... .css>`; 2 commented-out tags excluded; no `.cshtml` outside `Views/`. `git grep -n "assets/js" -- 'frontend/Diten.Web/**/*.cs'` 0 hits. `git grep` of the JS tree for `getScript`, `import(`, `createElement('script')` 0 loaders; `assets/js/` there only in comments and error texts.
- Likely selector lines, for orientation, not a design: `:84`, `:298-347`, `:394`, `:493-494`, `:508-514`, `:801-813`, `:828`, `:872-902`, `:922-935`, `:953`; self-test `:1387-1446`.
- Not measured: the change itself; every sabotage line above (read from the code, not run); CI on a real pull request; time on a CI runner.

## Follow-up Items

| FU | Item | Owner | Regression if deferred |
|---|---|---|---|
| FU-19 | The class files' debt (22 rows at `0504c96ea`), fixed by each page's domain when next touched or by its own job (XMC-001) | Owning domains | 🟡 old debt stays; a new match is red |
| FU-20 | Delete or wire the 2 unreferenced files | enterprise-strategy-business-performance (`systemStatus.js` owner to confirm) | 🟢 no reader; a violation there stays invisible |
| FU-21 | Missing file named at `StrategyPeriodDetail.cshtml:132` (a 404 is inferred, not measured live) | enterprise-strategy-business-performance | 🟡 line printed whenever the page is selected |
| FU-22 | Follow the partial chain, if the 59 callers should be named | PSS-UI tower | 🟡 8 files stay under `ESBP/Components` |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi |
|---|---|---|---|
| DL-1 | Başvuruya dayalı sayfa JS eşlemesi | Owned Objects, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Acceptance Criteria, Test Expectations | E2 |
