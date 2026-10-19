# SANDOP-CAPACITY 3.0.0 — the field-level half, and one finding CT retracted

Successor to `mvp6-sandop-capacity-conformance-01`, which left the required/null field diff and the three
MOD-0190 event payloads **UNDETERMINED**. `-01` is sealed and unedited (**K4**); the rows this lane settles
are listed in `SUPERSEDES.tsv`.

- Measured: **2026-10-16**, branch `feature/sce/mod-0190-0192-bounded-slice`, tree clean at start.
- Contract `sandop-capacity.openapi.yaml` sha256 `5213b535…`, 3.0.0 / `x-contract-version: v1`;
  annex `sandop-capacity-semantics-v3.0.0.md` sha256 `f9d9d555…`. 33 schemas extracted.
- Method: static extraction of both sides, then a mechanical diff. Scripts in `evidence/`.
  Plus **one executed guard** (see §4), which is the only part of this lane that ran code.
- **No pack edited, no acceptance box ticked.**

## 1. Response and event schemas — 11 of 11 exact

| schema | fields | verdict |
|---|---:|---|
| SandopPlan | 10/10 | MET |
| SandopSnapshot | 6/6 | MET |
| SignOff | 9/9 | MET |
| SandopPlanEventPayload | 3/3 | MET |
| SandopSnapshotEventPayload | 3/3 | MET |
| SandopSignOffEventPayload | 5/5 | MET |
| LifecycleEventEnvelope | 7/7 | MET |
| CapacityPlan | 8/8 | MET |
| CapacityScenario | 8/8 | MET |
| CapacityEvaluation | 9/9 | MET |
| InputSnapshotProvenance | 6/6 | MET |

**No missing field and no extra field anywhere.** The Sandop side is anonymous objects in
`SandopProjection.cs:6-9` and `SandopRepository.cs:53,66,75,79`; the Capacity side is records in
`CapacityPlanModels.cs:28-31`.

**MOD-0190's "three event payloads" are closed by this**, which `-01` could not settle:

| event | payload in code | contract required |
|---|---|---|
| `sandop.plan.created.v1` | `sandopPlanId, demandPlanId, demandPlanVersion` | the same three |
| `sandop.snapshot.captured.v1` | `sandopPlanId, snapshotId, provenance` | the same three |
| `sandop.sign-off.recorded.v1` | `sandopPlanId, snapshotId, signOffId, role, decision` | the same five |

One observation, not a violation: `SandopRepository.cs:79` sets `causationId=(Guid?)null`
unconditionally. The contract types `causationId` as `[string, null]` and does not require it, so always-null
conforms — but Sandop events therefore never carry causation, while the annex (line 66) expects Capacity's
terminal event to carry "scenario-created causation". Recorded for the owner; nothing is claimed broken.

## 2. Request schemas — all six enforce `additionalProperties: false`

The contract sets it on all six request schemas (`evidence/additional-properties.txt`). Both surfaces enforce
it, by different means:

| operation | required in contract | enforced by |
|---|---|---|
| `createSandopPlan` | 5 | `SandopWire.Validate` allow-list == required set, so any unknown member → 400 `INVALID_REQUEST` |
| `captureSandopSnapshot` | 4 (+1 optional `supplyInputRefs`) | allow-list of 5, required of 4 — exact match |
| `recordSandopSignOff` | 3 (+1 optional `comment`) | allow-list of 4, required of 3 — exact match |
| `createCapacityPlan` | 7 | `[JsonUnmappedMemberHandling(Disallow)]` on the record, **and** `Program.cs:120` service-wide |
| `createCapacityScenario` | 3 | the same, both places |
| `evaluateCapacityScenario` | 2 | the same, both places |

Sandop's validator additionally enforces what the schemas say beyond presence: `yyyy-MM-dd` horizons with
`end >= start`, an RFC-3339 `sourceCapturedAt`, the five-value `role` enum, the two-value `decision` enum, a
2000-character `comment` bound, and element-shape checks on `supplyInputRefs`.

## 3. A finding CT raised and then retracted

While reading `CapacityPlanModels.cs` CT recorded an asymmetry — that `CreateCapacityPlanRequest` lacked the
`JsonUnmappedMemberHandling` attribute its two siblings carried, so an unknown member on `createCapacityPlan`
would be silently dropped against a contract that declares `additionalProperties: false`.

