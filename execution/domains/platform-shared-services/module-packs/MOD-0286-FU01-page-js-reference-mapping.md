---
id: MOD-0286-FU01
name: UI Compliance Page JS Reference Mapping
slug: page-js-reference-mapping
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
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

> **Revision 04, 2026-10-31: not approved until its row exists in `execution/approvals/approvals.tsv`; both gates stay closed.** The formal review runs on these bytes: a `status` change is not exempt
> from review (`scripts/pack_approval.py:941` `review_exempt`, exempt keys `:95-96`), so `status: ready-for-dev` is set in this version; the field is not the approval. Owner's word in the PSS-UI tower chat,
> 2026-10-31: "Evet". Header values follow the parent's reasons (`MOD-0286-ui-design-system-page-pattern-governance.md:32-36`). DCP-002 at `0504c96ea`: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286-FU01
> --name "UI Compliance Page JS Reference Mapping"` -> `OK  MOD-0286-FU01: proven against Blueprint/registry.`

## Why a separate pack, and what it supersedes

An accepted job covers only the exact pack version its record binds (`scripts/acceptance_record.py:1025-1028`, `:1043-1044`, `module_coverage`). A parent revision would leave the criteria of the three accepted jobs
(documents, CI, single-level) open on the new version until they rebind and rerun, so the parent stays byte-identical. An FU id is its own module for the tools (`scripts/ct_queue.py:1359-1364`, `scripts/acceptance_record.py:75`).

For **this pack's work only** it supersedes these parent statements (lines at `0504c96ea`):

| Parent statement | Line | Here |
|---|---|---|
| Owned Objects, DL-3 part 1, "Not mapped in rev 15" (1)(2)(2b)(3) | `:336-346` | The 106 files map by reference; their own lines change |
| "DL-3 tek seviye sayfalar": "They get the FU-18 line", "Not mapped in rev 15", "`pages/` decision" | `:554-578` | Same; reference reading is chosen, moving JS is not |
| Repo Scope: "FU-18 line" among the rev 15 changes | `:658` | That line is replaced by this pack's own lines |
| Follow-up row FU-18 | `:966` | Closed by this pack; what stays unchecked moves to FU-23 |
| CI-4 tail on `Meetings/series/index.js`; "left out by name" on the 106 files | `:488`, `:516-518` | These files are page JS of the pages that reference them |
| CI table "page JS" cells; `fu` lists of Validation Rules and AC-19 | `:488-494`, `:739-757`, `:823` | Widened only by this pack's `FU-19` row |

AC-21 (b)'s count of 106 and AC-21 (g)'s FU-18 cases stay true for the commit they were accepted at and are not re-judged. Everything else in the parent binds, above all
DL-3 part 2's count rule and the pattern block the selector reads (`:611-644`).

## Open Decisions (Owner Decision Pending)

| # | Decision | Who | Proposed answer | Status |
|---|---|---|---|---|
| O-1 | Name of the owner decision for the `FU-19` rows, and each row's owner | Owner | Tower asks; proposal in Owned Objects, part 6 | Precondition of dispatch; the pack holds only a placeholder |
| O-2 | `Follow-up` row for MOD-0286-FU01 in `execution/registries/module-id-registry.md` | Owner (registry owner: Lead Architect) | Add in its own approved commit; the rule says before a pack is created, this pack comes first on the tower's instruction | Precondition of approval |
| O-3 | The 2 unreferenced files and the missing file named at `StrategyPeriodDetail.cshtml:132` | Owning domains (XMC-001) | Not here: FU-20, FU-21 | Does not block this pack |

## Module Summary

The selector prints `eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) <path>` for 106 JS files, so a new violation in them is never red. At `0504c96ea` 102 of the 106 map by a
`<script src>` in a view: 95 to one page, 7 to several, 22 pages, 113 (file, page) pairs. Debt that surfaces: 17 files, 21 file-check rows, 89 matches = 22 page-keyed rows, 92 matches, about 1% on the 2153-row baseline.

## Ownership and Boundaries

- In-scope: mapping those 106 files by reference; the baseline reader change; the `FU-19` rows; self-test cases.
- Out-of-scope: moving any JS; FU-17 (area-less verifier); name tables; fixing any old red; runtime code; `.github/`; CI-3 and CI-9's `savedView` half, which stay on the page's own folder.

## Owned Objects

**1. The class.** A "class file" is a path `map_path` returns as `fu18` today (`scripts/uic_changed_pages.py:892`, `:897`, `:902`); the path rule does not change. A reference brings in nothing else: `js/shared/**`, top-level files and another page's own folder stay as they are.

**2. Reference rule, and which view counts (Decision 1).** Binding raw strings, Python `re`: `STRIP` with `re.S` is removed first, `SCRIPT` runs with `re.I | re.S`, text as `text_of` (`:227`); capture `c` names `frontend/Diten.Web/wwwroot/` + `c`. `VARIABLE` (same flags) runs on the `STRIP`ped text with every `SCRIPT` match removed, so a `SCRIPT` capture wins for its tag. The selector holds the three strings in one module-level dict literal named exactly `FU01_REF_PATTERNS`, keys `STRIP`, `SCRIPT`, `VARIABLE`, plain string-literal values, equal byte for byte to this block (AC-13). Every run compiles the three patterns from this dict with the flags above, and the selector holds no other copy of them.

```text
STRIP	@\*.*?\*@|<!--.*?-->
SCRIPT	<script\b[^>]*?\ssrc\s*=\s*["']\s*(?:@Url\.Content\(\s*["'])?(?:~?/)?(assets/js/[^"'?#@{(\s]+?\.js)(?=["'?#])
VARIABLE	<script\b[^>]*?\ssrc\s*=\s*(?!["']?\s*(?:https?:)?//)(?:[^>]*?assets/js/|(?!["']\s*@Url\.Content\(\s*["'][^"']*["']\s*\)\s*["'])["']?[^"'>\s]*?(?<!@)@(?!@))
```

**Only a `.cshtml` whose own path belongs to the page counts**, by the rule the page's checks read (two-level: `Views/{Area}/{Module}/**`; single-level: files directly in `Views/{Module}/`). `Views/Shared/**`, `Views/Archive/**` and the two root files belong to no page.
**The partial chain is not followed:** a partial name is a Razor expression, and following it means parsing every view whenever a page is selected, against Decision 7. What it misses, measured: 8 mapped files are referenced only from `EnterpriseStrategyBusinessPerformance/Components/_SubdomainShell.cshtml` or `_Scripts.cshtml`; 59 files name `_SubdomainShell`, and these callers are neither named nor checked when such a file changes. The violation is still red, under page `EnterpriseStrategyBusinessPerformance/Components`, because the check runs on the file.
Captured, so J maps: `~/assets/js/a/x.js`, `/assets/js/a/x.js`, `@Url.Content("~/assets/js/a/x.js")`, and `~/assets/js/a/x.js?v=@ver` (a query never decides). A `VARIABLE` match is no path: `ölçülmedi <page> değişken script yolu <view>`, once per view, only for a view of a selected page, never red (0 today). It covers an unescaped `@` in the value (`src="@x"`, `src="@Url.Content(Model.X)"`, `src="~/assets/js/@x.js"`) and any local tag naming `assets/js/` that `SCRIPT` did not capture (`@Url.Content("~/assets/js/" + name + ".js")`, unquoted `src=~/assets/js/a/x.js`, `../assets/js/a/x.js`, `~/assets/js/a/data.json`). No reference and no line: `http://`, `https://` and `//` URLs (`https://cdn.x/assets/js/lib.js`, a CDN version with `@`), `@@`, a literal `@Url.Content("~/assets/vendor/a.js")`. Limit: a load outside `<script src` is not seen (no loader measured, Implementation Notes).

**3. Scope of a selected page.** The JS of its line checks (CI-4, CI-5, CI-6 JS, CI-7, CI-8, CI-9 `api/personalization`, CI-10) = its own-folder JS as today + every class file its own views reference at HEAD; `evaluate_page` reads only `js/{page}/` today (`:514`), so mapping alone would run no check. A referenced path absent at HEAD is not read (`Tree.read` gives `None`, `text_of` would fail): `başvurulan JS dosyası yok <page> <instance>`, never red. One exists today: `Views/EnterpriseStrategyBusinessPerformance/StrategyPeriodDetail.cshtml:132`.

**4. Own lines (Decision 2).** A changed class file present at HEAD with no page reference at HEAD keeps its own line, with the full repo path (a class file gone from HEAD has no such line: part 7 closes its rows, and the scan lines of part 9 still print):

- `eşlenmemiş sayfa JS'i, yalnız Shared görünümü yüklüyor, sayfa kontrolü yok (MOD-0286-FU01) <path>`: today `pages/enterprise-strategy/esbp-horizon-dates.js` (`_Layout`), `pages/governance/permissions.js` (`_PermissionBootstrap`).
- `eşlenmemiş sayfa JS'i, hiçbir görünüm yüklemiyor, sayfa kontrolü yok (MOD-0286-FU01) <path>`: today `pages/systemStatus.js`, `pages/enterprise-strategy/project-ppm-form-init.js`.
- `eşlenmemiş sayfa JS'i, yalnız Archive ya da kök görünüm yüklüyor, sayfa kontrolü yok (MOD-0286-FU01) <path>`: 0 files today. A Shared reference wins over these two kinds.

The `(FU-18)` text (`:84`) does **not** stay: it cites a row this pack closes. A page reference wins over all of these. A Shared file does not select pages: that is all pages, against parent AC-20; the 4 files carry no debt today (0 matches of the seven JS keys), but a Shared-only file cannot go red: FU-23 carries that.

**5. Several pages (Decision 3): one row per page + check + file.** Each owning page is evaluated once, with one line per check for the file however many of its views reference it. `n` is the file's count; each row's `b` is compared alone (`:763`), so one added match is one `YENİ KIRMIZI <page> <check> <instance> <n> +1` per owning page. **No double-count trap:** rows are never pooled and no row's budget absorbs another page's match; a stale high row on one page leaves the others red. Cost: the number of `YENİ KIRMIZI` lines counts owning pages, not violations (the exit reads only that one exists, `:870`); `--check-baseline`'s `eşleşme` (`:953`) sums rows, so it grows by 92 for 89 real matches, the 3 extra being `modal-form-utils.js` on two pages; a lowered count is edited in each row. Not one row per file: `self.rows` is keyed by page + check (`:494`), and a page-less row could not close when one page drops the file.

**6. Rows (Decision 6).** `fu` = `FU-19`, defined in Follow-up Items (the parent's list ends at FU-18, `:966`; a later parent revision starts at FU-24). `reason` = `sahip kararı <sahip karar adı> (<tarih>)`, filled from O-1, never `pss-mod0286-taban-01` or `tek-seviye-borc-01`. Reader, mapping, draft-mode change and rows land in **one pull request**, as parent AC-21 (a): mapping first makes each pull request on these pages red on old debt (inference from the 22 rows); rows first gives exit 2 on every pull request (measured on a scratch copy: `--check-baseline` exit 2, `HATA: taban satır 2155: CI-4 satır kontrolü instance'ı sayfanın dosya yolu değil`). In draft mode a class-file row gets `FU-19`, owner `belirsiz — sahip belirleyecek` (the worker fills it from O-1) and as `reason` one constant holding the filled O-1 text, as `SINGLE_REASON` (`:89`) does; both branches come before the single-level choices of `owner_of` (`:394`) and of `reason` (`:936`), else an ESBP row would be `FU-16`.
Proposal for O-1, from each page's present rows: EnterpriseStrategyBusinessPerformance 11 and `.../Components` 3 (no present row, by area) enterprise-strategy-business-performance; DemandIdeas 3, DeliveryExecutionManagement 1, DecompositionTreeBuilder 1 portfolio-delivery; ManagementGovernance/ProcessModeling 1 management-governance; Meetings/Series 1, Meetings/MeetingTypes 1 platform-shared-services.

**7. A reference that changes (Decision 5).** The two sides are computed only for a page with a changed view in the diff; its own views are read at HEAD and at the base. For a view of page P and class file J:

| Case | J read under P | Output | Exit effect |
|---|---|---|---|
| P gains J, `n = 0` | yes | `başvuru eklendi P J`, `PASS P <check> J 0` | none |
| P gains J, `n > 0`, no row | yes | `başvuru eklendi P J`, `YENİ KIRMIZI P <check> J <n> +<n>` | 1: taking on debt needs an owner decision |
| P gains J, `n > 0`, row with count `b` (the revert of SB-6) | yes | `başvuru eklendi P J`; the row rule: `TABAN FU-19 P <check> J <n>` when `n = b`, `YENİ KIRMIZI P <check> J <n> +<n-b>` when `n > b` | 1 only when `n > b` |
| A view drops J, another view of P keeps it | yes | check lines as usual, no `başvuru` line | none |
| P's last reference to J goes | no | `başvuru kalktı P J`; each row of P on J `kapandı - satır silinmeli P <check> J` | none: closed, never dropped |
| J deleted, P still references it | no | `başvurulan JS dosyası yok P J`; rows closed as above | none |
| J renamed to K, the view updated in the same pull request | K only | `başvuru eklendi P K`, `başvuru kalktı P J`; K has no row: `YENİ KIRMIZI P <check> K <n> +<n>` when `n > 0`, `PASS P <check> K 0` when `n = 0`; each row of P on J closed | 1 when K has `n > 0`: parent's rename rule |
| J renamed to K, the view not updated | neither | `başvurulan JS dosyası yok P J`, rows of P on J closed; K's own `hiçbir görünüm` line (part 4); no `başvuru` line | none |

Closed and not red because dropping a load adds no violation; a removed page keeps the parent's `sayfa kaldırıldı` closures. **Limit, on purpose:** moving a reference to `Views/Shared/**` prints `başvuru kalktı`, and J then has only the Shared line (FU-23).

**8. Baseline reader (Decision 4).** A row with `fu` other than `FU-19` takes today's path unchanged, so no present refusal loosens. A `FU-19` row is tested in this order; a refusal is exit 2 with `HATA: taban satır <n>: <text>`:

- *Form, every run, no git:* `check` in CI-4, 5, 6, 7, 8, 9, 10, else T-1 `FU-19 satırında kontrol CI-4, 5, 6, 7, 8, 9 ya da 10 olmalı`; `page` a page key whose first segment is outside `NOT_PAGE_AREAS` and `count` an integer >= 1, else T-7 `FU-19 satırında page ya da count bozuk`; `instance` under `wwwroot/assets/js/<folder>/`, ending `.js`, no `:`, normal form (`_norm_ok`), top folder outside `NOT_PAGE_AREAS`, else T-2 `FU-19 instance'ı wwwroot/assets/js/<klasör>/ altında .js yolu değil`.
- *Not the page's own JS, same layer:* else T-3 `FU-19 instance'ı sayfanın kendi klasöründe`. Own JS is what `evaluate_page` reads already: for a two-level page anything under `js/{Area}/{Module}/`, at any depth; for a single-level page only a file **directly** in `js/{Module}/`, so `WorkCenterNext` + `js/WorkCenterNext/fixtures/**` (10 pairs) is a legitimate reference row.
- *Class and reference, CI:* only for rows **added or changed against the base's baseline** (`git show`), reading only those pages' views: a class file, else T-4 `FU-19 instance'ı sınıf dosyası değil`; present at HEAD, else T-5 `FU-19 instance'ı HEAD'de yok`; referenced by a view of the row's own page, else T-6 `FU-19 instance'ı sayfanın görünümlerinde başvurulmuyor`.
- *Class and reference, `--check-baseline`:* the same three tests on all `FU-19` rows, reading every view; CI never runs it.

CI does not test unchanged rows because a stale row must not stop a pull request that did not make it stale; it prints `kapandı` when its page is selected, as the parent's stale rows do.

**9. CI (Decision 7).** Changed pages only: no all-pages mode, flag, variable or value (parent AC-20). All views are read **only** when the diff names a class file, at HEAD and at the base, to find the pages that reference it. When the diff selects a page, a shared file or a class file, two scan lines print before `süre:`: `başvuru taraması yok` or `başvuru taraması: HEAD <n> görünüm, taban <m> görünüm, <s> s`; and `okunan görünüm metni: HEAD <a>, taban <b>`, counted from the `.cshtml` paths held in each `Tree.cache`. Any other run prints no scan line: a diff that selects none of these prints what it prints today, and an empty diff prints only `süre: <s> s`, exactly as today (`:1094-1095`, parent AC-20). Cost at `0504c96ea`: 1530 views, 7.46 MB, 0.043 s to read + 0.059 s to scan one tree; checks with view text already read: all 22 pages 1.21 s, the 5 of `trace-ui.js` 0.30 s, the 3 of `strategy-apis.js` 0.008 s. **The workflow does not change:** `.github/workflows/ui-compliance.yml` already filters on `Views/**` (`:10`), `wwwroot/assets/js/**` (`:11`), the selector (`:16`) and the baseline (`:17`), keeps full history (`:30`) and runs `--base "$BASE_SHA"` (`:38`).

## Entity Fields

None. The deliverables are one script change and baseline rows.

## Repo Scope

Worker writes only: `scripts/uic_changed_pages.py` (with `--self-test`), `tests/ui-compliance-baseline.tsv`. Tower only: the owner decision record under `docs/records/decisions/`; the evidence script under `docs/records/audits/<YYYY-MM>/mod-0286-fu01-01/`; the oracle `docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py` (the earlier `uic-fu01-oracle.py` stays as a record). The worker changes neither.

## Protected Paths

`.antigravity/**`, `execution/registries/**`, `.github/**`, `frontend/Diten.Web/**` (read only), `services/**`, `gateway/**`, the parent pack (byte-identical). Other domains' fixes go to their owner (XMC-001).

## Dependencies

- The parent's selector and baseline on main at `0504c96ea` or later; the parent's pattern block, read at run time.
- **Registry row (O-2):** `execution/registries/module-id-registry.md:30` "requires reserving an ID here before creating a Module Pack". No tool measured here enforces it: `verify_module_id.py` proves the id from the parent, `ct_queue.py:251` accepts it from a pack.
- **Before dispatch:** owner decision and row owners (O-1); the tower's evidence script committed. The oracle `uic-fu01-oracle-02.py` is committed at `6cbe29914`.
- One slice needs `whole_pack=<WP>` on the approval row (`scripts/pack_approval.py:387-388`), which counts HIGH (`:365`). One slice because reader, mapping and rows must be one pull request.

## Runtime Constraints

Local temporary clone and the CI job only. Python standard library + git; no service, port, network or secret. Reads, never writes.

## Layout & Shell Contract

`shell: none`: no Razor view and no layout. CI-1 checks other pages' explicit layout and is not changed here.

## Backend File Convention

None: no backend.

## Frontend File Contract

None: no view and no `data_mode`. `frontend/Diten.Web/**` is only read. Lookup: none; no select, filter or default value, so no `/api/lookups` consumer and no fallback list.

## Validation Rules

No field. Baseline: the parent's rules (`:739-757`) stand; the `FU-19` row is defined in Owned Objects, part 8. Line and refusal texts are those of Owned Objects, parts 2, 3, 4, 7, 8, 9, byte for byte; instances are relative to `frontend/Diten.Web/`, as baseline rows. The two scan lines print only when the diff selects a page, a shared file or a class file; an empty diff's only line is `süre: <s> s`.

## Failure Path to Verify

- **Duplicate** - a repeated page + check + instance: exit 2 (existing rule). A view naming J twice, or several views of a page: one line per page and check. **Missing** - a referenced file absent at HEAD: named line, no exception. A class file with no reference: its own line.
- **Malformed or unreferenced row** - SB-13: exit 2 with its own text, so a row does not pass this test for another reason (R-0a, R-0b are the controls). A stale row unchanged in a pull request: `kapandı`, exit unaffected; in `--check-baseline`: exit 2.
- **Hidden violation** - a match on a counted line: `n > b` (SB-3, SB-4). One removed and one added in a file: `TABAN`, the parent's accepted limit.
- **Silent loss** - last reference gone, or J renamed with or without its view: lines of part 7 (SB-6). A commented-out tag is no reference (SB-12). A local `src` the rule cannot read (variable, concatenated, unquoted, relative, not `.js`): `ölçülmedi` (part 2). A `?v=@x` query keeps its reference. An external URL is no reference. A Shared-only file cannot go red: its own line, FU-23.
- **Wrong order** - rows without the reader: exit 2; mapping without rows: old debt red (part 6).
- **Second copy of the patterns** - a copy other than the dict is caught by SB-14: the behaviour case does not fail, so the sabotage does.
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

Each is measured in a temporary clone at the work's commit by the tower's evidence script, which calls the oracle `docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py` by its modes (below, `oracle <mode>`); SB ids are the sabotage table below. A number written "at `0504c96ea`" is that commit's value; the binding value is the named oracle mode's at the work's base, so a moved base does not contradict the criterion. Against that table a run passes when its exit matches, each named line is present (`grep -Fx`, `<J>` expanded) and each named negative ("no ...") is absent; other `PASS` and `TABAN` lines of the selected pages are expected.

- [ ] AC-1 (zorunlu) — Complete mapping: one commit appends one empty line to each of the 106 class files; `python3 scripts/uic_changed_pages.py --base <its parent>` exits 0, `grep -c '^YENİ KIRMIZI '` is 0, every (page, file) pair of `oracle ciftler <base>` has exactly one `CI-4` line `PASS` or `TABAN FU-19` with that page and instance (113 pairs, 22 pages at `0504c96ea`), `grep -c '^TABAN FU-19 '` equals the row count of `oracle satirlar <base>` (22 at `0504c96ea`), `grep -c '(FU-18)'` is 0.
- [ ] AC-2 (zorunlu) — The 4 unmapped files: SB-11 gives exit 0, no `YENİ KIRMIZI`, and each file's line of Owned Objects, part 4; `grep -c 'MOD-0286-FU01'` is 4.
- [ ] AC-3 (zorunlu) — New violations: SB-1, SB-2, SB-3, SB-4, SB-8, SB-9 each exit 1 with the table's lines, each owning page named once, and `grep -c '^YENİ KIRMIZI '` equal to the count `oracle turetim <base>` prints for it on stderr (`kırmızı`; at `0504c96ea` 1, 5, 2, 1, 2, 4); each revert exits 0. SB-1 on the parent's selector and baseline exits 0 with its `(FU-18)` line, so the red comes from this work.
- [ ] AC-4 (zorunlu) — Baseline holds, references change: SB-5, SB-6, SB-7, SB-12 exit 0 with the table's lines and without its negatives; the revert of SB-6 prints its named lines; after SB-6, a further commit touching page `DecompositionTreeBuilder` exits 0 with the `kapandı` line while `--check-baseline` exits 2.
- [ ] AC-5 (zorunlu) — Regression: SB-10 (a) stdout equals stdout of the same edit on the parent's selector and baseline, after dropping lines starting `süre:`, `başvuru taraması`, `okunan görünüm metni`; SB-10 (b), (c) exit 1 with the named lines.
- [ ] AC-6 (zorunlu) — Reader: each row of the SB-13 table, appended alone to the work's baseline, gives its `--check-baseline` result: R-0a and R-0b exit 0, every other row exit 2 with its table text on the `HATA: taban satır <n>: ` line, `<n>` the appended row's line; R-1, R-5 and R-10, committed, give the same text under `--base <parent>`; the unchanged baseline gives `--check-baseline` exit 0.
- [ ] AC-7 (zorunlu) — Row set by an independent oracle: `oracle satirlar <work>`, importing no selector code, computes (page, check, instance, count) from the git tree, Owned Objects parts 1-2 and the parent's pattern block; it equals the `FU-19` rows both ways, and the `FU-19` rows of `--draft-baseline`. A planted extra row and a planted lowered count each make the comparison fail, recorded. The oracle reads `STRIP` and `SCRIPT` from the pack by construction, so this criterion does not measure the selector's copy; AC-13 does.
- [ ] AC-8 (zorunlu) — One pull request, old rows untouched: `git diff --numstat <base> -- tests/ui-compliance-baseline.tsv` shows only added lines, as many as the rows of `oracle satirlar <base>`; `awk -F'\t' '$6 == "FU-19"' tests/ui-compliance-baseline.tsv` holds no `taban-01`, `tek-seviye-borc-01` or `belirsiz`; the script takes O-1's decision name and its page-to-owner mapping as parameters, and on every `FU-19` row (22 at `0504c96ea`) `reason` names that decision and `owner` equals the mapping's value for `page`, a page missing from the mapping being a failure; the other `fu` values keep their row counts (at `0504c96ea`: FU-14 1953, FU-16 134, FU-4 64, FU-12 1, FU-15 1).
- [ ] AC-9 (zorunlu) — Changed pages only: SB-10 (a) prints `başvuru taraması yok` and `okunan görünüm metni: HEAD 19, taban 0`; the AC-1 run prints `başvuru taraması: HEAD 1530 görünüm, taban 1530 görünüm, <s> s` and `süre:` (counts at `0504c96ea`; binding values `oracle gorunum <base> CRM/Accounts` and `oracle gorunum <base>`); `--base HEAD` on the work's commit (empty diff) exits 0 and its stdout is one line matching `^süre: [0-9]+\.[0-9] s$`; `--help` lists the same four modes as today.
- [ ] AC-10 (zorunlu) — Self-test: `python3 scripts/uic_changed_pages.py --self-test` exits 0, `grep -c '^FAIL '` is 0, and for each id `FU01-01` to `FU01-67` of Test Expectations `grep -c '^PASS FU01-<nn> '` is at least 1. On the source, by `git diff -U0 <base> <work> -- scripts/uic_changed_pages.py` limited to `self_test`: the removed lines are exactly the base's `FU18` definition and the positive assertions of AC-21g03 (first case), g06, g07, g08 (`:1351`, `:1388`, `:1430`, `:1438`, `:1446` at `0504c96ea`); their replacements name `NO_VIEW_LINE`, whose value holds `hiçbir görünüm`; `FU18` becomes the prefix `eşlenmemiş sayfa JS'i, `, so the negative assertions cover all three lines of part 4; no case name of the base is missing.
- [ ] AC-11 (zorunlu) — Scope: `git diff --name-only <base> <work>` lists only the two Repo Scope worker files; the parent pack and `.github/workflows/ui-compliance.yml` have the same blob at both commits.
- [ ] AC-12 (zorunlu) — CI: the `ui-compliance` job on the pull request's head exits 0; the acceptance record names run id, head SHA and the artefact's SHA-256.
- [ ] AC-13 (zorunlu) — The selector's copy: `python3 -B docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py secici <work>` prints `aynı` and exits 0 (it reads the selector with `ast` and imports nothing). SB-14 a to d: each mutation of the table, one compilable scratch commit changing only that value, makes that command exit 1 with `farklı<TAB><key>` and makes `--self-test` exit non-zero with `FAIL FU01-67 ...` and a FAIL on the row's behaviour case, which fails only if the run really reads the dict. SB-14 e shows a real run on the real tree reads the dict; recorded.

## Test Expectations

**Today at `0504c96ea` (origin/main when written):** class files 106 = pages 80 + deeper 17 + area 9; one page / several / Shared only / none: 95 / 7 / 2 / 2 (pages 73 / 3 / 2 / 2, deeper 17 / 0 / 0 / 0, area 5 / 4 / 0 / 0). Page-keyed debt 22 rows / 92 matches: CI-4 2 / 2, CI-5 4 / 4, CI-6 11 / 77, CI-7 4 / 6, CI-10 1 / 3. Baseline 2153 rows, 858 matches; after the work at this commit 2175, 950. `--draft-baseline` here equals the baseline's 2153 keys and counts, so no other red surfaces on the 22 pages. If main moves, the numbers of `oracle satirlar`, `ciftler` and `siniflar` at the work's base replace these. No .NET build or browser run: nothing runtime changes. Runs are kept with `scripts/run_record.py`.

Sabotage set: commits on a scratch branch of the clone, never pushed. A Commit path is under `frontend/Diten.Web/wwwroot/assets/js/` unless it starts `Views/`; `<J>` = that file's instance. Each run `--base <parent>`; each revert `--base <the sabotage commit>`, exit 0. The table's numbers and anchors are those at `0504c96ea`. `oracle turetim <base>` derives every `<n>` and `+<d>` of SB-1 to SB-6, SB-8, SB-9, SB-10, the owning pages of SB-1, SB-2, SB-3, SB-9, AC-3's counts (stderr `kırmızı`) and the anchors `:113`, `:3372` (stderr `çapa`); `oracle gorunum <base>` derives the `19` and `1530` of AC-9. The evidence script itself re-finds by text `Index.cshtml:179` and the tags of SB-7, SB-9; the oracle does not. A difference from the table is reported and the derived value is used.

| Id | Commit | Exit | Named lines |
|---|---|---|---|
| SB-1 | `alert('x');` appended to `Platform/PersonReferences/person-reference-picker.js` | 1 | `YENİ KIRMIZI HCM/Employees CI-7 <J> 1 +1` |
| SB-2 | `alert('x');` appended to `Traceability/trace-ui.js` | 1 | `YENİ KIRMIZI Traceability/<P> CI-7 <J> 1 +1` for each P of LotMigration, LotMovements, LotTrace, Lots, Serials |
| SB-3 | ` a.style.b;` appended to the first `\.style\.` line of `pages/enterprise-strategy/modal-form-utils.js` (`:113`) | 1 | `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance CI-6 <J> 4 +1`; `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance/Components CI-6 <J> 4 +1` |
| SB-4 | Same on `pages/enterprise-strategy/goals.js` (`:3372`) | 1 | `YENİ KIRMIZI EnterpriseStrategyBusinessPerformance CI-6 <J> 12 +1` |
| SB-5 | Empty line appended to `pages/enterprise-strategy/goals.js` | 0 | `TABAN FU-19 EnterpriseStrategyBusinessPerformance CI-6 <J> 11`; `TABAN FU-19 EnterpriseStrategyBusinessPerformance CI-5 <J> 1`; no `YENİ KIRMIZI` |
| SB-6 | Tag of `pages/decomposition/decompositionPage.js` deleted at `Views/DecompositionTreeBuilder/Index.cshtml:179` | 0 | `başvuru kalktı DecompositionTreeBuilder <J>`; `kapandı - satır silinmeli DecompositionTreeBuilder CI-7 <J>`; no `TABAN FU-19 DecompositionTreeBuilder CI-7 <J> 1`. Revert: `başvuru eklendi DecompositionTreeBuilder <J>`; `TABAN FU-19 DecompositionTreeBuilder CI-7 <J> 1` |
| SB-7 | Tag of `CRM/cycle-dates.js` deleted from `Views/CRM/CyclePeriods/Details.cshtml` (`Index` keeps it) | 0 | `PASS CRM/CyclePeriods CI-4 <J> 0`; no `başvuru kalktı` |
| SB-8 | Tag of `pages/enterprise-strategy/goals.js` added to `Views/CRM/CyclePeriods/Index.cshtml` | 1 | `başvuru eklendi CRM/CyclePeriods <J>`; `YENİ KIRMIZI CRM/CyclePeriods CI-5 <J> 1 +1`; `YENİ KIRMIZI CRM/CyclePeriods CI-6 <J> 11 +11` |
| SB-9 | Tag of `Traceability/trace-ui.js` deleted from `Views/Traceability/Serials/Index.cshtml`, `alert('x');` appended to the file | 1 | SB-2's line for the other 4 pages; `başvuru kalktı Traceability/Serials <J>`; no CI-7 line of Serials on J |
| SB-10 | (a) Empty line on `CRM/Accounts/index.js`; (b) `alert('x');` there; (c) `alert('x');` on `Recall/cases.js` | 0, 1, 1 | (a) AC-5, AC-9; (b) `YENİ KIRMIZI CRM/Accounts CI-7 <J> 1 +1`; (c) `YENİ KIRMIZI Recall CI-7 <J> 1 +1` |
| SB-11 | `alert('x');` appended to the 4 files of Owned Objects, part 4 | 0 | The 4 lines; none of those paths on a check line |
| SB-12 | `@* <script src="~/assets/js/pages/enterprise-strategy/goals.js"></script> *@` added to `Views/CRM/CyclePeriods/Index.cshtml` | 0 | No `başvuru eklendi`; no goals.js line of that page |
| SB-13 | Rows of the table below, one at a time (AC-6) | 0 or 2 | The row's text |
| SB-14 | a to d: each one scratch commit changing only that value of `FU01_REF_PATTERNS`, still compiling (AC-13). Runs: `oracle secici <the commit>` and `--self-test`, not `--base`; revert: `aynı`, exit 0 | 1 | `farklı<TAB><key>`; `--self-test` non-zero with `FAIL FU01-67 ...` and a FAIL on the row's case |
| SB-14a | `STRIP`: `@\*.*?\*@` becomes `@\#.*?\*@` | 1 | `farklı<TAB>STRIP`; `FAIL FU01-15 ...` |
| SB-14b | `SCRIPT`: `@Url\.Content` becomes `@Urx\.Content` | 1 | `farklı<TAB>SCRIPT`; `FAIL FU01-13 ...` |
| SB-14c | `VARIABLE`: `[^>]*?assets/js/\|` becomes `[^>]*?assets/jz/\|` (`\|` is the table escape of the pattern's bar) | 1 | `farklı<TAB>VARIABLE`; `FAIL FU01-62 ...` |
| SB-14d | `VARIABLE`: `(?<!@)@(?!@)` becomes `(?<!@)#(?!@)` | 1 | `farklı<TAB>VARIABLE`; `FAIL FU01-19 ...` |
| SB-14e | `SCRIPT`: `(assets/js/` becomes `(assets/jz/`, then SB-1's commit on top; run `--base <its parent>`; then the same with the dict restored | Not claimed for the mutated run (not run) | Mutated: `YENİ KIRMIZI HCM/Employees CI-7 <J> 1 +1` absent. Restored: present |

SB-13 rows: seven cells, tab-separated; `reason` is `sahip kararı sb-13 (2026-10-31)` and `owner` is `platform-shared-services` in every row. `<e>` = `wwwroot/assets/js/pages/enterprise-strategy`.

| R | page | check | instance | count | fu | Result (text after `taban satır <n>: `) |
|---|---|---|---|---|---|---|
| R-0a | DecompositionTreeBuilder | CI-4 | `wwwroot/assets/js/pages/decomposition/decompositionPage.js` | 1 | FU-19 | Accepted, exit 0: control |
| R-0b | WorkCenterNext | CI-7 | `wwwroot/assets/js/WorkCenterNext/fixtures/canonical-fixtures.js` | 1 | FU-19 | Accepted, exit 0: control, subfolder of a single-level page |
| R-1 | HCM/Employees | CI-7 | `wwwroot/assets/js/Platform/PersonReferences/person-reference-picker.js` | 1 | FU-14 | `CI-7 satır kontrolü instance'ı sayfanın dosya yolu değil` (today's text) |
| R-2 | Home | CI-2 | `Views/Home/Status.cshtml` | 1 | FU-19 | T-1 |
| R-3 | Home | CI-6 | `Views/Home/Status.cshtml` | 1 | FU-19 | T-2 |
| R-4a | CRM/Accounts | CI-7 | `wwwroot/assets/js/CRM/Accounts/details-contacts.js` | 1 | FU-19 | T-3: two-level, direct file |
| R-4b | CRM/Accounts | CI-7 | `wwwroot/assets/js/CRM/Accounts/sub/x.js` | 1 | FU-19 | T-3: two-level, any depth |
| R-4c | Recall | CI-7 | `wwwroot/assets/js/Recall/cases.js` | 1 | FU-19 | T-3: single-level, direct file |
| R-5 | Home | CI-7 | `wwwroot/assets/js/pages/systemStatus.js` | 1 | FU-19 | T-6: unreferenced |
| R-6 | Home | CI-6 | `wwwroot/assets/js/pages/governance/permissions.js` | 1 | FU-19 | T-6: Shared only |
| R-7 | Home | CI-6 | `wwwroot/assets/js/shared/diten-tags.js` | 1 | FU-19 | T-4 |
| R-8 | HCM/Employees | CI-4 | `wwwroot/assets/js/CRM/Accounts/index.js` | 1 | FU-19 | T-4: another page's own JS |
| R-9a | Home | CI-6 | `wwwroot/assets/js/dt-defaults.js` | 1 | FU-19 | T-2: no folder |
| R-9b | Home | CI-6 | `wwwroot/assets/js/pages/../x.js` | 1 | FU-19 | T-2 |
| R-10 | EnterpriseStrategyBusinessPerformance | CI-6 | `<e>/strategy-initiatives-api.js` | 1 | FU-19 | T-5: referenced, file missing |
| R-11 | Shared/X | CI-6 | `wwwroot/assets/js/pages/systemStatus.js` | 1 | FU-19 | T-7 |
| R-12 | Home | CI-6 | `wwwroot/assets/js/pages/systemStatus.js` | 0 | FU-19 | T-7 |

Self-test cases to add, in throw-away repositories, each case name starting with its id: FU01-01 class file with a two-level owner, red through it; FU01-02 with a single-level owner; FU01-03 in a subfolder of a single-level page's JS folder, referenced by it; FU01-04 to FU01-10 one per JS key on a referenced file, in order CI-4, CI-5, CI-6, CI-7, CI-8, CI-9 `api/personalization`, CI-10; FU01-11 two owning pages, both red, once each; FU01-12 two views of one page, one line; FU01-13 `Url.Content` form; FU01-14 `?v=1` form; FU01-15 tag in `@* *@`; FU01-16 tag in `<!-- -->`; FU01-17 `data-src` not read; FU01-18 `src="~/assets/js/@x.js"`, `ölçülmedi`; FU01-19 `src="@x"`; FU01-20 CDN URL with `@`, not variable; FU01-21 Shared only line; FU01-22 unreferenced line; FU01-23 Archive or root only line; FU01-24 path missing at HEAD, named line, no exception; FU01-25 to FU01-32 part 7's table, one per row in order; FU01-33 a page selected through another file reads its class files; FU01-34 no other page's view read when no class file changed (`okunan` line); FU01-35 an added unreferenced row exit 2 under `--base`, the same row unchanged `kapandı`, `--check-baseline` exit 2; FU01-36 draft gives `FU-19` and the O-1 reason for a class file on a single-level page, `FU-16` for a file of its own folder; FU01-37 to FU01-53 the SB-13 rows in table order, on fixtures of the same shape, each asserting its result text; FU01-54 empty diff, the only line is `süre:`, and a diff of one shared file prints the two scan lines; FU01-55 `?v=@x` keeps the reference, no `ölçülmedi`; FU01-56 `@Url.Content("~/assets/js/" + name + ".js")`, `ölçülmedi`; FU01-57 unquoted `src=~/assets/js/a/x.js`, `ölçülmedi`; FU01-58 `https://cdn.x/assets/js/lib.js`, no reference and no line; FU01-59 `~/assets/js/a/data.json`, `data.js` not read, `ölçülmedi`; FU01-60 `/assets/js/a/x.js` captured, J maps; FU01-61 `src="@Url.Content(Model.X)"`, `ölçülmedi`; FU01-62 `../assets/js/a/x.js`, `ölçülmedi`; FU01-63 `http://cdn.x/assets/js/lib.js`, no reference and no line; FU01-64 `//cdn.x/assets/js/lib.js`, no reference and no line; FU01-65 `src="@@x.js"`, no reference and no line; FU01-66 `@Url.Content("~/assets/vendor/a.js")`, no reference and no line; FU01-67 part 2's block of this pack parsed by the self-test (as the parent's pattern-block case, `scripts/uic_changed_pages.py:1310-1316`) equals `FU01_REF_PATTERNS`, and all three compile with part 2's flags.

## Page Help (UXH-001)

Not applicable: no page. Tur yok.

## Ready-for-dev Checklist

- [ ] Before approval: sections complete (authorization, Gateway, lookup, audit, help, tour stated as not applicable); DCP-002 preflight OK; registry `Follow-up` row added (O-2).
- [ ] Before approval: independent review of these bytes (`status: ready-for-dev` included) UYGUN, no open ENGEL; approval row on the same bytes with `whole_pack=<WP>`. Until that row exists the pack is not approved.
- [ ] Before dispatch: O-1 recorded; row count re-measured at the expected base; evidence script committed, the sabotage of `uic-fu01-oracle-02.py` recorded.

## Implementation Notes

- Measured at `0504c96eaf9251ea03ccb731c932823d964c4291`, clean tree: Python importing the selector read only (`map_path` over `git ls-tree`, `load_patterns`, `read_baseline`, `execute`), session scratch `fu01_measure.py`, `fu01_measure2.py`, `fu01_measure3.py`; the tower puts them in the review ZIP. The 106 and the debt match an earlier run at `1ee773e90` of a script that did not import the selector.
- Reference rule over 1530 views: 1305 captures, all prefix `~/`, 0 variable; the 14 other `assets/js/` literals are `<link ... .css>`; 2 commented-out tags excluded; no `.cshtml` outside `Views/`. `git grep -n "assets/js" -- 'frontend/Diten.Web/**/*.cs'` 0 hits. `git grep` of the JS tree for `getScript`, `import(`, `createElement('script')` 0 loaders; `assets/js/` there only in comments and error texts.
- Measured at `e16d4ff7d` (scratch `fu01_measure4.py`, `fu01_measure5.py`; views and JS tree as at `0504c96ea`): class pairs under `js/{page}/` 10, all `WorkCenterNext` subfolder files, no two-level page; class files held only by Archive or a root view 0; `git ls-tree -r --name-only HEAD frontend/Diten.Web/Views/Home/` gives `Status.cshtml`; the SB-13 rows do not exist in the baseline; `--self-test` prints `PASS <name>` / `FAIL <name>` (`:1540-1541`).
- Part 2's `SCRIPT` and `VARIABLE` at `5534e3b7b` (`python3 -B fu01_measure6.py`, session scratch): 25 fixtures as written in part 2; over 1530 views 1305 captures, `VARIABLE` 0, the (page, file) pairs identical to the first run (113), classes 102 / 2 / 2.
- Forms of FU01-60 to FU01-66, measured against part 2's block as read from this pack (`python3 -B fu01_measure7.py`, session scratch, at `6cbe29914`): `/assets/js/a/x.js` captured; `@Url.Content(Model.X)` and `../assets/js/a/x.js` `VARIABLE`; the two CDN URLs, `src="@@x.js"` and `@Url.Content("~/assets/vendor/a.js")` neither.
- `uic-fu01-oracle-02.py` at `6cbe29914` (`git log -1 --format=%h` on the file), read from HEAD: `satirlar` 106 class files, 113 pairs, 22 pages, 22 rows, 92 matches; `gorunum` 1530, `CRM/Accounts` 19; `degisken` 0; `turetim` anchors 113 and 3372, `kırmızı` SB-1=1 SB-2=5 SB-3=2 SB-4=1 SB-8=2 SB-9=4 (output kept at `docs/records/audits/2026-10/mod-0286-fu01-01/olcum/oracle-02-turetim.tsv`); `secici` exit 1 `FU01_REF_PATTERNS yok`, as expected before the work.
- `docs/records/audits/2026-10/mod-0286-fu01-01/olcum/fu01_measure8.py` at `f288abcbe`, run from the repo root on part 2's patterns, not through a selector: each mutation of SB-14 a to d compiles; `@* *@` form from no capture to captured; `Url.Content` form from captured to `VARIABLE`; `../` and `@x` forms from `VARIABLE` to no line; `~/assets/js/@x.js` stays `VARIABLE` under either `VARIABLE` mutation, so FU01-18 is not their case.
- Likely selector lines, for orientation, not a design: `:84`, `:298-347`, `:394`, `:493-494`, `:508-514`, `:801-813`, `:828`, `:872-902`, `:922-936`, `:953`; self-test `:1387-1446`.
- Not measured: the change itself; every sabotage line above (read from the code, not run); CI on a real pull request; time on a CI runner.

