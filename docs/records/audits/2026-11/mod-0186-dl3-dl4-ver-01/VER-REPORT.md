# MOD-0186 DL-3 + DL-4 — independent VER (read-only-auditor) · 2026-11-06

Lane: read-only-auditor (SOP §17.4 row), worktree-read-only, strict; it did not see CT notes. Range `bb9f8b513..7e1d75ca8`
(`07816fb56` service, `7e1d75ca8` web + manifest) on branch `feature/sce/mod0183-dl3-01`. Recorded by the MVP-6 CT from the
auditor's final message, layout condensed. Only the status column of the findings is the CT's ("kapandı" = closed by the
commit after this record's parent, with a test whose sabotage is red); every other word is the auditor's.

**Verdict: MOD-0186 DL-3/DL-4: ACCEPTABLE WITH CONDITIONS.** Service side correct (getReturn tenant + company + IsDeleted
scoped; customerRef copied only from the shipment read, stored on the receipt, replays answer the same). Conditions: Gateway
route (F-1), customerRef in create/transition answers (F-2), RB-10 hold gaps (F-3, F-4), saved-view index drift (F-5),
missing sabotage tests (F-6).

## RB rows (auditor)

- RB-1 partly — names projected, no id label; only Lsku tested.
- RB-2 met. RB-3 met (Delivered/Closed + search, customer from summary, no per-row getShipment, old record, no CRM without the key).
- RB-4 partly — select used; no test that the UUID input is gone.
- RB-5 met (187 keys × 7; free text as written).
- RB-6 partly — adapter Active, no isStockable, tested; JS clear-on-change and the real tree walk untested.
- RB-7 met (fill by name; Node tests red on raw ids).
- RB-8 met (one 404 for other tenant / company / unknown; company-predicate sabotage red; HTTP route untested).
- RB-9 partly — permission before markup; skeleton / error distinct; no separate empty state; viewports and RTL not measured; no reason column.
- RB-10 partly — gate before any request; NotPosted reason not shown (F-3); column drawn without the key (F-7); matching ambiguous (F-4).
- RB-21 met. RB-22 met (tr/fr "Lot" on the allow-list).
- RB-24 met for customer / account ids; the older shipment GUID column remains (F-13).
- RB-25 met. RB-26 met. RB-27 met (HTTP-level 400 / no-write not tested, F-14).

Other: service files outside the DL-4 list (ReturnEntitlement.cs, ReturnMutationResult.cs, TransitionReturnHandler.cs,
GetReturnListHandler.cs, ReturnAtomicityTests.cs) are in the §25 base list and needed. Old receipts without CustomerRef replay
as null. Permission gates all precede outbound requests. DATATABLE-CHECK.txt confirmed (same 37 rows as base 018f0119c).
SupplyChain suite on `7e1d75ca8`: 1033 passed, 0 failed, 1 skipped.

## Findings

F-1 | ENGEL | ocelot.json has no GET /api/shipment-bundle/returns/{returnId} route; live, the details page and holds show "return not found" for every return; integration-agent must add it (dependency, not a code defect) | açık | MVP-6 CT | 2026-11-10
F-2 | ORTA | customerRef reaches the browser in the create and transition answers (ReturnResponse passed through raw) | kapandı | MVP-6 CT | 2026-11-10
F-3 | ORTA | RB-10: a NotPosted line shows only "Not posted", never its reason | kapandı | MVP-6 CT | 2026-11-10
F-4 | ORTA | holds matched by skuId + lotId only: duplicate SKU/lot lines share holds, a NotPosted line can show another line's hold, an unreadable shipment shows "No quarantine record" on every line | kapandı | MVP-6 CT | 2026-11-10
F-5 | ORTA | saved-view column indexes are positional and shift with crm.account.read; old views land on wrong columns | kapandı | MVP-6 CT | 2026-11-10
F-6 | ORTA | sabotage not covered: RB-4 UUID input, RB-6 clear on warehouse change and real tree walk, RB-1 Item/Gsku/FinishedGood | kapandı | MVP-6 CT | 2026-11-10
F-7 | DÜŞÜK | without quarantine.holds.read the Quarantine records column header is still drawn | kapandı | MVP-6 CT | 2026-11-10
F-8 | DÜŞÜK | without crm.account.read the details page still draws the Customer row | kapandı | MVP-6 CT | 2026-11-10
F-9 | DÜŞÜK | no separate empty state on details; no reason column in the lines table; RB-9 viewports and RTL not done | açık | MVP-6 CT | 2026-11-10
F-10 | DÜŞÜK | MOD-0183 test file edited only to host ReturnsCapturingLogger (XMC-001) | kapandı | MVP-6 CT | 2026-11-10
F-11 | DÜŞÜK | new Node assertion in return-receipt-flow.test.mjs is always true | kapandı | MVP-6 CT | 2026-11-10
F-12 | DÜŞÜK | UXH-001: no "?" help entry or debt-ledger row for RETURN_DETAILS | açık | MVP-6 CT | 2026-11-10
F-13 | DÜŞÜK | the list still shows the shipment GUID as text (existed before); decide keep or replace with the number | açık | MVP-6 CT | 2026-11-10
F-14 | DÜŞÜK | no HTTP / middleware test for getReturn, none for RB-27 "400 and no return written" | açık | MVP-6 CT | 2026-11-10

## Not measured (auditor)

Web full suite on `7e1d75ca8`; any live run (impossible without the getReturn route); real browser with trace.lots.read,
inventory.locations.read, crm.account.read, quarantine.holds.read; viewports and RTL; whether MOD-0175 stores
sourceModule/sourceDocumentId from CUSTOMER_RETURN; the real INVENTORY tree walk; no sabotage applied by the auditor.
