# MOD-0190 / MOD-0192 — hash-bound SANDOP-CAPACITY handoff

**HOLD — publication verification gate not green.** The canonical two-file payload is present at the owner's exact 2.0.0 / wire-v1 hashes. The owner's MOD-0190 and MOD-0192 release-consent decisions cover those bytes; they do not prove pack uptake, real producer behavior or runtime acceptance. Do not promote either draft pack or dispatch DEV from this handoff.

| Consumer | Owner consent | Published contract input to pin | Uptake state |
|---|---|---|---|
| MOD-0190 S&OP Workflow & Sign-offs | Granted in the owner's three-clause publication message | Canonical YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`; patch `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04` | Pack still draft; no repin/runtime uptake verified here |
| MOD-0192 Capacity Planning | Granted separately in the same message | Same exact three hashes | Pack still draft; no repin/runtime uptake verified here |

Canonical paths are `docs/analysis/contracts/sandop-capacity.openapi.yaml` and `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md`. Baseline SHA-256 before the prior authorized apply was `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`; DEMAND remains `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d`. Independent final VER and prior publication evidence are linked in [SOP-22.md](SOP-22.md).

Before downstream pack/Phase 1.5 decisions, resolve the failing production DocsPathGuard disposition and the canonical annex's `UNAPPROVED`/noncanonical text by an exact authorized path. A changed annex hash cannot inherit current consumer consent automatically. Then each consumer owner can measure pack/mock/test repinning against the actually published target. This handoff does not authorize runtime, shared composition, migration, rollout, E5/G5, commit or push.
