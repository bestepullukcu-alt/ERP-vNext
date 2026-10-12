# Build result

## Baseline

The separate integration checkout was constructed at HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` from 490 byte-preserved Claims dirty inputs plus the archived exact Program.cs baseline `a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a`.

`dotnet build services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj -c Debug --no-restore -m:1 /nr:false` passed with 0 warnings and 0 errors. The first no-restore attempt found no local assets; a network restore did not complete and was cancelled. Existing NuGet/MSBuild `obj` inputs from the Claims source checkout were copied into this disposable integration checkout, after which the baseline build passed. This is a composition build, not a fresh restore claim.

## Approved target

The applied baseline-to-target diff is byte-identical to approved patch `70b80f7920f0d216ccd22795328df750a761c949e438a55ede7876c57cbf6444`. Resulting Program.cs is `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.

The same build command failed with 0 warnings and 4 `CS0234` errors. The four missing namespaces are exactly:

- `Diten.SupplyChainService.Api.Features.Returns`
- `Diten.SupplyChainService.Application.Features.Returns`
- `Diten.SupplyChainService.Persistence.Features.Returns`
- `Diten.SupplyChainService.Infrastructure.Features.Returns`

The target checkout contains none of the 46 Returns core paths. The build failure therefore records a missing authorized core delivery, not patch drift or a Claims composition regression.
