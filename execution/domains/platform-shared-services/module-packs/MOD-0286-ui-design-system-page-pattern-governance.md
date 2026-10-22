---
id: MOD-0286
name: UI Design System & Page Pattern Governance
slug: ui-design-system-page-pattern-governance
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: draft
build_gate: closed
operational_runtime_gate: closed
approval_status: none
owner: TBD
branch: chore/ct/pss-ui-ct-01
started: 2026-10-22
target: TBD
form_field_count: 0
slice: "P1 - Design System Specification (DL-1) + UI Compliance Checklist (DL-2). Documents only: no runtime page, entity, endpoint, permission or Ocelot route. P2 governance console deferred (DL-3)."
---

# MOD-0286 - UI Design System & Page Pattern Governance

> **Draft, revision 03, 2026-10-22.** This pack is a planning document. It authorizes no code. `status: draft`; both gates closed.
> `service: Diten.Platform` names the P2 host only - P1 touches no service. `shell: none` is the P1 value; P2 would be
> `platform-admin`. `golden_reference: none` because P1 has no DataTable; P2 console lists would be Slim (see Frontend
> File Contract). `entity_base: GlobalEntity` is the P2 value, justified under Runtime Constraints; P1 owns no entity.
> `approval_status: none` - no CT-APR-01 row exists for this pack (`scripts/pack_approval.py check`, see Implementation Notes).
> Revision 02 answers Codex review 01 (UYGUN DEĞİL, F-1..F-8) and CT additions C-1, C-2; revision 03 answers Codex
> review 02 (UYGUN DEĞİL, F-9..F-12); see Implementation Notes.
>
> DCP-002 gate (2026-10-22): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286 --name "UI Design System & Page Pattern Governance"`
> -> `OK  MOD-0286: proven against Blueprint/registry.` Registry row: `execution/registries/module-id-registry.md:164`
> (`planned`, platform-shared-services, carried to the main line at `41901c477`).
>
> Blueprint 8.8 (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx`, `Blueprint_Data`):
> domain "1) Platform & Shared Services", suite Platform Foundation, Wave W-1, Release "R1 - PPM MVP", Build, Tier 1,
> `Dev_Readiness` NOT STARTED (0/5 dependencies ready). Soft pages (5): Design System Console; Page Pattern Registry;
> Wizard Pattern Governance; Component Standard Detail; UI Compliance Checklist. Contract bundle UI-GOV-BUNDLE (page
> pattern schema, component standard schema, wizard metadata schema, accessibility checklist, AuditEvent v1,
> Correlation-ID, OTel). SoR objects: page patterns; wizard templates; component standards; UI governance rules;
> accessibility checklist definitions.

## Open Decisions (Owner Decision Pending)

