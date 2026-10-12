# Test-set overlap

- Targeted R01 GREEN: 10 executed cases.
- Returns namespace regression: 78 executed cases, including those same 10 targeted cases. Therefore 10 + 78 is not 88 unique tests.
- Main runtime probe: 52 separately executed HTTP/DB assertions. It overlaps behaviorally with unit/regression coverage but exercises the built Kestrel process, real HTTP serialization, JWT/RBAC middleware, and Mongo persistence.
- Restart probe: four assertions in a new API process against retained DB state. It is a separate phase from the 52 main assertions.

Counts describe executed evidence sets. Acceptance remains row-based in `ACCEPTANCE-MATRIX.md`.
