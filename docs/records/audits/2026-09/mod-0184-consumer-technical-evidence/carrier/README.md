# MOD-0184 test/mock/SDK technical consumer evidence

2026-09-16. Agent verdict: **PARTIAL PASS with fixture-policy finding**. No publication or owner consent.

Independent audit-only Python consumer harness checks the exact candidate using expectations transcribed
from owner decision §§3,5,6 rather than importing publication validate.py/error-matrix.json.
It JSON-decodes responses, validates candidate response schemas, applies independently enumerated status
allowlists, checks unwrapped response shapes, correlation headers/error-body equality, sanitized bounded
error shape, exact error messages and replay response status/current-header/original-audit fixture values.

## Results

- All **27 operation/status response definitions** enumerated; exact allowlists match.
- All **44 exchange fixtures PASS** consumer decoding/policy checks.
- Seven deliberately corrupted fixtures rejected: absent correlation, success-body correlation leakage,
  wrong status replay status, unknown202, outer envelope, error body/header mismatch, absent Bearer challenge.
- Inline response corpus: **26/27 operation/status groups pass bounded policy**. Create422 has four
  inherited examples with error.details and non-Carrier lifecycle codes/messages. These remain schema-valid
  under unchanged Error, but violate the annex bounded Carrier decision not to produce details.

### Finding CARRIER-FIXTURE-01

Candidate `#/components/responses/CarrierCreateUnprocessable/content/application~1json/examples`
retains `shipmentTransition`, `loadTransition`, `returnTransition`, `claimTransition`. All contain details;
annex §3 excludes details from this bounded Carrier slice. The publication README explicitly says the
inherited examples remain and no new create business rule is invented. Thus this is an **example/mock
selection ambiguity**, not evidence the Error schema is broken or actual Carrier runtime is incompatible.
A mock generator selecting any of those examples would emit a response our bounded consumer policy rejects.
The owner should specify whether these are legacy schema illustrations excluded from Carrier mock selection,
or publish a reviewed Carrier-specific example correction. Do not invent a new create422 business rule.
No candidate or canonical correction was made by this lane.

## Actual consumer inventory and integration implications

`rg -n -i 'carriers|createCarrier|changeCarrierStatus|queryCarriers' services frontend gateway
--glob '!**/bin/**' --glob '!**/obj/**'` returned exit1/no matches. No executable Carrier service client,
mock router or generated SDK was found in these runtime source locations. This audit parser is a **synthetic
consumer adapter**, not certification of a real/generated SDK or external consumer. External inventory remains
publisher-owned. Draft module code absence is not owner consent.

Root inspection additionally reports current ShipmentContextMiddleware catches the whole bundle, rejects
nil correlation before auth, and trims keys; Program.cs invalid-model mapping treats Guid.Empty as missing.
These are future Carrier composition hazards requiring the approved path isolation. No Carrier runtime exists
to certify; changing accepted Shipment behavior is neither necessary nor authorized by this evidence lane.

## Limits and reproduction

Fixtures contain declared outputs, not actual HTTP captures. All response headers for inline examples are
constructed from the normative expected policy, not measured server headers. Six precedence fixtures can
be decoded, but auth order/zero DB access was not executed. The two RP02 fixtures assert current header vs
original audit root, but persisted audit immutability is not measured. RP01/RP03–RP16 concurrency, restart,
commit uncertainty, atomicity and recovery remain pending actual implementation tests. No E4/runtime claim,
consumer uptake, version publication, pack promotion, approval, git mutation or production edit occurred.

Run from any directory:

```sh
/private/tmp/mod0184-oas-tools/bin/python /Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/carrier/check_consumer.py
```

Dependencies are the existing temporary PyYAML/jsonschema venv. `results.json` records the candidate,
annex, patch, fixtures, canonical1.0.0 and owner-decision SHA-256 values plus every result. Exit0 means
harness execution completed; inspect `verdict`/`inline_findings` for the policy finding. No silent PASS.
Owned files: this README, check_consumer.py and results.json only.
