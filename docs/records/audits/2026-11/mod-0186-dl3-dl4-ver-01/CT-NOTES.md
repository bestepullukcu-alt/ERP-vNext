# MOD-0186 DL-3 + DL-4 — CT notes · 2026-11-06

Commits: `07816fb56` (service), `7e1d75ca8` (web + manifest), the VER-fix commit after this record. Not pushed. Not an
acceptance record: DL-3/DL-4 inherit MOD-0183's merge gate (MOD-0177 FU on `main`) and need the getReturn Gateway route.

## Measured by the CT

- SupplyChain full suite, unfiltered, lane replica set 6155, on `7e1d75ca8`: 1033 passed, 1 skipped, 0 failed
  (`ERP-vNext-lanes/mod0183dl3/run-20261106-013804`). Returns service tests 141/141 on `07816fb56`.
- Web Returns tests after the VER fixes: 159/159 (fresh `--no-incremental` build; runs between 01:48 and 01:56 used a stale DLL
  while a parallel full-suite run shared `bin/`, and are not counted). The full Web assembly is to be re-run on the fix commit.
- Sabotage, each red then restored: copy of customerRef dropped (RB-27); company predicate dropped (RB-8); eligible status
  filter removed (RB-3); id as picker text (RB-24); shipment customer as fallback (RB-25); CRM body in the log (RB-26);
  isStockable on the layers (RB-6); customerRef left in the create answer (F-2); hold matching without the Posted / unique
  rule (F-4).
- Slim checker: the same 37 rows fail on the change and on base `018f0119c` (RU-SCR-06 stands).

## Declared scope differences

Service files beyond the DL-4 list but inside the §25 base list, needed for the snapshot, the answer and the replay:
`ReturnEntitlement.cs` (ReturnReferenceSnapshot), `ReturnMutationResult.cs`, `TransitionReturnHandler.cs`,
`GetReturnListHandler.cs`, test fixture `ReturnAtomicityTests.cs`. Manifest provider and its test follow "Self-registration".
Web: the two Node receipt tests (RB-7 changes their expected text). No file of another module is changed after F-10.

## Dispositions

- F-1 (getReturn Gateway route): open, owner integration-agent; message to be sent. Blocks the live run and E4.
- F-2…F-8, F-10, F-11: closed by the VER-fix commit.
- F-9, F-12, F-13, F-14: follow-ups, owner MVP-6 CT, by 2026-11-10. F-13 (shipment GUID column on the list, pre-existing)
  needs a decision: replace with the shipment number needs a per-row shipment read or a contract field.
