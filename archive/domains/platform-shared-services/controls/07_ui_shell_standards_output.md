# 07_ui_shell_standards_output.md — UI Shell & Component Standards

**Status:** Ready baseline

## Mandatory UI archetypes (reuse)
- Catalog/List pages
- Detail pages
- Inbox/Queue pages
- Workspace pages

## Current repo standards / best-fit mappings

| Area | Standard component / pattern | Path | Status |
|---|---|---|---|
| Page shell/layout | Shared MVC layout shell | `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | Ready |
| Table/list | No shared abstraction found; use consistent inline table pattern per feature | best-fit in feature views | Partial |
| Drawer/modal | Modal shell partial | `frontend/Diten.Web/Views/EnterpriseStrategyBusinessPerformance/Components/Forms/_ModalFormShell.cshtml` | Ready baseline |
| Forms/validation | ASP.NET Core Tag Helpers + validation partial | `frontend/Diten.Web/Views/Shared/_ValidationScriptsPartial.cshtml` | Ready |
| Filters/search | Inline GET form/filter pattern | feature-specific views | Partial |
| Status badges | Inline CSS/span badge pattern | feature-specific views + `wwwroot` styles | Partial |
| Shared JS/page module pattern | Page/function JS files grouped under `wwwroot/js` | `frontend/Diten.Web/wwwroot/js` | Ready baseline |
| Frontend tests | Vitest tests | `frontend/Diten.Web/tests` | Ready |

## Current frontend structure highlights
- Controllers: `frontend/Diten.Web/Controllers`
- Views: `frontend/Diten.Web/Views`
- Static assets: `frontend/Diten.Web/wwwroot`
- Service helpers: `frontend/Diten.Web/Services`
- View models: `frontend/Diten.Web/Models`

## Evidence Panel standard
- Evidence Panel remains a target-state reusable UI component owned by MOD-0031.
- In the current repo, use the established modal/partial pattern rather than inventing a new shell.
- Do not modify protected business-domain feature views to force Platform UI patterns.

## Guardrails
- Reuse existing MVC shell/layout patterns.
- Do not assume React/Vue-style component infrastructure; this repo is ASP.NET Core MVC oriented.
- Where no reusable component exists, use the best-fit existing pattern and document it in the batch output.
