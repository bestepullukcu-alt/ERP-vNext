# Q114 — MOD-0187 pack text patch: §32.2 layout rule + carriers.read prerequisite (DRAFT, NOT applied)

WP-MVP6-PACK-114 · Prompt Q114 v1 · DEV lane (single pack writer) · chat lane on the Linux VM bridge · agent `module-pack-author`.
Start 2026-09-27 13:48:12 +03 · end 2026-09-27 13:53 +03 (Istanbul). Repository read-only; writes only this folder and VM `/tmp/q114/`.

## Preflight

| Item | Value |
|---|---|
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| `git diff --name-only HEAD` | 19 paths (start and end) |
| `.git/index.lock` | absent (start and end) |
| Preimage `MOD-0187-claims-management.md` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` (expected `96c9a0ae…`) |

## Result

| Item | Value |
|---|---|
| Patch `01-MOD-0187-layout-and-carriers-read.patch` | `9708198c82fdb415e63a27217b576bd87b6b04c3a509c4f9237a14b85e4cd476`; +6/−2 (`git apply --numstat` 6 2), 3 hunks |
| Postimage (P1 + P2) | `b2fba5f34495a38b59286ce1749e3af77b5a2ecd512221c416269eded1ba7d7c` |
| Reference postimage P1 alone / P2 alone | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` / `b49c81bb646d675b43c25d11ef64e3fabaf2018dbf082f94e1149e304bb4fd42` |
| `git apply --check` on /tmp copy (`GIT_CEILING_DIRECTORIES=/tmp`) | exit 0; apply → `b2fba5f3…`; reverse → `96c9a0ae…`; no `.orig`/`.rej` |
| `verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` (name from frontmatter line `name: Claims Management`) | `OK MOD-0187: proven against Blueprint/registry.`, exit 0 |
| Sections touched | §32.2 (P1), §32.4 and §32.11 row CU-09…CU-13 (P2); nothing else |
| Effort numbers | §32.14 (preimage lines 730–745) byte-identical in the postimage (lines 734–749) |
| Sign-off | `SIGN-OFF.md`; fixed record path `docs/records/decisions/2026-09/mvp6-q114-claims-pack-signoff-owner-decision-01.md` (does not exist yet) |

### P1 — §32.2 (pack preimage line 562)

"every Claims `.cshtml` states `Layout = "_LayoutTenantShell";`" → "the Claims page view (`Index.cshtml`) states … explicitly; partial views (`_*.cshtml`) set no `Layout` …". Index.cshtml is the only page view in the §32.10 owned set (preimage line 672); the rest are partials. Pack standard: the explicit-layout rule is for the Razor page (`.antigravity/rules/module-pack-standard.md:84-90`, `:219`); `_ViewStart.cshtml` stays unchanged.
Evidence: `docs/records/audits/2026-09/mvp6-q64b-claims-build-01/README.md:138` (D-02, 5 partials with a Layout → 6 shells), `:139` (F-PACK-32.2); `docs/records/audits/2026-09/mvp6-q64d-claims-runtime-01/README.md:96` (one shell after the fix).

### P2 — carriers.read prerequisite (pack preimage lines 586–588 and 701)

Code path (accepted A12 360 successor `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz`, `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d`, extracted to /tmp; member prefix `mvp6-shipment-a12-rework-src/services/Diten.SupplyChainService/src/`). The common checkout has no Claims backend (pack §33 open gap 1, line 815).
- `Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimPermissions.cs:4-8` (`03c69751…`): `supplychain.claims.read/.create/.investigate/.decide/.settle`. No carrier key.
- `Diten.SupplyChainService.Infrastructure/Features/Carriers/CarrierPermissions.cs:4` (`a4a28863…`): `Read = "supplychain.carriers.read"`.
- `Diten.SupplyChainService.Api/Features/Carriers/CarriersController.cs:11,15` (`d82bd483…`): `[Authorize]`; `[HttpGet, CarrierPermission(CarrierPermissions.Read)]` on the carrier list.
- `Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimReferenceReader.cs` (`3529fa55…`):
  - `:91` forwards the caller's `Authorization`;
  - `:104` maps 401/403 to 503 `CLAIM_REFERENCE_UNAVAILABLE`;
  - `:123` explicit `claim.CarrierId` only;
  - `:127` `GET api/shipment-bundle/carriers`.
- `Diten.SupplyChainService.Persistence/Features/Claims/ClaimRepository.cs:66-69` (`bc427159…`): `observe` runs only when `!id.HasValue` (create). The transition path (`…/Handlers/CommandHandlers/TransitionClaimHandler.cs:15`, `4f73eb5b…`) passes the reader, but the repository does not call it, so transitions need no carriers.read.

Runtime evidence:
- `mvp6-q64b-claims-build-01/README.md:115`, `:141` (F-CU10);
- `mvp6-q64d-claims-runtime-01/README.md:21`, `:119`, `:151` (503 zero write without the grant, 201 with a lane-only grant).

## ASSUMPTIONs

- **A1:** Placement per the prompt: §32.4 (where the pack lists the UI role prerequisite G-SHIPREAD, preimage line 587) and the §32.11 CU-09…CU-13 row (CU-10 is grouped there, line 701). §33 open gap 2 (line 816) is not changed because the prompt allows no other section.
- **A2:** Scope "carrier link" = a create with `carrierId` sent, per the code path above. Transitions are excluded.
- **A3:** One patch file, as named in the prompt. SIGN-OFF gives one decision per part and reference postimages for a single-part re-cut; this file is applied only when both decisions are A.
- **A4:** The Claims backend is cited from the accepted A12 360 successor (hash-verified) because it is absent from the working tree. Extraction was only to /tmp.

## Findings (in scope)

- **F-Q114-1 (LOW):** The controlling CU-10 row text sits in the hash-bound draft `docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/ACCEPTANCE.md:26` (`e5b7f766…`) and has no carriers.read precondition. Pack §32.11 says that file is controlling. P2 puts the precondition in the pack row; the draft file needs its own revision decision.
- **F-Q114-2 (INFO):** Pack §33 open gap 2 (line 816) records only G-SHIPREAD as the conjunction the single-key action model cannot express. The carriers.read case for carrier-linked creates is the same kind of gap and could be added in a later §33 revision.

## Not done (by rule)

No pack or other repo edit, no rm, no git write, nothing marked approved.

Agent PASS ≠ CT ACCEPTED — returning to CT.
