# 04_database_migration_standards_output.md — DB & Migration Standards

**Status:** Ready

## Project values

| Area | Required project value | Notes | Status |
|---|---|---|---|
| DB engine | MongoDB | Connection string key: `ConnectionStrings:MongoDb` | Ready |
| Data access / driver | MongoDB C# Driver | `MongoDbContext` + repository pattern | Ready |
| Migration framework | No migration CLI found in repo | Do not assume EF migrations | Ready |
| Schema naming | Collection naming defined by repository/context usage | No centralized collection naming standard found | Partial |
| Table naming | N/A | MongoDB document model | N/A |
| PK / ID pattern | Per aggregate/document model | Confirm per-object implementation during module build | Partial |
| Soft delete / archive | Per aggregate/module rule | No global standard found in repo | Partial |
| Seed data convention | Startup seed via `DbInitializer.SeedData(app.Services)` | Use explicit seed/runbook updates where needed | Ready |
| Audit field convention | Per aggregate/module | Must be defined in each new Platform object where applicable | Ready |

## Migration model used in this repo
- MongoDB schema-less persistence
- Domain models map to documents/collections
- No migration CLI found in repo
- Model evolution is handled through:
  - code/model changes
  - startup seed/init routines
  - manual or runbook-driven data correction scripts where required

## Command policy
- Migration create command: N/A in current repo
- Migration apply command: N/A in current repo
- Migration check/list/script command: N/A in current repo

## Hard rules
- Do not introduce EF-style migration assumptions into this repo.
- Do not add a new migration mechanism without explicit approval.
- Any data-shape change with cross-document impact must be documented in a runbook or controlled seed/update script.
- Audit-related fields must be included on persisted Platform entities where applicable.
