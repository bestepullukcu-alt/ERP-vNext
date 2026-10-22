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

> **Draft, 2026-10-22.** This pack is a planning document. It authorizes no code. `status: draft`; both gates closed.
> `service: Diten.Platform` names the P2 host only - P1 touches no service. `shell: none` is the P1 value; P2 would be
> `platform-admin`. `golden_reference: none` because P1 has no DataTable; P2 console lists would be Slim (see Frontend
> File Contract). `entity_base: GlobalEntity` is the P2 value, justified under Runtime Constraints; P1 owns no entity.
> `approval_status: none` - no CT-APR-01 row exists for this pack (`scripts/pack_approval.py check`, see Implementation Notes).
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
| D-2 | Do the 11 UI rule files stay in `.antigravity/rules/` or move under the spec? | Owner | Stay (P1). The spec indexes them; the rules remain normative. | Authority order Module Pack > Domain Config > AGENTS.md > `.antigravity/`; moving is a protected-path change and would break 62 pack citations of `verify_datatable_page.py` plus the AGENTS.md §6.1 map. Rule wins over spec on conflict. |
| D-3 | BL-655: Blueprint retires MOD-0046 Tenant Core UI into "Platform Administration workspace + MOD-0286"; the repo maps MOD-0046 -> MOD-0009-FU03 (`module-id-registry.md:145`, `:67`) | Enterprise Architect / owner | Not decided by this pack. P1 proceeds under either mapping. | BL-655 is ERTELENDİ (`docs/roadmap/backlog/product-backlog.md:10841`, owner 2026-10-19, Master ADR-8.8-001 open, regression risk recorded there as green). |
| D-4 | P2 trigger (console, registry pages, API) | Owner | First real consumer (MOD-0295 or MOD-0297) reaching `approved`, or an explicit owner decision | Blueprint `Dependencies_Normalized`: both consumers are HARD dependants; both are NOT STARTED (`Dev_Readiness`). Building a console with no consumer repeats the "theme decision leaks page by page" problem in a new form. |
| D-5 | Relabel "MOD-0013" in `premium-modal-standard.md:1,11`, `dynamic-localization-standard.md:2,5,181` and `AGENTS.md:139` to MOD-0286 | Owner (protected path `.antigravity/**`) | Yes, in a separate approved commit; not in this pack | Blueprint MOD-0013 = Platform Standards Registry; registry row 70 already says the repo UI-standard concept aligns to MOD-0286. Regression risk: green (labels only; no code reads them). |
| D-6 | MOD-0297 identity: Blueprint 8.8 `Blueprint_Data` MOD-0297 = "HR Capability Block Shell" (domain 4, consumer of MOD-0286); repo registry row 189 MOD-0297 = "Tenant Subscription Management", deprecated alias -> MOD-0361 | Enterprise Architect | Record as a registry divergence; do not cite MOD-0297 as a consumer until resolved | Found while measuring consumers for this pack. `docs/reference/modules/platform/README.md` also lists MOD-0297 as "Tenant Subscription Lifecycle". Not this pack's to fix (XMC-001). |
| D-7 | Is the mobile (Android) design kit (`:core:design`, commits `0456e913e`, `4dbd105f9`; no `mobile/` folder in this tree) a consumer of MOD-0286 or a separate design system? | Owner | Separate; re-asked in the P2 revision | The web spec indexes Razor / Bootstrap / backbone-custom.css rules that do not apply to an Android kit; binding them now would make the P1 spec claim a surface it cannot measure. |

## Module Summary

MOD-0286 is the single governed source for UI standards: page archetypes, wizard patterns, component conventions and
accessibility-ready consistency across modules (Blueprint aim). In P1 it delivers two documents and nothing else:

- **DL-1 Design System Specification** - one index that names every page archetype, component standard, shell/layout
  rule, visual token, accessibility, language, unauthorized-face, search-registry, record-code and API-profile rule, each
  with its normative source (`path:line` or `path §n`) and its live code path. It changes no rule.
- **DL-2 UI Compliance Checklist** - a fixed-id list (`UIC-<n>`) of what a page or pack must satisfy, with the command
  that measures each item or the words "elle: inceleme kaydı" where no tool exists.

