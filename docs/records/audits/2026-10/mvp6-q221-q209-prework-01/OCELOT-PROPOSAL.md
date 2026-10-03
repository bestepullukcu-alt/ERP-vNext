# Q221 — `ocelot.json` proposal for Q209 — PROPOSAL ONLY, nothing applied

Target: `gateway/Diten.ApiGateway/ocelot.json`. **This file belongs to `integration-agent` only** (`AGENTS.md:108`; `.antigravity/rules/routes.md:124`). Q209 must not edit it directly; this text is input for an integration-agent work package. The file was not edited or reformatted.

| Version | sha256 | Routes | 5061 | 5065 | Source |
|---|---|---:|---|---|---|
| Working tree today (= HEAD; no ` M` on `gateway/`) | `b0121d2f5b7f809dc3dfd9a406b411bfd1ebb0b5b57faaf42b9ca69fabc1229f` | 269 | 35 × `/api/crm` | 0 | read 2026-10-02 |
| BASE (decided) | `67060bf9bd38c62623652a521ce79ddf3aed26a73b65c7fb9eb38f513018e9a7` | 275 | 6 × `/api/shipment-bundle/` | 35 × `/api/crm` | member `…/gateway/Diten.ApiGateway/ocelot.json` of `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` |
| **Proposed** = BASE + 14 fragment routes | `d26933feb982d7d7aa4979d6ee50ab5f8550fc2ad5f4f86402b8cb559924055e` (assembly rule in §4) | 289 | 20 = 10 `/api/shipment-bundle/` + 10 `/api/supply-chain/` | 35 × `/api/crm` | this file |

## 1. What changes against the working tree

1. **CRM 5061 → 5065: 35 routes.** In each of the 35 routes whose upstream starts with `/api/crm` (working-tree route index 121-155), the one line `"Port": 5061` becomes `"Port": 5065`. Nothing else in those routes differs between the working tree and BASE (compared route by route: 0 differences beyond the port). Authority: `AGENTS.md:86-87`, `:90`.
2. **6 supply-chain routes from BASE** (Shipments × 4, Carriers × 2) on 5061 — §3.1.
3. **14 routes from the four UI-draft fragments** on 5061 — §3.2. These are in no layer of BASE-STACK v2 (`SHARED-SEAM-PATCH-NEEDS.md:77-84`).
4. The other 234 routes and `GlobalConfiguration` are identical in the working tree and BASE (list equality checked in memory).

## 2. Full proposed route list — every route and its source

`=` means the route object is identical to the working-tree route with the same upstream template and methods.

