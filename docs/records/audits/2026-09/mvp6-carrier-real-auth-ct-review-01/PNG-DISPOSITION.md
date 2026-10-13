# Durable PNG disposition

## Decision

**OPEN — mandatory evidence criterion, no waiver.**

The successor acceptance trail identifies the durable screenshot as `RA2-18` / `SUC-19`. The final E2E lane again observed browser screenshot bytes suitable for inline display, but the approved automation surface exposed no durable PNG save/export operation.

DOM/accessibility transcripts, responsive geometry, browser session results, page export and page assets do not satisfy this criterion. Data-URL extraction, CDP, base64 extraction or native capture were not authorized and were not used.

## Effect on CT verdict

The fresh browser and HTTP/DB evidence is sufficient for the individual functional rows. The missing durable PNG prevents a complete bounded UI evidence acceptance, so the consolidated CT verdict is `PARTIAL`.

## Narrow closure condition

Use a supported screenshot artifact-save/export capability to retain the required PNG while running against UI manifest SHA-256 `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`. Record its checksum and bind it to the authenticated Carrier browser state. If the UI source changes, use the successor source manifest. No source change or broader test rerun is required by the current evidence.