Why now - three measurements (2026-10-22, this worktree at `41901c477`):

1. **The standard is scattered.** `wc -l .antigravity/rules/{frontend-standards,frontend-datatable-template,frontend-details-template,frontend-form-template,frontend-js-standard,premium-modal-standard,page-help-and-keyboard,views-organization,unauthorized-surface-standard,localization-standard,dynamic-localization-standard}.md`
   = 220 + 644 + 104 + 305 + 1124 + 93 + 233 + 113 + 112 + 58 + 181 = **3187 lines in 11 files**, plus two Golden
   Reference packs (`DEV-0000-golden-reference-slim.md` ready-for-dev, 242 lines; `DEV-0001-golden-reference-compact.md`
   in-progress, 257 lines). No file says which rule wins when two disagree (conflicts listed in Implementation Notes).
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

- In-scope (P1): DL-1 specification; DL-2 checklist; the `UIC-<n>` id namespace; the measurement record format.
- In-scope (P2, deferred, DL-3): the five Blueprint soft pages as a Platform Admin console over `Diten.Platform`;
  UI-GOV-BUNDLE schemas; `AuditEvent v1` emission for governance writes.
- Out-of-scope: changing any `.antigravity/rules/**` file (D-2, D-5 are owner commits); refactoring any list page;
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
- Document: DL-2 `compliance-checklist.md` (path per D-1), items `UIC-1 ... UIC-25` (table below; ids are stable, never renumbered).
- Record: measurement record **directory** `docs/records/audits/2026-10/mod-0286-uic-baseline-01/` (dated record, `docs-organization.md` §1 question 2). Every automatic UIC run is produced by `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/<run> -- <command>` (clean tree, new run directory, raw output and exit code captured in `run-record-v1.json`); manual items go into the same directory as a human-written `manual.md`.
- Entities, commands, queries, DTOs, API endpoints, frontend routes, permissions: **none in P1**.
- RCS-001 (record code): P1 has no user-visible record -> "yok". P2 `UiPagePattern` code: OTOMATİK proposed, type prefix
  TBD per `record-coding-standard.md` §5.5; the owner decides, and the common code service must exist first
  (`grep -rl 'IRecordCodeService\|IRecordCodeGenerator' services --include='*.cs'` -> 0 files today).
- Nav key: P1 none (no page). P2 proposal, in the form `NavNameLocalizer.Normalize` reads: `Nav.Module.UIGOVERNANCE`,
  `Nav.Page.UIPAGEPATTERNS`, `Nav.Page.UICOMPLIANCECHECKLIST`.

### DL-1 specification outline (every heading cites its source rule and live code)

