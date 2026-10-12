# MVP6 Carrier real Auth E2E execution

## Outcome

The bounded Carrier CT evidence lane completed against a fresh disposable snapshot. Real Auth-issued browser and API sessions exercised list, create, replay, row-driven status change, permissions, UAS-001, tenant and legal-entity isolation, persistence, and refresh-time legal-entity revocation through Gateway.

No product source was changed. The durable PNG criterion remains OPEN because the browser automation surface did not expose a supported save/export method. This lane does not grant full-module, rollout, E5 or G5 acceptance.

## Source control

- Controlling handoff SHA-256: `95dafe0fbfdfdf490f9ecdb57f65582d0146da435ad938f1df13c11105586cd2`.
- Final UI manifest SHA-256: `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`.
- Final services were composed from the approved NumericDate archive and verified as 22/22 final paths plus 3333/3333 build inputs.
- F-01/F-02 49/49 remains CLOSED / INHERITED by the controlling handoff hash. L10n/responsive remains INHERITED / NOT RERUN.

## Runtime

Native .NET 8 Release binaries ran on Gateway 5600, Web 5601, Auth 5656, Platform 5657, MDM 5659 and SupplyChain 5661. MongoDB used verifier-owned replica set `rsCarrierRealAuthExec01` at `127.0.0.1:38994`. Effective configuration was captured before service launch, and no process connected to 27017.

The browser authenticated through local Web/Auth and used a server-managed session. Carrier service calls flowed through Gateway. Direct acceptance calls also used real Auth-issued tokens, held only in process memory.

## Result details

The UI created `E2E-CARRIER-01`, reloaded it, opened its status offcanvas from the persisted row, and persisted `Suspended` with reason `E2E-VERIFY`. The API replay test returned 201 for the first request, 201 with the same id and `idempotentReplay=true` for the identical replay, and 409 `IDEMPOTENCY_KEY_REUSED` for a changed payload.

The read-only user received list 200 and create/status 403. The no-role browser received the UAS-001 denial surface with no protected page shell. A signed LE1 token requesting LE2 returned 404. A separate tenant-97 Auth session saw zero rows while tenant-1 retained two. A token without a signed legal-entity claim was rejected by the Carrier consumer with 403.

For revocation, the isolated John position assignment was soft-deleted after login. Refresh returned 200 with a new token that omitted `legal_entity_id`; Carrier then returned 403. The fixture was restored. After SupplyChain restart, both the UI row and replay row persisted with their expected statuses.

## Health and observations

Gateway, Auth and MDM health endpoints were healthy. Platform aggregate health returned 503 solely because `business_reference_data_provider` was unhealthy; self, MongoDB and Hangfire storage were healthy. This report preserves the 503 and does not label overall Platform health PASS.

## Evidence handling

The archive contains redacted configuration, safe request/response bodies, build logs, binary hashes, listener provenance and health bodies. It contains no bearer token, JWT, cookie, password, signing secret or raw Auth login/refresh response. See `SECRET-SCAN.txt` and `ARTIFACTS.sha256`.

