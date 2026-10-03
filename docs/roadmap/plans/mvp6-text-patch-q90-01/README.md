# Q90 — draft text patches R1…R3 (NOT applied)

Prepared 2026-09-26 19:45–19:49 +03:00 by the Q90 chat lane on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. **Draft only — no pack, DCP, contract or record was edited.** Sign-off: `SIGN-OFF.md` (Q92); apply: Q93; code revision: Q88c.

| Patch | Target | Preimage | Patch SHA-256 | Postimage (alone) | +/− | `git apply --check` on /tmp copy |
|---|---|---|---|---|---|---|
| R1 | `MOD-0192-capacity-planning.md` | `f6b4d0f3…` | `13be62ce0bef2c717de8e1b2d8c3ab1ec342545147df181f743f718397e142d6` | `ca52818ee1d2ddbf70582eb0026a4f0d5def656e0c72cc12b71566688f029280` | +11/−2 | exit 0; reverse check OK |
| R2 | `MOD-0190-sop-workflow-signoffs.md` | `003aba70…` | `5c65d9ed71e7783e79eb705a475a7bcdeb3a269fd330ccd70ecf6c0a9ed06859` | `ae1be96954a84710d1adec9daa8e08ad76e0a40a799cd177804f1bde3dd804aa` | +1/−1 | exit 0; reverse check OK |
| R3 | `MOD-0192-capacity-planning.md` | `f6b4d0f3…` | `5fe167125148e15545f3336f26eacf3548ef27beef180c6de63c7348ceadfb88` | `6c927cd7352341da59b7d2c4e99d898e0e0fbbcf617653939eaee32da9f5498b` | +1/−1 | exit 0; reverse check OK |

MOD-0192 after R1 + R3 (either order): `a1df34c15abff77b291b4a4ca3c79e8e3f775e6c671c76bedbd24b291b55593b`. `verify_module_id.py`: MOD-0190, MOD-0192 exit 0.

## Findings

- **F-Q90-1 — §23.13 still lists "DCP-009 §21.1 exclusion".** The last bullet of §23.13 in MOD-0190 and MOD-0192 names "DCP-009 §21.1 exclusion" among the remaining gaps; since Q86 it is closed. Not patched here (outside R2/R3 as dispatched, which cover §24); proposed for the next text revision.
- **F-Q90-2 — Q88 draft code needs Q88c.** The Q88 overlay keeps IDs in memory only; after Q93 applies R1, the Capacity details JS/view/resx/tests must write and read `scenarioId`/`evaluationId` (queue row Q88c, HELD).

## ASSUMPTIONs

- **A1 — query form.** The Details route style keeps `{capacityPlanId:guid}` as the only path segment, and §24 binds the manifest RoutePath `/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}`. R1 therefore uses an optional query (`?scenarioId=&evaluationId=`) so the route, the manifest and tests M-04/W-01 stay unchanged.
- **A2 — `history.replaceState`.** The address is updated in place (no extra history entries); this is page behaviour, not storage.
- **A3 — effort.** R1 adds a small frontend and test increment (read/validate/write two query values, one localized message, three acceptance rows): O/M/P **+2/+4/+6 h**, absorbed within the existing frontend 32/52/88; the §23.14 numbers are unchanged.
- **A4 — Q93 waits for Q87.** Q93 depends on Q92 and Q87 (the R1/R3 preimage is the Q86 postimage under VER).
- **A5 — times.** The F-Q79-05 owner decision is recorded as "~19:50" per the dispatch; the record file was written at 19:47.
