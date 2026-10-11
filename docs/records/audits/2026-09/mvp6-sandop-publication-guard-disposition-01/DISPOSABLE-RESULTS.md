# Disposable reader/scanner result

The prior real production TRX is copied byte-for-byte as `PRODUCTION-GUARD.trx` (SHA-256 `6220199ae614ea0df4cb3de11d84b35fa5180e138951ae622c639fd0b5aabbe8`): 38 PASS / 1 FAIL, with **36** `docs/analysis` hits in **12** files. `OFFENDERS.tsv` extracts every path:line. Current bytes and lines were re-read; all 36 still contain the cited path, and each file's current SHA-256 is pinned in that TSV.

In `/private/tmp/mvp6-sandop-guard-vte2i65y`, the proposed `historical-tool-policy-proposed.patch` passed `git apply --check` and applied to a copy of the **actual** `DocsPathGuardTests.cs` and docs rule. A second disposable copy of current authority accepted `authority-unapproved.patch` with `git apply --check`/`git apply` exit 0 and produced exact `authority-candidate.json.txt` bytes. The fixture then used the candidate payload and copied all 34 sealed inputs, their proof files, and existing canonical targets. **Only in that fixture**, a `SYNTHETIC_TEST_ONLY` decision re-bound payload SHA-256 `c74f046033584e59209c074cd76b29834a0fae8f8f3fb58f9728e410af28d5f5`. It was not written to the repository and is not owner approval.

`dotnet test /private/tmp/mvp6-sandop-guard-vte2i65y/test-project/GuardProbe.csproj --no-restore --filter FullyQualifiedName~CandidateProbeTests` used .NET SDK 8.0.417, xUnit 2.5.3, VSTest 17.11.1. The first sandbox attempt aborted because VSTest could not bind its localhost communication socket (`SocketException 13`); the permitted isolated retry exited **0** with **6/6 PASS**. Exact test code is `DISPOSABLE-PROBE.cs.txt`; raw TRX is `DISPOSABLE-CANDIDATE.trx` SHA-256 `ffd8ed55ce592ade0c3d992f35550a11fa38b4fe5c263662e8084cdd175132c1`. NuGet vulnerability metadata lookup warned `NU1900` due unavailable network; packages restored from local cache and tests ran.

| Probe | Observable result |
|---|---|
| Candidate reader + scanner | PASS: all exact seals consumed, no offender in minimal fixture. |
| Changed sealed JSON byte | Rejected at pinned SHA comparison (`Assert.Equal`). |
| Unregistered Python neighbor pointing to `docs/analysis` | Rejected by actual scanner with offender message. |
| `.py` misclassified as `historical-data` | Rejected by extension check (`Assert.EndsWith`). |
| Historical tool carrying canonical target | Rejected by empty-target check (`Assert.Empty`). |
| Wrong provenance line after synthetic re-binding | Rejected by exact proof-line regex (`Assert.Matches`). |

This is **disposable synthetic fixture evidence**, not a production `ReadAuthority` PASS. The actual repository authority still has its earlier approved payload/decision; its production scan remains the previously measured 38/1 FAIL until a real owner-bound, exact approved policy/authority update is separately applied and re-tested. No canonical SANDOP target is added by this candidate.
