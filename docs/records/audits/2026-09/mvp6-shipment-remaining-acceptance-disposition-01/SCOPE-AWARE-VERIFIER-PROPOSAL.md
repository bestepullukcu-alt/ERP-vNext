# Scope-aware Shipment DataTable verifier profile — review proposal

## Status and authority

This completes the existing `mvp6-shipment-remaining-acceptance-disposition-01` review package. It does not change `.antigravity`, the repository validator or product source. The historical generic result remains **49 PASS / 35 FAIL** and the generic repository gate remains FAIL.

The profile is a proposal pending the existing quality-gate owner decision in `DECISION-NEEDS.md` §DN-02. It is not a waiver, approval or general PASS.

## Controlling scope

The proposed profile evaluates only UI183-A01, A02, A05, A06, A07, A08, A13, A14 and A15 as referenced per row in `DATATABLE-35-ACCEPTANCE-MAP.tsv`. It must run against one immutable Lane A final manifest that contains the CT-accepted three-path accessibility successor. If the separately authorized 12-path shared UI candidate is applied, its exact target hashes must also be part of that same manifest. The profile cannot treat an unapplied candidate as final source.

## Applied checks

1. Build/Razor resolution: Index, filter and localization partials resolve and render even when absolute partial syntax is used.
2. DataTable contract: `data-dt-standard="v2"`, `new DataTable`, `window.DtDefaults.create`, `stateSave:false`, supported server paging, skeleton and responsive control/action access.
3. Same-origin transport: browser calls only `/SupplyChain/Shipments/api`; the MVC adapter alone targets Gateway 5000. Browser calls to Gateway or port 5061 fail the profile.
4. Frozen list behavior: only status, sourceDocumentId, page and pageSize are sent; enum casing and `pageSize<=200` are retained.
5. Seven-language presentation: action/detail/cancel/search/filter/apply/reset/status labels render without missing keys; Arabic RTL and 390/768/desktop measurements are recorded.
6. Accessibility: preserve the accepted three-path successor; verify labels, keyboard/focus and responsive behavior without reopening its bounded CT decision.
7. Bounded actions: detail, valid transition choices and POD remain permission/lifecycle gated; SweetAlert/shared wrappers are used for approved confirmations.
8. Negative surface: edit, delete, bulk, QuickView, import, export, ShowAll, SaveView, column visibility, select-all and `/bulk` remain absent from source, DOM and network.
9. Evidence integrity: bind source→build→process→browser; record exact viewport, culture, request route and console/missing-key results. Do not share Lane A's mutable browser/DB environment; verifier uses its own environment after writer-complete.

## Excluded generic checks

The 23 `OUT_OF_SCOPE_FULL_CRUD_EXPECTATION` rows remain explicit negative assertions. They are excluded as positive requirements because UI183-A14 requires unsupported edit/delete/bulk behavior to remain absent, UI183-A08 uses the frozen Shipment lifecycle rather than Active/Passive, and no approved import/export/personalization/column-management surface exists. Exclusion does not suppress the original failures; each row remains in the historical log and receives a final-source absence check.

## Eight Lane A evidence gaps

Rows 08, 10, 15, 16, 19, 20, 21 and 25 require rendered evidence rather than a key being declared in one specific partial. Lane A must supply seven-culture DOM/browser evidence, responsive action access at 390/768, filter keyboard/control binding, Apply/Reset network behavior and exact status token/casing. Static resource presence alone cannot close them.

## Four unresolved owner/policy conflicts

These are the only four policy conflicts in the 35-row result. The 23 out-of-scope expectations and eight evidence gaps retain their existing classifications and do not depend on these decisions.

### PC-02 — `_Filter` partial spelling versus resolved behavior

- **Conflicting exact rule:** `.antigravity/rules/frontend-datatable-template.md:12` mandates the literal `<partial name="_Filter" />`. `.antigravity/scripts/verify_datatable_page.py:484-491` implements that mandate as an exact regex.
- **Bounded acceptance:** `UI183-A02` requires the exact bounded filters and list query; `UI183-A14` requires the repository gate. The current source calls the same `_Filter.cshtml` through an absolute partial path, compiles, and rendered the status/source-document controls in browser evidence.
- **Current decision owners:** the shared frontend quality-gate owner owns the global literal rule and verifier implementation. The MOD-0183 `supply-chain-execution / control-tower` owner owns the bounded Shipment acceptance. A profile exception needs both scopes bound; this report supplies neither approval.
- **Recommended ruling:** allow semantic Razor resolution for this bounded profile. The exact `_Filter.cshtml` must compile, render, expose only the approved controls, bind labels, and produce only the frozen query.
- **Alternative:** require the literal relative token. That choice requires a separately authorized Shipment source successor and fresh Razor/browser regression; this plan does not make the edit.
- **Verifier effect:** recommended ruling replaces only this literal regex with build-time partial resolution plus DOM/network assertions. The alternative retains FAIL 02 until the source changes. Neither ruling changes the other 34 rows.

### PC-03 — generic Compact section parity versus bounded Shipment information architecture

