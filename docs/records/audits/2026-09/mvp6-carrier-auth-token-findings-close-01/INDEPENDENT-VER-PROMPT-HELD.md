# HELD — MVP6-CARRIER-AUTH-TOKEN-FINDINGS-VER-01

Do not execute until the exact successor in `OWNER-DECISION-TEXT.md` is approved and applied by a single writer with writer-complete status.

## NE

Independently verify F-01/F-02 closure on the final authorized source and complete the previously blocked native .NET 8 Phase 2.

## NEDEN

The earlier independent verifier found exact-cardinality and temporal-boundary defects. Candidate RED→GREEN evidence is E2 and is not independent runtime acceptance.

## NASIL

Verify all artifact hashes and extract `build-source.tar.gz` into a new disposable workspace. Use `/Users/natig/.dotnet/dotnet` SDK 8.0.417/runtime 8.0.23 without major roll-forward. Fresh-build and bind source→binary→process. In a lane-specific DB-010 environment, send correctly signed tokens covering valid exact claims; every missing and duplicate/conflicting security claim; expected-plus-extra audience; malformed/non-numeric time claims; future `iat`; future `nbf`; `iat != nbf`; expiry inside and outside configured skew; and excessive lifetime. Every rejection must occur before mediator/repository access. Reproduce the exact valid Platform→MDM chain and affected refusal/timeout cases. Bind inherited login/refresh/MFA/forced-password evidence to unchanged source, rerunning only cases materially affected by service-token validation.

## YAPMA

Do not fix source, select the first duplicate claim, weaken JWT validation, change clock skew, enter Carrier/UI/Gateway/Supplier scope, use operational databases, or perform Git mutation.

## DOĞRULA

Return SOP §22 PASS/REWORK/BLOCKED, row-level F-01/F-02 evidence, native source→binary→process hashes, zero-read proof, repository no-change and process cleanup. DEV/candidate PASS is not independent PASS or CT acceptance.