## Follow-up Items

| FU | Item | Owner | Regression if deferred |
|---|---|---|---|
| FU-19 | The class files' debt (22 rows at `0504c96ea`), fixed by each page's domain when next touched or by its own job (XMC-001) | Owning domains | 🟡 old debt stays; a new match is red |
| FU-20 | Delete or wire the 2 unreferenced files | enterprise-strategy-business-performance (`systemStatus.js` owner to confirm) | 🟢 no reader; a violation there stays invisible |
| FU-21 | Missing file named at `StrategyPeriodDetail.cshtml:132` (a 404 is inferred, not measured live) | enterprise-strategy-business-performance | 🟡 line printed whenever the page is selected |
| FU-22 | Follow the partial chain, if the 59 callers should be named | PSS-UI tower | 🟡 8 files stay under `ESBP/Components` |
| FU-23 | Files that can never go red: the 2 Shared-only files, and any reference moved to `Views/Shared/**`. Decide a page-less check of such a file; selecting all pages stays forbidden (parent AC-20) | PSS-UI tower | 🟡 a new violation there prints only its own line; no debt today |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi |
|---|---|---|---|
| DL-1 | Başvuruya dayalı sayfa JS eşlemesi | Owned Objects, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Acceptance Criteria, Test Expectations | E2 |
