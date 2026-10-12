# Structure decision — Supply Chain module self-registration (Q40)

🤖 Applying knowledge of @module-pack-author (with @backend-architect, @l10n-agent for review). Documents only; no ID is created.

## 1. Classification (orchestrator rule 9; CAP-001 §2/§3)

The work has two layers with different owners:

| Layer | What it is | Why it is not one module pack |
|---|---|---|
| **A. Service foundation (capability-level)** | One `IModuleManifestProvider` interface, one `ModuleRegistrationHostedService`, `PlatformRegistrationOptions`, the ModuleRegistration abstractions project reference, the `Program.cs` registration lines, the `PlatformRegistration` appsettings section and the hosted-service tests, all in `Diten.SupplyChainService.Api` | Five MVP6 modules (0183–0187) and later 0173–0197 share one service host and one push loop. It is cross-cutting and multi-module (CAP-001 §2 bullets 1, 2 and 6), so it does not belong to any single module |
| **B. Per-module manifest** | One `*ManifestProvider.cs` + its tests per module, mirroring that module's real frontend routes and buttons; plus that module's `Nav.Module.*` / `Nav.Page.*` keys | Code-owned identity of one module (standard §1, §2); belongs to that module's pack |
| **C. Shared single-writer surfaces** | `Program.cs` (service), `Diten.SupplyChainService.Api.csproj`, `appsettings*.json`, `frontend/Diten.Web/Resources/SharedResource.{7}.resx`, and (only if D2=B) Platform credential config/allow-list | Shared files with one writer each (domain-config K15; integration owner) |

## 2. Where each part belongs (no new ID)

| Part | Recommended home | Alternative | Why |
|---|---|---|---|
| A. Foundation | **Amendment/follow-up section in DCP-009 Supply Chain Inventory & Execution** (existing Delivery Capability Pack, `approved`, members include logistics 0183–0187). Recorded as a governance follow-up there, not a new DCP | Put it inside the first-shipping module's pack (the unapproved Carrier candidate did this under MOD-0184) | DCP-009 already owns the cross-module boundary for this service. A new DCP number would be a new ID (forbidden here). Hosting shared wiring in one module's pack makes that module the owner of every sibling's startup path |
| B. Per-module manifest | A new "Self-registration" section in each module pack: MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187 (for 0186/0187 on top of the pending alignment and UI-revision patches) | — | Standard §1: every tenant-assignable module ships its own provider |
| C. Shared surfaces | Single CT-appointed integration owner (also for gateway/nav per the UI handoffs), plus an l10n reviewer for the resx values | — | One writer per shared file |
| S&OP (MOD-0190), Capacity (MOD-0192) | **Excluded now.** Both packs are `shell: none`, `draft`, with no UI scope | Add later if a UI is approved | The standard mirrors real UI; with no UI there is nothing to mirror |

## 3. Sequencing rule (proposed)

1. Foundation (A) lands in the **integrated target** (Q14/Q15), together with the **first** module whose UI routes exist there.
2. Each provider (B) lands **in the same change as that module's UI** and its nav keys (C). The rationale:
   - the manifest must mirror real routes (standard §2);
   - `NavManifestL10nGuardTests` derives expected keys from every `*ManifestProvider.cs` in `services/`, so a provider without its 7-language keys fails `dotnet test`;
   - a provider without its UI would register dead pages.
3. Reconcile is authoritative (standard §4): once a provider ships, removing a page or action later is pruned on the next push, so late corrections are safe but must still pass the reconcile-state test.

## 4. Existing prior art (read, not reused blindly)

`docs/roadmap/plans/mvp6-carrier-ui-dispatch-close-01/candidates/MODULE-REGISTRATION.patch` (`18001827…`) and `NAVIGATION-L10N.patch`
(`e752b46e…`) are **unapproved, unapplied** candidates (package verdict "APPLICATION HELD"). They already chose ModuleCode
`carrier-management`, Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, SortOrder 400, Icon `bx-truck`, the
legacy `X-Internal-Api-Key` path, and 7-language values for `Nav.Domain.SUPPLYCHAINEXECUTION`, `Nav.Module.CARRIERMANAGEMENT`
and `Nav.Page.CARRIERS`. This package **keeps those choices** for consistency and extends them to the other modules. Gaps in that
candidate: its tests check one direction only (no frontend-route completeness, no UI-button completeness, no reconcile-state test).
Its `Program.cs` preimage is an older baseline, not the integrated target.

## 5. Decisions required (put to the owner in OWNER-DECISION-TEXT.md)

| ID | Decision | Recommended |
|---|---|---|
| D1 | Home of the foundation | DCP-009 follow-up section (A) + per-module pack sections (B) |
| D2 | Registration authentication | **A: legacy `X-Internal-Api-Key`** (no Platform change; same as DevEnablement and the Carrier candidate). **B:** a per-service credential like MDM (`X-Module-Registration-Credential-Id` + secret), which needs a Platform change: `ModuleRegistrationCredentialOptions` today has only `Mdm`, and the controller's credential branch is hard-coded to two MDM ModuleCodes |
| D3 | Identity strings | Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, ModuleCodes = existing pack slugs (MANIFESTS.md) |
| D4 | Rollout | Provider ships with the module's UI in the integrated target; none ahead of its UI |
| D5 | Returns target keys are not constants (`ReturnPermissions.ForTarget` switch) | **A:** the completeness test reflects the `ForTarget` mapping as the source of truth (no backend change). **B:** add six `public const` fields to `ReturnPermissions.cs` (a Returns-owned backend path, which needs a pack scope note) |
