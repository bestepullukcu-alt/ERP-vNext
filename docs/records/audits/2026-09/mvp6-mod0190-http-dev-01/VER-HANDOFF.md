# Independent VER handoff — MOD-0190 HTTP DEV v1.0

Writer-complete for the **exact transfer and Program.cs patch**, with HTTP acceptance **FAIL/PARTIAL**. A different verifier must use an immutable source snapshot from `/private/tmp/mvp6-mod0190-http-integration-02`, verify the 341 normal + 38 Sandop hashes and Program target, and inspect raw evidence before any execution. Do not use the mutable writer checkout. This is not a request to promote or fix source.

Independently verify: archive/manifest and 340/340 protected normal paths; registered worktree; Program.cs target `0f6bf84e1c7ddff79868d32a23099e8934a33e3ac80ccecbe8b16f5e0a6c5cc2`; fresh build and API DLL SHA `7ca1154360b076a39a6d9a028faa606835f5d88d46bf1d81d2fcf65848e6a9ae`; missing `IDemandFixtureReader` DI registration in Development startup and production POST 500; published 401 body/header parity; 18/19 direct-Mongo failpoint discrepancy. Keep runtime 10 roll-forward separate from target .NET 8 acceptance. No authorization to change Program.cs, source, tests, contract, MOD-0192, gateway or git.

An independent VER may confirm this FAIL/PARTIAL handoff. Full acceptance should wait for an exact rework decision and fresh DEV evidence, not reuse the current raw results as GREEN. Gateway E5/G5 and rollout remain separate.
