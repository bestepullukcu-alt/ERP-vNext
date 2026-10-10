# Browser environment handoff

## Reusable result

- Use the Codex In-app Browser directly against an isolated loopback URL.
- Bind the product process to an explicit unused loopback port and verify it first with terminal HTTP.
- Navigate IAB to the same exact URL. A successful AX/DOM render proves browser reachability for that environment instance.
- Use the documented screenshot operation only for inline visual inspection.

## Gate that remains open

Do not label an inline screenshot as a durable PNG. The present browser backend does not expose a supported screenshot artifact-save/export API. Do not use data URLs, byte/base64 extraction, CDP, proxy/tunnel, native capture or alternate encodings to manufacture a file.

The environment is ready for browser reachability checks. It is not ready for acceptance criteria that mandate a persistent PNG until the browser backend exposes an explicit screenshot-to-artifact path operation.
