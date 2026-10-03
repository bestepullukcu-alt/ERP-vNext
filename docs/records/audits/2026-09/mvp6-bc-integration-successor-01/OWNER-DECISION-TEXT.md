# Narrow B-target C-uptake decision

I approve applying `PROPOSED-UPTAKE.patch` SHA-256
`f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f`
to the registered B integration source set only when its exact 422-row
preimage manifest is SHA-256
`cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c`.

The three file preimages and targets must match `BC-DELTA.tsv`, and the
resulting 422-row successor manifest must be SHA-256
`dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634`.
The operation must preserve `Program.cs`
`50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0`,
the B hosted/read-fault `CapacityAtomicityTests.cs`
`2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43`,
and the accepted S&OP atomicity test
`eba2da6f47a4935b61857a95534b3d1697635910190e3838494d96d956af7f43`.

One integration owner may apply only those three replacements. Afterward,
the bounded duplicate-name HTTP message, replay/error precedence, deterministic
duplicate and unique-index race behavior, preserved X01/X07/hosted tests, and
affected integration/restart behavior must be verified, followed by an
independent VER.

This decision does not authorize any `Program.cs`, contract, guard, gateway,
new business rule, rollout, E5/G5, commit, push or stash change. This document
is a decision draft until the owner explicitly approves these exact hashes.

