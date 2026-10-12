# MVP6-ROOT-R2-INDEPENDENT-VER-01

## Verdict

**PASS — candidate artifact integrity and bounded semantics only.** Runtime, HTTP, JWT/RBAC, persistence, producer uptake, publication, consumer uptake and DEV GO are not claimed.

The supplied archive SHA-256 `2d575dbb53c204ad19f5479599de054541facf03ac2e858c43d200f7230b5a62` and extracted YAML SHA-256 `dab5a2f9974283e2404a29a7fb8ed07a77ae8ed843929b1c7b4546c5615dd7cb` matched. The internal SHA256SUMS manifest passed.

The exact patch applied to the archived 2.0.0 baseline and produced a byte-identical candidate. OpenAPI 3.1 parsing and 259 local `$ref` resolutions passed. Root optional nullable UUID, valid example, null/malformed negative cases and non-root/shared-component preservation passed.

The semantics document records explicit upgraded-producer emission, authoritative persisted root, trace separation and no derivation/backfill. These are candidate design assertions, not runtime evidence.

Candidate revision R2 is separate from contract version 2.0.0: metadata remains `version: 2.0.0`, `x-status: CANDIDATE`, `x-candidate-revision: R2`. The old 2.1.0 approval/hash was not reused or inferred.

Evidence archive: `root-r2-independent-ver-01-evidence.tar.gz`. Extract it and verify `EVIDENCE-SHA256SUMS` for the complete evidence set. No repository source, contract, authority, guard or git file was changed.
