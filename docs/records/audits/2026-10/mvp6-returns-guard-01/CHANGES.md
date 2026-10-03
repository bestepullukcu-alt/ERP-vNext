# Q187 — Returns backend: per-target transition guard (F-Q65b-1 · OD-F-Q65b-1 · D186-05)

| Field | Value |
|---|---|
| WP | Q187 v1 · Template T1 v1 · T1 LANE writer (SOP v2.5 §17/§36.2) |
| CT-QUEUE row | `Q187 · Returns backend: per-target transition guard (F-Q65b-1, OD-F-Q65b-1) — overlay in new folder mvp6-returns-guard-01 · READY · LANE 3 (backend writer) · Q186` |
| Written by | LANE 3 (Cowork, Linux VM, repo via bridge), .antigravity backend writer, 2026-10-01 |
| Record | `docs/records/audits/2026-10/mvp6-ct-verdicts-q183-q184-q65b-2026-10-01.md` `b226a4f4dd56c9094e983a47daf33af37df58a760fddda5661292f1b4630c991` (F-Q65b-1 line 40, OD-F-Q65b-1 line 48) |
| Base stack | BASE-STACK v2 `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` |
| Source | BC-SOURCE `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`, extracted to a temp folder outside the repo |
| Preimages | BASE-MANIFEST `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` |
| Defect evidence | `docs/records/audits/2026-09/mvp6-q65b-returns-mac-01/REPORT.md` `60965bd6c30b462f6540edcad8615e1474ef51d7be1f60cfd85a2627cd99294a` (F-Q65b-1, D-3) |
| Pack | MOD-0186 `933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe` (D186-05 line 300: "no generic permission guessed for uncovered actions") |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `git status --porcelain` only; no git write; no tracked file edited |

## 1. Compose position

```text
BASE a8a236de… → Q117 83e6322c… → Q121 93bf1c07… → Q131 4a4a0860… → Q187 overlay-returns-guard.tar.gz (this) → module draft
```

- This is a new fix layer. It is applied **after Q131 and before the module draft**.
- How to apply: extract `overlay-returns-guard.tar.gz` at the root of the composed tree. Members are repo-relative paths.
- It modifies 4 files and adds 1, so the tree goes from 14,572 to **14,573** files.
- No path overlaps Q117, Q121 or Q131 (CHECK-RUN C03: 0 of 33 layer members). Every preimage is a BASE row.
- Recording this layer in a base-stack record is not part of this WP.

## 2. What was wrong (F-Q65b-1)

The transition endpoint required two permission keys at the same time (a conjunction):

- **The generic key.** `ReturnsController.cs:20` put `ReturnPermission(ReturnPermissions.Transition)` (`"supplychain.returns.transition"`) on the endpoint. The attribute checks it in its MVC filter. `ReturnContextMiddleware.cs:64-65` checks it again as the endpoint "base" key.
- **The target key.** `ReturnContextMiddleware.cs:94-95` also checked `ReturnPermissions.ForTarget(targetStatus)`.

No manifest registers the generic key, so no real user can hold it. As a result, every transition was 403. Q65b could exercise the workflow only through the lane-only catalog document D-3.

A second, latent defect existed: when `ForTarget` returned `null` (the target `Requested`), there was no target check at all. The generic key alone was enough.

## 3. What changed (OD-F-Q65b-1: "check the target-state key; no generic key registered")

| File (under `services/Diten.SupplyChainService/`) | Change | G |
|---|---|---|
| `src/…Api/Features/Returns/ReturnsController.cs` | Transition endpoint: `ReturnPermission(ReturnPermissions.Transition)` → `ReturnPermission` (no endpoint key). `[Authorize]` on the controller is kept. The route is unchanged. | G1 |
| `src/…Infrastructure/Features/Returns/ReturnPermissionAttribute.cs` | `permission` becomes optional (`string? permission = null`). With no endpoint key, the MVC filter passes only an authenticated caller who holds the target-state key the middleware verified (stored in `HttpContext.Items[TargetGrantItem]`). Otherwise it returns 403 (fail-closed, defence in depth). Behaviour with a key (read/create) is unchanged. | G1, G2 |
| `src/…Api/Features/Returns/ReturnContextMiddleware.cs` | Line 65: the base-key check runs only when the endpoint has a key. Lines 94-95: `grant is not null && !Has(grant)` → `grant is null \|\| !Has(grant)` gives 403 (fail-closed). The verified key is then stored for the filter. | G1, G2 |
| `src/…Infrastructure/Features/Returns/ReturnPermissions.cs` | `public const string Transition = "supplychain.returns.transition"` is **removed**. Its only user was the controller (BC-SOURCE grep, and a repo grep outside `docs/`/`execution/`). The `ForTarget` map is unchanged. | G3 |
| `tests/…Tests/Returns/ReturnTransitionGuardTests.cs` (**NEW**) | G4 tests, see §5 | G4 |

**Why the attribute stays on the transition endpoint.** `ReturnContextMiddleware` selects Returns endpoints by `ReturnPermissionAttribute` metadata (line 50: `if (permission is null) { await next(http); return; }`). Deleting the attribute would skip the whole Returns context pipeline: 401, scope, correlation, idempotency, schema and the target check. So the attribute stays and carries no key.

**No new permission key.** The set of keys in Returns `src/` goes from 9 to 8, with `transition` removed and nothing added (C08). `TargetGrantItem` is an `HttpContext.Items` slot name, not a permission key.

## 4. G2: status for a target with no key

