# Conditional production-uptake authority request

I approve applying `PROPOSED-UPTAKE.patch` SHA-256
`f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f`
against the exact MOD-0192 43-source manifest SHA-256
`36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8`,
only after the SANDOP-CAPACITY 3.0.0 / wire v1 successor has been published with YAML
SHA-256 `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`
and annex SHA-256 `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`.

The authorized production change is limited to the exact successor error message in
`CapacityContractError.cs`. The two Capacity-owned test changes may be applied with it.
The repository's existing deterministic duplicate, retry-and-reread race convergence,
receipt, lifecycle, fixture, transaction, and unknown-commit behavior must remain unchanged.

This authorization does not cover canonical or guard changes, `Program.cs`, MOD-0190,
duplicate-key policy expansion, executor/X01/X07 rework, gateway, live producers,
publisher, rollout, E5/G5, commit, push, or stash. Applying the patch and independent
verification remain separate work after publication.
