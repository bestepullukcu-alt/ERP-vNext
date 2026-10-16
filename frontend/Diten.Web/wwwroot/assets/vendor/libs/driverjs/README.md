# Driver.js — vendored

| | |
|---|---|
| Library | Driver.js (step-by-step tours) — https://driverjs.com |
| Version | **1.8.0** (npm release 2026-07-17; 1.9.0 was younger than two weeks when chosen) |
| Licence | MIT — `LICENSE` in this folder (Copyright (c) Kamran Ahmed) |
| Source | npm `driver.js@1.8.0`, `https://registry.npmjs.org/driver.js/-/driver.js-1.8.0.tgz`, package integrity `sha512-+8/IO7h1v14IzWh2GP60N7T3PFZweXwdn5e5POuxRSBoCYUojsBxzqawPeXh3YZIibRy7EehYNEyxe7slwwtdg==` (equal to the registry's record) |
| Vendored | 2026-10-13, WP-UX-TOUR-DRIVERJS-01 (owner approval 2026-10-13) |

Files, copied byte for byte from the package's `dist/` (never minified, formatted or patched here):

| file | from | SHA-256 |
|---|---|---|
| `driver.js.iife.js` | `dist/driver.js.iife.js` | `c6ade0b831c6c043daf480861208cd2fa45ea4aac581cc8bb8e234281c011ddf` |
| `driver.css` | `dist/driver.css` | `d095d440021fcf133ad46d37f18a2745fb76440f14f5208d17e203c039f765c9` |
| `LICENSE` | `license` | `ec3ce3a08736fefd6a03a6d5b52b0705e6919fe06de9d7bd3fb63dcfb492d76d` |

`tests/driverjs-vendor.test.js` pins these hashes: a changed byte is red, by file name.

**Who uses it.** Only `assets/js/shared/diten-tour.js` (DitenTour). Pages never call the library. It is loaded once
per shell by `Views/Shared/_PageHelpPanel.cshtml` (tenant shell and platform admin shell), never from a CDN. The
library writes a step's title and text with `innerHTML`; DitenTour escapes every string first.

**It does not update itself.** To move to another version:

1. Pick a release at least two weeks old; read its changelog for breaking changes in `driver()` options or the IIFE
   global (`window.driver.js.driver`).
2. Download the npm tarball; compare its `sha512` with `npm view driver.js@<v> dist.integrity`. Different → stop.
3. Copy `dist/driver.js.iife.js`, `dist/driver.css` and `license` → `LICENSE` here, unchanged.
4. Update this table and the hashes in `tests/driverjs-vendor.test.js` in the same change.
5. Run `tests/diten-tour*.test.js` and `tests/driverjs-*.test.js`, then the three tours by hand (Task Center,
   Timesheet, Time approvals).
