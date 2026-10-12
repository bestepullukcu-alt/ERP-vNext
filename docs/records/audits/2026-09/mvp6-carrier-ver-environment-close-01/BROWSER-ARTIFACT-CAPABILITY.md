# Browser screenshot artifact capability

The supported browser API can capture a screenshot as bytes and display it inline in the conversation. It does not
expose a supported `saveScreenshot(path)` or `exportScreenshot()` operation. `ContentAPI.export()` exports page
content, not a viewport PNG; `pageAssets` packages assets already observed in the page and is not screenshot export.

Therefore the successor may retain visible inline browser captures and browser-session hashes, but a durable PNG in
the repository/workspace remains **OPEN** under the currently permitted mechanism. The previous data-URL rejection
must not be bypassed with CDP, base64/different encoding, native screen capture, or another capture path. If the
browser tool later exposes an explicit screenshot artifact-save/export API, the successor may use that named API and
must record its returned path and file hash.

No browser E2E or capture was run for this environment-preparation task.