| # | Upstream template | Methods | Port | Source |
|---:|---|---|---:|---|
| 0 | `/api/shipment-bundle/shipments` | GET POST | 5061 | BASE (added; not in the working tree) |
| 1 | `/api/shipment-bundle/shipments/{shipmentId}` | GET | 5061 | BASE (added; not in the working tree) |
| 2 | `/api/shipment-bundle/shipments/{shipmentId}/transition` | POST | 5061 | BASE (added; not in the working tree) |
| 3 | `/api/shipment-bundle/shipments/{shipmentId}/pod` | POST | 5061 | BASE (added; not in the working tree) |
| 4 | `/api/shipment-bundle/carriers` | GET POST OPTIONS | 5061 | BASE (added; not in the working tree) |
| 5 | `/api/shipment-bundle/carriers/{carrierId}/status` | POST OPTIONS | 5061 | BASE (added; not in the working tree) |
| 6 | `/api/shipment-bundle/claims` | GET POST OPTIONS | 5061 | draft fragment — Claims v4 (in no layer) |
| 7 | `/api/shipment-bundle/claims/{claimId}/transition` | POST OPTIONS | 5061 | draft fragment — Claims v4 (in no layer) |
| 8 | `/api/shipment-bundle/returns` | GET POST OPTIONS | 5061 | draft fragment — Returns v3 (in no layer) |
| 9 | `/api/shipment-bundle/returns/{returnId}/transition` | POST OPTIONS | 5061 | draft fragment — Returns v3 (in no layer) |
| 10 | `/api/supply-chain/sandop-plans` | GET POST OPTIONS | 5061 | draft fragment — S&OP v3 (in no layer) |
| 11 | `/api/supply-chain/sandop-plans/{sandopPlanId}` | GET OPTIONS | 5061 | draft fragment — S&OP v3 (in no layer) |
| 12 | `/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots` | GET POST OPTIONS | 5061 | draft fragment — S&OP v3 (in no layer) |
| 13 | `/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs` | GET POST OPTIONS | 5061 | draft fragment — S&OP v3 (in no layer) |
| 14 | `/api/supply-chain/capacity-plans` | POST OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 15 | `/api/supply-chain/capacity-plans/{capacityPlanId}` | GET OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 16 | `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios` | POST OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 17 | `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}` | GET OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 18 | `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations` | POST OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 19 | `/api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}` | GET OPTIONS | 5061 | draft fragment — Capacity v3 (in no layer) |
| 20 | `/api/v1/ppm` | GET POST PUT PATCH DELETE OPTIONS | 5062 | BASE = working tree |
| 21 | `/api/v1/ppm/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5062 | BASE = working tree |
| 22 | `/api/platform/working-calendars` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 23 | `/api/platform/working-calendars/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 24 | `/api/platform/organization-units` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 25 | `/api/platform/organization-units/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 26 | `/api/platform/positions` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 27 | `/api/platform/positions/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 28 | `/api/platform/position-assignments` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 29 | `/api/platform/position-assignments/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 30 | `/api/platform/navigation/{everything}` | GET PUT OPTIONS | 5057 | BASE = working tree |
| 31 | `/api/platform/tenant-security/{everything}` | GET PUT OPTIONS | 5057 | BASE = working tree |
| 32 | `/api/legal-entities` | GET POST OPTIONS | 5059 | BASE = working tree |
| 33 | `/api/legal-entities/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5059 | BASE = working tree |
| 34 | `/api/global-products` | GET POST OPTIONS | 5059 | BASE = working tree |
| 35 | `/api/global-products/{everything}` | GET POST OPTIONS | 5059 | BASE = working tree |
| 36 | `/api/product-abbreviations` | GET POST PATCH OPTIONS | 5059 | BASE = working tree |
| 37 | `/api/product-abbreviations/{everything}` | GET POST PATCH OPTIONS | 5059 | BASE = working tree |
| 38 | `/api/finished-goods` | GET POST OPTIONS | 5059 | BASE = working tree |
| 39 | `/api/finished-goods/{everything}` | GET POST OPTIONS | 5059 | BASE = working tree |
| 40 | `/api/gskus` | GET POST OPTIONS | 5059 | BASE = working tree |
| 41 | `/api/gskus/{everything}` | GET POST OPTIONS | 5059 | BASE = working tree |
| 42 | `/api/lskus` | GET POST OPTIONS | 5059 | BASE = working tree |
| 43 | `/api/lskus/{everything}` | GET POST OPTIONS | 5059 | BASE = working tree |
| 44 | `/api/golden-reference-slim` | GET POST PUT PATCH OPTIONS DELETE | 5058 | BASE = working tree |
| 45 | `/api/golden-reference-slim/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5058 | BASE = working tree |
| 46 | `/api/golden-reference-compact` | GET POST PUT PATCH OPTIONS DELETE | 5058 | BASE = working tree |
| 47 | `/api/golden-reference-compact/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5058 | BASE = working tree |
| 48 | `/api/personalization/views` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 49 | `/api/personalization/views/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 50 | `/api/platform/administrators` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 51 | `/api/platform/access/explain/me` | GET OPTIONS | 5057 | BASE = working tree |
| 52 | `/api/platform/administrators/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 53 | `/api/platform/account` | GET PUT OPTIONS | 5057 | BASE = working tree |
| 54 | `/api/platform/account/{everything}` | GET PUT OPTIONS | 5057 | BASE = working tree |
| 55 | `/api/platform/audit/events` | GET OPTIONS | 5057 | BASE = working tree |
| 56 | `/api/platform/audit/events/{everything}` | GET OPTIONS | 5057 | BASE = working tree |
| 57 | `/api/platform/audit/export` | GET OPTIONS | 5057 | BASE = working tree |
| 58 | `/api/platform/audit/retention` | GET PUT OPTIONS | 5057 | BASE = working tree |
| 59 | `/api/platform/audit/redact-actor` | POST OPTIONS | 5057 | BASE = working tree |
| 60 | `/api/platform/module-catalog` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 61 | `/api/platform/module-catalog/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 62 | `/api/platform/module-domains` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 63 | `/api/platform/module-domains/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 64 | `/api/platform/module-services` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 65 | `/api/platform/module-services/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 66 | `/api/platform/interface-registry` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 67 | `/api/platform/interface-registry/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 68 | `/api/platform/subscription-plans` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 69 | `/api/platform/subscription-plans/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 70 | `/api/platform/subscription-features` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 71 | `/api/platform/subscription-features/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 72 | `/api/platform/feature-categories` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 73 | `/api/platform/feature-categories/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 74 | `/api/platform/notifications` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 75 | `/api/platform/notifications/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 76 | `/api/platform/tenants` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 77 | `/api/platform/tenants/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 78 | `/api/admin/tenants` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 79 | `/api/admin/tenants/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5057 | BASE = working tree |
| 80 | `/api/lookups/{everything}` | GET OPTIONS | 5057 | BASE = working tree |
| 81 | `/api/v1/reference-data` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 82 | `/api/v1/reference-data/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 83 | `/api/v1/document-management` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 84 | `/api/v1/document-management/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 85 | `/api/v1/document-repository` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 86 | `/api/v1/document-repository/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 87 | `/api/v1/workflow` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 88 | `/api/v1/workflow/{everything}` | GET POST PUT PATCH DELETE OPTIONS | 5057 | BASE = working tree |
| 89 | `/api/v1/work-items/{itemId}/actions/{actionCode}` | POST OPTIONS | 5057 | BASE = working tree |
| 90 | `/api/v1/work-items/{everything}` | GET OPTIONS | 5057 | BASE = working tree |
| 91 | `/api/v1/tasks` | GET POST OPTIONS | 5057 | BASE = working tree |
| 92 | `/api/v1/tasks/{everything}` | GET POST PUT DELETE OPTIONS | 5057 | BASE = working tree |
| 93 | `/api/v1/meetings` | GET POST OPTIONS | 5057 | BASE = working tree |
| 94 | `/api/v1/meetings/{everything}` | GET POST PUT DELETE OPTIONS | 5057 | BASE = working tree |
| 95 | `/api/v1/notifications` | GET OPTIONS | 5057 | BASE = working tree |
| 96 | `/api/v1/notifications/{everything}` | POST OPTIONS | 5057 | BASE = working tree |
| 97 | `/api/platform-auth/{everything}` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 98 | `/api/tenant-auth/{everything}` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 99 | `/api/auth/{everything}` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 100 | `/api/users` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 101 | `/api/users/{everything}` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 102 | `/api/roles/{everything}` | GET POST PUT OPTIONS DELETE | 5056 | BASE = working tree |
| 103 | `/api/permissions/{everything}` | GET OPTIONS | 5056 | BASE = working tree |
| 104 | `/api/v1/enterprise-strategy` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 105 | `/api/v1/enterprise-strategy/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 106 | `/api/v1/delivery-execution/initiatives` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 107 | `/api/v1/delivery-execution/initiatives/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 108 | `/api/v1/delivery-execution-management/initiatives` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 109 | `/api/v1/delivery-execution-management/initiatives/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 110 | `/api/v1/delivery-execution/projects` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 111 | `/api/v1/delivery-execution/projects/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 112 | `/api/v1/delivery-execution-management/projects` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 113 | `/api/v1/delivery-execution-management/projects/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 114 | `/api/v1/demand-ideas` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 115 | `/api/v1/demand-ideas/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 116 | `/api/v1/task-reports` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 117 | `/api/v1/task-reports/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 118 | `/api/v1/strategic-themes` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 119 | `/api/v1/strategic-themes/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 120 | `/api/v1/uploads` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 121 | `/api/v1/uploads/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 122 | `/api/v1/system` | GET OPTIONS | 5004 | BASE = working tree |
| 123 | `/api/v1/system/{everything}` | GET OPTIONS | 5004 | BASE = working tree |
| 124 | `/api/esbp` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 125 | `/api/esbp/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5004 | BASE = working tree |
| 126 | `/api/v1/platform/persons` | GET POST OPTIONS | 5057 | BASE = working tree |
| 127 | `/api/v1/platform/persons/{everything}` | GET POST OPTIONS | 5057 | BASE = working tree |
| 128 | `/api/v1/platform/audit/events` | POST OPTIONS | 5057 | BASE = working tree |
| 129 | `/api/v1/platform/workflows/instances` | POST OPTIONS | 5057 | BASE = working tree |
| 130 | `/api/v1/platform/workflows/instances/{everything}` | GET POST OPTIONS | 5057 | BASE = working tree |
| 131 | `/api/v1/hcm/employees` | GET OPTIONS | 5060 | BASE = working tree |
| 132 | `/api/v1/hcm/employees/{everything}` | GET POST PATCH DELETE OPTIONS | 5060 | BASE = working tree |
| 133 | `/api/v1/hcm/employees/drafts` | POST OPTIONS | 5060 | BASE = working tree |
| 134 | `/api/v1/hcm/employees/drafts/{everything}` | GET POST PATCH OPTIONS | 5060 | BASE = working tree |
| 135 | `/api/pv-case-intake-triage` | GET POST | 5011 | BASE = working tree |
| 136 | `/api/pv-case-intake-triage/{intakeDraftId}` | GET PUT | 5011 | BASE = working tree |
| 137 | `/api/pv-case-intake-triage/{intakeDraftId}/triage` | POST | 5011 | BASE = working tree |
| 138 | `/api/pv-case-intake-triage/{intakeDraftId}/route` | POST | 5011 | BASE = working tree |
| 139 | `/health` | GET OPTIONS | 5004 | BASE = working tree |
| 140 | `/healthz` | GET OPTIONS | 5004 | BASE = working tree |
| 141 | `/api/crm/accounts` | GET POST PUT PATCH OPTIONS DELETE | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 142 | `/api/crm/accounts/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 143 | `/api/crm/contacts` | GET POST PUT PATCH OPTIONS DELETE | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 144 | `/api/crm/contacts/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 145 | `/api/crm/territory-management/{everything}` | GET OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 146 | `/api/crm/territory-models` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 147 | `/api/crm/territory-models/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 148 | `/api/crm/resources/{everything}` | GET OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 149 | `/api/crm/visit-frequency-policies` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 150 | `/api/crm/visit-frequency-policies/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 151 | `/api/crm/consents` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 152 | `/api/crm/consents/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 153 | `/api/crm/preferences` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 154 | `/api/crm/preferences/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 155 | `/api/crm/campaigns` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 156 | `/api/crm/campaigns/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 157 | `/api/crm/cycle-capacities` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 158 | `/api/crm/cycle-capacities/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 159 | `/api/crm/route-optimization/preview` | POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 160 | `/api/crm/visit-content/preview` | POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 161 | `/api/crm/visit-content/{everything}` | POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 162 | `/api/crm/visit-plan/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 163 | `/api/crm/visit-report` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 164 | `/api/crm/visit-report/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 165 | `/api/crm/cycle-periods` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 166 | `/api/crm/cycle-periods/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 167 | `/api/crm/planned-visits` | GET POST PUT PATCH OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 168 | `/api/crm/planned-visits/{everything}` | GET POST PUT PATCH OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 169 | `/api/crm/segments` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 170 | `/api/crm/segments/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 171 | `/api/crm/strategy-templates` | GET POST OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 172 | `/api/crm/strategy-templates/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 173 | `/api/crm/subjects/{everything}` | GET OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 174 | `/api/crm/knowledge` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 175 | `/api/crm/knowledge/{everything}` | GET POST PUT OPTIONS | 5065 | BASE: working-tree route, port 5061 → 5065 |
| 176 | `/api/mdm/brands` | GET POST OPTIONS | 5059 | BASE = working tree |
| 177 | `/api/mdm/brands/{everything}` | GET POST PUT OPTIONS | 5059 | BASE = working tree |
| 178 | `/api/mdm/products` | GET POST OPTIONS | 5059 | BASE = working tree |
| 179 | `/api/mdm/products/{everything}` | GET POST PUT OPTIONS | 5059 | BASE = working tree |
| 180 | `/api/mdm/brand-products/contract` | GET OPTIONS | 5059 | BASE = working tree |
| 181 | `/api/tep-shell-metadata` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 182 | `/api/tep-shell-metadata/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 183 | `/api/talent-data-foundation` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 184 | `/api/talent-data-foundation/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 185 | `/api/hiring-risk-indicators` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 186 | `/api/hiring-risk-indicators/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 187 | `/api/early-warning-signals` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 188 | `/api/early-warning-signals/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 189 | `/api/restricted-integrity-registry` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 190 | `/api/restricted-integrity-registry/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 191 | `/api/professional-reputation-ledger` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 192 | `/api/professional-reputation-ledger/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 193 | `/api/industry-talent-pool` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 194 | `/api/industry-talent-pool/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 195 | `/api/industry-skill-passport` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 196 | `/api/industry-skill-passport/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 197 | `/api/candidate-career-passport` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 198 | `/api/candidate-career-passport/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 199 | `/api/talent-development-network` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 200 | `/api/talent-development-network/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 201 | `/api/industry-succession-pool` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 202 | `/api/industry-succession-pool/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 203 | `/api/verified-certification-registry` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 204 | `/api/verified-certification-registry/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 205 | `/api/mentorship-recommendation-network` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 206 | `/api/mentorship-recommendation-network/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 207 | `/api/salary-benchmarking` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 208 | `/api/salary-benchmarking/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 209 | `/api/workforce-analytics` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 210 | `/api/workforce-analytics/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 211 | `/api/sector-talent-trends` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 212 | `/api/sector-talent-trends/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 213 | `/api/talent-supply-demand-forecasting` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 214 | `/api/talent-supply-demand-forecasting/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 215 | `/api/skills-gap-heatmap` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 216 | `/api/skills-gap-heatmap/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 217 | `/api/sector-mobility-intelligence` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 218 | `/api/sector-mobility-intelligence/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 219 | `/api/association-operations` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 220 | `/api/association-operations/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 221 | `/api/industry-knowledge-network` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 222 | `/api/industry-knowledge-network/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 223 | `/api/tep-consent-visibility-policies` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 224 | `/api/tep-consent-visibility-policies/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 225 | `/api/tep-association-memberships` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 226 | `/api/tep-association-memberships/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 227 | `/api/tep-verified-participants` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 228 | `/api/tep-verified-participants/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 229 | `/api/tep-review-board` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 230 | `/api/tep-review-board/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 231 | `/api/tep-trust-levels` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 232 | `/api/tep-trust-levels/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 233 | `/api/tep-candidate-profiles` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 234 | `/api/tep-candidate-profiles/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 235 | `/api/tep-exit-reference-records` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 236 | `/api/tep-exit-reference-records/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 237 | `/api/tep-reference-exchange` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 238 | `/api/tep-reference-exchange/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 239 | `/api/tep-rehire-recommendations` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 240 | `/api/tep-rehire-recommendations/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 241 | `/api/tep-candidate-disputes` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 242 | `/api/tep-candidate-disputes/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5064 | BASE = working tree |
| 243 | `/api/employee-projections` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 244 | `/api/employee-projections/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 245 | `/api/sensitive-access` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 246 | `/api/sensitive-access/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 247 | `/api/position-assignments` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 248 | `/api/position-assignments/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 249 | `/api/offboarding-cases` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 250 | `/api/offboarding-cases/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 251 | `/api/applicant-intake` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 252 | `/api/applicant-intake/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 253 | `/api/candidate-pipeline` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 254 | `/api/candidate-pipeline/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 255 | `/api/offer-management` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 256 | `/api/offer-management/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 257 | `/api/employee-onboarding` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 258 | `/api/employee-onboarding/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 259 | `/api/employment-changes` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 260 | `/api/employment-changes/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 261 | `/api/performance-reviews` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 262 | `/api/performance-reviews/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 263 | `/api/competency-skills` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 264 | `/api/competency-skills/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 265 | `/api/learning-training` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 266 | `/api/learning-training/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 267 | `/api/succession` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 268 | `/api/succession/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 269 | `/api/workforce-planning` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 270 | `/api/workforce-planning/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 271 | `/api/headcount-budget` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 272 | `/api/headcount-budget/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 273 | `/api/hr-kpi-analytics` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 274 | `/api/hr-kpi-analytics/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 275 | `/api/hr-documentation` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 276 | `/api/hr-documentation/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 277 | `/api/time-attendance-leave` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 278 | `/api/time-attendance-leave/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 279 | `/api/compensation-benefits` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 280 | `/api/compensation-benefits/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 281 | `/api/self-service` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 282 | `/api/self-service/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 283 | `/api/hr-case-management` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 284 | `/api/hr-case-management/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 285 | `/api/hr-compliance` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 286 | `/api/hr-compliance/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 287 | `/api/development-plans` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |
| 288 | `/api/development-plans/{everything}` | GET POST PUT PATCH OPTIONS DELETE | 5063 | BASE = working tree |

In every one of the 20 supply-chain routes the downstream template equals the upstream template, the scheme is `http` and the host is `localhost`.

## 3. Exact text of the routes that are new

### 3.1 From BASE (verbatim; proposed routes 0-5)

```json
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/shipments",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/shipments",
      "UpstreamHttpMethod": [
        "GET",
        "POST"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}",
      "UpstreamHttpMethod": [
        "GET"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}/transition",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}/transition",
      "UpstreamHttpMethod": [
        "POST"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}/pod",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/shipments/{shipmentId}/pod",
      "UpstreamHttpMethod": [
        "POST"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/carriers",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/carriers",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/carriers/{carrierId}/status",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/carriers/{carrierId}/status",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    }
