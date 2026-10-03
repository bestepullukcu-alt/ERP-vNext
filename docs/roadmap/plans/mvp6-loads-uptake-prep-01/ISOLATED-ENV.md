# Isolated environment recipe — Loads producer uptake (Mac Terminal only)

Method: HEAD archive + accepted overlay (process v1.0 §5; the recipe used by A12 VER-02). Never build from the dirty common checkout. All runtime, build and test steps run in Mac Terminal (native .NET 8, MongoDB replica set); chat lanes cannot run them.

| Step | Input | sha256 | Source |
|---|---|---|---|
| 1 | `git archive 4a8d4d4b339528a88e6220fb8402e5a2c771136c` into a new `/private/tmp/mvp6-185-uptake.<rand>/` | — | HEAD (Loads files are untracked at HEAD: 0 tracked) |
| 2 | Overlay BC-SOURCE (422-row composite incl. all 42 Loads files, Program.cs composition) | archive `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064`; manifest `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634` | `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` (SOURCE-LOCATIONS.tsv) |
| 3 | Copy the published contracts (uncommitted working tree) for evidence hashing only | YAML `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`; annex `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` | `docs/analysis/contracts/` (CT ACCEPTED Q25/Q32) |
| 4 (runtime real-Auth only) | Auth/Platform/MDM overlay, 22 paths | archive `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`; manifest `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731` | `docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-01/final-source.tar.gz` |

Checks before editing: verify every sha256 above; confirm the 11 owned-path preimages (OWNED-PATHS.md); record a BUILD-INPUT-MANIFEST (entry projects, closure, 0 missing) as A12 VER-02 did.

Runtime: isolated Mongo replica set on a lane port (LoadSchema requires replica-set transactions); never port 27017; seed legacy documents for LU-03…LU-07 directly in the isolated database; clean up processes, ports, secrets and data at the end. Evidence folder: `docs/records/audits/2026-09/mvp6-loads-uptake-dev-01/` (writer) and a separate `…-independent-ver-01/` (verifier).
