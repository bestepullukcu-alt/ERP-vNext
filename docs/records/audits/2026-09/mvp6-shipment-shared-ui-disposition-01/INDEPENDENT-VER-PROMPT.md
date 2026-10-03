# MVP6-SHIPMENT-SHARED-UI-INDEPENDENT-VER-01

Role: independent verifier; do not write the candidate or product source.

Verify the owner-approved exact combined patch and all preimage/target rows in `BASELINE-PATCH-TARGET.tsv`. Reconstruct a disposable target and prove patch apply plus byte equality.

For UI183-A03, use the real ASP.NET pipeline. Exercise unauthenticated list, detail, create, transition and POD adapter routes. Each must return 401 JSON with `INVALID_REQUEST`, `contractVersion=v1`, no login Location and a valid matching correlation header/body. Verify `/SupplyChain/Shipments` still returns the standard 302 login redirect. Verify authenticated missing permissions remain 403 and that unmarked MVC pages preserve existing challenge behavior. Do not weaken `[Authorize]` or treat correct internal routing as authorization proof.

For PRES-183-02, run a real browser at measured `window.innerWidth` 390 and 768 in all seven tenant cultures. Open a responsive DataTable row and assert the localized modal heading and close accessible name, including Arabic `التفاصيل` / `إغلاق`, while localized row labels/actions and RTL remain intact. Check missing keys and console errors. Do not use module-local hardcoding or a general DOM text replacement.

Run only impacted Diten.Web build and focused Shipment/DataTable regressions. Report inherited RED separately from fresh GREEN. Candidate static tests are not final browser acceptance. Do not modify accessibility-writer files, Gateway, Auth service, canonical, guard, pack or Git.
