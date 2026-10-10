# MVP6-ROOT-CANDIDATE-RECONSTRUCT-R2

A new candidate was reconstructed from the verified canonical 2.0.0 baseline. The previous 91d505 hash was not reproduced or claimed. The candidate keeps contract version 2.0.0, uses revision marker R2 and status CANDIDATE.

Semantics: optional nullable `ShipmentDetail.lifecycleCorrelationId`; upgraded producer emits the authoritative persisted root explicitly; consumers do not derive or backfill. Existing scope/read permissions, wire v1, lifecycle and non-root operations remain unchanged.

The permanent archive is `root-r2-candidate.tar.gz`. Extract with:

`tar -xzf root-r2-candidate.tar.gz -C /tmp && sha256sum -c /tmp/r2pack/SHA256SUMS`

The verified exact YAML path for Producer DEV tests is `/tmp/r2pack/shipment-bundle-2.0.0-root-r2-candidate.yaml` after extraction. Candidate YAML SHA-256 is recorded in the archive manifest. New candidate hashes require a separate exact owner disposition; no canonical, runtime, authority, guard or git change was made.

Independent verification prompt: `VER-PROMPT-R2.md` inside the archive. Archive SHA-256: `2d575dbb53c204ad19f5479599de054541facf03ac2e858c43d200f7230b5a62`.
