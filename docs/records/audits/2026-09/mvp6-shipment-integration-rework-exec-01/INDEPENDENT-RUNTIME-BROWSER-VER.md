# MVP6-SHIPMENT-INTEGRATION-REWORK-INDEPENDENT-VER-01

Role: independent verifier; do not write product source.

## Immutable writer handoff

- Base HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Final source overlay: `final-source-overlay.tar.gz`, SHA-256 `8aba3d890f160cbed88eecc284cdcba78eda03b6b0955cf3061199a62171112e`.
- Final source manifest: `FINAL-SOURCE-MANIFEST.tsv`, SHA-256 `a469678926c0b085075e6c14d8977789e26eb2e787558acf86905081030fa51e`.
- Successor delta: `SUCCESSOR-DELTA.tsv`, SHA-256 `467cbaa87ee34705a997edd40bd1754ce0c3026cae9baaba72a82bd3302383fb`.
- Carrier Auth-chain PASS handoff: `mvp6-carrier-auth-chain-recovery-01/CARRIER-REAL-AUTH-E2E-HANDOFF.md`, SHA-256 `95dafe0fbfdfdf490f9ecdb57f65582d0146da435ad938f1df13c11105586cd2`.
- Latest Carrier real-Auth evidence boundary: `mvp6-carrier-real-auth-e2e-exec-01/CARRIER-REAL-AUTH-E2E-CT-HANDOFF.md`, SHA-256 `111a50136d1405cb09f716f451d0ba8ba304e13ac85c8bc95b0797c1eeb776af`; durable PNG remains open there.

## Execution

1. Create a separate disposable source copy from base HEAD plus the exact overlay. Do not share the writer's mutable worktree.
2. Verify all manifest hashes, 258 transferred paths, 161 preserved paths, three Shipment successor paths, 28 UI paths and both gateway target hashes.
3. Fresh restore/build SupplyChain, Gateway, CRM and Web with native .NET 8. Bind source→binary→process.
4. Use separate ports and a DB-010 isolated Mongo replica set; never contact operational `27017`.
5. Start the published Shipment/Carrier composition through Gateway and use real Auth-issued sessions from the current PASS handoff. Do not mint diagnostic bearer tokens or modify Auth source.
6. Execute only the existing MOD-0183 UI acceptance: Shipment list, create, detail, valid transition and POD; real-row actions; replay/conflict/error/correlation behavior; persistence and process restart; tenant/LE isolation; read/create/transition/POD permission separation; UAS-001; browser same-origin Web→Gateway traffic.
7. Re-run the focused route ownership checks and prove CRM routes remain 5065 while all ShipmentBundle routes remain 5061.
8. Preserve inherited L10n/responsive evidence by source hash unless drift is found. Use only a supported screenshot save/export facility; otherwise keep durable PNG OPEN.
9. Record failed attempts, raw redacted HTTP/process/DB evidence, cleanup and a SOP §22 verdict. Do not equate technical PASS with CT/full-module acceptance, rollout, E5 or G5.

No source correction, new endpoint, business rule, canonical/guard change, commit, push or stash is authorized. A product defect must be reported as a separate exact rework finding.
