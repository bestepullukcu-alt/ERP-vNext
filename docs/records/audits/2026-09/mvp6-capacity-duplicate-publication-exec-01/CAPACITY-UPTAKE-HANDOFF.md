# Capacity uptake handoff

Publication gate: **GREEN**.

The exact canonical targets now present in the real checkout are:

- `docs/analysis/contracts/sandop-capacity.openapi.yaml` — `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`
- `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` — `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`
- preserved v2 annex — `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`

The publication patch SHA-256 is `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23`. Production-mode DocsPathGuard passed 39/39 after publication. The separately authorized Capacity uptake writer may now apply `PROPOSED-UPTAKE.patch` SHA-256 `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f` only after verifying its own exact source preimages. That uptake must produce a successor source manifest and must not mutate the integration baseline.
