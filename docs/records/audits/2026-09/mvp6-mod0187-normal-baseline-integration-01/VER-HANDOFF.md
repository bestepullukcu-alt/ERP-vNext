# Independent normal-baseline VER handoff

Input: `normal-source.tar.gz` SHA-256 `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21`, `combined-source-manifest.tsv` SHA-256 `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`, and `test-evidence.tar.gz` SHA-256 `c49bc090b704baca8e42536e64814ecfc4fe0ac3b2b4e45447fa7e0f5cf66dcf`.

Extract into a new disposable directory. Verify all 341 source hashes before build. Use a fresh DB-010 replica set and a new .NET 8 build; bind executed binary, process, JWT-authenticated HTTP and tenant/LE-scoped Mongo evidence. Independently verify R14 valid/malformed UTF-8 and 128/129 Unicode-scalar boundary, R21 authoritative nil root and separate non-nil outbound trace, and exact malformed producer `500 SHIPMENT_ROOT_INVALID` → Claims `502 CLAIM_REFERENCE_INVALID` with zero writes and inbound correlation preservation. Unknown/broken 5xx, refusal/timeout, missing/null and malformed successful payload remain distinct.

Do not include the separate R22/R25 evidence-injection patch, repair product source, modify common checkout, or infer CT acceptance from this DEV PASS.
