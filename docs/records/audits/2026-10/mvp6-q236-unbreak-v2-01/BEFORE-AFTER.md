# Q236 — Before / after

**The repository file is unchanged.** The Edit tool was refused; the change exists only as a proposal and in the
scratch copy.

| | sha256 | Bytes | Where |
|---|---|---:|---|
| Before (= repository bytes, start and end) | `b17792801495c58fd3d7a46c5cf9197f4761da8acd5753465282830d432cc3b2` | 996 | `before/DependencyInjection.cs.txt` |
| After (proposed) | `f05c1fcd6a4e49950f04a6e0c952487c94c575a76811c9473d434c3f95ce7fcf` | 2,279 | `proposed/DependencyInjection.cs.txt` |

Target: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/DependencyInjection.cs`

## The change

One line changed, one comment block, one list and one predicate added. Nothing removed.

```diff
 public static class DependencyInjection
 {
+    // Q236 (2026-10-03): MediatR must not register handlers of feature modules that Program.cs does not compose.
+    // Their repositories and reference readers are unregistered, so in Development the container validation at
+    // builder.Build() fails for them and takes Shipments, Carriers and Loads down too (Q217, Q232).
+    // Excluded today: Returns (MOD-0186), Claims (MOD-0187), SandopPlans (MOD-0190), CapacityPlans (MOD-0192).
+    // Remove an entry only when Program.cs registers that module's persistence AND its readers (Q209).
+    // Validators are not filtered: they have no constructor dependencies and resolve on their own.
+    private static readonly string[] MediatRExcludedFeatureNamespaces =
+    [
+        "Diten.SupplyChainService.Application.Features.Returns",
+        "Diten.SupplyChainService.Application.Features.Claims",
+        "Diten.SupplyChainService.Application.Features.SandopPlans",
+        "Diten.SupplyChainService.Application.Features.CapacityPlans",
+    ];
+
+    private static bool IsComposedFeatureType(Type type) =>
+        type.Namespace is not { } ns || !MediatRExcludedFeatureNamespaces.Any(excluded =>
+            ns == excluded || ns.StartsWith(excluded + ".", StringComparison.Ordinal));
+
     public static IServiceCollection AddApplication(this IServiceCollection services)
     {
-        services.AddScoped<RequestContext>(); services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
+        services.AddScoped<RequestContext>(); services.AddMediatR(c => { c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly); c.TypeEvaluator = IsComposedFeatureType; });
         services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
```

## How a later lane finds and removes an exclusion

```bash
grep -rn "MediatRExcludedFeatureNamespaces" services/Diten.SupplyChainService/src
```

Delete the one string for the module Q209 has composed. The match is on the namespace and its children
(`…Features.Claims` and `…Features.Claims.*`), by exact segment, so `Features.ClaimsX` would not be caught.

## Untouched

`Program.cs` (`7fdb5ef0…` at start and end), every `appsettings`, every `.csproj`, `ocelot.json`, all test code,
all feature code. No repository, reader, hosted service, middleware branch, route or permission key was added.

## The one settings change that unblocks the edit

`.claude/settings.local.json`, `permissions.deny`, lines 157-161: `Edit(services/**)`, its absolute-path twin,
`Write(services/**)`, its twin, and `NotebookEdit(services/**)`. Narrow the two `Edit(...)` entries so they no
longer cover
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/DependencyInjection.cs`
(or remove them for the duration of this WP). A deny rule normally wins over an allow, so adding an allow alone
is not expected to work. Not verified — the settings file is not this lane's to change.
