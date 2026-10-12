# SOP §22 — Independent Loads R2 VER

Verdict: normative/schema candidate PASS — E1/E2 only. Handoff evidence requires one metric correction before a clean package PASS. Publication and runtime remain NO-GO.

## Finding

F-R2-01 (Low, evidence): original README.md:24 and compatibility.md:2 report 11 preserved non-Loads operations. Independent enumeration finds **10 paths / 14 operations**: Shipment 5, Carrier 3, Returns 3, Claims 3. All fourteen operation objects are unchanged. This is an inaccurate handoff count, not lost operation coverage or a normative defect. Correct only a separate evidence revision; preserve frozen R2 and candidate bytes. No other concrete normative defect found within this static review.

## Independent reproduction

Input: /private/tmp/mvp6-loads-candidate-r2-132mn1u9. User approved candidate preparation only. Reviewed AGENTS, read-only auditor/workflow, module pack (still draft), D185 design and R185 precision approval records, candidate annex, validators and patch. Outputs confined to /private/tmp/mvp6-loads-r2-ver.

- Manifest: 21/21 entries match. Patch SHA256 3533d85a0e03ddd4a25606c66df15752c19964fce7aa76fc5ee0f2a6474426ac.
- Fresh disposable canonical baseline: git apply --check and apply succeed; both resulting outputs exactly match candidate hashes.
- YAML SHA256 93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571.
- Annex SHA256 a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1.
- Independent structure comparison: 10 non-Loads paths / 14 operations unchanged; shared components unchanged. Remaining document content unchanged except version and Loads path objects.
- Reexecuted validate.py on COPIED package: 60 assertions, 259 local references, 133 valid inline examples. 68 consumer records include 8 executed Shipment profile cases and 60 declared future runtime oracles.
- Reexecuted precision_corpus.py on copy: 70 static cases, seven incorrect-alternative distinctions.
- Reexecuted check_negative_controls.py on copy: 10 actual document/schema mutations rejected.
- Two canonical inputs and all 17 R1 input hashes match prior sealed values.

## Semantic assessment and limits

D185 eligible Draft/Planned, full reference checks at specified transitions, Cancelled-only release, root-before-fingerprint, historical replay, receipt/audit/outbox atomicity and no live publisher are preserved in annex. Carrier required omission maps 502 explicitly; schema-valid missing Shipment decision fields remain 503. No residual 1.2.0 references in current candidate YAML/annex. Query keys decode once, ASCII-only case-fold, exact six-key set; unknown keys cannot bind scope. UUID lexical rule is ASCII hex, no trim, nil correlation allowed but trusted identity non-nil. Authentication parser failure401 is separate from successful-auth unusable/duplicate claims403. Rejection fallback is trace-only, no business root. Safe error shape excludes secret/root disclosure. 2.0.0 is candidate metadata, no implied migration/route negotiation/consent or guard authorization.

Tests are nonempty and schema-linked, but are reference-oracle execution rather than implementation tests. Seven 'wrong oracle alternatives rejected' are discriminating assertions, not seven executed mutant implementations. Actual ten mutation checks are separate. Authentication success is supplied as corpus input; no JWT parser, HTTP server, replay/persistence/concurrency or consumer SDK execution occurred. These limits are already acknowledged in candidate; no E4 or runtime PASS inferred. Existing runtime/architecture tests were not rerun because this is separate spec candidate verification.

## No-change

Before/after 14,428 repository non-build file hashes identical; all 22 original package file hashes identical. Snapshot excludes .git, bin, obj, node_modules, .venv and __pycache__; .git not written by reviewer. Git branch, HEAD, status and staged inventory identical; git diff --check empty. Branch feature/mvp6-logistics; HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c. No runtime files, contract publication, guard binding, pack promotion, commits, push or stash changes.

Evidence: before.json, after.json, no-change.json, independent-results.json, independent.py, validate.log, precision.log, negative.log, copy/*results.json. Command scripts retained only under this temp directory. Reviewer did not edit original package or repository.