| `targetStatus` | Before | Now | Why |
|---|---|---|---|
| Not a `ReturnStatus` name (for example `"Bogus"`, `"authorized"`, `""`, a non-string) | 400 `INVALID_REQUEST` (schema, `ReturnWire.TransitionValid`) | **400 `INVALID_REQUEST`**, unchanged and still checked first | Schema 400 comes before 403 (precedence kept) |
| In the enum but unmapped: **`Requested`** (`ForTarget` = null) | No target check; with the generic key, the lifecycle rules decided | **403 `INVALID_REQUEST`** (existing 403 envelope, `contractVersion: v1`, correlationId echoed) | The request is schema-valid, so this is an authorization decision. No key grants `Requested`, so no caller can be authorized. 403 reuses the existing per-target 403 path, so no new code, status or wire shape. A 409 or 422 would need the handler to run, which means passing authorization; G2 forbids that. The MVC filter also returns 403 when no verified key exists. |

## 5. Tests (G4): `ReturnTransitionGuardTests.cs`

These are middleware and MVC-filter unit tests with a fabricated, already-authenticated principal (the same method as `ReturnIsolationTests` `Middleware_PostAuthenticationUnit_OrderedContextMatrix`). They are not JWT validation. The transition attribute is read by reflection from the real `ReturnsController.Transition`.

| Req | Test | Expectation |
|---|---|---|
| (a) | `Transition_OnlyTargetKey_PassesMiddlewareAndFilter` × 7 targets | Only the target key (for example `supplychain.returns.authorize` for Authorized and Rejected) → 200 (next called), grant stored, filter passes |
| (b) | `Transition_WithoutTargetKey_Is403ForEachTarget` × 7 | No keys → 403. Every other Returns key (including read, create and the generic key) but not the target key → 403. Zero next calls, 403 envelope, filter 403 |
| (c) | `Transition_OnlyGenericTransitionKey_Is403` × 8 (7 + Requested) | Only `"supplychain.returns.transition"` → 403 |
| (d) | `Transition_UnmappedTarget_Is403_EvenWithEveryKey` | `Requested` with every key → 403 |
| (d) | `Transition_TargetOutsideSchema_Is400BeforePermission` × 3 | `Bogus`, `authorized` or `""` → 400 with or without keys (schema first) |
| (d) | `Filter_TransitionWithoutVerifiedTargetKey_FailsClosed` | Filter: no verified key → 403 even with every key held. A verified key that the caller holds passes; one the caller does not hold → 403. Unauthenticated → 403 |
| (e) | `Endpoints_ReadCreateUnchanged_TransitionHasNoEndpointKey_NoGenericConstant` | Transition `Permission` is null. List and Create keys are unchanged. `[Authorize]` is present. No `Transition` field and no constant equal to the generic key. GET with read → 200; GET with generic + create → 403 |
| (e) | Existing tests | The 7 existing Returns test files are unchanged (C12): replay, isolation (including the line-49 `ForTarget` map test), atomicity, concurrency, lifecycle, reference, contract. None asserted the old conjunction. |

## 6. Not changed

- Routes, wire format, error codes, `ReturnContractError`, `ReturnWire` schema, lifecycle rules, handlers, repository, outbox and receipts.
- Other modules, `Program.cs` and the frontend (the UI side is Q188).
- No permission key was registered or added anywhere.

## 7. Deviations and findings (for CT)

- **D-1: precedence for callers with no key on the transition endpoint.**
  - Before, the generic-key 403 (line 65) came before the header, idempotency, scope, route, media and schema checks.
  - Now the target key is the only gate, and it needs the parsed body. A transition caller without the target key therefore gets 400, 404 or 415 from those earlier checks first, then 403 after a valid schema.
  - Transition order now: 401 → 403 (signed context claims) → 400 headers → 400 Idempotency-Key → 404 scope → 400 id/query → 415 → 400 schema → **403 target key**.
  - Read and Create precedence is unchanged.
  - The pack's R08 "base then parsed action" has no base step for transitions after OD-F-Q65b-1.
- **D-2: compile not run.** There is no dotnet SDK in this lane (VM and container), and dot.net/nuget.org are blocked by egress. Syntax was checked with tree-sitter-c-sharp 0.26.0 (in a venv in the temp folder outside the repo) plus a token balance check. The Mac build and test is Q190.
- **D-3: extra file `FIXES.patch`.** It is a plain `diff -u` between the temp pre and post trees (not git) for VER convenience. Applying it to a fresh BC-SOURCE copy reproduces 5/5 postimages (C14).
- **F-1 (pack text drift; report only, not edited).** MOD-0186 still describes the removed conjunction:
  - line 637: "`.transition` plus target keys";
  - line 848: M-03, `supplychain.returns.transition` as an "API-only allow-list" key;
  - line 865: "Every row action also needs `supplychain.returns.transition`".
  - The owner is the pack owner (and Q188 on the UI side).

## 8. Files in this folder

| File | Content |
|---|---|
| `overlay-returns-guard.tar.gz` | 5 files at repo-relative paths; `--sort=name --mtime=@0 --owner=0 --group=0 --numeric-owner --mode=0644 --format=gnu --no-recursion`, `gzip -n -9`; files only, no AppleDouble; rebuild is byte-identical |
| `OVERLAY-MANIFEST.tsv` | path · preimage_layer · preimage_sha256 · postimage_sha256 · change · bytes_post |
| `CHECK-RUN.tsv` | C01–C14 static checks (13 PASS, 1 N/A) |
| `FIXES.patch` | Review diff (D-3) |
| `CHANGES.md` | This file |
| `SHA256SUMS` | sha256 of the 5 files above |

Agent PASS ≠ CT ACCEPTED.
