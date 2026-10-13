# Shipment integration-owner handoff

Use `carrier-predecessor-source.tar.gz` only after its SHA-256 and every row in `PREDECESSOR-MANIFEST.tsv` match. Extract into a new isolated target and fail closed if any target path is already present with different bytes. This pin authorizes no transfer or application.

The Shipment successor is the existing, unapplied `SHIPMENT-SHARED-SUCCESSOR.patch` with SHA-256 `6d9cad8e59cdab9559a3c822a1b0135672b1a4672d7988235586983cb45ba1bd`.

All nine modified predecessor preimages and all three required absent preimages match. Result: **NO CONFLICT**. Apply order, when separately authorized, is Carrier predecessor archive first and Shipment successor second. Preserve the other nine Carrier predecessor files byte-for-byte. UI manifests are separate and are not transfer inputs.

Inherited build/focused-test evidence is historical E1/E2 evidence bound to the same predecessor and successor hashes. It was not rerun here and does not establish runtime, browser, owner approval, release acceptance, or integrated checkout acceptance.
