# Q236 — The narrowing mechanism

## Version actually referenced

| Package | Version | Where |
|---|---|---|
| MediatR | **12.3.0** | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Diten.SupplyChainService.Application.csproj` (`<PackageReference Include="MediatR" Version="12.3.0" />`) |
| FluentValidation.DependencyInjectionExtensions | 11.9.0 | same file |

(The machine's NuGet cache also holds MediatR 14.0.0; this project does not reference it.)

## The API, and proof that it exists in 12.3.0

`MediatRServiceConfiguration.TypeEvaluator` — a `Func<Type, bool>`; MediatR registers a scanned type only when the
function returns true.

| Proof | Evidence |
|---|---|
| Documented in the shipped package | `~/.nuget/packages/mediatr/12.3.0/lib/net6.0/MediatR.xml:408-412`: member `P:Microsoft.Extensions.DependencyInjection.MediatRServiceConfiguration.TypeEvaluator` — "Optional filter for types to register. Default value is a function returning true." |
| Present in the binary | `MediatR.dll` (sha256 `844c9bf80951b1b11eac279c25fe4d941cb9961a7dfd10fa8530341c4d14b7e6`) contains the symbol |
| Compiles | Build of the proposed bytes: 0 errors, 0 warnings (`evidence/build-A-fixed.log`) |
| Does what is needed | Measured, not inferred: same binary set, the only difference is the assignment `c.TypeEvaluator = …`. Without it 18 handler registrations fail validation; with it 0 do (`SABOTAGE-PROOF.md`) |

No other mechanism was used or assumed. MediatR 12.3.0 has no namespace or folder filter on
`RegisterServicesFromAssembly`; `TypeEvaluator` is the one hook.

## What the filter covers

`TypeEvaluator` applies to every type MediatR discovers by scanning (request handlers, and notification handlers,
stream handlers, pre/post processors if any existed). In this assembly the only scanned MediatR types are the 29
request handlers:

| Module | Handlers | After the change |
|---|---:|---|
| Shipments | 5 | registered |
| Carriers | 3 | registered |
| Loads | 3 | registered |
| Returns | 3 | **not registered** |
| Claims | 3 | **not registered** |
| SandopPlans | 6 | **not registered** |
| CapacityPlans | 6 | **not registered** |

SourceIntake has no MediatR handler (its three namespaces hold a coordinator, a mapper and client contracts).
The four pipeline behaviors are registered by explicit `AddTransient` lines, not by the scan, and are untouched.

## The FluentValidation line

`services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly)` is **left as it is**.

| Question | Answer | Evidence |
|---|---|---|
| Do validators of the held modules fail to resolve? | **No. Only handlers do.** | Sabotage run B: 18 distinct failing implementation types, all handlers, 0 validators (`evidence/service-B-sabotage.log`). Run A/C: validators still registered, 0 errors |
| Why | Every validator in the Application assembly has a parameterless constructor | `grep -rnE "class [A-Za-z]+Validator\("` over `Application/Features` = 0 hits |
| Could they be filtered too? | Yes — 11.9.0's `AddValidatorsFromAssembly` has an optional `filter` parameter (`FluentValidation.DependencyInjectionExtensions.xml:67-75`). Not used: it is not needed to boot, and the WP asks to narrow one call | — |

Consequence of leaving them: validators of the four held modules stay registered and unused. Harmless.
