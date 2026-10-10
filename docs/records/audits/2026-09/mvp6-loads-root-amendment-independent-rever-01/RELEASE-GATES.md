# Release gates remaining after technical re-VER

1. Release owner selects the final version against final bytes. `3.1.0-rc.1` remains candidate metadata.
2. Consumer inventory and exact-hash consent must cover the final YAML and annex. Optionality does not prove compatibility for strict readers on the unchanged route/wire `v1`.
3. Separate authority is required to publish canonical YAML/annex and to activate any required guard binding.
4. Loads producer uptake, persistence provenance, runtime fail-closed behavior and independent HTTP/JWT/Mongo verification remain unproven.
5. Pack promotion, frontend/integration uptake, rollout and E5/G5 remain outside this result.

This PASS applies only to the unpublished candidate-02 artifact, corrected annex and applicable patch.

