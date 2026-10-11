# Independent VER handoff — MVP6-CARRIER-AUTH-TOKEN-REWORK-VER-01

## Role

Use a verifier that did not write this rework. Keep the repository read-only and work from `final-source.tar.gz` in a disposable directory.

## Exact inputs

- patch: `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`
- delta manifest: `5f7369c8b09ab28f37a8b21e958e2a14258dba60e1307d474d8110cca5f80153`
- final 22-path manifest: `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`
- build-source manifest: `1b3eaf5101cd6a9ee2919c14e71fd51b50c6c433a08ef4dcdbf4492cc09d914e`
- final source archive: `47c39cdbeaa61a59b9b4891439bc758589911d535f7b84736e8ce3a4a88688e6`
- DEV evidence archive: `ba051ce775814a66b6e59abe9027c03ff67a48037376f64554c03d7c900a42a5`

## Required verification

1. Verify archive, manifest, preimage, patch, and target hashes; confirm only the authorized two files differ.
2. Use native `/Users/natig/.dotnet/dotnet` SDK 8.0.417/runtime 8.0.23 with no major roll-forward.
3. Reproduce the 28 validator tests and 8 Platform successor tests.
4. In a new DB-010 lane-owned replica set, reproduce exact-cardinality, audience, iat/nbf, configured-skew, expiry, and maximum-lifetime negatives over real HTTP.
5. Run real Auth login and refresh through Platform and MDM; record redacted claims only and require the authoritative `legal_entity_id` on both tokens.
6. Bind source, build, binary, process, HTTP, and DB inputs. Do not reuse the DEV processes or database.
7. Read `ATTEMPT-DISPOSITION.md`; do not treat the rejected 27017-bound attempt as evidence.

Do not fix source, weaken JWT validation, archive tokens or secrets, or claim Carrier E2E/CT acceptance.
