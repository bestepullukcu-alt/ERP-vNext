# MVP6-BROWSER-EVIDENCE-ENVIRONMENT-RECOVERY-01 — SOP §22

**Date:** 2026-09-24  
**Profile:** environment/evidence-only; no product writer  
**Verdict:** **PARTIAL — LOCALHOST BROWSER ACCESS PASS; DURABLE PNG SAVE/EXPORT OPEN**

## Controlling inputs

- Carrier durable-PNG disposition: `docs/records/audits/2026-09/mvp6-carrier-real-auth-ct-review-01/PNG-DISPOSITION.md`, SHA-256 `54c5ab2021449fda5ddbf4e0209cf2f8c460bc5a163604c74dbda5bd57d27f3f`.
- Shipment independent VER: `docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01/SOP-22.md`, SHA-256 `1d6df9659489e9b25592e80776c79cda7ff8166b047754f0b1ea530e072dcf0b`.
- Shipment browser attempt disposition: `ATTEMPT-DISPOSITION.md`, SHA-256 `efa72c4fcd858da05db80709936a6646f6ec9debf3de2feb2682b59547cf0488`.
- Browser documentation read in this run: core CUA API, `screenshots`, `local-web-development`, selected IAB documentation and `pageAssets` capability documentation.

## Fresh harmless probe

The only exposed browser-provider surface was **Codex In-app Browser (IAB)**. Google Chrome appeared only as a native application, not as an automation browser provider with a documented screenshot artifact API.

A secrets-free static page was served at `http://127.0.0.1:38765/`. It contained no product data, authentication, external requests or user information.

| Check | Result | Evidence |
|---|---|---|
| Terminal localhost access | PASS | HTTP 200; `raw/terminal.headers` and `raw/terminal.body.html` |
| IAB localhost access | PASS | Title and AX tree rendered `Browser Evidence Environment Probe`; two browser GET requests returned 200 |
| Supported screenshot capture | PASS, inline only | `getScreenshot()` returned a 22,179-byte image; SHA-256 `e3e113d6c9502079dfc8b0d3da58d3baec7488781012ea13c47b6307eb36c3d7`; image was displayed inline by the supported API |
| Explicit screenshot save/export | NOT AVAILABLE | No `saveScreenshot(path)` or `exportScreenshot()` capability is documented or advertised |
| Generic content export | UNSUPPORTED HERE | IAB rejected `tab_content_export`; generic content export would not be a viewport PNG even where supported |
| `pageAssets` bundle | NOT APPLICABLE | It exports page resources already observed; documentation does not define it as screenshot export |
| Durable PNG round-trip | OPEN | No supported API returned a PNG filesystem/artifact path; no file hash can be bound |

## Disposition

The prior Shipment `ERR_CONNECTION_REFUSED` finding was environment-instance specific; it is **not** a general IAB inability to reach loopback. In this root session, IAB reached the separately bound loopback service directly. This result can be reused as an environment prerequisite, but it does not prove that a future approval-isolated product process will share the IAB network namespace.

The remaining durable-PNG gap has three distinct parts:

1. **Technical access:** loopback browser access is available in this session.
2. **Security boundary:** data-URL/base64 extraction, CDP, proxy/tunnel, native capture and alternate encoding remain prohibited and were not attempted.
3. **Missing supported tool:** the current IAB backend has no explicit screenshot artifact-save/export operation returning a persistent local PNG path.

No waiver is proposed. Carrier `RA2-18` / `SUC-19` and Shipment durable-PNG criteria remain OPEN. The inline image and its in-memory hash are environment evidence only and are not a persistent PNG artifact.

## One-time environment action

Provide or enable a supported browser automation backend/API that explicitly saves a screenshot as a PNG artifact and returns its local artifact path, for example a documented `saveScreenshot(path)` or screenshot-export operation. After that capability is present, rerun only this harmless page round-trip, record the returned PNG path and SHA-256, then hand the proven mechanism to Carrier/Shipment evidence lanes. No product, Auth, Gateway or contract change is required.

## No-change boundary

No Carrier, Shipment, Auth, Gateway, pack, contract, guard or Git state was changed. The temporary HTTP server used port 38765 and was isolated from Lane A. This verdict is not Carrier or Shipment acceptance.
