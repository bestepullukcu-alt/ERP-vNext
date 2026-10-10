# MVP6-SHIPMENT-SHARED-UI-DISPOSITION-01 — SOP §22

Date: 2026-09-25  
Role: orchestration / shared UI disposition preparation  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **CANDIDATE READY; APPLICATION HELD**

## Input and source binding

- Exact final Shipment source manifest: `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`.
- Policy/error VER SOP SHA-256: `e348a295acdb31fc9f3f1d7afb86c75f8cec6db203bc383922082a528babe1a9`.
- Presentation VER SOP SHA-256: `5525856473c5cc4c2c2dd9f693d4662cd295cf3b89e9edf9b5ee60edb2d3db4b`.
- Controlling UI acceptance SHA-256: `83d0db58eff28b0ac56af7c0299fd775aa575615fc1d80d8ad6f57d0630eefa1`.
- All candidate work and execution occurred under `/private/tmp/mvp6-shipment-shared-ui-disposition-01/`. No product source was changed.

## Disposition

### UI183-A03

The HTML page and JSON adapter profiles require different challenge representations. Controller-wide `[Authorize]` remains the authentication boundary. The minimum candidate adds explicit metadata to the five Shipment adapter actions and lets the existing cookie handler return the established v1 JSON/correlation 401 only for marked endpoints. Unmarked HTML pages retain `/account/login` redirection. No `[AllowAnonymous]`, path-prefix oracle, global redirect removal or Auth-service change is used.

Inherited exact-source RED is the recorded 302 adapter response. Fresh candidate execution through `WebApplicationFactory` proves all five adapters return 401 JSON/no Location and the page remains 302. Existing Shipment permission/scope/header/query/intent tests remain green.

### PRES-183-02

The shared DataTable modal read `Details` while the Shipment payload exposed lowercase `details`; the shared DataTable payload omitted modal chrome. `Close` also existed only in English and Turkish SharedResource files. The candidate extends the existing shared payload with `Details`/`Close`, completes the five missing translations, reads the title through `dtText`, and updates only the responsive modal's close accessible name inside the existing `.dtr-bs-modal` lifecycle hook. It does not hardcode Shipment translations or scan/replace general DOM text.

The inherited exact-source browser measurement remains RED. A fresh executable guard proves all seven shared resource sets and both shared JS lookups; the final real-browser 390/768 check remains a required independent post-application step.

## Exact artifacts and verification

- A03 patch: `A03-JSON-CHALLENGE.patch` SHA-256 `08906865d9f062b756c5d1e2c316258c9446d6ff07e7bc2685a489c4438a210f`.
- Modal patch: `PRES-183-02-DATATABLE-L10N.patch` SHA-256 `9f4b036b85d06144490ec43d536dc2b8b560822e081551823eb3c1eac9d541be`.
- Combined candidate: `COMBINED-SHARED-UI-CANDIDATE.patch` SHA-256 `1e53a3a63661e85b6a165dcf8aaf48451cc077af85713234e22496ec6e861c45`.
- Baseline/target table: `BASELINE-PATCH-TARGET.tsv` SHA-256 `0a8b1e48d53630070bacbce61294bc8ecf5d82961ab7196dea0794dd81c47470`.
- Disposable patch apply and 12/12 target byte checks: PASS.
- Native .NET SDK/runtime: 8.0.417 / 8.0.23; build PASS with three pre-existing nullability warnings in unrelated Auth tests.
- Focused fresh tests: 9/9 PASS. The two candidate tests cover five adapter challenge variants and shared modal localization; seven existing tests cover Shipment controller and index regressions.

## Ownership and ordering

The two findings do not edit the same file, but both affect shared Diten.Web behavior. One integration owner must validate the exact preimages and apply the combined patch once. The application order encoded by the combined patch is authentication marker/handler/controller/test first, then shared DataTable payload/resources/JS/test. Any preimage mismatch stops the operation.

No accessible real decision authorizes these exact shared target bytes. `OWNER-DECISION-TEXT.md` contains the only missing decision. After authorized application and writer-complete, a different verifier must execute `INDEPENDENT-VER-PROMPT.md`.

## Boundaries

The candidate does not touch the Shipment accessibility writer's `_Form.cshtml`, shared layouts, Gateway, Auth service, backend services, canonical contracts, guard, pack or Git. It does not close PRES-183-01, PRES-183-03, PNG, other Shipment acceptance rows, full-module acceptance, rollout or E5/G5.