| # | Heading | Normative source (rule wins) | Live code measured 2026-10-22 |
|---|---|---|---|
| 1 | Page archetypes: List Slim (<=8 fields, offcanvas), List Compact (>8, full pages), Details, Form, bespoke console, wizard | `module-pack-standard.md` §3, §5; `frontend-datatable-template.md`; `frontend-form-template.md`; `frontend-details-template.md`; AGENTS.md §6 "Golden Reference DataTable Kararı" | Slim `Views/DevEnablement/GoldenReferenceSlim/` (7 resx); Compact `Views/DevEnablement/GoldenReferenceCompact/`; bespoke consoles: MOD-0155-FU05 Day/Week setup and MOD-0155-FU02 execution calendar (packs say "NOT a Golden DataTable CRUD surface"); wizard: `Views/MasterData/LegalEntities/Wizard.cshtml` (438 lines, `Layout = "_LayoutTenantShell"`, JS `wwwroot/assets/js/MasterData/LegalEntities/wizard.js` cited at MOD-0220 pack line 562) - the only wizard surface found; no wizard rule file exists |
| 2 | Component standards: premium modal/confirm, toast, offcanvas, quick view, list shell + factory, legacy CRUD table, select2, skeleton, filter partial, Save View, ColReorder, "?" help + shortcuts + tour | `premium-modal-standard.md`; `frontend-js-standard.md` (anti-pattern table :981); `frontend-datatable-template.md` (state standard :168, UI-019 :197); `page-help-and-keyboard.md` (UXH-001); `views-organization.md` §3 | `Views/Shared/_GlobalConfirmation.cshtml` (479 lines, `window.showConfirm` :206); `_GlobalNotification.cshtml` (269; Notyf :177, `window.showToast` :133, `DitenToastText` :109); `Views/Shared/Components/DataTable/_ListShell.cshtml` (58) + `_BulkActionBar.cshtml`, consumed by 41 views; `wwwroot/assets/js/diten-datatable.js` (1414; `createList` :961, legacy `createCrudTable` :251); `dt-defaults.js` (1603); `_TableSkeleton.cshtml` (51); `wwwroot/assets/js/shared/` premium-modal.js 126, diten-dialog.js 128, diten-help.js 138, diten-shortcuts.js 283, diten-tour.js 149, diten-tour-catalog.js 41 (3 flows: WORKCENTER, MYTIMESHEET, TIMEAPPROVALS), diten-enter-primary.js 207; select2 referenced in 210 JS files |
| 3 | Shell and layout: `_LayoutPlatformAdmin`, `_LayoutTenantShell`, frozen `_Layout`; `shell` x layout derivation | `views-organization.md` §2; `module-pack-standard.md` §2 "shell x Layout"; `frontend-standards.md` PROD-001, PROD-005 | `Views/Shared/_LayoutPlatformAdmin.cshtml`, `_LayoutTenantShell.cshtml`, `_Layout.cshtml`; `Views/Platform/_ViewStart.cshtml` |
| 4 | Visual tokens: `--diten-*` / `--bs-*`, light/dark, RTL, no inline CSS (FG-003) | `frontend-standards.md` §4.2 "No Hardcoded Colors", UI-001; FG-003 (`frontend-js-standard.md:145`, `frontend-standards.md:194`) | `wwwroot/assets/css/backbone-custom.css` 8622 lines; 1152 lines reference `--diten-`/`--bs-`; 32 dark-theme selectors; 13 `[dir="rtl"]` rules; 93 non-Archive `.cshtml` files still contain `style="` |
| 5 | Accessibility: AA 4.5:1, focus ring, keyboard | `frontend-datatable-template.md:628` A11y minimums ("Focus ring kaldırma yasak"); BL-100, BL-112, BL-236 (measured gaps) | no tool in repo; BL-100 method = real Tab-key count (17/60 stops without ring on 2026-08-14) |
| 6 | Language: platform en+tr, tenant 7 (en, tr, fr, es, zh, ar, ru); no hardcoded text; `window.L10n` bridge A (`GetAllStrings(true)`) for new surfaces; dynamic localization | `localization-standard.md` §1-§4; `dynamic-localization-standard.md` :78 "iki köprü"; AGENTS.md §6 "Yerelleştirme" | `Resources/Views/**/*.{lang}.resx` (platform sample `Views/Platform/Tenants` 2 files; tenant sample GoldenReferenceSlim 7); `SharedResource.{lang}.resx`; `_IndexL10n.cshtml` / `_L10n.cshtml` |
| 7 | Unauthorized face (UAS-001), Ctrl+K registry, record code `MG-ORG-000042` (RCS-001) | `unauthorized-surface-standard.md`; `platform-global-search-registry.md` §2, §5; `record-coding-standard.md` §1, §6 | `wwwroot/assets/js/diten-unauthorized.js` (52); `wwwroot/assets/json/platform-search.en.json`, `.tr.json` (+ legacy `platform-search.json`); record-code service: not found (0 files) |
| 8 | API profile (proxy / direct-gateway), no token in browser JS | `routes.md` §"Profil 1 / Profil 2"; `frontend-js-standard.md` JS-015 and anti-pattern table | verifier `--api-profile`; `window.API.{service}` for direct-gateway |
| 9 | Conflicts register: rule vs rule, rule vs spec; rule wins | this pack, Implementation Notes | - |

### DL-2 UI Compliance Checklist (draft items)

Columns: item - applies to (list / details / form / all pages / pack) - mandatory or optional - how measured - source.
"auto" = a command exists and is quoted; "elle" = manual review record. Automatic: 15 (UIC-1..7, 13, 14, 17, 19..22, 25). Manual: 10.

