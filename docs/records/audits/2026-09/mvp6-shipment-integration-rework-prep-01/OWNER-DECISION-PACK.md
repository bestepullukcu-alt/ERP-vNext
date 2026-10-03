# Exact owner decision — UNAPPROVED

I authorize the single Shipment integration owner to apply the following two immutable inputs to the registered Shipment integration checkout, only after all recorded preimages match:

1. `BC-SOURCE-CLOSURE.tar.gz`, containing exactly the 258 `TRANSFER` rows in `SOURCE-TRANSFER-MANIFEST.tsv`, derived byte-for-byte from the accepted BC archive identified in `INPUTS.tsv`. The 161 `PRESERVE_IDENTICAL` rows and three `PRESERVE_SUCCESSOR` rows in `SOURCE-CLOSURE-PLAN.tsv` must not be overwritten.
2. `shipment-integration-successor.patch`, whose exact SHA-256 is recorded in `ARTIFACTS.sha256`, and whose only targets are `gateway/Diten.ApiGateway/ocelot.json` and `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`. It restores the two Carrier routes to SupplyChain port 5061, retains all 35 CRM routes on 5065, and adds route-owner regression assertions plus the already documented 5063/5064/5065 ports to the test allowlist.

The integration owner must preserve the exact 28-path Shipment UI source in `UI-PRESERVATION.tsv`, stop on any preimage mismatch, run the listed SupplyChain/Gateway/CRM builds and route tests, and hand the immutable result to an independent verifier.

This decision does not authorize UI redevelopment, a Program.cs rewrite, deletion of accepted module registrations, Auth changes, canonical/guard changes, runtime rollout, E5/G5, commit, push or stash. Preparation of this text is not owner approval.
