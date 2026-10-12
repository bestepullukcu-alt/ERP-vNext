# HELD — MOD-0187 VER Prompt v1.0

**HELD — yürütülemez.** This is a read-only verification draft dependent on a completed, versioned DEV handoff. It is not a dispatch, acceptance or waiver.

## NE

Independently verify the bounded Claims implementation against the frozen SHIPMENT-BUNDLE contract, the signed D187-01…06 decisions and the exact DEV manifest after DEV is released.

## NEDEN

Developer tests and mocks do not establish independent wire, persistence, isolation, replay or failure evidence. Claims settlement must remain an operational lifecycle state with no finance/payment side effect.

## NASIL

1. Stop if the DEV handoff, exact manifest, owner decisions, required contract amendment uptake or fresh baseline is missing.
2. Use a hash-identical disposable copy and an isolated replica-set Mongo only. Never use operational MongoDB 27017.
3. Reproduce the three routes and compare exact request/response schemas, statuses, headers and correlation behavior.
4. Independently capture actual request bytes/body/headers for create/list/replay; test malformed, missing and mutant evidence records.
5. Verify D187-01 reference outcomes, D187-02 lifecycle amount rules, D187-03 arbitrary decimal behavior, D187-04 permission matrix, D187-05 replay/error policy and D187-06 atomic numbering/duplicate behavior.
6. Inject dependency refusal/timeout, transaction/write-boundary failures, unknown commit, unavailable/standalone Mongo, index failure and restart. Query persisted counts; do not accept prose assertions.
7. Verify no source-of-record, Warehouse, Inventory, finance or payment mutation; outbox remains Pending and replay creates no new event/read.

## YAPMA

Do not fix source, tests, contracts or manifests. Do not rewrite historical evidence, add waivers, promote the pack, claim E5/G5, or start MOD-0188. Do not modify gateway/shared/Program.cs or commit/push/stash.

## DOĞRULA

Return SOP §22 agent verdict separately from CT decision, with path:line findings, commands/exits, TRX IDs, HTTP/restart records, source/binary/frozen hashes, fresh versus historical architecture results, exact accepted scope and remaining GAPs. Final branch/HEAD/status/diff must match the read-only baseline.

## Gates

- Signed owner decisions D187-01…06: **PENDING**
- Approved/ready-for-dev pack: **PENDING**
- Completed DEV handoff and manifest: **PENDING**
- VER dispatch: **HELD**