- **Conflicting exact rule:** `.antigravity/rules/frontend-datatable-template.md:20` requires `_Form.cshtml` and `Details.cshtml` to share the same logical section map. `.antigravity/scripts/verify_datatable_page.py:118-168` compares localized section keys in order.
- **Bounded acceptance:** `UI183-A05` freezes 13 create inputs. `UI183-A06` separately requires read-only detail data including nullable carrier/load, POD and lifecycle root. Those detail-only facts are not create inputs.
- **Current decision owners:** the shared frontend standard/quality-gate owner owns the parity mandate. The MOD-0183 `supply-chain-execution / control-tower` owner owns A05/A06 and the approved field boundary. A module-only reviewer cannot silently override either.
- **Recommended ruling:** approve a Shipment-only bounded exception and verify A05 and A06 independently. Every create field must belong to an accessible create section; every required detail fact must belong to a detail section. Detail-only read models do not create empty or editable form sections.
- **Alternative:** enforce exact section parity by redesigning the presentation without adding editable business fields. This needs an explicit UI design/source decision and a new presentation successor.
- **Verifier effect:** recommended ruling disables only section-key equality and substitutes exact A05/A06 field/section inventories. The alternative leaves FAIL 03 controlling until the approved redesign passes the generic equality check.

### PC-04 — `_IndexL10n` partial spelling versus rendered localization

- **Conflicting exact rule:** `.antigravity/rules/frontend-datatable-template.md:15` defines the `_IndexL10n.cshtml` JSON bridge and its template at lines 276-279 uses the literal relative partial. `.antigravity/scripts/verify_datatable_page.py:792-800` checks that literal token.
- **Bounded acceptance:** `UI183-A13` requires seven-language rendered text, RTL and no raw/missing keys; `UI183-A14` requires the localized DataTable surface. The source resolves the same `_IndexL10n.cshtml` by absolute partial path.
- **Current decision owners:** the shared frontend quality-gate owner owns the literal rule/verifier. The MOD-0183 `supply-chain-execution / control-tower` owner owns A13/A14. Both scopes must be represented in an exception decision.
- **Recommended ruling:** accept semantic Razor resolution for this bounded profile, provided the exact partial resolves, produces valid JSON, `index.l10n.js` merges it, all seven cultures render correctly, and missing-key/console checks pass.
- **Alternative:** require the literal relative token through a separately authorized source successor and rerun the seven-language presentation checks.
- **Verifier effect:** recommended ruling replaces only FAIL 04's literal regex with resolution, JSON, merge and rendered-output checks. The alternative keeps FAIL 04 until source modification. The eight evidence-gap localization rows remain separate and still require fresh evidence.

### PC-28 — non-Platform direct-Gateway default versus Shipment same-origin MVC

- **Conflicting exact rule:** `.antigravity/scripts/verify_datatable_page.py:418-434` defaults non-Platform modules to `direct-gateway`; lines 995-1003 then require `window.API.{service}` in browser JavaScript.
- **Bounded acceptance:** `UI183-A01` requires same-origin browser traffic through `/SupplyChain/Shipments/api`; only the MVC adapter may call Gateway 5000. Its falsification set rejects browser traffic to service port 5061. The integration boundary also assigns global route ownership outside the module UI.
- **Current decision owners:** the shared frontend quality-gate owner owns the default API profile. The MOD-0183 `supply-chain-execution / control-tower` owner owns A01; the integration owner owns Gateway routing. Direct browser transport cannot be selected by the verifier alone.
- **Recommended ruling:** select a Shipment-specific same-origin-MVC profile. Require same-origin browser requests, inspect the controller's Gateway 5000 target, and fail any browser request to Gateway/service ports.
- **Alternative:** amend the bounded acceptance to direct-Gateway browser traffic. That requires explicit MOD-0183, integration and security-owner approval plus new CORS/auth/browser acceptance. It is not a verifier-only choice.
- **Verifier effect:** recommended ruling replaces FAIL 28's `window.API` positive check with browser-origin/network and controller-target assertions. The alternative retains the generic direct-Gateway check and leaves A01 in conflict.

## Owner decision form

The owner may approve the recommended rulings as one bounded profile decision by recording all four identifiers and their scope:

> `PC-02`, `PC-03`, `PC-04` ve `PC-28` için SCOPE-AWARE-VERIFIER-PROPOSAL.md içindeki önerilen hükümleri, yalnız MOD-0183 Shipment bounded verifier profili kapsamında onaylıyorum. Generic DataTable sonucu 49 PASS / 35 FAIL olarak korunacaktır. Bu karar `.antigravity` guard/verifier değişikliği, waiver, repository-geneli PASS veya ürün kaynak değişikliği yetkisi vermez. Profil ancak exact A+B successor manifesti üzerinde bağımsız verification sırasında proposal olarak uygulanabilir; kalıcı guard aktivasyonu ayrıca yetkilendirilmelidir.

The owner may instead select an alternative per identifier, but must name that identifier and authorize the required source/design or transport amendment. Silence or partial approval leaves the corresponding row `DECISION REQUIRED`.

Until all four have an exact disposition, UI183-A14 remains `OPEN_POLICY_PROFILE`; the scope-aware profile is not active and cannot return PASS.

## Result semantics

The future verifier reports three independent results:

- historical generic gate: **49 PASS / 35 FAIL — preserved**;
- bounded profile: per-check PASS/FAIL/OPEN on the immutable final manifest;
- UI183-A14: eligible for CT review only after the four policy conflicts are approved and all eight evidence rows have fresh Lane A evidence.

No bounded result becomes a waiver, generic validator PASS, repository-wide quality PASS, full Shipment acceptance, rollout or E5/G5 decision.