| # | Decision | Who | Proposed answer | Rationale |
|---|---|---|---|---|
| D-1 | Where the DL-1 specification and DL-2 checklist live | Owner (CT proposes) | `docs/reference/modules/platform/ui-design-system-page-pattern-governance/` (`spec.md`, `compliance-checklist.md`) | `docs-organization.md` §1 question 5 (today's truth, updated when it changes) and §3.1 row "Modül belgesi (spec, karar notu) -> `docs/reference/modules/{platform\|tenant}/<modül>/`". Alternative `docs/reference/architecture/` fits "cross-cutting" but its README says it is for post-development architecture. `docs/` root is forbidden (K1). Not finalised here. |
| D-2 | Do the 11 UI rule files stay in `.antigravity/rules/` or move under the spec? | Owner | Stay (P1). The spec indexes them; the rules remain normative. | Authority order Module Pack > Domain Config > AGENTS.md > `.antigravity/`; moving is a protected-path change and would break 62 pack citations of `verify_datatable_page.py` plus the AGENTS.md §6.1 map. On a rule-vs-spec conflict the rule wins; rule-vs-rule conflicts are decided per D-8 (Conflicts register). |
| D-3 | BL-655: Blueprint retires MOD-0046 Tenant Core UI into "Platform Administration workspace + MOD-0286"; the repo maps MOD-0046 -> MOD-0009-FU03 (`module-id-registry.md:145`, `:67`) | Enterprise Architect / owner | Not decided by this pack. P1 proceeds under either mapping. | BL-655 is ERTELENDİ (`docs/roadmap/backlog/product-backlog.md:10841`, owner 2026-10-19, Master ADR-8.8-001 open, regression risk recorded there as green). |
| D-4 | P2 trigger (console, registry pages, API) | Owner | First real consumer (MOD-0295 or MOD-0297) reaching `approved`, or an explicit owner decision | Blueprint `Dependencies_Normalized`: both consumers are HARD dependants; both are NOT STARTED (`Dev_Readiness`). Building a console with no consumer repeats the "theme decision leaks page by page" problem in a new form. |
| D-5 | Relabel "MOD-0013" in `premium-modal-standard.md:1,11`, `dynamic-localization-standard.md:2,5,181` and `AGENTS.md:139` to MOD-0286 | Owner (protected path `.antigravity/**`) | Yes, in a separate approved commit; not in this pack | Blueprint MOD-0013 = Platform Standards Registry; registry row 70 already says the repo UI-standard concept aligns to MOD-0286. Regression risk: green (labels only; no code reads them). |
| D-6 | MOD-0297 identity: Blueprint 8.8 `Blueprint_Data` MOD-0297 = "HR Capability Block Shell" (domain 4, consumer of MOD-0286); repo registry row 189 MOD-0297 = "Tenant Subscription Management", deprecated alias -> MOD-0361 | Enterprise Architect | Record as a registry divergence; do not cite MOD-0297 as a consumer until resolved | Found while measuring consumers for this pack. `docs/reference/modules/platform/README.md` also lists MOD-0297 as "Tenant Subscription Lifecycle". Not this pack's to fix (XMC-001). |
| D-7 | Is the mobile (Android) design kit (`:core:design`, commits `0456e913e`, `4dbd105f9`; no `mobile/` folder in this tree) a consumer of MOD-0286 or a separate design system? | Owner | Separate; re-asked in the P2 revision | The web spec indexes Razor / Bootstrap / backbone-custom.css rules that do not apply to an Android kit; binding them now would make the P1 spec claim a surface it cannot measure. |
| D-8a | Conflict 1: `premium-modal-standard.md:27-28,35-36,43-44,51-52` prescribe icon HTML with inline `style="font-size: 2.5rem; color: ..."`; FG-003 (`frontend-js-standard.md:145`, `frontend-standards.md:194`) forbids inline CSS. Same authority layer (`.antigravity/rules`). | Rule owner (owner; `.antigravity/**`) | Apply FG-003: the premium-modal icon markup moves to classes in `backbone-custom.css`; `premium-modal-standard.md` §2 is corrected in a separate approved commit. Until then UIC-13 treats the four prescribed lines as the only tolerated `style=` and lists them in the record. | FG-003 is the newer, cross-cutting rule (UI-001 family); the modal snippet is a template detail with no runtime reader. Status: **karar bekliyor**. |
| D-8b | Conflict 2: `frontend-js-standard.md:53` (7.1) "Tercih edilen yol: `window.DitenDataTable.createCrudTable(...)`" vs the same file's anti-pattern table (`:981`) and BL-440 (`product-backlog.md:6199-6203`, decided 2026-09-23, `createList` factory). Same file, stale line. | Rule owner (owner / CT as BL-440 owner) | UIC-4 stays on `createList`; line 53 is updated to name `createList` as the preferred path and `createCrudTable` as legacy, in a separate approved commit. | The verifier already resolves `createList` (`verify_datatable_page.py:535-536`); 37 pages use it, 81 still use `createCrudTable` (Module Summary). Status: **karar bekliyor**. |
| D-8c | Conflict 3: `frontend-standards.md:208` "`Views/Platform/_ViewStart.cshtml` zaten admin layout'u atadığı için view'da manuel `Layout = ` opsiyoneldir" vs `module-pack-standard.md:88` ("Razor sayfasinda layout ACIKCA yazilir") and `views-organization.md:39` ("her Razor sayfasında ... layout açıkça seçilir"). Same layer. | Rule owner (owner) | Apply the explicit layout (UIC-1 as written); line 208 is corrected in a separate approved commit. | Two rules against one; `module-pack-standard.md:292, :608` make the explicit layout a testable AC in every pack. Status: **karar bekliyor**. |

## Module Summary

MOD-0286 is the single governed source for UI standards: page archetypes, wizard patterns, component conventions and
accessibility-ready consistency across modules (Blueprint aim). In P1 it delivers two documents and nothing else:

- **DL-1 Design System Specification** - one index that names every page archetype, component standard, shell/layout
  rule, visual token, accessibility, language, unauthorized-face, search-registry, record-code and API-profile rule, each
  with its normative source (`path:line` or `path §n`) and its live code path. It changes no rule.
- **DL-2 UI Compliance Checklist** - a fixed-id list (`UIC-<n>`) of what a page or pack must satisfy, with the command
  that measures each item (run contract below) or the words "elle: inceleme kaydı" where no tool exists.

Why now - three measurements (2026-10-22, this worktree at `41901c477`):

1. **The standard is scattered.** `wc -l .antigravity/rules/{frontend-standards,frontend-datatable-template,frontend-details-template,frontend-form-template,frontend-js-standard,premium-modal-standard,page-help-and-keyboard,views-organization,unauthorized-surface-standard,localization-standard,dynamic-localization-standard}.md`
   = 220 + 644 + 104 + 305 + 1124 + 93 + 233 + 113 + 112 + 58 + 181 = **3187 lines in 11 files**, plus two Golden
   Reference packs (`DEV-0000-golden-reference-slim.md` ready-for-dev, 242 lines; `DEV-0001-golden-reference-compact.md`
   in-progress, 257 lines). No file says which rule wins when two disagree (Conflicts register, D-8a..c).
2. **Packs cannot name their pattern.** `ls execution/domains/*/module-packs/*.md | grep -v README | wc -l` = **169**
   packs; **9** have no `golden_reference:` line; of the 160 that do, **15** write free text because no pattern name fits
   (`custom-integration` x3, `not_applicable` x2, `slim+compact`, `n/a ...` x5, `custom-admin-register`,
   `custom-platform-seed`, `secrets-configuration-vault-golden-flow`, `manifest-import-diff-review-confirm-active-snapshot`);
   enum values: `none` 65, `compact` 58, `slim` 22. (The CT brief counted 173 / 13 / 16 on another basis; the
   numbers here are this worktree's, command above.)
3. **Three list implementations coexist.** `find Views -name Index.cshtml -not -path '*/Archive/*' | wc -l` = **192**
   list pages; JS files under `wwwroot/assets/js` containing `DtDefaults` = **177**, `createList(` (BL-440 factory) =
   **37**, legacy `createCrudTable(` = **81** (`grep -rl ... --include='*.js' | wc -l`). Non-partial pages outside
   Archive/Shared = **629**, of which Create/Edit/Details = **270**. `frontend-datatable-template.md:153` records "bugün
   132'si sapmış" for legacy list screens.

Theme decisions leaking page by page, from the backlog: BL-100 focus ring missing on theme classes
(`product-backlog.md:1819`), BL-112 theme 3px ring overrides ours (`:1972`), BL-236 `--bs-secondary-color` below AA on
20 texts in both themes and "a design-system decision" (`:2567`, `:2573`), "ürün geneli (tema)" (`:3661`), BL-337 Sneat
theme customizer to be removed from all three shells (`:3995`).

## Ownership and Boundaries

- In-scope (P1): DL-1 specification; DL-2 checklist; the `UIC-<n>` id namespace; the run contract and record format.
- In-scope (P2, deferred, DL-3): the five Blueprint soft pages as a Platform Admin console over `Diten.Platform`;
  UI-GOV-BUNDLE schemas; `AuditEvent v1` emission for governance writes.
- Out-of-scope: changing any `.antigravity/rules/**` file (D-2, D-5, D-8a..c are owner commits); refactoring any list page;
  DEV-0000 / DEV-0001 packs and their live code (stay in developer-enablement; referenced, not absorbed - XMC-001); the
  BL-440 list-factory plan (stays with the Control Tower); theme vendor CSS (`wwwroot/assets/vendor/css/core.css`);
  `_Layout.cshtml` (frozen); MOD-0285 System Navigation; MOD-0018 permission model; the MOD-0046 / BL-655 decision (D-3);
  the **mobile (Android) design kit** - it exists only in history (`git log --all --oneline --grep=':core:design'` -> 4
  commits, among them `0456e913e` "M0.3 :core:database ... + M0.5 :core:design" and `4dbd105f9` "M1 rollout Wave 1"); no
  `mobile/` folder exists in this tree (`ls -d mobile` -> none). Its relation to the web design system is D-7.
- Boundary with MOD-0013 Platform Standards Registry (Blueprint): MOD-0013 would hold *all* platform standards; MOD-0286
  holds only UI ones. No MOD-0013 pack exists in the repo (`ls execution/domains/*/module-packs/MOD-0013-*` -> none).

## Owned Objects

P1 owns documents and an id namespace, no runtime object:

- Document: DL-1 `spec.md` (path per D-1).
- Document: DL-2 `compliance-checklist.md` (path per D-1), items `UIC-1 ... UIC-32` (table below; ids are stable, never renumbered; a new item takes the next number). The document has two keyed tables: the checklist (rows `| UIC-<n> |`, the only place a UIC id starts a row) and a separate section `## Koşu sözleşmesi` whose rows are keyed `| RC-<n> |` with a "UIC kapsamı" column (F-12). AC-4 counts UIC ids over the whole file; AC-16 binds every automatic UIC id to at least one RC row.
- Record: measurement record **directory** `docs/records/audits/2026-10/mod-0286-uic-baseline-01/` (dated record, `docs-organization.md` §1 question 2) with run directories `auto-01/` (all automatic items, one `run_record.py` run), `sabotage-01/` (AC-13, second run after `auto-01` is committed) and the human-written `manual.md` (format under AC-6). Evidence flow: "Kanıt toplama akışı" below.
- Entities, commands, queries, DTOs, API endpoints, frontend routes, permissions: **none in P1**.
- RCS-001 (record code): P1 has no user-visible record -> "yok". P2 `UiPagePattern` code: OTOMATİK proposed, type prefix
  TBD per `record-coding-standard.md` §5.5; the owner decides, and the common code service must exist first
  (`grep -rl 'IRecordCodeService\|IRecordCodeGenerator' services --include='*.cs'` -> 0 files today).
- Nav key: P1 none (no page). P2 proposal, in the form `NavNameLocalizer.Normalize` reads: `Nav.Module.UIGOVERNANCE`,
  `Nav.Page.UIPAGEPATTERNS`, `Nav.Page.UICOMPLIANCECHECKLIST`.

### DL-1 specification outline (every heading cites its source rule and live code)

| # | Heading | Normative source | Live code measured 2026-10-22 |
|---|---|---|---|
| 1 | Page archetypes: List Slim (<=8 fields, offcanvas), List Compact (>8, full pages), Details, Form, bespoke console, wizard | `module-pack-standard.md` §3, §5; `frontend-datatable-template.md`; `frontend-form-template.md`; `frontend-details-template.md`; AGENTS.md §6 "Golden Reference DataTable Kararı" | Slim `Views/DevEnablement/GoldenReferenceSlim/` (7 resx); Compact `Views/DevEnablement/GoldenReferenceCompact/`; bespoke consoles: MOD-0155-FU05 Day/Week setup and MOD-0155-FU02 execution calendar (packs say "NOT a Golden DataTable CRUD surface"); wizard: `Views/MasterData/LegalEntities/Wizard.cshtml` (438 lines, `Layout = "_LayoutTenantShell"`, JS `wwwroot/assets/js/MasterData/LegalEntities/wizard.js` cited at MOD-0220 pack line 562) - the only wizard surface found; no wizard rule file exists |
| 2 | Component standards: premium modal/confirm, toast, offcanvas, quick view, list shell + factory, legacy CRUD table, select2, skeleton, filter partial, Save View, ColReorder, "?" help + shortcuts + tour | `premium-modal-standard.md`; `frontend-js-standard.md` (anti-pattern table :981); `frontend-datatable-template.md` (state standard :168, UI-019 :197); `page-help-and-keyboard.md` (UXH-001); `views-organization.md` §3 | `Views/Shared/_GlobalConfirmation.cshtml` (479 lines, `window.showConfirm` :206); `_GlobalNotification.cshtml` (269; Notyf :177, `window.showToast` :133, `DitenToastText` :109); `Views/Shared/Components/DataTable/_ListShell.cshtml` (58) + `_BulkActionBar.cshtml`, consumed by 41 views; `wwwroot/assets/js/diten-datatable.js` (1414; `createList` :961, legacy `createCrudTable` :251, filter button attrs :1046, save-view button :1052, colReorder default :1083, export guard :907-911); `dt-defaults.js` (1603); `_TableSkeleton.cshtml` (51); `wwwroot/assets/js/shared/` premium-modal.js 126, diten-dialog.js 128, diten-help.js 138, diten-shortcuts.js 283, diten-tour.js 149, diten-tour-catalog.js 41 (3 flows: WORKCENTER, MYTIMESHEET, TIMEAPPROVALS), diten-enter-primary.js 207; select2 referenced in 210 JS files |
| 3 | Shell and layout: `_LayoutPlatformAdmin`, `_LayoutTenantShell`, frozen `_Layout`; `shell` x layout derivation | `views-organization.md` §2; `module-pack-standard.md` §2 "shell x Layout"; `frontend-standards.md` PROD-001, PROD-005 | `Views/Shared/_LayoutPlatformAdmin.cshtml`, `_LayoutTenantShell.cshtml`, `_Layout.cshtml`; `Views/Platform/_ViewStart.cshtml` |
| 4 | Visual tokens: `--diten-*` / `--bs-*`, light/dark, RTL, no inline CSS (FG-003) | `frontend-standards.md` §4.2 "No Hardcoded Colors", UI-001; FG-003 (`frontend-js-standard.md:145`, `frontend-standards.md:194`) | `wwwroot/assets/css/backbone-custom.css` 8622 lines; 1152 lines reference `--diten-`/`--bs-`; 32 dark-theme selectors; 13 `[dir="rtl"]` rules; 93 non-Archive `.cshtml` files still contain `style="` |
| 5 | Accessibility: AA 4.5:1, focus ring, keyboard, icon-only button labels, filter trigger ARIA | `frontend-datatable-template.md:628-631` A11y minimums ("Focus ring kaldırma yasak"; `title` + `aria-label`; `aria-controls` + `aria-expanded`); BL-100, BL-112, BL-236 (measured gaps) | no contrast/focus tool in repo; BL-100 method = real Tab-key count (17/60 stops without ring on 2026-08-14); factory filter button `diten-datatable.js:1046` carries `title`, `aria-controls`, `aria-expanded` and **no `aria-label`** (`grep -c aria-label diten-datatable.js` = 2, lines 600 and 1303, neither the toolbar) - FU-10 |
| 6 | Language: platform en+tr, tenant 7 (en, tr, fr, es, zh, ar, ru); no hardcoded text; `window.L10n` bridge A (`GetAllStrings(true)`) for new surfaces; dynamic localization | `localization-standard.md` §1-§4; `dynamic-localization-standard.md` :78 "iki köprü"; AGENTS.md §6 "Yerelleştirme" | `Resources/Views/**/*.{lang}.resx` (platform sample `Views/Platform/Tenants` 2 files; tenant sample GoldenReferenceSlim 7); `SharedResource.{lang}.resx`; `_IndexL10n.cshtml` / `_L10n.cshtml`; pattern A used by 5 views (`grep -rl 'GetAllStrings(true)' frontend/Diten.Web/Views`), Golden Slim uses the explicit-key bridge (`_IndexL10n.cshtml:9-13`) |
| 7 | Unauthorized face (UAS-001), Ctrl+K registry, record code `MG-ORG-000042` (RCS-001) | `unauthorized-surface-standard.md`; `platform-global-search-registry.md` §2, §5; `record-coding-standard.md` §1, §6 | `wwwroot/assets/js/diten-unauthorized.js` (52); `wwwroot/assets/json/platform-search.en.json`, `.tr.json` (+ legacy `platform-search.json`; `/Platform/Tenants` at `:7` and `:117` of both); record-code service: not found (0 files) |
| 8 | API profile (proxy / direct-gateway), no token in browser JS | `routes.md` §"Profil 1 / Profil 2"; `frontend-js-standard.md` JS-015 and anti-pattern table | verifier `--api-profile`; `window.API.{service}` for direct-gateway |
| 9 | Conflicts register: rule vs rule, rule vs spec; per conflict: parties, layer, applied source, rationale, decider, status | D-8a..c; Implementation Notes "Çatışma kaydı" | - |

### DL-2 UI Compliance Checklist (draft items)

Columns: item - applies to (list / details / form / all pages / pack) - mandatory or optional - how measured - source.
"auto" = a command exists; its argv, fixture and expected output are in the run contract below. "elle" = manual review
record (format under AC-6). Automatic: 22 (UIC-1..7, 13, 14, 17, 19..22, 24..27, 29..32). Manual: 10 (UIC-8..12, 15, 16, 18, 23, 28).
Paths in this table are templates; the run contract spells the fixture paths out in full from the repo root.

| Id | Item | Applies to | Req. | How measured | Source |
|---|---|---|---|---|---|
| UIC-1 | `Layout = "_LayoutPlatformAdmin"` or `"_LayoutTenantShell"` written explicitly in every non-partial view (files not starting with `_`, listed by name - no glob) | all pages | zorunlu | auto: `grep -L 'Layout = "' <each non-partial .cshtml>` -> stdout empty | `views-organization.md:39`; `module-pack-standard.md:88`; D-8c |
| UIC-2 | Skeleton in the shape of the content (`#skeleton-loader` / `backbone-skeleton`), no `spinner-border`, no "Loading" text | all data pages | zorunlu | auto (list): verifier run of UIC-3; auto (other): `grep -c 'backbone-skeleton\|skeleton-loader' <Index.cshtml>` >= 1 and `grep -c 'spinner-border' <Index.cshtml>` = 0 | `views-organization.md:47, :53` |
| UIC-3 | Slim / Compact file set complete per `golden_reference` (verifier checks `Index.cshtml exists` ... `backbone-custom.css exists`, Slim `_CreateEditOffcanvas.cshtml`, Compact `Create.cshtml` / `Edit.cshtml`; `verify_datatable_page.py:953-966`) | list | zorunlu | auto: `python3 .antigravity/scripts/verify_datatable_page.py . --area {Area} --module {Module} --reference slim\|compact --api-profile ... --format report` exit 0. Boundary: the verifier does not read `form_field_count` (`grep -c form_field_count verify_datatable_page.py` = 0) - that is UIC-26 | `module-pack-standard.md` §3, §5; `frontend-datatable-template.md:12, :32, :42, :46` |
| UIC-4 | List built with `DitenDataTable.createList` (or `DtDefaults.create`); no new `createCrudTable`, no raw `$(...).DataTable` | list | zorunlu | auto: verifier `[factory]` line names `createList`; `grep -c 'createCrudTable(' <index.js>` prints 0 | `frontend-js-standard.md:30, :981`; BL-440; D-8b |
| UIC-5 | Quick view via event delegation (`.js-quick-view`), no inline `onclick` | list (Slim) | zorunlu | auto: verifier checks `index.js includes .js-quick-view selector`, `index.js uses event delegation (closest('.js-quick-view'))` (UIC-3 run) | `frontend-datatable-template.md` |
| UIC-6 | `window.L10n` bridge = partial + loader (verifier, list); **new surfaces** use pattern A (`GetAllStrings(true)`) | all pages with JS text | zorunlu (pattern A: new surfaces only) | auto: verifier bridge checks (UIC-3 run); `grep -c 'GetAllStrings(true)' <L10n partial>` >= 1 for a new surface. Boundary: the verifier does not test pattern A (`grep -c GetAllStrings verify_datatable_page.py` = 0); Golden Slim is the **legacy explicit-key bridge** and measures 0 - not a defect of the fixture | `dynamic-localization-standard.md:28, :30, :78` |
| UIC-7 | Language set complete by **language name** (`{Marker}.{en,tr}.resx` platform / `{Marker}.{en,tr,fr,es,zh,ar,ru}.resx` tenant, marker = the `{Module}Index.cs` class); the two required module keys (`{Module}Title`, `PageDescription`) and the 12 toolbar vocabulary keys in `SharedResource` present per language; SharedResource parity | all pages | zorunlu | auto: `test -f` per language file; per language `grep -c 'name="\({Module}Title\|PageDescription\)"' <module resx>` = 2 and `grep -c 'name="\(Search\|Export\|Import\|Filter\|Apply\|Reset\|ShowAll\|SaveView\|ColumnVisibility\|Status\|Cancel\|AreYouSure\)"' frontend/Diten.Web/Resources/SharedResource.{lang}.resx` = 12; `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant\|platform`. Boundary: the checker reads `SharedResource.*.resx` only and is red today by shared debt (run contract); the checker's "still equal to English" lines are the only measurement of `localization-standard.md:21` and cover SharedResource only | `localization-standard.md:8, :21, :29-31, :49-53, :58`; `frontend-standards.md:79-82` |
| UIC-8 | No hardcoded user-visible text in `.cshtml` / `.js`; no `\|\| 'Export'` fallbacks; unauthorized text from resx in all required languages | all pages | zorunlu | elle: inceleme kaydı (no script covers view text) | `localization-standard.md:3`; `unauthorized-surface-standard.md:77` |
| UIC-9 | RTL (`ar`) renders without overlap | tenant pages | zorunlu | elle: browser in `ar`, screenshot in record | `localization-standard.md` §1; `backbone-custom.css` 13 RTL rules |
| UIC-10 | Dark theme renders with readable contrast | all pages | zorunlu | elle: both themes, screenshot | BL-236 method (`product-backlog.md:2567`) |
| UIC-11 | Visible focus ring on every Tab stop | all pages | zorunlu | elle: real Tab-key count `n/N` in record | `frontend-datatable-template.md:628`; BL-100 |
| UIC-12 | Text contrast >= 4.5:1 (AA) on card surface | all pages | zorunlu | elle: DevTools contrast per token, table in record | BL-236; BL-100 |
| UIC-13 | No inline CSS: no `style="` in the module's views, no `.style.` in its JS (tolerated until D-8a: the four `premium-modal-standard.md` icon lines, listed in the record) | all pages | zorunlu | auto: `grep -l 'style="' <each .cshtml of the module>` -> stdout empty; `grep -c '\.style\.' <index.js>` prints 0 | FG-003 (`frontend-js-standard.md:145`; `frontend-standards.md:194`); UI-001; D-8a |
| UIC-14 | Confirm / toast through `window.showConfirm` / `window.showToast`; no `alert(`, no raw `Swal.fire` outside the shared helper; bulk delete uses the same confirm as single delete; the shared helper sets `buttonsStyling: false` | all pages | zorunlu | auto: `grep -n 'alert(\|Swal\.fire\|toastr\.' <index.js>` -> stdout empty; on a page with single + bulk delete `grep -c 'window.showConfirm' <index.js>` = 2 (both paths through the same helper); factory `grep -c 'buttonsStyling: false' frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml` = 1 | `premium-modal-standard.md:16`, §4; `frontend-js-standard.md:51, :981`; `frontend-datatable-template.md:23` |
| UIC-15 | "?" help keys `Help.{KOD}.Purpose/Audience/Step{n}/Step{n}Title` in all required languages; `Help.{KOD}.Related.{İLGİLİ KOD}` for every related page listed for `{KOD}` in `PageHelpCatalog.Related` (`frontend/Diten.Web/Services/Help/PageHelpCatalog.cs:17-24`), none otherwise; Enter = primary action; tour only for the five flows | all pages | zorunlu | elle for behaviour; auto for keys, per required language on `frontend/Diten.Web/Resources/PageHelpResource.{lang}.resx`: `grep -c 'name="Help\.{KOD}\.\(Purpose\|Audience\)"'` = 2; `grep -c 'name="Help\.{KOD}\.Step[1-5]"'` >= 1 and equal to `grep -c 'name="Help\.{KOD}\.Step[1-5]Title"'`; `grep -c 'name="Help\.{KOD}\.Related\.'` = the number of `new(` entries on the `["{KOD}"]` line of `PageHelpCatalog.cs` (0 when the page has no line); fixture USERS measured: 2 / 5 = 5 / 2 (catalog `:20` has 2 entries) in all 7 files | `page-help-and-keyboard.md:48-53, :220` (UXH-001) |
| UIC-16 | Unauthorized user sees one explanation, no skeleton, no redirect | all pages | zorunlu | elle: login without the permission, screenshot | `unauthorized-surface-standard.md` §1-§2 (UAS-001) |
| UIC-17 | Ctrl+K registry entry in `platform-search.en.json` and `.tr.json` (platform fixture `Platform/Tenants`, C-1) | platform pages | zorunlu | auto: `grep -c '"url": "/Platform/{Module}"' frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json` >= 1 per file | `platform-global-search-registry.md` §2, §5; `frontend-standards.md:213` |
| UIC-18 | User-visible record code in `MG-ORG-000042` form from the common service; class OTOMATİK / İKİSİ DE / KALSIN in the pack | pages creating records | zorunlu where a code exists | elle: pack row + create one record; common service absent today (see Owned Objects) | `record-coding-standard.md` §1, §6 (RCS-001) |
| UIC-19 | API profile declared (`proxy` for Platform area, `direct-gateway` elsewhere) and followed; browser never calls a service port | list | zorunlu | auto: verifier `--api-profile proxy\|direct-gateway` (UIC-3 run; check `direct-gateway profile uses window.API service base`) | `routes.md` §Profil 1/2; AGENTS.md §3 |
| UIC-20 | No `document.cookie`, `access_token`, `Authorization: Bearer` in page JS | all JS | zorunlu | auto: verifier (list); `grep -n 'document.cookie\|Bearer\|access_token' <index.js>` -> stdout empty | `frontend-js-standard.md:981` |
| UIC-21 | Nav keys in the pack are in runtime form (`Nav.Page.CODE`, upper, alphanumeric) | pack | zorunlu | auto, four conditions read from the tool's captured output (F-10): the output is written to a file and the exit printed separately (`python3 scripts/check_pack_nav_keys.py > $OUT/navkeys.out 2>&1; echo "rc=$?"`); accepted only when (1) `rc=0` or `rc=1` - `rc=1` only with the two known lines MOD-0176:645 / MOD-0177:915 quoted in the record, `rc=2` = tool error, rejected; (2) `grep -c 'could not read' $OUT/navkeys.out` = 0 (`check_pack_nav_keys.py:207-211` prints "could not read the packs" and exits 2 without naming any pack); (3) `grep -c 'MOD-0286' $OUT/navkeys.out` = 0; (4) `git ls-files -- 'execution/domains/*/module-packs/MOD-0286-*.md' \| wc -l` = 1 (the pack is tracked, hence scanned: the script reads `git ls-files` only, `:131`, glob `:36`) | `module-pack-standard.md` §7 |
| UIC-22 | `data-dt-standard="v2"` and `data-dt-data-mode` equal to the pack's `data_mode` | list | zorunlu | auto: verifier `--data-mode server\|client` (checks `Data mode declared: ...`, `Index.cshtml has <table id="..." data-dt-standard="v2">`) | `frontend-datatable-template.md:16, :72, :195` |
| UIC-23 | Required-field markers (UI-020); Compact form / details information parity (UI-024); required contract parity (validator = ViewModel = Razor) | form / details | zorunlu | elle: field list compared in record | `frontend-standards.md:99-100`; `frontend-datatable-template.md:20, :21` |
| UIC-24 | Save View: `dt-save-filter-btn` always rendered (`d-none` until dirty), `savedView` config on the page, dirty-state follows applied state only; personalization through the shared client, no raw `fetch('/api/personalization/...')` in page JS | list | **zorunlu, no exception** (`frontend-js-standard.md:73` 10.1 and `frontend-datatable-template.md:24` name none; the <= 2-column exception of `:76` belongs to ColReorder, UIC-32 - F-9) | auto: `grep -c 'savedView' <index.js>` >= 1; `grep -c 'api/personalization' <index.js>` = 0; factory `grep -c 'dt-save-filter-btn' frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js` = 2; elle: dirty-state exercised | `frontend-datatable-template.md:24, :221-230`; `frontend-js-standard.md:33, :73` |
| UIC-25 | Touch protocol on a legacy list: `--format gaps` run, deviations numbered, owner asked | list (legacy edit) | zorunlu | auto: `python3 .antigravity/scripts/verify_datatable_page.py . --area {Area} --module {Module} --format gaps` output quoted | `frontend-datatable-template.md:151` |
| UIC-26 | `form_field_count` in the pack equals the user-field count of the create/edit form (hidden inputs excluded) and the Slim/Compact decision follows it (<= 8 slim) | list / pack | zorunlu | auto: `grep -c 'form-control\|form-select\|form-check-input' <create/edit form>` = pack `form_field_count` (fixture: 6 = `DEV-0000-golden-reference-slim.md:16`). Boundary: counts lines, one control per line assumed | `module-pack-standard.md` §3; AGENTS.md §6 "Golden Reference DataTable Kararı" |
| UIC-27 | Every field wrapped in `.diten-field` with an icon before the control, `aria-hidden="true"`; exceptions only `form-check`/switch and configurable fields; the date icon opens the picker (`diten-datefield.js`) | form | zorunlu | auto: `grep -c 'diten-field' <form>` >= field count; `grep -c 'aria-hidden="true"' <form>` >= field count minus exceptions; elle: date icon clicked | `frontend-form-template.md:21-31` |
| UIC-28 | Before delivery every control on the screen is pressed; a control that does nothing fails (UI-027) | all pages | zorunlu | elle: control list with result per control in record | `frontend-standards.md:135, :165-166` |
| UIC-29 | Icon-only toolbar buttons carry `title` + `aria-label`; filter trigger carries `aria-controls="inlineFilterCollapse"` + `aria-expanded` | list | zorunlu | auto: page `grep -c 'id="inlineFilterCollapse"' <_Filter.cshtml>` = 1 and `grep -c "collapseId: 'inlineFilterCollapse'" <index.js>` = 1; factory `sed -n 1046p diten-datatable.js` shows `title`, `'aria-controls': collapseId`, `'aria-expanded'` - and **no `aria-label`** today (FU-10, shared factory, XMC-001) | `frontend-datatable-template.md:630-631` |
| UIC-30 | Every form panel's primary button carries `data-dt-primary="enter"` or `"chord"`; irreversible actions (publish, approve, delete, retire, ...) only `chord` = Ctrl/⌘+Enter; bare Enter does nothing there | form / panels | zorunlu | auto: `grep -n 'data-dt-primary=' <each panel .cshtml>` lists one marker per form panel; elle: each `chord` panel's action classified as irreversible in the record | `page-help-and-keyboard.md:16, :105-118` |
| UIC-31 | Export file = screen (visible columns, applied filter/search/sort, all matching rows): server-mode lists declare `export: { mode: 'server', url, fileName }`; client-mode lists use the browser buttons (`exportColumns`); `export.mode: 'server'` with `dataMode: 'client'` throws | list | **zorunlu** (client mode: browser buttons suffice, every row is already in the browser) | auto: server list `grep -c "mode: 'server'" <index.js>` >= 1; client list `grep -c 'exportColumns' <index.js>` >= 1; factory guard `diten-datatable.js:907-911` | `frontend-datatable-template.md:107-129` (owner 2026-09-24, BL-452); `frontend-js-standard.md:31` |
| UIC-32 | ColReorder active by default on every list with the standard column structure (control + checkbox + N data + action; `colReorder: { columns: ':gt(1):not(:last-child)' }` from the factory, `diten-datatable.js:1083`) | list | **zorunlu** (the only exception, in the source's words: a special page with <= 2 data columns may disable it, and then a comment line stating the reason is mandatory - `frontend-js-standard.md:76`) | auto: `grep -c 'colReorder' <index.js>` = 0 (no override); on a disabling page the override line is quoted with its reason comment and `grep -c '<th' <_DataTable.cshtml>` minus control, checkbox and action columns <= 2 | `frontend-datatable-template.md:192`; `frontend-js-standard.md:74-76, :341` |

How other packs bind to the list (proposal, not yet a rule): a module pack carries one Acceptance Criteria item
"UI uyum: UIC-<ids> listesi, ölçüm kaydı `docs/records/audits/<yyyy-mm>/<dizin>/`". Writing that into
`module-pack-standard.md` is a `.antigravity` change (Follow-up Items, FU-1).

### Koşu sözleşmesi (run contract) - automatic items, rows keyed `RC-<n>`

Rows are keyed `RC-<n>` (not `UIC-<n>`) so that AC-4's uniqueness grep never meets a second UIC-keyed table (F-12); the
"UIC kapsamı" column names the checklist ids a row measures. In DL-2 this table is its own section `## Koşu sözleşmesi`.
Working directory: **always the repo root** (`run_record.py:131` runs `subprocess.run(argv, cwd=ROOT)` - no shell, so
pipes, globs and `->` are not interpreted; every item below is one block inside a single `sh -c '...'` argv, see "Kanıt
toplama akışı"). Fixtures: tenant list items `DevEnablement/GoldenReferenceSlim` (`G=` views, `J=` index.js, `R=` resx),
platform items `Platform/Tenants` (`T=`; Ctrl+K entry measured: `platform-search.en.json:7, :117` and `.tr.json:7, :117`),
pack items this pack; `OUT=` is the `--out` run directory (a file the script writes there becomes a recorded artifact,
`run_record.py:141-151`). `grep -c` prints `0` and exits 1 when nothing matches; `grep -l`/`-n` with no match print nothing
and exit 1 - acceptance below is by **stdout** unless an exit code is named. Expected values were measured 2026-10-22 at `79f87c1bf`;
the rows changed in rev 03 (RC-6, RC-8, RC-11, RC-13, RC-21) were measured at `19f0b2f7a`.

| Id | UIC kapsamı | argv block (inside `sh -c`) | Expected stdout / exit |
|---|---|---|---|
| RC-1 | UIC-1 | `grep -L 'Layout = "' $G/Index.cshtml; grep -L 'Layout = "' $T/Index.cshtml $T/Create.cshtml $T/Details.cshtml $T/Security.cshtml` | empty (measured: `Index.cshtml:8` has `Layout = "_LayoutTenantShell"`; all four Tenants pages contain `Layout = "`) |
| RC-2 | UIC-2 | `grep -c 'backbone-skeleton\|skeleton-loader' $G/Index.cshtml; grep -c 'spinner-border' $G/Index.cshtml` | `1` then `0` |
| RC-3 | UIC-3, UIC-5, UIC-6, UIC-19 | `python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format report` | today exit 1: 97 `[PASS]`, 1 `[FAIL]` (`personalizationClient sends tenant header only for tenant users`, FU-4); `[factory] index.js calls DitenDataTable.createList`; quick-view, `Bulk selection + reload-with-toast + clear-selection wiring` and `direct-gateway profile uses window.API service base` lines `[PASS]`. Target after FU-4: exit 0 |
| RC-4 | UIC-4 | `grep -c 'createCrudTable(' $J; grep -c 'createList(' $J` | `0` then `1` |
| RC-5 | UIC-6 (pattern A) | `grep -c 'GetAllStrings(true)' $G/_IndexL10n.cshtml; grep -c 'GetAllStrings(true)' frontend/Diten.Web/Views/WorkCenterNext/_L10n.cshtml` | `0` (legacy bridge fixture, expected) then `1` (pattern-A fixture) |
| RC-6 | UIC-7 | `for l in en tr fr es zh ar ru; do test -f $R/GoldenReferenceSlimIndex.$l.resx && echo "$l ok" \|\| echo "$l MISSING"; grep -c 'name="\(GoldenReferenceSlimTitle\|PageDescription\)"' $R/GoldenReferenceSlimIndex.$l.resx; grep -c 'name="\(Search\|Export\|Import\|Filter\|Apply\|Reset\|ShowAll\|SaveView\|ColumnVisibility\|Status\|Cancel\|AreYouSure\)"' frontend/Diten.Web/Resources/SharedResource.$l.resx; done; test -f $G/GoldenReferenceSlimIndex.cs && echo marker ok; for l in en tr; do test -f frontend/Diten.Web/Resources/Views/Platform/Tenants/TenantsIndex.$l.resx && echo "$l ok"; done; python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant; echo "exit=$?"; python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context platform; echo "exit=$?"` | 7 x (`ok`, `2`, `12`) (measured 2026-10-22 at `19f0b2f7a`: `GoldenReferenceSlimIndex.en.resx:7-8` carry `GoldenReferenceSlimTitle`, `PageDescription`; all 7 `SharedResource.*.resx` carry the 12 keys), `marker ok`, 2 x `ok`; checker tenant exit 1 today (11 `[X]` lines: fr/es/zh/ar/ru "Missing 35 key(s) from SharedResource.en.resx" + 6 "still equal to English"); platform exit 1 (1 `[X]`: tr 5 keys equal to English). Shared debt, not this fixture's; the record quotes the `[X]` lines as-is |
| RC-7 | UIC-13 | `grep -l 'style="' $G/Index.cshtml $G/_Filter.cshtml $G/_DataTable.cshtml $G/_IndexL10n.cshtml $G/_CreateEditOffcanvas.cshtml $G/_DetailsQuickView.cshtml; grep -c '\.style\.' $J` | empty then `0` |
| RC-8 | UIC-14 | `grep -n 'alert(\|Swal\.fire\|toastr\.' $J; grep -c 'window.showConfirm' $J; grep -c 'buttonsStyling: false' frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml` | empty, then `2` (single delete `index.js:135`, bulk delete `:158`), then `1` (`_GlobalConfirmation.cshtml:64`) |
| RC-9 | UIC-17 | `grep -c '"url": "/Platform/Tenants"' frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json` | `...en.json:2` and `...tr.json:2` (two entries each, groups "Platform Operations" and "Common Tasks"); >= 1 per file accepted |
| RC-10 | UIC-20 | `grep -n 'document.cookie\|Bearer\|access_token' $J` | empty |
| RC-11 | UIC-21 | `python3 scripts/check_pack_nav_keys.py > $OUT/navkeys.out 2>&1; echo "rc=$?"; grep -c 'could not read' $OUT/navkeys.out; grep -c 'MOD-0286' $OUT/navkeys.out; git ls-files -- 'execution/domains/*/module-packs/MOD-0286-*.md' \| wc -l` | `rc=1`, `0`, `0`, `1` (measured 2026-10-22 at `19f0b2f7a` with the pack tracked; `navkeys.out` = 3 lines: MOD-0176:645, MOD-0177:915, "14 key(s) off form, 12 known, 2 new"). Accepted: `rc=0`, or `rc=1` with only those two pack lines quoted in the record; `rc=2` or a "could not read" line = tool error, rejected; `git ls-files` must print `1` |
| RC-12 | UIC-22 | `python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --data-mode client --api-profile direct-gateway --format gaps` | today exit 1 (same FU-4 gap); report form shows `[PASS] Data mode declared: client`, `[PASS] Client mode: DataTables serverSide is off` |
| RC-13 | UIC-24 | `grep -c 'savedView' $J; grep -c 'api/personalization' $J; grep -c 'dt-save-filter-btn' frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js` | `1`, `0` (measured at `19f0b2f7a`: no raw personalization fetch in the fixture; the factory owns the client), `2` (`:994`, `:1052`) |
| RC-14 | UIC-25 | same command as RC-12 without `--data-mode` | exit 1, one numbered gap line (FU-4) |
| RC-15 | UIC-26 | `grep -c 'form-control\|form-select\|form-check-input' $G/_CreateEditOffcanvas.cshtml; grep -n '^form_field_count' execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md` | `6` then `16:form_field_count: 6` (fields: Code, Name, ReferenceType, Priority, Description, IsActive) |
| RC-16 | UIC-27 | `grep -c 'diten-field' $G/_CreateEditOffcanvas.cshtml; grep -c 'aria-hidden="true"' $G/_CreateEditOffcanvas.cshtml` | `10` then `5` (5 iconed fields + 1 switch exception = 6) |
| RC-17 | UIC-29 | `grep -c 'id="inlineFilterCollapse"' $G/_Filter.cshtml; grep -c "collapseId: 'inlineFilterCollapse'" $J; sed -n 1046p frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js; grep -c 'aria-label' frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js` | `1`, `1`, the attr line (`title: l.Filter, 'aria-controls': collapseId, 'aria-expanded': 'false'`), `2` - toolbar `aria-label` missing today (FU-10) |
| RC-18 | UIC-30 | `grep -n 'data-dt-primary=' $G/_CreateEditOffcanvas.cshtml $G/_DetailsQuickView.cshtml` | `_CreateEditOffcanvas.cshtml:88: ... data-dt-primary="enter"` (create/edit = reversible); quick view has no form, no marker |
| RC-19 | UIC-31 | `grep -c 'exportColumns' $J; grep -c "mode: 'server'" $J` | `1` then `0` (client-mode fixture; a server-mode fixture is measured when one is named) |
| RC-20 | UIC-32 | `grep -c 'colReorder' $J` | `0` (factory default applies) |
| RC-21 | UIC-15 (keys only; behaviour stays manual) | `for l in en tr fr es zh ar ru; do f=frontend/Diten.Web/Resources/PageHelpResource.$l.resx; grep -c 'name="Help\.USERS\.\(Purpose\|Audience\)"' $f; grep -c 'name="Help\.USERS\.Step[1-5]"' $f; grep -c 'name="Help\.USERS\.Step[1-5]Title"' $f; grep -c 'name="Help\.USERS\.Related\.' $f; done; grep '\["USERS"\] = \[' frontend/Diten.Web/Services/Help/PageHelpCatalog.cs \| grep -o 'new(' \| wc -l` | 7 x (`2`, `5`, `5`, `2`) then `2` (measured at `19f0b2f7a`; catalog `PageHelpCatalog.cs:20`) |

With `G=frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim`, `J=frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js`,
`R=frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim`, `T=frontend/Diten.Web/Views/Platform/Tenants`,
`OUT=docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01` set at the top of the `sh -c` script.

### Kanıt toplama akışı (evidence flow, F-6)

`run_record.py` refuses a dirty tree (`:121-123`, untracked files count - `status_paths()` uses `--untracked-files=all`,
`:79-85`) and refuses changes left outside the run directory (`:137-139`); it records into a new directory under
`docs/records/audits/` (`EVIDENCE_ROOT`, `:46`). Fifteen or more sequential runs therefore cannot be made: the first run's
untracked record dirties the tree for the second. The flow is:

1. Commit everything first (today the tree has 3 untracked paths under `docs/records/reviews/`; they block any run until committed).
2. **One run for all automatic items:** `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01 -- sh -c '<script>'`
   where `<script>` sets `G J R T OUT` and then, for every RC row, `echo "RC-n"; <block>; echo "exit=$?"`. The run's own exit
   is the last block's exit - it is **for the record only**, not the acceptance; acceptance is read per row from the
   `RC-n` / `exit=` pairs in `stdout.txt` against the expected column above. The baseline is red on FU-4 by design.
   RC-11 writes `$OUT/navkeys.out` inside the run directory: `run_record.py` creates `--out` before the command (`:127`),
   refuses only changes left **outside** it (`:137-139`, read from `git status`) and hashes every regular file inside it as an
   artifact (`:141-151`). A file under `/tmp` would also pass `:137-139` (outside the repo, invisible to `git status`) but would
   not be recorded - not used. Measured by reading the tool; a `run_record.py run` was not executed in this revision (tree dirty, step 1).
3. Commit the `auto-01` record. The commit named in `run-record-v1.json` is the code version the acceptance binds to.
4. Second run (AC-13 sabotage) into `.../sabotage-01/`, same tool, after step 3; commit it.
5. `python3 scripts/run_record.py verify <dir>/run-record-v1.json` exit 0 for both runs; `manual.md` written by hand (AC-6).

### Kapsama tablosu (coverage of "zorunlu" phrases, F-4)

Measured 2026-10-22 with `grep -n -i "zorunlu" .antigravity/rules/<file>.md | wc -l`; lines are the grep hits. A phrase is
"covered" when a UIC item measures it; "meta" = a heading or file description, not a requirement. Rule text written in
capitals without the word (e.g. `frontend-form-template.md:21-31` "HER ALAN ... sarılır") is not in these counts.

| File | N | Covered by UIC (lines) | Not covered (lines: phrase) |
|---|---|---|---|
| `frontend-standards.md` | 15 | 9: UIC-7/8 (79-82), UIC-23 (99-100), UIC-28 (135), UIC-1 (206), UIC-17 (213); meta 2, 97 | 4: 91 HTML5 input types; 118 modal id referenced from JS must exist; 175, 188 KPI card rules (no KPI archetype in P1) |
| `frontend-datatable-template.md` | 23 | 18: UIC-3 (12, 19, 32, 42, 46, 59 via verifier `:964-968`, `:230-254`, 281), UIC-4 (13), UIC-22 (16, 195), UIC-23 (20, 21), UIC-14 (23, `showConfirm` count), UIC-24 (24), UIC-1 (278, 399), UIC-29 (630, 631); meta 3 | 4: 22 delete endpoint ownership; 238 colVis explicit override on runtime mutation (no UIC measures it - F-11); 622, 623 filter select attributes (`multiple` + `data-placeholder`, empty `ShowAll` option) |
| `frontend-js-standard.md` | 31 | 9: UIC-4 (30), UIC-3 (52 via verifier bulk wiring `:590` and coherence `:771-774`; 134 via verifier `:1060-1067`), UIC-31 (31), UIC-24 (33 `api/personalization` = 0, 73), UIC-14 (51 `showConfirm` count), UIC-32 (76, 341) | 22: 50 endpoint ownership; 56 row-action dropdown alignment; 67 `viewName` payload; 79, 81, 90-92, 103-105, 110, 444 select2 filter init (10); 121, 124 multi-select summary; 135 Compact Add New `href: '/{Module}/Create'` (verifier reads only the factory call, `:586` - F-11 re-check); 137 popup layer from modal; 147 sticky strip menu direction; 289 async init with `await loadDefaultView()` (factory-internal today, `diten-datatable.js:1031`; Golden Slim `index.js` has 0 hits, 69 legacy JS files carry the line - F-11); 345 column `name`; 913 radius fix; 972 `Index.cshtml` load order - factory-internal mechanics, no UIC item yet |
| `premium-modal-standard.md` | 1 | 1: UIC-14 (16, factory `buttonsStyling: false` = 1) | 0 |
| `page-help-and-keyboard.md` | 8 | 7: UIC-15 (48-51 by key family counts; 53 by the `Related` count against `PageHelpCatalog.Related`; 220), UIC-30 (109); meta 44 | 0 |
| `views-organization.md` | 2 | 2: UIC-2 (47, 53) | 0 |
| `unauthorized-surface-standard.md` | 1 | 1: UIC-8/16 (77) | 0 |
| `localization-standard.md` | 7 | 7: UIC-8 (3), UIC-7 (8, 58; 21 by the checker's "still equal to English" lines, SharedResource only; 29-31 by the `{Module}Title` / `PageDescription` count; 49, 53 by the 12-key vocabulary count - the verifier does not read `SharedResource`, `grep -c SharedResource verify_datatable_page.py` = 0, so the rev 02 "verifier v2" mapping was wrong) | 0 |
| `dynamic-localization-standard.md` | 2 | 2: UIC-6 (28, 30) | 0 |
| `frontend-details-template.md` | 2 | 1: UIC-1 (6); meta 3 | 0 |
| `frontend-form-template.md` | 2 | 1: UIC-1 (6); meta 3 | 0 (the §21-31 field rules are UIC-27, outside this count) |

Total 94 phrases: 58 covered, 6 meta, 30 not covered (listed). The uncovered phrases are a DL-2 revision input, not a P1 blocker (AC-15, FU-11).
Rev 03 re-check of every "covered" cell against the measuring command (F-11): 3 lines moved to "not covered" (`frontend-datatable-template.md:238`,
`frontend-js-standard.md:135`, `:289`); 1 line re-mapped inside "covered" (`frontend-js-standard.md:52` UIC-4 -> UIC-3); 5 cells kept "covered" only by
adding a measurement to the item (UIC-7 for `localization-standard.md:29-31, :49-53`; UIC-14 for `:23`/`:51` and `premium-modal-standard.md:16`;
UIC-15 for `page-help-and-keyboard.md:48-53`; UIC-24 for `frontend-js-standard.md:33`).

## Entity Fields

P1'de yok: no entity; the deliverables are Markdown files under `docs/` (D-1). P2 (DL-3) draft, not binding:
`UiPagePattern` (Code, Name, Archetype enum, GoldenReference enum, Status), `UiComponentStandard` (Code, Name, SourceRule,
LiveCodePath), `UiWizardTemplate` (Code, Steps), `UiChecklistItem` (UicId, AppliesTo, Mandatory, Measurement, SourceRule) -
all `GlobalEntity` (Runtime Constraints). Business version fields must not be named `Version` (`entity-base-template.md`).

## Repo Scope

- `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` (this file).
- DL-1 / DL-2: the D-1 folder under `docs/reference/...` (two files) - created only after D-1 is answered.
- Measurement record directory: `docs/records/audits/2026-10/mod-0286-uic-baseline-01/` (`auto-01/`, `sabotage-01/` written by `scripts/run_record.py`, plus `manual.md`).
- Nothing under `services/`, `frontend/`, `gateway/`, `.antigravity/`, `execution/registries/` in P1.

## Protected Paths

- `.antigravity/**` (D-2, D-5, D-8a..c and FU-1 are separate owner-approved commits).
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `Views/Archive/**`, `Controllers/Archive/**` (frozen).
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only; P2).
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`
  and every other domain's service (domain-config Protected Paths).
- `execution/domains/developer-enablement/module-packs/DEV-0000-*.md`, `DEV-0001-*.md` and their live code (referenced only).
- `docs/roadmap/backlog/product-backlog.md` (CT owns; this pack cites lines, does not edit).
- `wwwroot/assets/vendor/**` (vendor CSS; BL-236 token lives there); `wwwroot/assets/js/diten-datatable.js` (BL-440 owner; FU-10).

## Dependencies

P1 runtime dependencies: **none** (documents only). Tooling the checklist relies on, all present in this worktree:
`.antigravity/scripts/verify_datatable_page.py` (cited by 62 packs), `.antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py`
(`resx-check` skill), `scripts/check_pack_nav_keys.py`, `scripts/run_record.py` (`run`, `verify`), `scripts/acceptance_record.py`, `scripts/pack_approval.py`.

P2 (DL-3) HARD dependencies from Blueprint `Dependencies_Normalized` and their repo state (2026-10-22):

| Blueprint dependency | Repo pack | Registry row | State |
|---|---|---|---|
| MOD-0013 Platform Standards Registry | none | `:70` holds MOD-0013 as "Premium Modal Standard", retired-from-module-namespace | not started; identity note D-5 |
| MOD-0014 Module Boundary Registry | `MOD-0014-module-boundary-registry.md` | `:71` | pack `status: in-progress`; code presence not measured here |
| MOD-0018 RBAC / ABAC Authorization | `MOD-0018-*` (parent ready-for-dev; FU12 done, FU10/FU13 ready-for-dev) | `:72` | partial runtime |
| MOD-0021 Audit Trail Service | `MOD-0021-FU01` review, `FU02` ready-for-dev | `:91` "ready-for-dev / implemented evidence" | Platform ledger `tests/architecture/audit-ledger/Diten.Platform.md` İzler has accepted path `a` (`platform-merkezi`) |
| MOD-0028 Documentation & Evidence Management | `MOD-0028-FU01..FU03` approved | `:110` "review / planned" | partial |

Consumers (HARD, Blueprint): MOD-0295 Operational Agent Workspace (W-3, NOT STARTED; **no registry row** - `grep -n '^| MOD-0295 ' module-id-registry.md` -> none) and MOD-0297 HR Capability Block Shell (W-2, NOT STARTED; registry collision D-6).

## Runtime Constraints

- P1: no process, port, route, database or permission. Gateway 5000 / Platform 5057 are untouched.
- P2 (draft): records are platform-level catalogue entries, not tenant-owned, hence `GlobalEntity`: a page pattern or
  checklist item is the same for every tenant; DTOs carry no `TenantId`; uniqueness of `Code` is global. Soft delete
  only (`IsDeleted`, `DeletedAt`); `retire` is the user-facing verb, hard delete never.
- Platform modules: 2 languages (en, tr); `Views/Platform/UiGovernance/` under `_LayoutPlatformAdmin`.
- Lookups (P2): archetype and status option sets are design-time enums shipped with the module; if a dropdown needs a
  Platform lookup key, it is declared as a new PSS lookup in the P2 revision (`platform-lookups-reference-data.md`), never a hardcoded fallback list.

## Layout & Shell Contract

- P1: `shell: none` - no Razor view, no layout.
- P2: `shell: platform-admin` -> every file in `frontend/Diten.Web/Views/Platform/UiGovernance/*.cshtml` writes
  `Layout = "_LayoutPlatformAdmin";` explicitly (module-pack-standard §2; `Views/Platform/_ViewStart.cshtml` default is not relied on).
  Frontend route `/Platform/UiGovernance`; module folder name `UiGovernance` is a proposal.

## Backend File Convention

P1'de yok: no backend. P2 copies Golden Reference Slim exactly
(`services/Diten.Platform/src/Diten.Platform.Application/Features/UiGovernance/` with `Commands/`, `Queries/`,
`Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, `UiGovernanceModels.cs`; handler and validator
names without `Command` / `Query` suffix).

## Frontend File Contract

P1'de yok: no view. P2: each console list (Page Pattern Registry, Component Standard Detail list, UI Compliance Checklist)
is a Slim DataTable (expected <= 8 form fields each; count to be measured in the P2 revision) with the Slim file set
(`Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` via `_ListShell`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml`,
`_DetailsQuickView.cshtml`, `UiGovernanceIndex.cs`, `index.js`, `index.l10n.js`, `UiGovernanceIndex.{en,tr}.resx`);
`data_mode: client` with `data_mode_max_rows` measured, added to the frontmatter in the P2 revision.

## Validation Rules

P1'de yok: no field. Document-level rules instead: every `UIC-<n>` id unique; every automatic item has the five run-contract
fields (working directory, fixture, full argv, shell call made explicit, expected stdout/exit); every spec heading cites >= 1
source rule. P2: `Code` required, max 64, unique global; `Name` required, max 200.

## Failure Path to Verify

P1 (document failures, each must be reproducible):

- **Missing source citation** - a spec heading without `path` reference -> AC-1 fails; the DL-1 commit is not accepted.
- **Duplicate UIC id** - `grep -o '^| UIC-[0-9]*' compliance-checklist.md | sort | uniq -d` non-empty -> AC-4 fails.
- **Unmeasured "auto" item** - an item marked auto whose RC row has no `RC-n` / `exit=` pair in `auto-01/stdout.txt` -> AC-5 fails.
- **Auto item outside every RC row's "UIC kapsamı"** - AC-16 fails.
- **Tool error read as a pass** - `navkeys.out` contains "could not read" or `rc=2` -> UIC-21 / AC-8 fail even when no line names MOD-0286.
- **Spec contradicts a rule** - the rule wins; the contradiction goes to the Conflicts register, the spec text is corrected.
- **Rule contradicts a rule** - neither wins by itself; the register row names the applied source, the decider and "karar bekliyor" until the owner decides (D-8a..c); a row marked "çözüldü" without a decision -> AC-3 fails.
- **Unauthorized / concurrency** - not applicable in P1 (no runtime). P2 inherits the template cases (409 duplicate code, 400 missing name, 403 actor, 409 concurrency).

## Authorization Convention

P1: none. P2 draft (PKS-001):

```text
Policy:     [Authorize(Policy = "PlatformActor")]
Permission: platform.ui-governance.read | create | update | retire
Actor type: platform_admin
```

## Audited Events

P1 owns **no write command**; the table has no command row and claims no exception class (a missing command is an
absence, not an İ1-İ5 exception). The version history of the two documents is their trace. Answer to "does the service
have audit infrastructure today?" for P2: yes - `tests/architecture/audit-ledger/Diten.Platform.md` İzler lists
`platform-merkezi` (`a`, `IAuditableCommand`). P2 draft rows (every write is visible to all tenants, therefore never İ1;
none is K2 class):

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateUiPagePatternCommand | ui-governance.page-pattern.created | UiPagePattern | a | AfterState: Code, Name, Archetype, GoldenReference | - |
| UpdateUiPagePatternCommand | ui-governance.page-pattern.updated | UiPagePattern | a | Before/After: Name, Archetype, Status | - |
| RetireUiPagePatternCommand | ui-governance.page-pattern.retired | UiPagePattern | a | BeforeState: Code, Status | - |
| UpsertUiChecklistItemCommand | ui-governance.checklist-item.updated | UiChecklistItem | a | Before/After: Mandatory, Measurement, SourceRule | - |

## Gateway / API Routing Decision

- P1: Gateway change **gereksiz** - no endpoint.
- P2: gerekli - explicit `/api/ui-governance` upstream to Platform 5057 (incl. OPTIONS), browser via proxy profile
  `/Platform/UiGovernance/api`; `ocelot.json` is a separate integration-agent task.

## Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-1 `spec.md` exists at the D-1 path and each of its 9 top-level headings cites at least one source rule as `` `.antigravity/rules/<file>.md:<line>` `` or `` §<n> ``; `grep -c '\.antigravity/rules/' spec.md` >= 9.
- [ ] AC-2 (zorunlu) — The DL-1 and DL-2 commits change no rule or runtime file: the diff of those commits restricted to `.antigravity`, `services`, `frontend`, `gateway`, `execution/registries` is empty.
- [ ] AC-3 (zorunlu) — The spec's Conflicts register lists every rule-vs-rule and rule-vs-spec conflict found, at least the four of this pack (D-8a inline `style=` vs FG-003; D-8b `createCrudTable` 7.1 vs `createList`; D-8c "opsiyoneldir" layout vs explicit layout; D-5 "MOD-0013" label), and every row carries: parties as `path:line`, same-layer yes/no, applied source, rationale, decider, status (`karar bekliyor` / `çözüldü`). "Rule wins" appears only on rule-vs-spec rows; no rule-vs-rule row is `çözüldü` without a recorded owner decision.
- [ ] AC-4 (zorunlu) — DL-2 `compliance-checklist.md` has >= 32 rows `| UIC-<n> |` with unique ids and the five columns (item, applies to, requirement, how measured, source), all in the checklist table; the run contract is a separate section `## Koşu sözleşmesi` whose rows start `| RC-<n> |` (never `| UIC-`), so the uniqueness grep runs over the whole file: `grep -oE '^\| UIC-[0-9]+' compliance-checklist.md | sort | uniq -d` is empty and `grep -c '^| RC-' compliance-checklist.md` >= 21. Measured on this pack at rev 03: duplicates 0, `| UIC-` rows 32, `| RC-` rows 21.
- [ ] AC-5 (zorunlu) — All 22 automatic items are measured in **one** run `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01 -- sh -c '<script>'` on a clean tree, `<script>` as in the run contract (fixtures Golden Slim / `Platform/Tenants` / this pack; every RC row preceded by `echo "RC-n"` and followed by `echo "exit=$?"`); acceptance is read per RC row from `auto-01/stdout.txt` against the expected column, the run's overall exit is recorded, not judged (baseline red on FU-4); `python3 scripts/run_record.py verify docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01/run-record-v1.json` exits 0; the commit in that `run-record-v1.json` is the code version the acceptance binds to.
- [ ] AC-6 (zorunlu) — Every manual item names its evidence form (screenshot, count `n/N`, table, control list) and `docs/records/audits/2026-10/mod-0286-uic-baseline-01/manual.md` carries one table row per manual item in the form `UIC-n | sonuç | kanıt | inceleyen rol | tarih` (role = CT or independent reviewer, named; C-2), for the Golden Slim fixture, or `ölçülmedi` with the reason in the `sonuç` column.
- [ ] AC-7 (zorunlu) — The run record for the Golden Slim verifier reports the result as measured, including the known red on 2026-10-22 (1 gap, `personalization-client.js`, exit 1); no PASS is claimed without a green run record.
- [ ] AC-8 (zorunlu) — After the DL-2 commit, the RC-11 block (`python3 scripts/check_pack_nav_keys.py > $OUT/navkeys.out 2>&1; echo "rc=$?"; grep -c 'could not read' $OUT/navkeys.out; grep -c 'MOD-0286' $OUT/navkeys.out; git ls-files -- 'execution/domains/*/module-packs/MOD-0286-*.md' | wc -l`) satisfies all four: (1) `rc=0`, or `rc=1` where every finding line is one of the two known SCE lines (MOD-0176:645, MOD-0177:915, FU-9) and those lines are quoted in the record - `rc=2` fails; (2) "could not read" count `0` (`check_pack_nav_keys.py:207-211` prints that text and exits 2 on an unreadable pack set, without naming any pack - F-10); (3) `MOD-0286` count `0`; (4) `git ls-files` count `1`, proving the pack is in the scanned set (the script reads `git ls-files` only, `:131`). No `; true`: the exit is written, not swallowed. Measured 2026-10-22 at `19f0b2f7a` (pack tracked): `rc=1`, `0`, `0`, `1`.
- [ ] AC-9 (zorunlu) — `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286 --name "UI Design System & Page Pattern Governance"` exits 0 at the DL-1 and DL-2 commits.
- [ ] AC-10 (zorunlu) — `find docs -maxdepth 1 -type f ! -name README.md ! -name .DS_Store | wc -l` = 0 after DL-1/DL-2 (docs-organization K1).
- [ ] AC-11 (zorunlu) — P2 not started: `frontend/Diten.Web/Views/Platform/UiGovernance` and `services/Diten.Platform/src/Diten.Platform.Application/Features/UiGovernance` do not exist at DL-2 acceptance.
- [ ] AC-12 (zorunlu) — The binding sentence for other packs is proposed only in Follow-up Items (FU-1); `.antigravity/rules/module-pack-standard.md` is unchanged in the DL-1/DL-2 commits.
- [ ] AC-13 (isteğe bağlı) — Sabotage evidence with a **named new red**: a scratch copy in repo shape outside the tree (`<scratch>/frontend/Diten.Web/{Views,Models,wwwroot/assets/js,wwwroot/assets/css}` + `<scratch>/execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`; the verifier builds `frontend/Diten.Web/...` from its root argument, `verify_datatable_page.py:937-949`) first reproduces the baseline (97 `[PASS]` / 1 `[FAIL]`, measured 2026-10-22); then `_IndexL10n.cshtml` and `index.l10n.js` of the copy are deleted and `python3 .antigravity/scripts/verify_datatable_page.py <scratch> --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format report` turns `[PASS] _IndexL10n.cshtml exists` and `[PASS] index.l10n.js exists` into `[FAIL]` (measured: 7 `[PASS]` / 2 `[FAIL]`, exit 1 - the verifier stops at missing files). Recorded as `run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01 -- sh -c '<both verifier runs>'` after `auto-01` is committed. Removing the `createList` call is **not** a valid sabotage (`resolve_list_factory` returns silently, `:535-536`). Limit: the argv holds an absolute scratch path; reproduction requires rebuilding the copy from the file list above.
- [ ] AC-14 (isteğe bağlı) — The spec's component inventory table carries each shared file's line count measured at the DL-1 commit (`wc -l`).
- [ ] AC-15 (zorunlu) — DL-2 carries the coverage table: for each of the 11 rule files the `grep -n -i "zorunlu" | wc -l` count, the lines mapped to UIC ids, and the uncovered lines listed by phrase; the counts are re-measured at the DL-2 commit and the 30 uncovered phrases are carried as a DL-2 revision input (FU-11); a line is "covered" only when the named UIC item's measuring command reads that phrase (F-11).
- [ ] AC-16 (zorunlu) — Every item marked auto has a run-contract row `RC-<n>` with working directory (repo root), fixture, full argv (shell made explicit as `sh -c`), and expected stdout/exit; each of the 22 auto UIC ids appears in at least one RC row's "UIC kapsamı" column (a row may cover several ids, e.g. RC-3 = UIC-3, UIC-5, UIC-6, UIC-19); RC ids are unique: `grep -oE '^\| RC-[0-9]+' | sort | uniq -d` empty. Measured at rev 03: 21 RC rows, 0 duplicates, 22/22 auto ids covered (UIC-15 keys additionally in RC-21).

## Test Expectations

No code, no build. Document-level evidence, commands as written; none of the runs below is claimed here - the record
(AC-5) holds the real exit codes.

- Verifier on the Slim reference (positive): `python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format report`.
  Measured in this worktree on 2026-10-22: **exit 1**, 97 `[PASS]`, 1 `[FAIL]` - "personalizationClient sends tenant header only for
  tenant users" (`frontend/Diten.Web/wwwroot/assets/js/personalization-client.js`). Not fixed here (other module, XMC-001; Follow-up FU-4).
- Verifier sabotage (negative, AC-13): measured 2026-10-22 in the session scratch directory exactly as AC-13 describes - copy baseline 97/1 (a copy without `Models/` adds a second red, `Form ViewModel source file is discoverable`, so `Models/` is part of the copy), sabotage 7/2 with the two named `[FAIL]` lines; `--format gaps` lists them as `1) _IndexL10n.cshtml exists — Missing: ...`, `2) index.l10n.js exists — Missing: ...`. Not yet recorded through `run_record.py` (tree dirty, step 1 of the evidence flow).
- resx-check (negative): `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant`
  -> measured 2026-10-22: exit 1, 11 `[X]` lines (fr/es/zh/ar/ru each "Missing 35 key(s) from SharedResource.en.resx"; fr 43, es 32, zh 31, ar 31, ru 31, tr 5 keys "still equal to English"); `--context platform` -> exit 1, 1 `[X]` (tr 5). Shared debt; the record quotes the `[X]` lines as-is and separates them from any key this work adds (none). The "33 keys" figure of revision 01 is superseded by this measurement; no known-red note was found in `.antigravity/skills/i18n-localization/SKILL.md` (`grep -n -i 'bilinen\|known'` -> 0).
- Nav keys: RC-11 / AC-8 block measured 2026-10-22 at `19f0b2f7a` (pack tracked, output captured to a scratch file): `rc=1`, "could not read" `0`, `MOD-0286` `0`, `git ls-files` `1`; the three output lines are MOD-0176:645, MOD-0177:915 and the summary "14 key(s) off form, 12 known, 2 new" (FU-9).
- Identity: `verify_module_id.py --check-id MOD-0286 ...` -> exit 0 (measured 2026-10-22, see header).
- Approval: `python3 scripts/pack_approval.py check execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` -> measured 2026-10-22 while the file is uncommitted: exit 2 ("paket bu ref'te yok" - the tool reads the pack from the git ref); expected 1 (no approval row) once committed, until the owner approves.
- Docs placement: the AC-10 `find` command.

## Page Help (UXH-001)

- P1: no page, no `Help.{KOD}.*` keys, no tour ("tur yok").
- P2 proposal: page codes `UIGOV-PATTERNS`, `UIGOV-COMPONENTS`, `UIGOV-WIZARDS`, `UIGOV-CHECKLIST`, `UIGOV-CONSOLE`; each with
  `Help.{KOD}.Purpose`, `Help.{KOD}.Audience`, `Help.{KOD}.Step1..3` + titles in en and tr; `PageHelpCatalog` rows; no tour (not one of the five flows).

## Ready-for-dev Checklist

- [ ] D-1 answered by the owner (document path); D-2 answered (rules stay); D-8a..c answered or explicitly left "karar bekliyor" in the register.
- [ ] Owner named (`owner: TBD` -> person) and `target` set.
- [ ] Golden Reference Slim pack read as the P2 reference (not needed for P1; recorded for the P2 revision).
- [ ] Frontmatter: `service`, `shell`, `golden_reference`, `entity_base` present; `data_mode` noted as not applicable in P1 and required in the P2 revision.
- [ ] Layout & Shell Contract: P1 none, P2 `_LayoutPlatformAdmin` explicit - written.
- [ ] Backend / Frontend conventions: P1 none, P2 Slim - written.
- [ ] Validation Rules, Failure Path, Authorization, Gateway: P1 answers written; P2 drafts marked non-binding.
- [ ] Audited Events: no P1 command; P2 draft rows carry path `a` (AUD-001).
- [ ] Acceptance Criteria in `AC-<n> (zorunlu|isteğe bağlı)` form; Test Expectations quote commands only.
- [ ] Run contract rows `RC-1..RC-21` cover every auto item (AC-16); evidence flow is one `auto-01` run + one `sabotage-01` run.
- [ ] Page help: "no page" in P1 - written.
- [ ] Teslim dilimleri table present; DL-3 marked not deliverable from this pack.
- [ ] CT-APR-02 independent review of this pack version recorded in `execution/approvals/reviews.tsv` before any approval row.

## Implementation Notes

- Rev 02 (2026-10-22): Codex inceleme 01 UYGUN DEĞİL; F-1…F-8 kabul edildi ve işlendi; CT ek C-1, C-2 işlendi; karşılaştırma `docs/records/reviews/MOD-0286/2026-10-22-33dc5e14-ct-karsilastirma-01.md`.
- Rev 03 (2026-10-22): Codex inceleme 02 UYGUN DEĞİL; F-9…F-12 kabul edildi ve işlendi; karşılaştırma `docs/records/reviews/MOD-0286/2026-10-22-d15b6c8a-ct-karsilastirma-02.md`.
- Rev 03 mapping: F-9 -> UIC-24 exception removed (`frontend-js-standard.md:73`, `frontend-datatable-template.md:24` name none), UIC-32 exception in the source's words (`:76`: <= 2 data columns + reason comment); F-10 -> UIC-21, RC-11, AC-8 four-condition contract, output captured to `$OUT/navkeys.out`, `; true` removed, measured `rc=1 / 0 / 0 / 1`; F-11 -> coverage table re-checked cell by cell: 3 lines moved to "not covered" (`frontend-datatable-template.md:238`, `frontend-js-standard.md:135`, `:289`), 1 re-mapped (`:52` -> UIC-3), 5 cells kept by adding measurements (UIC-7, UIC-14, UIC-15, UIC-24; all fixture values measured at `19f0b2f7a`), totals 58 / 6 / 30, AC-15 and FU-11 updated; F-12 -> run contract keyed `RC-1..RC-21` with "UIC kapsamı" column, AC-4 and AC-16 rewritten, Owned Objects and Teslim dilimleri name the separate `## Koşu sözleşmesi` section; `grep -oE '^\| UIC-[0-9]+' | sort | uniq -d` on this pack = empty (measured). No UIC id added or renumbered (UIC-33 for `:289` rejected: the fixture measures 0 because the factory owns the await, so the item would pass by absence).
- Rev 02 mapping: F-1 -> run contract + UIC-1/2/7/13/14/17/20/21 rewritten with full paths and explicit `sh -c`; F-2 -> UIC-3/6/7 boundaries + UIC-26; F-3 -> UIC-24 zorunlu, UIC-31/32 split with the rules' own exceptions; F-4 -> UIC-27..30 + coverage table + AC-15; F-5 -> D-8a..c + register below + AC-3; F-6 -> evidence flow + AC-5; F-7 -> AC-13 named reds, measured; F-8 -> AC-8 narrowed + FU-9; C-1 -> UIC-17 fixture `Platform/Tenants`; C-2 -> AC-6 row format.
- **Çatışma kaydı (Conflicts register, seeds the spec's §9):**

| # | Parties (`path:line`) | Same layer | Applied source | Rationale | Decider | Status |
|---|---|---|---|---|---|---|
| 1 | `premium-modal-standard.md:27-28,35-36,43-44,51-52` inline `style=` vs FG-003 `frontend-js-standard.md:145`, `frontend-standards.md:194` | yes (`.antigravity/rules`) | FG-003 (classes in `backbone-custom.css`) | cross-cutting, newer rule; snippet has no runtime reader | rule owner (owner) | karar bekliyor (D-8a) |
| 2 | `frontend-js-standard.md:53` "Tercih edilen yol: createCrudTable" vs `frontend-js-standard.md:981` anti-pattern table and BL-440 (`product-backlog.md:6199-6203`) | yes (same file) | `createList` (BL-440 decision 2026-09-23) | verifier resolves `createList`; line 53 stale | rule owner (owner / CT) | karar bekliyor (D-8b) |
| 3 | `frontend-standards.md:208` "manuel `Layout = ` opsiyoneldir" vs `module-pack-standard.md:88` and `views-organization.md:39` explicit layout | yes | explicit layout (UIC-1) | two rules vs one; `module-pack-standard.md:292, :608` make it a testable AC | rule owner (owner) | karar bekliyor (D-8c) |
| 4 | "MOD-0013" labels `premium-modal-standard.md:1,11`, `dynamic-localization-standard.md:2,5,181`, `AGENTS.md:139` vs Blueprint MOD-0013 = Platform Standards Registry | yes (label) | MOD-0286 | registry row 70 aligns the UI-standard concept to MOD-0286 | owner (`.antigravity/**`) | karar bekliyor (D-5) |
| 5 | rule vs spec (any) | no (spec below rules, D-2) | the rule | authority order | pack author corrects the spec | çözüldü by construction |

- Two copies of the list shell exist: `Views/Shared/Components/DataTable/_ListShell.cshtml` (58 lines) and `Views/PPM/Shared/_ListShell.cshtml` (41 lines). The spec names the first as canonical; the second is a finding for the PPM owner (FU-3).
- The Golden Slim reference is red on one shared check today (Test Expectations). The spec must not describe a green baseline it does not have.
- Shared factory finding (UIC-29): `diten-datatable.js:1046` toolbar filter button has `title`, `aria-controls`, `aria-expanded` but no `aria-label`; `frontend-datatable-template.md:630` requires both. Not this pack's file (FU-10, XMC-001). Runtime DOM (DataTables Buttons may add attributes) ölçülmedi.
- MOD-0220: pack `status: ready-for-dev` while `Views/MasterData/LegalEntities/Wizard.cshtml` and `wizard.js` exist and are cited by the pack itself (line 562). The spec records the wizard as "exists, status mismatch in pack"; it does not touch MOD-0220.
- RCS-001: no common record-code service found in `services/` (0 hits); UIC-18 can only be checked in pack text until it exists.
- Language of this pack follows MOD-0230 revision 05 (`67fe7446c`, English body, Turkish standard tokens). That pack's frontmatter has `build_gate`, `operational_runtime_gate`, `slice` and no `approval_status`; `approval_status` is used only by MOD-0251 (`conditionally-approved-...`). This pack writes `approval_status: none` to make the missing approval explicit.
- Counting bases differ from the CT brief for packs (169 vs 173; 9 vs 13 missing; 15 vs 16 free text) and `DtDefaults` (177 files vs 176 pages); the brief's figures were not reproduced here, the commands above are the pack's basis.
- Post-write checks (rev 01, 2026-10-22, file uncommitted): `wc -l` 373; AC rows 14, `UIC-` rows 25; `verify_module_id.py --check-id MOD-0286` exit 0; `pack_approval.py check` exit 2 (uncommitted); `check_pack_nav_keys.py` exit 1 with 2 off-form keys in MOD-0176 / MOD-0177 (pre-existing); the four P2 keys checked with the script's own scanner. Rev 02 counts are in the handback and re-measured after commit.

## Follow-up Items

| Id | Item | Owner | Regression risk if deferred |
|---|---|---|---|
| FU-1 | Add to `module-pack-standard.md` §6/§7 the binding sentence "UI uyum: UIC-… listesi, ölçüm kaydı <yol>" as a required AC item for packs with a page | Owner (`.antigravity` approval) | 🟡 - without it new packs keep writing free-text UI claims; no runtime regression |
| FU-2 | Relabel MOD-0013 -> MOD-0286 in the three rule locations (D-5) | Owner | 🟢 - labels only |
| FU-3 | Duplicate `_ListShell.cshtml` under `Views/PPM/Shared/` - PPM owner to converge on the shared component | PPM pack owner (XMC-001: ask first) | 🟡 - two shells drift; verifier reads only the shared one |
| FU-4 | Golden Slim verifier red: `personalization-client.js` tenant-header check | developer-enablement / BL-440 owner | 🔴 - the reference every Slim pack copies fails its own gate; new packs inherit the red |
| FU-5 | Registry: add MOD-0295 row; resolve MOD-0297 collision (D-6) | Enterprise Architect | 🟡 - consumer dependencies of MOD-0286 cannot be cited until then |
| FU-6 | `MOD-0220` pack status vs wizard code mismatch | MDM pack owner | 🟢 - documentation only |
| FU-7 | P2 (DL-3) revision of this pack: entity fields, `data_mode`, permissions, Ocelot task, Help keys, Ctrl+K entries, 2-language resx - after D-4 | Pack author, on owner trigger | - |
| FU-8 | Accessibility tooling: no repo script measures contrast or focus ring (UIC-11, UIC-12 manual); evaluate adding one under `.antigravity/scripts/` | Owner / CT | 🟡 - manual items are skipped under time pressure |
| FU-9 | `check_pack_nav_keys.py` global exit 1: off-form nav keys at MOD-0176 pack line 645 and MOD-0177 pack line 915 (SCE) - ask the SCE pack owner (XMC-001) before touching | SCE pack owner / CT | 🟡 - every pack's global nav-key gate stays red until fixed; AC-8 is narrowed so DL-2 does not depend on it |
| FU-10 | Shared factory `diten-datatable.js:1046`: toolbar filter button lacks `aria-label` (rule `frontend-datatable-template.md:630`); other icon-only toolbar buttons to be measured | BL-440 owner (CT) (XMC-001) | 🟡 - every v2 list inherits the A11y gap; UIC-29 factory part stays red |
| FU-11 | DL-2 revision: decide a UIC item or an explicit "not in P1" for the 30 uncovered "zorunlu" phrases in the coverage table (select2 filter init x10, delete endpoint ownership x2, HTML5 input types, modal id reference, KPI card rules x2, filter select attributes x2, dropdown alignment, `viewName`, multi-select summary x2, popup layer, sticky menu, column `name`, radius fix, load order, colVis explicit override `frontend-datatable-template.md:238`, Compact Add New href `frontend-js-standard.md:135`, async init / `await loadDefaultView()` `:289`) | Pack author | 🟡 - a list can pass DL-2 while breaking one of these |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | Design System Specification (Tasarım Sistemi Şartnamesi) | Owned Objects §DL-1 outline, Conflicts register (D-8a..c), Repo Scope, Protected Paths, AC-1, AC-2, AC-3, AC-9, AC-10, AC-14 |
| DL-2 | UI Compliance Checklist + run contract + measurement binding (UI Uyum Kontrol Listesi) | Owned Objects §DL-2 table (rows `UIC-<n>`), run contract as its own section `## Koşu sözleşmesi` (rows `RC-<n>`, F-12), evidence flow, coverage table, Test Expectations, AC-4, AC-5, AC-6, AC-7, AC-8, AC-11, AC-12, AC-13, AC-15, AC-16 |
| DL-3 | P2 governance console - **deferred; not delivered from this pack; not assignable to a WP** until D-4 and the P2 revision (FU-7) | Entity Fields (P2), Layout & Shell Contract (P2), Backend File Convention, Frontend File Contract, Authorization Convention, Audited Events (P2 rows), Gateway / API Routing Decision, Page Help (P2) |
