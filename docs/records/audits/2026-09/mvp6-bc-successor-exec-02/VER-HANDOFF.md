# Independent VER handoff

Verify `MVP6-BC-SUCCESSOR-EXEC-02` without writing repository or writer sources.

1. Require source archive `BC-SOURCE.tar.gz` SHA-256
   `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`.
2. Extract to a new disposable directory and require the 422-row manifest
   SHA-256 `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`.
3. Verify the three changed and three protected hashes in
   `CHANGED-PRESERVED.tsv`; require every archive file to match
   `SOURCE-MANIFEST.tsv`.
4. Use a verifier-owned `rsmod192` replica set and ports. Do not reuse the
   writer database or processes.
5. Fresh build, then run the exact CapacityPlans filter. Reconcile individual
   TRX test identities; do not add historical 36 and 34 counts.
6. Independently issue authenticated HTTP create/scenario/replay,
   changed-payload and duplicate-name requests. Require exact 409 code/message,
   response correlation, one scoped scenario and no duplicate aggregate.
7. Restart the same verifier-built binary against the same verifier DB and
   verify persisted read/replay plus duplicate-name behavior.
8. Confirm X01, X07, hosted/read-fault, deterministic duplicate and race tests
   pass in the fresh run.
9. Keep technical PASS separate from CT acceptance, full-module, rollout and
   E5/G5.

