# MVP6-SHIPMENT-SHARED-UI-CT-DISPOSITION-01 — SOP §22

Date: 2026-09-25  
Role: Control Tower acceptance reviewer  
Branch / HEAD observed: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **ACCEPTED_BOUNDED — UI183-A03 and PRES-183-02 only**

## Exact authority → source → independent evidence chain

The exact owner authority is preserved in the writer package as `AUTHORITY.md` SHA-256 `d33a492a5af24281696c4959d1d20245f8bd92e095e9edaeee18070e662070b0`. It binds patch SHA-256 `1e53a3a63661e85b6a165dcf8aaf48451cc077af85713234e22496ec6e861c45` and preimage/target table SHA-256 `0a8b1e48d53630070bacbce61294bc8ecf5d82961ab7196dea0794dd81c47470`.

The integration writer applied that patch once after all 12 preimages matched. The resulting immutable source archive SHA-256 is `490d51be87d249265a2cc9fe6d1f8f3b23bd6976f4e5830d733bff2dfff613a3`; its 360-row source manifest SHA-256 is `7d7bec63a1f2e9e906864e5dea6dc96a00e94a280b830dd06f95a8b68246b4fc`. Writer artifacts revalidated 15/15.

The independent verifier used that immutable source and produced report SHA-256 `f75a2912616127a1d93a18da0e0e686b3b74d018e6c475492e9157243aad0e7c`, evidence archive SHA-256 `fbde626ca38281e95796f024b975d6597134c6aa29ed11381c872bd0b9b287ab`, and artifact manifest SHA-256 `4304e356b2a0694ace1f2c5b7504b7beb4553b6865343ac0fbd755fb704cf867`. It independently verified 360/360 sources, 12/12 shared targets, native .NET 8 build, 19/19 focused regressions, the five 401 JSON adapter challenges, separate HTML 302 behavior, seven-language Details/Close localization, Arabic RTL and real 390/768 viewport behavior.

## CT decision

Control Tower accepts only:

1. **UI183-A03:** the exact five metadata-marked Shipment JSON adapters return the established `401 INVALID_REQUEST` JSON/correlation surface while the ordinary HTML page retains its login redirect. This does not alter authenticated 403 or global authentication behavior.
2. **PRES-183-02:** the exact shared DataTables modal title and Close accessible name derive from the seven-language shared localization payload. At 768px Chinese fits all columns, so absence of a responsive modal is an applicability result, not a forced-modal requirement.

This technical acceptance does not establish full Shipment UI acceptance, common-checkout rollout, full-module acceptance, E5 or G5.

## Accessibility relationship

PRES-183-01 remains a separate prior bounded acceptance under SOP SHA-256 `7453b4fcdc0a264b0d1233771e65b41c9935f0cb25f982fe5fccd98556c52f61`. Its exact successor manifest SHA-256 `dcc6662696db5e689d2d4f6facbb5106ca53d06a957caf21c5fabc912a0c7c88` is not re-accepted here. The writer preserved all three accepted files byte-identically, and the independent verifier produced a fresh final-source accessibility regression PASS. The inherited CT acceptance and fresh no-regression result remain distinct.

## Historical remaining-acceptance record

The 2026-09-24 `shared_ui_candidate / APPLICATION_NOT_AUTHORIZED` row remains byte-for-byte in its historical inventory. It accurately describes the state at that time. `CURRENT-SUCCESSOR-STATUS.md` now points to the applied 360-source successor and this CT decision without rewriting the earlier SOP, inventory or 49 PASS / 35 FAIL evidence.

## Open boundaries

A08, A09 and A12 are NOT RUN and remain OPEN. A10 remains unauthorized. A14 remains an open policy/profile gate and the historical generic 49/35 result is not waived. Durable PNG remains OPEN. Because PNG remains open, UI183-A13 is only partially closed despite PRES-183-01 and PRES-183-02 acceptance.

## Effort

No new delivered hours are recorded. `0183-4-DELIVERED` already carries 40/64/104 O/M/P for the bounded frontend. `0183-6-REMAINING` retains 14/24/44 for broader UI/browser/PNG work, and `S7-REMAINING` retains 2.4/4/8 because there is no reliable separate mapping from this successor to that shared reserve. Crediting the same implementation or verification again would double count completed frontend/shared work.

No tests were rerun by CT. Product, pack, contract, guard, board and Git state were not changed.