```

### 3.2 From the draft fragments (proposed routes 6-19; inserted directly after route 5)

**Claims v4** — `overlay/_shared-integration/gateway-claims-routes.ocelot-fragment.json` in `docs/records/audits/2026-09/mvp6-claims-ui-draft-04/claims-ui-draft-overlay-v4.tar.gz` (`2b34741a…`)

```json
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/claims",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/claims",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/claims/{claimId}/transition",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/claims/{claimId}/transition",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    }
```

**Returns v3** — `overlay/_shared-integration/gateway-returns-routes.ocelot-fragment.json` in `docs/records/audits/2026-10/mvp6-returns-ui-draft-03/returns-ui-draft-overlay-v3.tar.gz` (`50724097…`)

```json
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/returns",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/returns",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/shipment-bundle/returns/{returnId}/transition",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/shipment-bundle/returns/{returnId}/transition",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    }
```

**S&OP v3** — `overlay/_shared-integration/gateway-sandop-routes.ocelot-fragment.json` in `docs/records/audits/2026-09/mvp6-sop-ui-draft-03/sop-ui-draft-overlay-v3.tar.gz` (`716e7c4c…`)

```json
    {
      "DownstreamPathTemplate": "/api/supply-chain/sandop-plans",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/sandop-plans",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}",
      "UpstreamHttpMethod": [
        "GET",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}/snapshots",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/sandop-plans/{sandopPlanId}/sign-offs",
      "UpstreamHttpMethod": [
        "GET",
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    }
```

**Capacity v3** — `overlay/_shared-integration/gateway-capacity-routes.ocelot-fragment.json` in `docs/records/audits/2026-09/mvp6-capacity-ui-draft-03/capacity-ui-draft-overlay-v3.tar.gz` (`5c0a3b61…`)

```json
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}",
      "UpstreamHttpMethod": [
        "GET",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}",
      "UpstreamHttpMethod": [
        "GET",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}/evaluations",
      "UpstreamHttpMethod": [
        "POST",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    },
    {
      "DownstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "localhost",
          "Port": 5061
        }
      ],
      "UpstreamPathTemplate": "/api/supply-chain/capacity-plans/{capacityPlanId}/evaluations/{evaluationId}",
      "UpstreamHttpMethod": [
        "GET",
        "OPTIONS"
      ],
      "UpstreamScheme": "http"
    }
