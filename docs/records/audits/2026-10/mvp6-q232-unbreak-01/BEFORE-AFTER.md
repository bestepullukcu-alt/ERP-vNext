# Q232 — Before / after: `Program.cs`

**The "after" is a PROPOSAL. It was not written to the repo.** The Edit tool was refused on
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`
("File is in a directory that is denied by your permission settings"). The repo file still has the "before" hash.

| | sha256 | bytes | byte copy |
|---|---|---:|---|
| Before — in the repo now (start and end of this WP) | `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8` | 4,352 | `before/Program.cs.txt` |
| After — proposed, built and run in a scratch copy | `b417e7694fd8bde6bb19e95ebd047f661ecf48235b697a8b5149ab3b46ae7897` | 4,770 | `proposed/Program.cs.txt` |

## The whole difference (`diff before after`)

```text
7a8,11
> using Diten.SupplyChainService.Persistence.Features.Returns;
> using Diten.SupplyChainService.Persistence.Features.Claims;
> using Diten.SupplyChainService.Persistence.Features.SandopPlans;
> using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
47a52,55
> builder.Services.AddReturnPersistence();
> builder.Services.AddClaimPersistence();
> builder.Services.AddSandopPersistence();
> builder.Services.AddCapacityPersistence();
```

Eight added lines: four calls, placed directly after `builder.Services.AddLoadPersistence();` (line 47 before),
and the four `using` directives they need, placed after the Carriers persistence `using` (line 7 before).
Nothing removed, nothing else changed. `builder.Build()` moves from line 64 to line 72.

## Namespaces, verified by reading each file

Paths under `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/`.

| Call | Declared at | Namespace | Registers |
|---|---|---|---|
| `AddReturnPersistence()` | `Returns/ReturnPersistenceRegistration.cs:7` | `…Persistence.Features.Returns` (line 4) | `ReturnRequestContext`, `IReturnRepository`, `IReturnCommitProbe`, `ReturnOutboxStore`, hosted `ReturnSchema` |
| `AddClaimPersistence()` | `Claims/ClaimPersistenceRegistration.cs:9` | `…Persistence.Features.Claims` (line 5) | `ClaimRequestContext`, `IClaimRepository`, `IClaimCommitProbe`, `ClaimOutboxStore`, hosted `ClaimSchema` |
| `AddSandopPersistence()` | `SandopPlans/SandopPersistenceRegistration.cs:4` | `…Persistence.Features.SandopPlans` (line 2) | `ISandopRepository` only |
| `AddCapacityPersistence()` | `CapacityPlans/CapacityPersistenceRegistration.cs:8` | `…Persistence.Features.CapacityPlans` (line 4) | `CapacityRequestContext`, `ICapacityRepository`, `ICapacityLeaseStore`, hosted `CapacitySchema` |

## Not changed (task 3)

No gateway route, permission key, middleware branch, hosted service, config key or Shipment-middleware exclusion.
`Program.cs:66-68` (the three `UseWhen` branches) are the same bytes before and after.
