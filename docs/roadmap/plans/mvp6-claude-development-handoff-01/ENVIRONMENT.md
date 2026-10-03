# Environment and reproducibility

Observed handoff branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Initial short-status count: 336 entries. Dirty contents are user work, not disposable noise. This count is not a source manifest. WORKTREES.txt is a read-only snapshot; several historical /private/tmp worktrees are prunable/missing. Do not prune or assume they can run.

Native runtime reported in accepted evidence: `/Users/natig/.dotnet/dotnet`, SDK 8.0.417, runtime 8.0.23, arm64. Verify current availability. Major roll-forward to .NET 10 does not establish native .NET 8 acceptance.

Use fresh isolated lane ports, isolated DB-010-compliant Mongo databases/replica set and explicit configuration binding. Never connect test work to operational port 27017. A historical harness fell back to 27017 through the wrong configuration section; exclude such attempts and validate resolved configuration before startup. No reusable token/session is transferred; recreate authorized fixtures and real Auth login.

Frontend must use Gateway. Historical integrated route decision: 35 CRM routes to 5065 and 6 ShipmentBundle routes to 5061. Do not infer current route correctness without exact source binding. Do not edit gateway during A12 work.

A12 archive lacks frontend and frontend-test csproj, gateway and gateway-test csproj, SupplyChain Application and referenced building-block projects. Recover these from an authorized known build baseline, not HEAD alone or a dirty checkout. Keep a full build-input manifest plus the separate owned 360-source manifest and source→binary→process→browser chain.

IAB localhost round-trip previously worked. Earlier connection-refused was process-specific, not a universal restriction. Durable PNG export was unavailable in the prior backend. Claude tooling may differ: use only documented available APIs, verify durable artifact path/hash, and keep OPEN if unavailable. Do not bypass prior restrictions through base64/data-URL extraction, CDP, tunnels or unapproved native capture.

Platform aggregate health 503 due business_reference_data_provider is an unresolved environment observation; Auth→Platform→MDM functional PASS does not make aggregate health PASS. Outbox records were Pending; do not claim publisher behavior or exactly-once delivery.

End every runtime lane with process/port cleanup and secret removal. Persist redacted evidence, request/response metadata, exact source/binary hashes and before/after DB assertions, never bearer tokens or reusable credentials.
