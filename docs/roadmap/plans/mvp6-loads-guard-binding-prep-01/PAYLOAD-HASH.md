# Candidate payload SHA-256 — computation

Algorithm (DocsPathGuardTests.cs lines 220–222, and the fixture's `Approve()` at 314–315):

```csharp
var payload = doc.RootElement.GetProperty("canonicalTargets").GetRawText() + "\n" +
    doc.RootElement.GetProperty("sealedInputs").GetRawText();
Assert.Equal(Hash(Encoding.UTF8.GetBytes(payload)), decision.PayloadSha256);   // lowercase hex SHA-256
```

`GetRawText()` returns the property value exactly as written in the file bytes (from `[` to the matching `]`, with the original whitespace and newlines). The payload does not include `schemaVersion`, `status` or `decision`, so it is the same in the UNAPPROVED candidate and in the activated file.

## Reproduction

`guard-emulator.py.txt` → `raw_value_span()` extracts each raw array span with a string-aware bracket matcher, and `payload()` joins them with one `\n`.

| Input | Payload SHA-256 | Check |
|---|---|---|
| Current `docs/reference/architecture/docs-path-authority.json` (`6d3865f8…8167`) | `3bd20e2608ec62cd5c2da1fc60bc4419b6aefa594e0efc45f32b35df17ad8dfb` | equals `payloadSha256` in the currently bound decision `mvp6-capacity-guard-single-disposition-owner-decision-01.json` → algorithm calibrated |
| `docs-path-authority.candidate.json.txt` (`36013649effa878319db150dca5a942bfac592fe012fc4c71526f0a3010ca825`) | **`a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0`** | candidate payload |

The exact payload bytes (21 403 bytes, UTF-8, no trailing newline) are stored as `authority-payload.txt`, so anyone can check with:

```sh
sha256sum docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/authority-payload.txt
# a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0
```

The same value is written in `owner-decision.candidate.json.txt` (SHA-256 `825f0853d0f1ef50c6ba5bed732ee964cece6585327eff2c6277d13207b84e5e`) as `payloadSha256`.

## Hashes the payload depends on

- YAML target `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`, carrier target `87557ef9…1fee` (unchanged).
- 35 existing seals (unchanged bytes), plus the 8 new seal hashes in ANALYSIS.md §3.
- Provenance file `PROVENANCE.md.txt` → final path `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md`, SHA-256 `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a`. It must be copied byte-for-byte; any change invalidates the payload.
