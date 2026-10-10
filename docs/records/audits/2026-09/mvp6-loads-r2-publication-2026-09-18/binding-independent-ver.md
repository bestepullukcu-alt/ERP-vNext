# Independent binding candidate VER — PASS (E2)

Real repository strict read-only; all outputs in this disposable directory. Read AGENTS, read-only agent/workflow, docs-organization and actual guard. Candidate report/manifest reviewed independently.

All candidate artifact manifest hashes match. Binding diff independently applied to current original manifest reproduces exact candidate manifest and UNAPPROVED decision bytes. Proposed payload SHA256 is 9bc2cb3737d0e8d3330fdc9945d3d268b64ab582ef49dee11cee524811d3846a. SealedInputs array unchanged: 17 source hashes and provenance hashes/line links pass. Carrier annex target remains unchanged. Original real decision is not repurposed.

Fresh independent disposable snapshot: 14,436 input files copied byte-identically from candidate snapshot, excluding bin/obj/.git. Input map snapshot-input-hashes.json. Original guard SHA256 f6b6f3c6427e254e1c1ebf6834225c18041e1bdec6ac3607008238fee6e14b1b; source matches actual repository guard. No guard/harness fixes. New restore/rebuild succeeded. Initial sandbox VSTest socket failure retained in test.log; allowed local-socket rerun succeeded, 47 PASS / 0 FAIL / 0 skipped. 35 original fixtures plus 12 candidate tests.

Evidence review: fixture constructor reads actual canonical/sealed/provenance files before assertions (guard lines 277–292); VerifyRoot scans actual files and preserves CS/PS1/presence controls. Expected-error assertions therefore cannot pass due to failed fixture setup. Synthetic positive uses existing fixture Approve, synthetic decisions fail production mode. Case-result messages prove rejection at intended old-pin, unapproved status, decision status, old-payload, synthetic-kind and unused-target gates. Original negatives reject null/missing hash, unknown source, changed history, missing proof and empty scan. No vacuous PASS observed.

Loads annex is publication artifact, not fabricated guard exemption: adding it without genuine sealed-tool use fails unused-target control. R2 publication compatibility/runtime claims are outside this binding VER; parent separately verifies approved R2 patch. No activation or real decision produced here.

No-change: 14,436 real repository files before/after identical, no additions/deletions; branch/HEAD/status/staged same (no-change.json). Build/test outputs only disposable copy; no operational DB/runtime writes.

Verdict: binding candidate independent PASS, sufficient to proceed with explicitly authorized real decision/activation stage. This does not claim activated normal guard PASS, full architecture PASS, pack promotion, runtime GO, or CT acceptance. Those remain parent publication checks.

Command: BINDING_EVIDENCE=<this-directory> dotnet test <this-directory>/snapshot/tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --filter 'FullyQualifiedName~BindingCandidateTests|FullyQualifiedName~DocsPathGuardTests.AuthorityFixture' --logger 'trx;LogFileName=independent47-final.trx' --results-directory <this-directory>/results -p:NuGetAudit=false -p:UseSharedCompilation=false --no-restore