| Id | Item | Applies to | Req. | How measured | Source |
|---|---|---|---|---|---|
| UIC-1 | `Layout = "_LayoutPlatformAdmin"` or `"_LayoutTenantShell"` written explicitly in every non-partial view | all pages | zorunlu | auto: `grep -L 'Layout = "' frontend/Diten.Web/Views/{Area}/{Module}/[A-Z]*.cshtml` -> empty | `views-organization.md` §2; `module-pack-standard.md` §2 |
| UIC-2 | Skeleton in the shape of the content (`#skeleton-loader` / `backbone-skeleton`), no `spinner-border`, no "Loading" text | all data pages | zorunlu | auto (list): `verify_datatable_page.py`; auto (other): `grep -c 'backbone-skeleton\|skeleton-loader'` >= 1 and `grep -c 'spinner-border'` = 0 | `views-organization.md` §3, §3.1 |
| UIC-3 | Slim / Compact file set complete and matching `form_field_count` | list | zorunlu | auto: `python3 .antigravity/scripts/verify_datatable_page.py . --area {Area} --module {Module} --reference slim\|compact` | `module-pack-standard.md` §3, §5 |
| UIC-4 | List built with `DitenDataTable.createList` (or `DtDefaults.create`); no new `createCrudTable`, no raw `$(...).DataTable` | list | zorunlu | auto: verifier `[factory]` line; `grep -c 'createCrudTable(' index.js` = 0 | `frontend-js-standard.md:981`; `frontend-datatable-template.md` |
| UIC-5 | Quick view via event delegation (`.js-quick-view`), no inline `onclick` | list (Slim) | zorunlu | auto: verifier | `frontend-datatable-template.md` |
| UIC-6 | `window.L10n` bridge = partial + loader; new surfaces use pattern A (`GetAllStrings(true)`) | all pages with JS text | zorunlu | auto: verifier (list); `grep -c 'GetAllStrings(true)' _IndexL10n.cshtml` >= 1 (other) | `dynamic-localization-standard.md:30, :78` |
| UIC-7 | Language set complete: 2 resx (platform) or 7 (tenant) per marker class; SharedResource parity | all pages | zorunlu | auto: `ls frontend/Diten.Web/Resources/Views/{Area}/{Module}/*.resx \| wc -l` = 2 or 7; `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant\|platform` | `localization-standard.md` §1; `resx-check` skill |
| UIC-8 | No hardcoded user-visible text in `.cshtml` / `.js`; no `\|\| 'Export'` fallbacks | all pages | zorunlu | elle: inceleme kaydı (no script covers view text - `resx-check/SKILL.md`) | `localization-standard.md` §4 |
| UIC-9 | RTL (`ar`) renders without overlap | tenant pages | zorunlu | elle: browser in `ar`, screenshot in record | `localization-standard.md` §1; `backbone-custom.css` 13 RTL rules |
| UIC-10 | Dark theme renders with readable contrast | all pages | zorunlu | elle: both themes, screenshot | BL-236 method (`product-backlog.md:2567`) |
| UIC-11 | Visible focus ring on every Tab stop | all pages | zorunlu | elle: real Tab-key count `n/N` in record | `frontend-datatable-template.md:628`; BL-100 |
| UIC-12 | Text contrast >= 4.5:1 (AA) on card surface | all pages | zorunlu | elle: DevTools contrast per token, table in record | BL-236; BL-100 |
| UIC-13 | No inline CSS: no `style="` in views, no `element.style` in JS | all pages | zorunlu | auto: `grep -l 'style="' Views/{Area}/{Module}/*.cshtml` -> empty; `grep -c '\.style\.' index.js` = 0 | FG-003 (`frontend-js-standard.md:145`; `frontend-standards.md:194`); UI-001 |
| UIC-14 | Confirm / toast through `window.showConfirm` / `window.showToast`; no `alert(`, no raw `Swal.fire` outside the shared helper | all pages | zorunlu | auto: `grep -n 'alert(\|Swal\.fire\|toastr\.' index.js` -> empty | `premium-modal-standard.md` §4; `frontend-js-standard.md:981` |
| UIC-15 | "?" help keys `Help.{KOD}.Purpose/Audience/Step{n}/Step{n}Title` in all required languages; Enter = primary action; tour only for the five flows | all pages | zorunlu | elle for behaviour; auto for keys: `grep -c 'Help\.{KOD}\.' Resources/.../*.resx` per language | `page-help-and-keyboard.md` §2-§6 (UXH-001) |
| UIC-16 | Unauthorized user sees one explanation, no skeleton, no redirect | all pages | zorunlu | elle: login without the permission, screenshot | `unauthorized-surface-standard.md` §1-§2 (UAS-001) |
| UIC-17 | Ctrl+K registry entry in `platform-search.en.json` and `.tr.json` | platform pages | zorunlu | auto: `grep -c '"url": "/Platform/{Module}"' wwwroot/assets/json/platform-search.en.json platform-search.tr.json` = 1 each | `platform-global-search-registry.md` §2, §5 |
| UIC-18 | User-visible record code in `MG-ORG-000042` form from the common service; class OTOMATİK / İKİSİ DE / KALSIN in the pack | pages creating records | zorunlu where a code exists | elle: pack row + create one record; common service absent today (see Owned Objects) | `record-coding-standard.md` §1, §6 (RCS-001) |
| UIC-19 | API profile declared (`proxy` for Platform area, `direct-gateway` elsewhere) and followed; browser never calls a service port | list | zorunlu | auto: verifier `--api-profile proxy\|direct-gateway` | `routes.md` §Profil 1/2; AGENTS.md §3 |
| UIC-20 | No `document.cookie`, `access_token`, `Authorization: Bearer` in page JS | all JS | zorunlu | auto: verifier (list); `grep -n 'document.cookie\|Bearer' index.js` -> empty | `frontend-js-standard.md:981` |
| UIC-21 | Nav keys in the pack are in runtime form (`Nav.Page.CODE`, upper, alphanumeric) | pack | zorunlu | auto: `python3 scripts/check_pack_nav_keys.py` exit 0 | `module-pack-standard.md` §7 |
| UIC-22 | `data-dt-standard="v2"` and `data-dt-data-mode` equal to the pack's `data_mode` | list | zorunlu | auto: verifier `--data-mode server\|client` | `frontend-datatable-template.md:72` |
| UIC-23 | Required-field markers (UI-020); Compact form / details information parity (UI-024) | form / details | zorunlu | elle: field list compared in record | `frontend-standards.md` UI-020, UI-024 |
| UIC-24 | Save View dirty-state rule; export file = screen (BL-452); ColReorder | list | isteğe bağlı | elle: three actions exercised, noted in record | `frontend-datatable-template.md:107, :221` |
| UIC-25 | Touch protocol on a legacy list: `--format gaps` run, deviations numbered, owner asked | list (legacy edit) | zorunlu | auto: `python3 .antigravity/scripts/verify_datatable_page.py . --area {Area} --module {Module} --format gaps` output quoted | `frontend-datatable-template.md:151` |

