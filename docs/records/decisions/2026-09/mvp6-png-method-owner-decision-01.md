# Owner decision — durable PNG evidence method (MVP6-PNG-METHOD-OWNER-DECISION-01)

- Decided: 2026-09-26T10:59+0300 (Istanbul), owner (CEO Natig Yusubov), CT conversation, question tool: **Accept as standard**.
- approvedBy: current-role-user-message-2026-09-26. Queue Q56 → DONE.
- Context: `docs/records/audits/2026-09/mvp6-ct-a12-ver02-intake-2026-09-26.md`; first use `mvp6-shipment-a12-runtime-independent-ver-02/png/` (16 files, PNG-INDEX.tsv).

## Approved method
Durable PNG evidence for MVP6 UI acceptance is produced with Playwright's documented API `page.screenshot({ path, fullPage })` from the same authenticated test browser contexts that produce the acceptance evidence, in a native local run. Each file is recorded with SHA-256, capture time, URL, source evidence file and tool version in a PNG index.

## Boundaries
- Playwright's internal use of CDP is accepted. Raw CDP calls, base64 extraction, tunnels, remote debugging exposure or third-party capture services remain not authorised.
- Screenshots must not show secrets, tokens or real personal data; test identities only.
- This decision supersedes the earlier "no supported PNG method" position only for this method. It does not by itself close any acceptance row: each row is closed by its own CT disposition.
