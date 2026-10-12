# Q220 v2 — Before / after

**The "after" column is a PROPOSAL. It was not written to the repo.** The session's permission settings deny `Edit` and
`Write` under `services/**`; see `SOP-22.md` §1. The repo files still have the "before" hashes.

Byte copies: `before/*.txt` and `proposed/*.txt` in this folder. The proposed bytes were built and tested in an isolated
scratch copy (`BUILD-AND-TEST.tsv`).

## 1. `…/Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs`

| | sha256 | bytes |
|---|---|---:|
| Before — in the repo now | `0e74fec0687634d94c61e7c2965f495545b716f446f42fc6c45f1e1bf590961a` | 417 |
| After — proposed | `18d180638bd9b4b1cd9cf612f7f1048f7b0eedbb0b0416ca4e206197e11ac12d` | 459 |

Before, line 5:

```csharp
{ services.AddScoped<ISandopRepository,SandopRepository>();return services; } }
```

After, line 5 — one call added, nothing else:

```csharp
{ services.AddScoped<ISandopRepository,SandopRepository>();services.AddHostedService<SandopSchema>();return services; } }
```

Lines 1–4 are unchanged.

## 2. `…/Persistence/Features/SandopPlans/SandopSchema.cs`

| | sha256 | bytes |
|---|---|---:|
| Before — in the repo now | `49f87409f29534c835a2aed82e349a22ba383bf5ef9ae4c4a3c17cbc0ae90bad` | 1,668 |
| After — proposed | `9fd754a4eda154c2e0497f9e75802904090ac2cb00e5f2c393570e9d95911450` | 1,987 |

Before, lines 1–5:

```csharp
using MongoDB.Bson;using MongoDB.Driver;
namespace Diten.SupplyChainService.Persistence.Features.SandopPlans;
public static class SandopSchema
{ public static async Task EnsureAsync(IMongoDatabase db,CancellationToken ct=default)
{
```

After, lines 1–8:

```csharp
using MongoDB.Bson;using MongoDB.Driver;using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.SandopPlans;
// Hosted so that service start-up creates the indexes; the static entry point stays for direct callers.
public sealed class SandopSchema(IMongoDatabase database):IHostedService
{ public Task StartAsync(CancellationToken ct)=>EnsureAsync(database,ct);
 public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
 public static async Task EnsureAsync(IMongoDatabase db,CancellationToken ct=default)
{
```

What changes:

- `static class` → `sealed class … : IHostedService` with a primary constructor taking `IMongoDatabase`, as
  `CapacitySchema.cs:5` and `ReturnSchema.cs:5` do.
- `StartAsync` calls the existing static method. `StopAsync` returns a completed task, as the peers do.
- The static `EnsureAsync(IMongoDatabase, CancellationToken)` keeps its exact signature (CT decision 2), so
  `SandopContractTests.cs:6` compiles untouched.

What does not change — **the index definitions, byte for byte:**

| Lines | sha256 of those seven lines |
|---|---|
| Before: lines 6–12 (the `Ix` helper and the six `CreateOneAsync` calls) | `d0978366f16800c04fab7bb0fd188485ca789133dd6962b1ac143ed45490f66d` |
| After: lines 9–15 | `d0978366f16800c04fab7bb0fd188485ca789133dd6962b1ac143ed45490f66d` |

The closing line `} }` is also unchanged.

No `.csproj` change is needed: `Microsoft.Extensions.Hosting` types already resolve in the Persistence project (the
five peers use `IHostedService` there), and the scratch build confirms it.

## 3. New test — `…/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopRegistrationTests.cs` (proposed, not created)

sha256 `e3a4292c3d4da4e2b1f02dd3c238b6d893b32d67838753cf45987e4b39e89a83`, 2,209 bytes. Full text:
`proposed/SandopRegistrationTests.cs.txt`.

One test, `Registration_path_creates_the_six_indexes_without_the_test_helper`:

1. Opens a fixed-name database `DitenSupplyChain_Mod0190_Registration_Test` (DB-010: fixed suffix, not per run).
2. Drops the six `sandop_*` collections there and asserts none of the six indexes exists.
3. Builds a container with only `AddSingleton(db)` and `AddSandopPersistence()`.
4. Asserts exactly one registered `IHostedService` is a `SandopSchema`, and starts the hosted services.
5. Asserts each of the six indexes now exists, with the expected `unique` flag (4 unique, 2 not).

It never calls `SandopSchema.EnsureAsync` or the `SandopTestHost` helper. If the hosted-service line is removed, step 4
fails — shown in the scratch copy (`BUILD-AND-TEST.tsv`, row t2).

The existing helper line `SandopContractTests.cs:6` needs no adjustment.