**That was wrong, and the cause was CT's own command.** The attribute sits on the line *immediately above*
the record, and the `sed -n '5,27p'` window CT used to read the file started one line too late and cut it off.
All three Capacity request records carry it, and `Program.cs:120` sets
`UnmappedMemberHandling = Disallow` service-wide as well, so the behaviour is enforced twice over.

It is recorded rather than quietly dropped because the near miss is the useful part: CT was one step away from
"fixing" a file that was already correct, in a module where editing a reserved file without authority has
already cost this programme a reverted change (pack `:199`).

## 4. The guard that came out of it — `CapacityRequestShapeTests`, 6/6

The retraction left something worth keeping: nothing in the repository held those three records to the
contract's `additionalProperties: false`. There is now a guard, and it is built to the repository's own rule
that a rule is measured against production code, not a copy of itself:

- it does **not** set `UnmappedMemberHandling` in its own options, so the three refusals can only come from the
  attributes on the production records;
- its options otherwise match the service (web defaults, case-sensitive, `Program.cs:117-119`);
- **sabotage half, with no production edit:** `CapacityProvenance`, a record in the same file *without* the
  attribute, **skips** an unknown member under the identical options. So the refusals are attribute-driven —
  remove an attribute and its test goes red.
- negative halves: the declared members must still bind, and a missing required member must still be refused.

Measured: **6 passed, 0 failed, 21 ms.** Full suite after adding it: **529 passed, 1 failed of 530**, 3 m 10 s,
the one failure being `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, which throws
unless `CLAIMS_RESTART_MODE` is set — the same by-design self-exclusion as before, from `a3c335256`.

## 5. Two method errors CT made in this lane and corrected

Both are in `evidence/` so the corrections are checkable, and both produced a false NOT MET before being fixed:

1. **Shorthand anonymous-object members were not counted.** `payload=new{sandopPlanId=aggregateId,snapshotId,provenance}`
   uses C# shorthand for two of its three members; the first extractor only matched `name=` forms and so
   reported `SandopSnapshotEventPayload` as 1/3 and `SandopSignOffEventPayload` as 1/5. Fixed by splitting the
   balanced brace body on top-level commas and accepting bare identifiers.
2. **A leftover filter in CT's own script** excluded `payload` from the envelope comparison, reporting
   `LifecycleEventEnvelope` as 6/7 missing `payload` when `SandopRepository.cs:79` plainly carries it.

Neither was a defect in the product. Both were caught by checking a surprising result against the source
rather than reporting it.

## 6. One stale pin inside MOD-0192's pack, for the owner

`MOD-0192-capacity-planning.md:253` still pins the canonical contract at
`9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and the annex at
`sandop-capacity-semantics-v2.0.0.md`, while the published contract is 3.0.0 (`5213b535…`) with the v3.0.0
annex — the version the pack's own `:201` line says was corrected on 2026-10-11. The §22 hash line was not
updated with it.

The same line gives the authoritative precedence, which is worth quoting because `-01` had to infer it:
`401 → 403 → 400 → 404 → receipt → 409/422 → commit`. Static reading confirms the first three in both
middlewares; the rest is behavioural and stays REQUIRES RUNTIME.

**A pack edit is the owner's.** Recorded, not acted on.

## 7. What the five open lines need after this lane

| line | state |
|---|---|
| MOD-0190 `:201` | fields **MET**, three event payloads **MET**, operations/codes/`contractVersion` **MET** from `-01`. Remaining: 409/422/503 precedence at runtime. |
| MOD-0192 `:201` | fields **MET**, request shapes **MET** and now guarded. Remaining: the replay matrix (behavioural) and the strict-reading header observation, both blocked behind `:199`. |
| MOD-0192 `:199` | **BLOCKED, unchanged** — integration-owner authorization for two `Program.cs` lines plus a re-pin of both packs' composition hashes. |
| MOD-0192 `:200` | **BLOCKED, unchanged**, behind `:199`. |
| MOD-0192 `:203` | needs the G5 live run. `g5_live_conform.py`, which `:201` names, does not exist in the repository (recorded in `-01`). |

Every remaining item is either runtime-only or waiting on an authorization CT does not hold. **No box was
ticked and no pack was touched.**
