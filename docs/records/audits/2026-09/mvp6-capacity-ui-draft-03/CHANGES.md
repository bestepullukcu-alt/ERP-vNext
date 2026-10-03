# Q183 — MOD-0192 Capacity planning UI draft overlay v3: nav page key rename (C3–C4)

**Agent verdict: DRAFT v3 written (archive only). Not built, not tested.** Agent PASS ≠ CT ACCEPTED.

| Field | Value |
|---|---|
| WP / Prompt / Template | Q183 · Prompt Q183 v1 · T1 v1 (SOP v2.5 §36.2) · frontend-ui-ux |
| CT-QUEUE row | line 267: `Q183 · S&OP + Capacity UI draft v3: nav page key rename (F-Q84b-1, OD-NAVKEY-CAP) — new folders mvp6-sop-ui-draft-03 and mvp6-capacity-ui-draft-03 · READY · LANE 2 (frontend-ui-ux) · Q182` |
| Lane | Cowork LANE 2 (Linux VM; `uname -s` = Linux); temp folder outside the repo (`$HOME/q183-tmp/`) |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched) |
| Base Stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (unchanged; this WP only revises the draft overlay) |
| Input (v2) | `docs/records/audits/2026-09/mvp6-capacity-ui-draft-02/capacity-ui-draft-overlay-v2.tar.gz` `48b60b782f1bbd84a74a95c3cbe01f90aec9d0062c528d748726b266db1028b3`; folder SHA256SUMS `6aba548d8f4f7f44caa17bcea05929bf7d161a73685751490bea4c668349074e` (all OK) |
| Output (v3) | `capacity-ui-draft-overlay-v3.tar.gz` `5c0a3b618d27607eecc62e3a7b33b99f1df11e4f48f19d5c819a426c6a8d96d5` — 47 files, same internal paths, modes and owner as v2; 8 files changed, 39 byte-identical |
| Authority | F-Q84b-1 (`docs/records/audits/2026-09/mvp6-q84b-sop-mac-01/REPORT.md` `a324b35b…`, line 166); OD-NAVKEY-CAP in `docs/records/audits/2026-09/mvp6-ct-verdicts-q84b-2026-09-30.md` (`f57ee43c…`, lines 44) |

## Why

- **F-Q84b-1:** the draft fragments carried `Nav.Page.CAPACITY_PLANS`. The runtime looks the page name up as `"Nav.Page." + Normalize(PageCode)`.
  `Normalize` (`frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs` `e8299a5c…`, lines 66–74) upper-cases the code and drops every
  non-alphanumeric character. So page code `CAPACITY_PLANS` (CapacityPlanningManifestProvider.cs:30 (PageCode "CAPACITY_PLANS", ParentPageCode null :34)) is read as `Nav.Page.CAPACITYPLANS`. With the underscore key,
  `NavManifestL10nGuardTests.Every_nav_visible_manifest_page_has_a_Nav_Page_key_in_all_seven_languages` fails in 7 languages
  (Q84b REPORT line 86), and the sidebar would print raw English.
- **OD-NAVKEY-CAP (owner, 30 Sep):** fix by a key rename in the draft (v3) plus the `platform-registration.md` item 5 correction; no product code change.

## What changed (exactly C3–C4; `FILE-PLAN.tsv`)

- **C3:** `overlay/_shared-integration/sharedresource-nav-keys/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx.fragment.xml` —
  `name="Nav.Page.CAPACITY_PLANS"` → `name="Nav.Page.CAPACITYPLANS"`. The values are unchanged in all 7 languages.
- **C4:** `overlay/_shared-integration/platform-registration.md` item 5 now names `Nav.Page.CAPACITYPLANS` (line 16), and has one added
  line: "Key = Nav.Page. + NavNameLocalizer.Normalize(PageCode)" (line 18).
- **Not changed:** PageCode values, manifest provider, routes, permissions, views, tests, and every other file (39/47 byte-identical to v2).

- **O-1 (not changed, outside C3/C4):** `overlay/_shared-integration/README.md:11` still names `Nav.Page.CAPACITY_PLANS` in its file table (a description line, not a key). The WP allows only C3–C4, so it is unchanged; the key the build reads is in the fragments.

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
