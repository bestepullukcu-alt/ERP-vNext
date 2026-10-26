# `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs` — remove one entry

Delete these two lines from `NotInThisJob` (around line 70):

```csharp
        ["services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj"] =
            "main (SCE / MVP-6): needs MVP6_MOD0192_MONGO_URI (its own replica set, CapacityTestMongo); SCE's file",
```

**It must leave in the same change as the matrix row.** The guard asserts
`Assert.Empty(rows.Intersect(NotInThisJob.Keys))` — the two lists may not overlap — and
`Every_service_test_project_is_a_matrix_row_or_named_with_its_reason` is red if the name is removed
without a row. Its own doc says it: *"Removing a name without adding its row is red; a row gained
removes the name."*

The reason the entry gave is now satisfied: the row provides `MVP6_MOD0192_MONGO_URI` as its own
replica set, and *"wiring it is its owner's change (XMC-001)"* — the owner being SCE / MVP-6, which is
whose change this is.

The adjacent comment block above `NotInThisJob` names Platform's CompositionGuard as the precedent for
leaving the list (`WP-PLATFORM-COMPOSITION-GUARD-OWNED-01`, row `platform-composition-guard`); add
SupplyChain to that sentence or leave it, as its owner prefers.
