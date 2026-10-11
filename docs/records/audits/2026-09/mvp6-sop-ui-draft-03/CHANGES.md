# Q183 — MOD-0190 S&OP workflow/sign-offs UI draft overlay v3: nav page key rename (C1–C2)

**Agent verdict: DRAFT v3 written (archive only). Not built, not tested.** Agent PASS ≠ CT ACCEPTED.

| Field | Value |
|---|---|
| WP / Prompt / Template | Q183 · Prompt Q183 v1 · T1 v1 (SOP v2.5 §36.2) · frontend-ui-ux |
| CT-QUEUE row | line 267: `Q183 · S&OP + Capacity UI draft v3: nav page key rename (F-Q84b-1, OD-NAVKEY-CAP) — new folders mvp6-sop-ui-draft-03 and mvp6-capacity-ui-draft-03 · READY · LANE 2 (frontend-ui-ux) · Q182` |
| Lane | Cowork LANE 2 (Linux VM; `uname -s` = Linux); temp folder outside the repo (`$HOME/q183-tmp/`) |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Base Stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (unchanged; this WP only revises the draft overlay) |
| Input (v2) | `docs/records/audits/2026-09/mvp6-sop-ui-draft-02/sop-ui-draft-overlay-v2.tar.gz` `7a9666c7ee1dfab4f85c1324db38ab06ed87ed1c385de706629e60ed68e394e3`; folder SHA256SUMS `0bbb22a8efa2435f6eb1a3b51b36a6276b871a31029420afa7a09405f6ce6cc1` (all OK) |
| Output (v3) | `sop-ui-draft-overlay-v3.tar.gz` `716e7c4c6325d26e8711c9b401d12fb61594f3eb310d59c280f3307d64904332` — 47 files, same internal paths, modes and owner as v2; 8 files changed, 39 byte-identical |
| Authority | F-Q84b-1 (`docs/records/audits/2026-09/mvp6-q84b-sop-mac-01/REPORT.md` `a324b35b…`, line 166); OD-F-Q84b-1 in `docs/records/audits/2026-09/mvp6-ct-verdicts-q84b-2026-09-30.md` (`f57ee43c…`, lines 43) |

## Why

- **F-Q84b-1:** the draft fragments carried `Nav.Page.SANDOP_PLANS`. The runtime looks the page name up as `"Nav.Page." + Normalize(PageCode)`.
  `Normalize` (`frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs` `e8299a5c…`, lines 66–74) upper-cases the code and drops every
  non-alphanumeric character. So page code `SANDOP_PLANS` (SopWorkflowSignoffsManifestProvider.cs:30 (PageCode "SANDOP_PLANS", ParentPageCode null :34)) is read as `Nav.Page.SANDOPPLANS`. With the underscore key,
  `NavManifestL10nGuardTests.Every_nav_visible_manifest_page_has_a_Nav_Page_key_in_all_seven_languages` fails in 7 languages
  (Q84b REPORT line 86), and the sidebar would print raw English.
- **OD-F-Q84b-1 (owner, 30 Sep):** fix by a key rename in the draft (v3) plus the `platform-registration.md` item 5 correction; no product code change.

## What changed (exactly C1–C2; `FILE-PLAN.tsv`)

- **C1:** `overlay/_shared-integration/sharedresource-nav-keys/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx.fragment.xml` —
  `name="Nav.Page.SANDOP_PLANS"` → `name="Nav.Page.SANDOPPLANS"`. The values are unchanged in all 7 languages.
- **C2:** `overlay/_shared-integration/platform-registration.md` item 5 now names `Nav.Page.SANDOPPLANS` (line 14), and has one added
  line: "Key = Nav.Page. + NavNameLocalizer.Normalize(PageCode)" (line 16).
- **Not changed:** PageCode values, manifest provider, routes, permissions, views, tests, and every other file (39/47 byte-identical to v2).

## Static checks (`CHECK-RUN.tsv`, all PASS)

- All 7 fragments are well-formed XML.
- For every nav-visible page (ParentPageCode null) in the manifest provider, `Nav.Page.`+Normalize(PageCode) is present in 7/7 fragments.
- `Nav.Module.`+Normalize(ModuleCode) is present in 7/7.
- No fragment key contains `_`, and there are no extra keys.
- The v3 file list = the v2 file list.
- Exactly the 8 C-files differ from v2.
- Item 5 of `platform-registration.md` names the new key and the Normalize rule.
- Negative control: the v2 fragments contain the normalized key in 0/7 files, so the NAV-PAGE check fails on v2.

The C# guard test itself runs at the Mac build (Q88b / a Q84b re-run), not here.
