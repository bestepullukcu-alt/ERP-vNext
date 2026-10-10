# Normal / evidence composition difference

| Dimension | Production | ClaimsEvidence |
|---|---|---|
| Registered probe behavior | `NoOpClaimCommitProbe` | `EvidenceClaimCommitProbe` |
| Invalid evidence values | Ignored; Claims GET 200 | Rejected with HTTP 500 at first Claims resolution |
| Missing bounded config | Irrelevant | Rejected with HTTP 500 at first Claims resolution |
| Fixed ID / stage injection | Unavailable | Only exact fixed UUID and six allowlisted stages |
| Endpoint or auth bypass | None | None |
| Process startup | Healthy | Health can become ready before lazy Claims service resolution |

The owner-approved source guarantees environment selection. Runtime evidence demonstrates the normal NoOp path and fail-closed Claims resolution. It does not demonstrate process-startup rejection because DI creation is lazy in this composition.

