# Q176 — pack text patch: MOD-0186 / MOD-0190 / MOD-0192 Layout line (OD-PACK-LAYOUT) — CHANGE NOTES

WP Q176 · Prompt Q176 v1 · Template T1 v1 (SOP v2.5 §36.2) · module-pack-author (`/prepare-module-pack` text-edit rules) · LANE 1 (Cowork, Linux VM).
Written 2026-09-29T13:33+03:00. Branch `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. **Patch only — the packs are NOT changed**
(apply follows independent VER Q177 and owner sign-off). Commit YOK, push YOK.

Authority: OD-PACK-LAYOUT in `docs/records/audits/2026-09/mvp6-ct-verdicts-q170-q160-2026-09-28.md` (`65d651d18e56b83c01ddca0206567996069254ac8113b5de959ad2632d6e0abb`).
Precedent: Q114 Claims P1 (`docs/records/audits/2026-09/mvp6-q114-claims-pack-patch-01/02-MOD-0187-P1-only.patch`), live result
`execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` (`3d1a00e2…`) lines 562–563:

> - `shell: tenant` → the Claims page view (`Index.cshtml`) states `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`)
>   set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02); `_ViewStart.cshtml` unchanged.

## Patch

`packs-layout.patch` — sha256 `f43b67212d9198ca19f9c5f5f3285daf50d7a15e8d5261bf27fd82598a9ea0ee`; 3 hunks (`@@ -610,7 +610,7 @@`, `@@ -291,7 +291,7 @@`, `@@ -300,7 +300,7 @@`); 3 lines removed, 3 added, nothing else.

| Pack (`execution/domains/supply-chain-execution/module-packs/`) | Line | Preimage sha256 | Postimage sha256 |
|---|---:|---|---|
| `MOD-0186-reverse-logistics.md` | 613 | `fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27` | `933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe` |
| `MOD-0190-sop-workflow-signoffs.md` | 294 | `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` | `c076790df7fbcfe35064606d168806a40eb8ae0462691c3b60adb2103a185142` |
| `MOD-0192-capacity-planning.md` | 303 | `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b` | `ace0f58fa75620765301dc5b6a49cf322b0f4cd166e8b4b26a2878af00a98f7d` |

## Per hunk: old line → new line

### MOD-0186-reverse-logistics.md line 613

- old: - `shell: tenant` → every Returns `.cshtml` states `Layout = "_LayoutTenantShell";` explicitly; `_ViewStart.cshtml` unchanged.
- new: - `shell: tenant` → the Returns page view (`Index.cshtml`) states `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- source/authority: OD-PACK-LAYOUT; wording mirrors MOD-0187:562–563 (Q114 P1); rule UI-PM-01 (checklist r2, live `.antigravity/agents/frontend-ui-ux.md` `90247ddc…`).

### MOD-0190-sop-workflow-signoffs.md line 294

- old: - `shell: tenant` → every S&OP `.cshtml` states `Layout = "_LayoutTenantShell";` explicitly; `_ViewStart.cshtml` unchanged.
- new: - `shell: tenant` → the S&OP page views (`Index.cshtml`, `Details.cshtml`) state `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- source/authority: OD-PACK-LAYOUT; wording mirrors MOD-0187:562–563 (Q114 P1); rule UI-PM-01 (checklist r2, live `.antigravity/agents/frontend-ui-ux.md` `90247ddc…`).

### MOD-0192-capacity-planning.md line 303

- old: - `shell: tenant` → every Capacity `.cshtml` states `Layout = "_LayoutTenantShell";` explicitly; `_ViewStart.cshtml` unchanged.
- new: - `shell: tenant` → the Capacity page views (`Index.cshtml`, `Details.cshtml`) state `Layout = "_LayoutTenantShell";` explicitly; partial views (`_*.cshtml`) set no `Layout`, because an explicit Layout on a partial renders a second shell (Q64b D-02; UI-PM-01); `_ViewStart.cshtml` unchanged.
- source/authority: OD-PACK-LAYOUT; wording mirrors MOD-0187:562–563 (Q114 P1); rule UI-PM-01 (checklist r2, live `.antigravity/agents/frontend-ui-ux.md` `90247ddc…`).

## Checks (on /tmp copies only)

- Preimages = dispatch hashes (asserted before editing); each old line occurs exactly once in its pack.
- `patch --dry-run -p1` on fresh copies → exit 0; real apply → exit 0; 0 `.orig`/`.rej`; results byte-identical to the edited copies;
  `patch -R --dry-run` → exit 0.
- Page views per module (pack view list and the v2 overlays `mvp6-*-ui-draft-02`): Returns `Index.cshtml` only (`_DetailsQuickView.cshtml`
  is a partial); S&OP `SandopPlans/Index.cshtml` + `Details.cshtml`; Capacity `CapacityPlans/Index.cshtml` + `Details.cshtml`; all other views are partials.
- No pack has a revision table → no revision row added (Q114 precedent added none).

## ASSUMPTIONs

- **A1 (wording):** the Claims wording after Q114 differs from the dispatch sample, so it is mirrored (per the dispatch), with the module name and
  page views adjusted and "UI-PM-01" added beside "Q64b D-02". Returns names only `Index.cshtml` although the dispatch sample says "(Index, Details)",
  because Returns has no Details page view (its details are the `_DetailsQuickView` partial).
- **A2 (one line):** each change stays on one line (−1/+1) to meet "1 line per pack"; the Claims precedent wraps the same text over two lines.

## Deviations

- **D-1 (placement):** the dispatch asks for "a NEW chat (not the ledger-writer chat)"; this ran in the LANE 1 ledger-writer chat. The placement gate
  (`uname -s` = Linux) held, this WP writes no ledgers, and no ledger was touched (same treatment as Q168 D-1 / Q171 D-1).
- **D-2 (interruption):** the first attempt (29 Sep ~10:26 +03:00) lost the device bridge before any repo write; the VM `/tmp` was reset. The run was
  resumed at 13:32 +03:00 after re-checking that the three packs still equal their preimages and that this folder did not exist.

Agent PASS ≠ CT ACCEPTED — return to CT.