How other packs bind to the list (proposal, not yet a rule): a module pack carries one Acceptance Criteria item
"UI uyum: UIC-<ids> listesi, ölçüm kaydı `docs/records/audits/<yyyy-mm>/<file>.md`". Writing that into
`module-pack-standard.md` is a `.antigravity` change (Follow-up Items, FU-1).

## Entity Fields

P1'de yok: no entity; the deliverables are Markdown files under `docs/` (D-1). P2 (DL-3) draft, not binding:
`UiPagePattern` (Code, Name, Archetype enum, GoldenReference enum, Status), `UiComponentStandard` (Code, Name, SourceRule,
LiveCodePath), `UiWizardTemplate` (Code, Steps), `UiChecklistItem` (UicId, AppliesTo, Mandatory, Measurement, SourceRule) -
all `GlobalEntity` (Runtime Constraints). Business version fields must not be named `Version` (`entity-base-template.md`).

## Repo Scope

- `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` (this file).
- DL-1 / DL-2: the D-1 folder under `docs/reference/...` (two files) - created only after D-1 is answered.
- Measurement record directory: `docs/records/audits/2026-10/mod-0286-uic-baseline-01/` (run directories written by `scripts/run_record.py`, plus `manual.md`).
- Nothing under `services/`, `frontend/`, `gateway/`, `.antigravity/`, `execution/registries/` in P1.

## Protected Paths

