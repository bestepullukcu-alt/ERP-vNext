# Compatibility and final-version gate

The new `LoadSummary.lifecycleCorrelationId` property is optional and nullable. That is schema-additive for tolerant JSON readers. It is not proof that every consumer is compatible:

- strict readers that reject unknown response properties can fail on the unchanged Loads list route;
- wire `contractVersion` remains `v1`, so the response offers no negotiated old/new representation;
- candidate metadata does not provide migration, route negotiation, cutover or rollback;
- static fixtures do not prove upgraded producer always-emission or UI fail-closed behavior.

The focused repository scan identifies the current Loads producer/controller and governance references, but no exact live frontend/SDK Loads response parser. This result cannot be extended to repo-external consumers.

After F-01 is corrected, the release owner must choose the final version against the corrected exact bytes. Actual consumer inventory and consent must name the final YAML and annex hashes. Publication and runtime uptake remain later gates.
