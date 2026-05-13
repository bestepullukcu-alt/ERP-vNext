# 06_environment_commands_output.md — Environment Commands

**Status:** Ready

| Command type | Exact command | Scope | Mandatory? |
|---|---|---|---|
| Backend build | `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj` | backend runtime project | Y |
| Backend targeted tests | `dotnet test services/Diten.Application.Tests` | targeted backend tests | Y |
| Backend end-to-end tests | `dotnet test services/Diten.EnterpriseStrategy.EndToEnd.Tests` | use only when relevant | N |
| Frontend build | `dotnet build frontend/Diten.Web/Diten.WebUI.csproj` | frontend runtime project | Y |
| Frontend targeted tests | `npm --prefix frontend/Diten.Web test` | frontend UI tests | Y |
| Lint | `N/A — no lint command configured in repo` | repo policy | Y |
| Migration create/apply/check | `N/A — no migration CLI configured in repo` | MongoDB schema-less model | Y |
| Local backend run | `dotnet run --project services/Diten.Platform/src/Diten.Platform.API` | dev verification | Y |
| Local frontend run | `dotnet run --project frontend/Diten.Web` | dev verification | Y |

## Usage rules
- Prefer targeted commands over full-solution commands.
- Only run end-to-end tests when the affected scope touches those scenarios.
- Do not invent lint or migration commands not present in the repo.
