# UNAPPROVED owner decision text

I approve the exact CRM port migration candidate with patch SHA-256
`70dfa5b55b7cd255a109b8f1c8fe8b5437888ed4b0cdef6a0ce6f3d4109a0a43`
and manifest SHA-256
`826a95cbb2af672395935ce2c95dd82255bd297089b1bbc0effe80c5c8a7ca58`.

This decision preserves `Diten.SupplyChainService` on its approved permanent
port `5061`, extends the local microservice band through `5065`, and assigns
`5065` to `Diten.CrmService`. It authorizes one integration owner to apply the
exact 35-file port-only migration after every preimage hash matches. It does
not authorize overwrite, conflict resolution by guess, a different port, or
any CRM/SupplyChain business change.

After that migration is applied and verified, I separately approve the
regenerated 12-path Shipment shared successor patch SHA-256
`ef895ba69fc7eac4c251f4c2b672fb7f325a0ed226f5dbcf88a40a086a69982a`
with manifest SHA-256
`bfa84f57d836d2539db221c2ca1f4989cf554023686a473a779d6eee1576aa36`.
Its gateway preimage is
`7ab90b19ac915aadee5a9c16dce96bdf04eec2bf4d1be46720b82418cb2466a6`
and its gateway target is
`77363833c9ee973db6af6e798141afe9843515a324b4b00b37d86a80709d6618`.
The earlier Shipment shared patch hash `6d9cad8e...` is not authorized for the
new preimage.

The integration owner must run config/JSON checks, CRM and SupplyChain health
on their distinct permanent ports, focused gateway route tests, and composed
runtime verification before UI DEV dispatch. This approval does not grant UI
DEV, runtime rollout, gateway deployment, commit, push, stash, E5/G5, or
full-module acceptance.
