# MVP6-SHIPMENT-CARRIER-PREDECESSOR-PIN-01 — SOP §22

Date: 2026-09-24  
Lane: read-only inspection plus owned evidence output  
Repository: `/Users/natig/Projects/ERP-vNext-recovery`  
Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Start dirty inventory: 2,602 entries; normalized inventory SHA-256 `0b8aec454e0327257ff8dc955170ca7901ca5ef3ef1f19d36f61a703d78b5882`.

## Verdict

**PASS — immutable Carrier predecessor input is pinned; Shipment successor preimages are conflict-free.**

This is an evidence and transfer-input disposition. It is not owner approval, source transfer, patch application, integrated acceptance, release acceptance, or UI/browser verification.

## Controlling identity

The Shipment dispatch-close package binds the Carrier predecessor through the 18-row `SHARED-TARGET-VERIFICATION.tsv`, SHA-256 `8b727462abfddcb8b054b8d75b40f6e3029cc84e57462190d9950850c62757ed`. A fresh read of all 18 files in the registered Carrier checkout matched every recorded target hash. The materialized checkout remains mutable and is not the handoff authority; `carrier-predecessor-source.tar.gz` plus this package's manifests are the immutable handoff.

No suitable durable archive containing exactly these 18 inputs was found in the inspected Carrier audit archives. Existing Carrier tarballs concern Auth evidence/source and do not represent this shared integration set. The new archive therefore contains exactly the 18 required paths, with normalized tar metadata and no UI-owned source.

## Acceptance boundary by surface

| Surface | Inputs | Boundary |
|---|---:|---|
| Gateway | 2 | Carrier route composition and its focused source test; no Shipment runtime claim |
| SupplyChain composition/registration | 9 | Carrier registration infrastructure, configuration and focused test source; no Shipment provider except after the separate successor patch |
| Shared localization | 7 | Carrier navigation strings in seven languages; excludes Carrier page resources and all UI-owned files |

`PREDECESSOR-MANIFEST.tsv` provides the path, byte hash, size, provenance and row-level boundary.

## Later Carrier changes

The quality-close patch changed only Carrier `index.l10n.js` and its JavaScript test. Its final 21-path UI manifest has zero intersection with the 18 shared predecessor paths. The separate shared Search proposal targets `main.js` and `_LayoutTenantShell.cshtml`, also has zero intersection, and remains unapplied. A scan of all Carrier audit patch headers found 52 declared changed paths and zero intersections with the pin; Auth successor/rework records therefore do not alter these 18 files. `CHANGE-INTERSECTION.tsv` records the exact quality artifacts. The 21-path UI manifest was not merged into this shared manifest.

## Shipment successor preimages

The existing Shipment patch has SHA-256 `6d9cad8e59cdab9559a3c822a1b0135672b1a4672d7988235586983cb45ba1bd`. Nine modified paths match the pinned Carrier bytes. The three Shipment-owned additions are absent in the Carrier source. Result: 12/12 preconditions match and **no conflict was found**. No patch was applied, rebased or rewritten.

## Evidence reuse

The previous disposable checks are inherited only because the 18 predecessor hashes, Shipment successor patch hash and referenced donor inputs are unchanged. They remain historical E1/E2 candidate evidence. No build or test ran in this lane, and no inherited result is labelled fresh. See `INHERITED-EVIDENCE.tsv`.

## Change and preservation statement

Only this audit directory was created. Carrier, Auth and Shipment product files, gateway source, module packs, contracts, Git index/branch and the registered Carrier checkout were not modified. Archive creation copied bytes read-only from the Carrier checkout. Hash pinning does not grant transfer, application, owner approval or release acceptance.

The normalized dirty inventory outside this output remained `0b8aec454e0327257ff8dc955170ca7901ca5ef3ef1f19d36f61a703d78b5882`, equal to the start baseline. A second deterministic archive construction produced the same SHA-256 as the delivered archive.

## Integration-owner disposition

The predecessor input is ready for a separately authorized Shipment integration lane. That lane must verify the archive and row hashes, require matching or absent target preimages, transfer the 18 inputs into its isolated target, then apply the already recorded Shipment successor. Any mismatch must stop the lane; automatic overwrite or rebase remains prohibited.
