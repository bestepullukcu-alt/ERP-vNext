# Probe / launch / exit transcript

1. AUTH-05 immutable integration source was copied into `/private/tmp/mvp6-mod0187-e4-persistence-01/source`. The supplied combined manifest `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8` verified 97/97 before and after execution.
2. A no-incremental API build completed in 93.70 seconds with 0 warnings and 0 errors. API binary: `9468a3d06bb6cf67d8cfefc6d8b1b0332b70e7c556fee04738892207798ded2d`.
3. MongoDB 8.0.18 started as PID 94572 on `127.0.0.1:27893`, replica set `claims_e4p01`, with test commands enabled and database `diten_claims_e4p01`. `hello.isWritablePrimary=true` was observed.
4. First API process PID 95466 started the fresh binary on `127.0.0.1:5065`. The main probe executed signed JWT HTTP requests and direct scoped DB observations.
5. The first probe attempt completed its scenarios but its evidence parser rejected .NET's seven-digit UTC fraction. No product source changed. The lane probe was corrected to accept the actual `+00:00` wire value and rerun with new UUID scopes.
6. Corrected main probe exit: 0; reported `R19_R23=PASS`, `R20=PASS`, `R24=PASS`, `R26_R08_seed=PASS`.
7. First API process received Ctrl-C and exited 0.
8. Second API process PID 96381 started the same binary and database on port 5065. Restart probe exit: 0; exact long coefficient, identity, list, replay and collection counts survived without an extra write.
9. Second API process received Ctrl-C and exited 0. Mongo shutdown intentionally closed the client connection; no matching API or Mongo process remained.

JWT secret and bearer values were ephemeral and are not archived. Raw HTTP responses, request body hashes, scoped DB counts, audit actors and outbox documents are in `raw/process-boundary.json` and `raw/restart.json`.