- `.antigravity/**` (D-2, D-5 and FU-1 are separate owner-approved commits).
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `Views/Archive/**`, `Controllers/Archive/**` (frozen).
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent only; P2).
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`
  and every other domain's service (domain-config Protected Paths).
- `execution/domains/developer-enablement/module-packs/DEV-0000-*.md`, `DEV-0001-*.md` and their live code (referenced only).
- `docs/roadmap/backlog/product-backlog.md` (CT owns; this pack cites lines, does not edit).
- `wwwroot/assets/vendor/**` (vendor CSS; BL-236 token lives there).

## Dependencies

P1 runtime dependencies: **none** (documents only). Tooling the checklist relies on, all present in this worktree:
`.antigravity/scripts/verify_datatable_page.py` (cited by 62 packs), `.antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py`
(`resx-check` skill), `scripts/check_pack_nav_keys.py`, `scripts/acceptance_record.py`, `scripts/pack_approval.py`.

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

P1'de yok: no field. Document-level rules instead: every `UIC-<n>` id unique; every automatic item quotes a runnable
command; every spec heading cites >= 1 source rule. P2: `Code` required, max 64, unique global; `Name` required, max 200.

## Failure Path to Verify

P1 (document failures, each must be reproducible):

- **Missing source citation** - a spec heading without `path` reference -> AC-1 fails; the DL-1 commit is not accepted.
- **Duplicate UIC id** - `grep -o '^| UIC-[0-9]*' compliance-checklist.md | sort | uniq -d` non-empty -> AC-4 fails.
- **Unmeasured "auto" item** - an item marked auto whose command was not run -> AC-5 fails; the record must show the exit code.
- **Spec contradicts a rule** - the rule wins; the contradiction goes to the spec's Conflicts register, the spec text is corrected.
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
- [ ] AC-3 (zorunlu) — The spec's Conflicts register lists every rule-vs-rule and rule-vs-spec conflict found, at least the three in Implementation Notes (`premium-modal-standard.md:28,36,44,52` inline `style=` vs FG-003; `frontend-standards.md:208` "manuel `Layout = ` opsiyoneldir" vs `module-pack-standard.md` §2 explicit layout; "MOD-0013" label at `AGENTS.md:139` and the two rule files), each with its `path:line` and each resolved "rule wins".
- [ ] AC-4 (zorunlu) — DL-2 `compliance-checklist.md` has >= 25 rows `| UIC-<n> |` with unique ids and the five columns (item, applies to, requirement, how measured, source); `grep -o '^| UIC-[0-9]*' compliance-checklist.md | sort | uniq -d` is empty.
- [ ] AC-5 (zorunlu) — At least 15 items are automatic; each quotes a command, and each command was run once against `DevEnablement/GoldenReferenceSlim` (or the item's stated fixture) through `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/<uic-id> -- <command>` on a clean tree; the exit code and raw output are in that run directory's `run-record-v1.json`, and `python3 scripts/run_record.py verify <dir>/run-record-v1.json` exits 0 for every run.
- [ ] AC-6 (zorunlu) — Every manual item names its evidence form (screenshot, count `n/N`, or table) and `docs/records/audits/2026-10/mod-0286-uic-baseline-01/manual.md` carries that evidence for the Golden Slim fixture or states "ölçülmedi" with the reason, one heading per `UIC-<n>`.
- [ ] AC-7 (zorunlu) — The run record for the Golden Slim verifier reports the result as measured, including the known red on 2026-10-22 (1 gap, `personalization-client.js`, exit 1); no PASS is claimed without a green run record.
- [ ] AC-8 (zorunlu) — `python3 scripts/check_pack_nav_keys.py` exits 0 with this pack and the two documents present.
- [ ] AC-9 (zorunlu) — `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0286 --name "UI Design System & Page Pattern Governance"` exits 0 at the DL-1 and DL-2 commits.
- [ ] AC-10 (zorunlu) — `find docs -maxdepth 1 -type f ! -name README.md ! -name .DS_Store | wc -l` = 0 after DL-1/DL-2 (docs-organization K1).
- [ ] AC-11 (zorunlu) — P2 not started: `frontend/Diten.Web/Views/Platform/UiGovernance` and `services/Diten.Platform/src/Diten.Platform.Application/Features/UiGovernance` do not exist at DL-2 acceptance.
- [ ] AC-12 (zorunlu) — The binding sentence for other packs is proposed only in Follow-up Items (FU-1); `.antigravity/rules/module-pack-standard.md` is unchanged in the DL-1/DL-2 commits.
- [ ] AC-13 (isteğe bağlı) — Sabotage evidence: a scratch copy of Golden Slim `index.js` with the `createList` / `DtDefaults` call removed makes the verifier exit 1; the run is recorded with `run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-uic-4 -- <command against the copy>` (the copy lives outside the repo tree so the pre-run clean-tree check holds).
- [ ] AC-14 (isteğe bağlı) — The spec's component inventory table carries each shared file's line count measured at the DL-1 commit (`wc -l`).

## Test Expectations

No code, no build. Document-level evidence, commands as written; none of the runs below is claimed here - the record
(AC-5) holds the real exit codes.

- Verifier on the Slim reference (positive): `python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format gaps --ok-line`.
  Measured once in this worktree on 2026-10-22: **exit 1**, one gap - "personalizationClient sends tenant header only for
  tenant users" (`frontend/Diten.Web/wwwroot/assets/js/personalization-client.js`). Not fixed here (other module, XMC-001; Follow-up FU-4).
- Verifier sabotage (negative, AC-13): copy `frontend/Diten.Web` to the scratch dir, delete the `createList` call line in
  `wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js`, rerun the command against the copy -> expected exit 1 with a `[factory]` gap. Not run (çalışma testi yapılmadı).
- resx-check (negative): `python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py . --context tenant`
  -> today FAILs on main by known debt (33 keys per non-English language, `resx-check/SKILL.md`); the record quotes the `[X]` lines as-is and separates them from any key this work adds (none).
- Nav keys: `python3 scripts/check_pack_nav_keys.py` -> expected exit 0 (the three P2 keys are in normalized form); result of the run after this draft is in Implementation Notes.
- Identity: `verify_module_id.py --check-id MOD-0286 ...` -> exit 0 (measured 2026-10-22, see header).
- Approval: `python3 scripts/pack_approval.py check execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` -> measured 2026-10-22 while the file is uncommitted: exit 2 ("paket bu ref'te yok" - the tool reads the pack from the git ref); expected 1 (no approval row) once committed, until the owner approves.
- Docs placement: the AC-10 `find` command.

## Page Help (UXH-001)

- P1: no page, no `Help.{KOD}.*` keys, no tour ("tur yok").
- P2 proposal: page codes `UIGOV-PATTERNS`, `UIGOV-COMPONENTS`, `UIGOV-WIZARDS`, `UIGOV-CHECKLIST`, `UIGOV-CONSOLE`; each with
  `Help.{KOD}.Purpose`, `Help.{KOD}.Audience`, `Help.{KOD}.Step1..3` + titles in en and tr; `PageHelpCatalog` rows; no tour (not one of the five flows).

## Ready-for-dev Checklist

- [ ] D-1 answered by the owner (document path); D-2 answered (rules stay).
- [ ] Owner named (`owner: TBD` -> person) and `target` set.
- [ ] Golden Reference Slim pack read as the P2 reference (not needed for P1; recorded for the P2 revision).
- [ ] Frontmatter: `service`, `shell`, `golden_reference`, `entity_base` present; `data_mode` noted as not applicable in P1 and required in the P2 revision.
- [ ] Layout & Shell Contract: P1 none, P2 `_LayoutPlatformAdmin` explicit - written.
- [ ] Backend / Frontend conventions: P1 none, P2 Slim - written.
- [ ] Validation Rules, Failure Path, Authorization, Gateway: P1 answers written; P2 drafts marked non-binding.
- [ ] Audited Events: no P1 command; P2 draft rows carry path `a` (AUD-001).
- [ ] Acceptance Criteria in `AC-<n> (zorunlu|isteğe bağlı)` form; Test Expectations quote commands only.
- [ ] Page help: "no page" in P1 - written.
- [ ] Teslim dilimleri table present; DL-3 marked not deliverable from this pack.
- [ ] CT-APR-02 independent review of this pack version recorded in `execution/approvals/reviews.tsv` before any approval row.

## Implementation Notes

- Conflicts found while indexing (the spec's Conflicts register starts with these; the rule file wins in each case until the owner changes it):
  1. `premium-modal-standard.md` §2 prescribes icon HTML with inline `style="..."` (lines 27-28, 35-36, 43-44, 51-52), while FG-003 (`frontend-js-standard.md:145`, `frontend-standards.md:194`, UI-001) forbids inline CSS.
  2. `frontend-standards.md:208` (PROD-005, Admin modülleri bullet) reads "`Views/Platform/_ViewStart.cshtml` zaten admin layout'u atadığı için view'da manuel `Layout = ` opsiyoneldir."; `module-pack-standard.md` §2 ("Razor sayfasinda layout ACIKCA yazilir; `_ViewStart.cshtml` varsayilani degistirilmez") and `views-organization.md` §2 require it explicitly in every page. Measured 2026-10-22: `grep -n 'opsiyoneldir' .antigravity/rules/frontend-standards.md` -> 208.
  3. "MOD-0013" labels in `premium-modal-standard.md`, `dynamic-localization-standard.md` and `AGENTS.md:139` name the Blueprint's Platform Standards Registry, not the UI standard (D-5).
- Two copies of the list shell exist: `Views/Shared/Components/DataTable/_ListShell.cshtml` (58 lines) and `Views/PPM/Shared/_ListShell.cshtml` (41 lines). The spec names the first as canonical; the second is a finding for the PPM owner (FU-3).
- The Golden Slim reference is red on one shared check today (Test Expectations). The spec must not describe a green baseline it does not have.
- MOD-0220: pack `status: ready-for-dev` while `Views/MasterData/LegalEntities/Wizard.cshtml` and `wizard.js` exist and are cited by the pack itself (line 562). The spec records the wizard as "exists, status mismatch in pack"; it does not touch MOD-0220.
- RCS-001: no common record-code service found in `services/` (0 hits); UIC-18 can only be checked in pack text until it exists.
- Language of this pack follows MOD-0230 revision 05 (`67fe7446c`, English body, Turkish standard tokens). That pack's frontmatter has `build_gate`, `operational_runtime_gate`, `slice` and no `approval_status`; `approval_status` is used only by MOD-0251 (`conditionally-approved-...`). This pack writes `approval_status: none` to make the missing approval explicit.
- Counting bases differ from the CT brief for packs (169 vs 173; 9 vs 13 missing; 15 vs 16 free text) and `DtDefaults` (177 files vs 176 pages); the brief's figures were not reproduced here, the commands above are the pack's basis.
- Post-write checks (2026-10-22, this worktree, file uncommitted): `wc -l` 373; `grep -c '^## '` 24 (22 required + Open Decisions + Page Help); AC rows 14, `UIC-` rows 25, duplicate ids 0; `verify_module_id.py --check-id MOD-0286` exit 0; `pack_approval.py check` exit 2 (uncommitted, see Test Expectations); `check_pack_nav_keys.py` exit 1 with 2 new off-form keys in MOD-0176 / MOD-0177 (other packs, pre-existing) - it reads `git ls-files` only, so this untracked pack was not scanned by it; its four keys (`Nav.Module.UIGOVERNANCE`, `Nav.Page.UIPAGEPATTERNS`, `Nav.Page.UICOMPLIANCECHECKLIST`, `Nav.Page.CODE`) were checked with the script's own scanner (result in the handback; to be re-run after commit, AC-8).

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

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | Design System Specification (Tasarım Sistemi Şartnamesi) | Owned Objects §DL-1 outline, Repo Scope, Protected Paths, AC-1, AC-2, AC-3, AC-9, AC-10, AC-14 |
| DL-2 | UI Compliance Checklist + measurement binding (UI Uyum Kontrol Listesi) | Owned Objects §DL-2 table, Test Expectations, AC-4, AC-5, AC-6, AC-7, AC-8, AC-11, AC-12, AC-13 |
| DL-3 | P2 governance console - **deferred; not delivered from this pack; not assignable to a WP** until D-4 and the P2 revision (FU-7) | Entity Fields (P2), Layout & Shell Contract (P2), Backend File Convention, Frontend File Contract, Authorization Convention, Audited Events (P2 rows), Gateway / API Routing Decision, Page Help (P2) |
