# MVP6-LOADS-ROOT-AMENDMENT-INDEPENDENT-REVER-01

Role: independent read-only verifier who did not prepare candidate-02.

Verify candidate-02 YAML `aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744`, annex `e2107301e3c6cfae5e6768de77c19f5b4fa5ea7ff42f1a4023650bd3fb7750cd`, and patch `27cc3a8f6c5000b9d06b80b2b0037e84750cd7bc40611da4b5f6fa33582e11cc`.

Apply the patch to disposable copies of canonical YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` and Loads annex `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`; require byte equality with both successor targets.

Compare candidate-01 and candidate-02. Require the YAML to be byte-identical and the annex to differ only by `info.version2.0.0` → `info.version3.1.0-rc.1` in the release/migration boundary. Verify the annex title, top candidate statement and corrected normative sentence consistently identify `3.1.0-rc.1`, while wire `contractVersion v1` and the warning about negotiation/migration/cutover/rollback remain.

Do not rerun full OpenAPI/ref/example validation merely to produce a fresh count if YAML hash identity is proven. Bind the prior independent result from `mvp6-loads-root-amendment-independent-ver-01` as inherited evidence and label it accordingly. Independently confirm that root authority, optional/nullable and fail-closed policy, error/status, permissions, replay and correlation text outside the one-line correction remains byte-equivalent.

Report PASS/REWORK in a new audit directory with hashes, apply logs, exact delta, inherited-evidence boundary and remaining release gates. Do not edit candidate/canonical files, select a final version, grant consumer consent, publish, or authorize runtime work.
