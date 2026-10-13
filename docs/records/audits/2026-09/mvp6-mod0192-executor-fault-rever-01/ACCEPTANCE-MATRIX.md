# MOD-0192 independent acceptance matrix — executor fault re-verification

This successor changes only the earlier X01/X06/X07 evidence disposition. All unlisted evidence is inherited only when its 43-path hash remains bound; it is not freshly rerun here.

| Criterion | Verdict | Controlling evidence / limit |
|---|---|---|
| A192-01 | PASS | Inherited content-bound core evidence; 43/43 current hashes match. |
| A192-02 | PASS | Inherited content-bound core evidence. |
| A192-03 | PASS | Inherited content-bound core evidence. |
| A192-04 | PASS | Inherited content-bound core evidence. |
| A192-05 | PASS | Inherited content-bound core evidence. |
| A192-06 | PASS | Inherited content-bound core evidence. |
| A192-07 | PASS | Inherited content-bound core evidence. |
| A192-08 | PASS | Inherited content-bound core evidence. |
| A192-09 | PASS | Inherited content-bound core evidence. |
| A192-10 | PARTIAL | Lease/restart repository evidence retained; X07 audit/read-failure reconciliation remains open and no hosted executor composition is claimed. |
| A192-11 | PASS | Inherited literal fixture-oracle evidence only; no generalized optimizer claim. |
| A192-12 | PASS | Inherited content-bound core evidence. |
| A192-13 | PASS | Inherited content-bound core evidence. |
| A192-14 | PASS | Inherited content-bound core evidence. |
| A192-15 | PASS | Inherited content-bound core evidence. |
| A192-16 | PASS | Inherited content-bound core evidence. |
| A192-17 | PARTIAL | Direct repository isolation/fault checks pass; no composed JWT/HTTP surface. |
| A192-18 | PARTIAL | Fresh build and 16/16 CapacityPlans pass. Full suite remains historical 154/155 non-PASS and was not rerun. |
| X01 | PARTIAL | Five-collection rollback, committed ack-loss, same-key replay and scoped counts independently pass. Known precommit code is wrong: `COMMIT_RESULT_UNRESOLVED`, contract requires `DEPENDENCY_UNAVAILABLE`. |
| X02 | PASS | Inherited content-bound evidence. |
| X03 | PARTIAL | Inherited two-process test evidence; no hosted executor. |
| X04 | PARTIAL | Inherited repository evidence only. |
| X05 | PASS | Inherited content-bound evidence. |
| X06 | PASS | Independent failpoint proves all four terminal effects roll back, then same fence writes one audit and one Pending event. |
| X07 | PARTIAL | No-commit and committed ack-loss outcomes pass; reconciliation does not read scoped audit and read-failure behavior remains open. |
| X08 | PARTIAL | Inherited repository evidence only. |
| X09 | PARTIAL | Inherited repository evidence only. |
| X10 | PASS | Inherited content-bound evidence. |
