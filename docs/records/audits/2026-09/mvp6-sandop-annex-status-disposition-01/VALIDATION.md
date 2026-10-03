# Candidate validation — status text only

| Check | Result |
|---|---|
| Canonical annex input SHA-256 | `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442` MATCH |
| Candidate annex SHA-256 | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| Unified patch SHA-256 | `5d66663f0983e0022f5d8f1b1aaaf6e28148c417be29217f54b48edfc82204b0` |
| YAML SHA-256 before/after candidate construction | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` MATCH, no edit |
| Disposable `git apply --unsafe-paths --check`, `git apply --unsafe-paths` | exit 0 / exit 0; applied annex byte-identical to candidate |
| Exact edited lines | 1, 3, 5, 9, 13, 15, 19, 21, 36, 38, 40, 45, 58 of 61; no insertion or removal of a line |
| Inverse of 15 explicit editorial replacements | Recovers canonical baseline byte for byte |
| Twelve operation matrix rows | 12/12 byte-identical; SHA-256 over joined rows `fd7e0a86d8dcdee057ba1c9ae84af64e1d8253c3c66212745c6cc089b3ffe012` |
| Executor bullet lines 47–54 | Byte-identical; SHA-256 `7ffbf1174a42c2417c32eb739cd8917601c8a297fb43d5087620a4b1ab7338df` |
| Other normative anchor lines | Transport line 7, receipt/fingerprint line 11, matrix rows 23–34 and control lines 59–61 byte-identical |
| YAML↔annex binding | OpenAPI 3.1, version `2.0.0`, status `FROZEN`, same annex pointer and 12 operation IDs present in candidate |

Changed text removes false present-tense `UNAPPROVED`, noncanonical, unpublished and candidate-only labels from the **published** contract narrative. The earlier candidate-preparation approvals and the historical `DECISION.md` status remain explicitly identified as historical. The candidate does **not** claim runtime uptake, Phase 1.5 or a passing DocsPathGuard gate. The existing full OpenAPI validator evidence remains bound to the unchanged YAML hash; no new full validator or runtime test was run for this editorial candidate.