```

In the file the 14 objects follow route 5 as one comma-separated run (§4).

## 4. Assembly rule and checks

- **Rule:** take the BASE member bytes; directly after the sixth route object (`/api/shipment-bundle/carriers/{carrierId}/status`), insert the 14 fragment route objects of §3.2 in the order shown, each as `,` + newline + the object, formatted as BASE formats its routes (2-space JSON, objects indented 4 spaces, LF, no final newline). The sha256 in the table above is of that text. It was computed in memory; **no such file was written**.
- **Order:** no record prescribes a position or an order for the 14 routes. Ocelot matches these explicit, non-overlapping templates the same way in any order, and the file has no catch-all route (0 found), so the order has no functional effect. The order shown is a proposal only.
- **No duplicate upstream template + method** in the proposed list: 0 duplicates (checked case-insensitively, the same rule as the gateway test `NoTwoRoutes_ShareTheSameUpstreamTemplateAndHttpMethod`).
- **`/…XYZ` not matched:** every new template is explicit and none ends in `{everything}`, so `/api/supply-chain/sandop-plansXYZ`, `/capacity-plansXYZ`, `/returnsXYZ`, `/claimsXYZ` have no route (packs MOD-0190 :419, MOD-0192 :435-436; Q210 :59; Q211 :74-75). By reading only; not run.
- **Header pass-through** (`Authorization`, `X-Correlation-Id`, `Idempotency-Key`, `X-Tenant-Id`, `X-Legal-Entity-Id`): no route in the proposal carries a header transform, and `GlobalConfiguration` is unchanged. That Ocelot forwards them by default is stated by the Returns fragment's own comment; it is **not verified here** (insufficient evidence without a run).

## 5. Coverage of the operations the four VER records list as unreachable

| VER record | Operation | Proposed route # |
|---|---|---|
| Q210 Returns `REACHABILITY.md:59` | GET + POST `/api/shipment-bundle/returns` | 8 |
| | POST `/api/shipment-bundle/returns/{returnId}/transition` | 9 |
| | GET `/api/shipment-bundle/shipments/{shipmentId}` (dependency read) | 1 (BASE) |
| Q211 Claims `REACHABILITY.md:12-14` | GET + POST `/api/shipment-bundle/claims` | 6 |
| | POST `/api/shipment-bundle/claims/{claimId}/transition` | 7 |
| Q212 S&OP `REACHABILITY.md:59` | GET + POST `/api/supply-chain/sandop-plans` | 10 (see OD-9) |
| | GET `/{sandopPlanId}` | 11 |
| | GET + POST `/{sandopPlanId}/snapshots` | 12 |
| | GET + POST `/{sandopPlanId}/sign-offs` | 13 |
| Q213 Capacity `REACHABILITY.md:93` (pack :432-435) | POST `/api/supply-chain/capacity-plans` | 14 |
| | GET `/{capacityPlanId}` | 15 |
| | POST `/{capacityPlanId}/scenarios` | 16 |
| | GET `/{capacityPlanId}/scenarios/{scenarioId}` | 17 |
| | POST `/{capacityPlanId}/scenarios/{scenarioId}/evaluations` | 18 |
| | GET `/{capacityPlanId}/evaluations/{evaluationId}` | 19 |

All listed operations are covered: 14 new routes + 1 existing BASE route.

## 6. The route-count guard

- **Test:** `CrmAndSupplyChainRoutes_MapOnlyToTheirOwnedServicePorts` in `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`.
  It exists only in the **BASE** version of that file (sha256 `efcc548e…`, lines 125-138). The working-tree test file (`2e64c273…`, 308 lines) has no such test and no `shipment-bundle` text. So the BASE test file must land with, or before, the gateway change.
- **What it counts** (BASE test lines 131-138): routes whose upstream starts with `/api/shipment-bundle/` (ordinal) must number **6** and all be on 5061; CRM routes must number **35** and all be on 5065.
- **Expected new numbers with this proposal:**
  - `Assert.Equal(35, crmRoutes.Length)` — **stays 35**.
  - `Assert.Equal(6, supplyChainRoutes.Length)` — **becomes 10** (6 BASE + 2 Claims + 2 Returns). The Claims draft carries a patch for 6 → 8 (`gateway-tests-ocelot-count.patch.txt`); Returns adds two more and carries no patch (F-Q201-8), so 8 would be wrong once both land.
  - Total routes: 269 today → 275 BASE → **289** proposed. The only total check, `OcelotJson_DeserializesIntoNonEmptyRouteList`, asserts `>= 80` and is unaffected.
- **Not covered by any guard (OD-7):** the 10 `/api/supply-chain/` routes (S&OP 4, Capacity 6). The predicate at BASE test line 132 does not match them, so no assertion counts them or pins them to 5061. Whether to add one, and with what number, is not decided by any record.
- `KnownDownstreamPorts`: the BASE test lists 5061 and 5065 (line 17). The working-tree test lists neither 5063, 5064 nor 5065 (line 16) although the working-tree `ocelot.json` already has 108 routes on 5063/5064 — recorded as F-Q221-5; it is fixed by the same BASE test file.

## 7. Points where the proposal does not decide

| OD | Point |
|---|---|
| OD-7 | No count or port guard for the `/api/supply-chain/` family |
| OD-8 | Four BASE Shipment routes (0-3) have no `OPTIONS` method. NET-001 says `OPTIONS` is mandatory (`.antigravity/rules/routes.md:52`, `:126`); the packs say "explicit routes with OPTIONS (NET-001)". BASE content is decided, so nothing is changed here; integration-agent decides |
| OD-9 | Route 10 carries `GET` on the S&OP collection because pack MOD-0190 :417 lists "GET+POST". `SandopPlansController` has no collection GET (six actions, none on the bare collection for GET), so that GET would reach the service and get 405. The fragment's own comment asks the integration owner to confirm or drop it |
| — | NET-001 asks for two routes per module (`/{resource}` and `/{resource}/{everything}`, all methods). The packs ask for explicit routes and no catch-all. Pack outranks `.antigravity/` (`AGENTS.md:24-27`), so the explicit form is used; recorded so the integration-agent sees the deviation from its own rule |
