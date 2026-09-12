# MOD-0151 — FU01 Live Smoke Retry (Contract + TerritoryModel + TerritoryNode)

> **Tarih:** 2026-07-25 · **Tür:** Final live smoke (geliştirme değil) · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **User/Role:** bestepullukcu / 97c5 Admin · **Verdict:** **PASS**
> 23/23 smoke PASS (contract 6 + model 5 + node 5 + 7 negatif) · published-values **73/73** · isReady=True ·
> **Kod/DataSeeder/reference/RBAC/Mongo:** DEĞİŞTİRİLMEDİ · Gateway-only (direct 5061 yok).

---

## 1. Preflight

**Files reviewed:** [RBAC smoke retry report](./mod-0151-rbac-smoke-retry-after-seed-2026-07-23.md) ·
[permission seed report](./mod-0151-territory-permission-catalog-seed-97c5-grant-2026-07-23.md) ·
[FU01 live smoke (partial)](./mod-0151-fu01-live-smoke-2026-07-23.md) ·
[correct-tenant publish](./mod-0151-territory-reference-correct-tenant-publish-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
smoke scripts (`smoke-mod-0151-fu01-territory.ps1`, `smoke-mod-0151-territory-publishedvalues.ps1`) · MOD-0151 pack ·
Territory controllers + `TenantResolutionMiddleware`.

**Health status:** gateway(5000)=**200** · authsvc(5056)=**200** · platform(5057)=**200** · crm(5061)=**200**.

**Tenant/token confirmation:** Login (X-Tenant-Id header) → HTTP 200, `tenant_id=97c59330-dbc4-4665-b29c-0c26dbb5cc93`,
**5/5 crm.territory.* claim mevcut**, forbidden yok. Payload TenantId gönderilmedi (tenant yalnız header).

**No-code-change confirmation:** Hiçbir runtime kod, MOD-0151/MOD-0048 kodu/data, reference set/value, RBAC seed,
permission grant, gateway, UI, registry, Mongo'ya dokunulmadı. Yalnız Gateway üzerinden read/create smoke + bu rapor.
Create edilen tek şey `SMOKE-*` test kayıtları (aşağıda cleanup).

---

## 2. Published-values Final Check

| Set/Check | Expected | Actual | Result |
|---|---|---|---|
| 12 set toplam value | 73 | **73** (0 fail) | ✅ |
| required 10 set / 62 value | 62 | 62 | ✅ |
| optional 2 set / 11 value | 11 | 11 | ✅ |
| territory-level rank | 10,20,30,40,50,60 | 10,20,30,40,50,60 | ✅ |
| business-scope-type operational-scope | false/false | false/false | ✅ |
| business-scope-type non-sales-resource-planning | false/false | false/false | ✅ |
| attributes string metadata | evet | evet | ✅ |
| product-portfolio published? | no | **not-published** | ✅ |
| brand-group published? | no | **not-published** | ✅ |
| commercial-role-scope-policy published? | no | **not-published** | ✅ |
| `micro-zone` ayrı set? | no | **not-published** | ✅ |

---

## 3. Contract Smoke

| Check | Expected | Actual | Result |
|---|---|---|---|
| HTTP | 200 | 200 | ✅ |
| moduleId | MOD-0151 | MOD-0151 | ✅ |
| runtimeScope | FU01-territory-model-node-backend-only | FU01-territory-model-node-backend-only | ✅ |
| tenantId | 97c59330-… | 97c59330-… | ✅ |
| isReady | true | **True** | ✅ |
| missingRequiredReferenceSets | boş | boş | ✅ |
| flags models/nodes | true/true | true/true | ✅ |
| flags rules/accountApply/resource/workflow/evidence/import/ui | false | hepsi false | ✅ |

---

## 4. Positive Model Smoke

| Step | Expected | Actual | Result |
|---|---|---|---|
| `POST /territory-models` | 201 | 201 (id=a0e4bb36-…) | ✅ |
| `GET /territory-models/{id}` | 200, draft/v1 | 200, status=draft, versionNumber=1, corr korunuyor | ✅ |
| status/version/tenant/correlation | draft/1/correct | draft/1/smoke-fu01-…/97c59330 | ✅ |
| `GET /territory-models` (list contains) | model listede | listede | ✅ |
| `PUT /territory-models/{id}` (draft update) | 200 | 200 | ✅ |

---

## 5. Positive Node Smoke

| Step | Expected | Actual | Result |
|---|---|---|---|
| root country node | 201 | 201 (id=c7b67b10-…) | ✅ |
| child zone node (level-skip country→zone, rank 20→50) | 201 | 201 (id=f42bbe7f-…) | ✅ |
| microzone node + MicroZoneProfile | 201 | 201 (id=7eabb5d5-…) | ✅ |
| `GET /territory-models/{id}/nodes` | 3 node | 200, nodes=3 | ✅ |
| `PUT .../nodes/{nodeId}` (draft update) | 200 | 200 | ✅ |

---

## 6. Negative Validation Smoke

| Scenario | Expected | Actual | Result | Notes |
|---|---|---|---|---|
| Duplicate ModelCode | 409 | 409 | ✅ | — |
| Invalid model date (From>To) | 400 | 400 | ✅ | — |
| Duplicate TerritoryCode in model | 409 | 409 | ✅ | — |
| Backward rank (zone→region) | 400 | 400 | ✅ | — |
| Invalid territory level | 400 | 400 | ✅ | — |
| MicroZoneProfile on non-microzone (zone) | 400 | 400 | ✅ | — |
| Child date outside parent | 400 | 400 | ✅ | — |
| Non-draft mutation | — | **SKIP** | ⏭️ | FU01'de activation/status-transition endpoint yok; DB hand-edit yasak |
| Cross-tenant access | — | **SKIP** | ⏭️ | ikinci tenant token yok; payload TenantId ile simüle edilmez |

**7/7 negatif PASS; 2 SKIP by design.**

---

## 7. Cleanup Status

- **Created records:** 1 TerritoryModel (`SMOKE-MOD0151-20260725131204`) + nodeslar (`TR-SMOKE`, `TR-ZONE-SMOKE`,
  `TR-MICRO-SMOKE` ve negatif testlerin başarılı ara kaydı `TR-DP-SMOKE`), hepsi **draft**.
- **Retained (silinmedi):** FU01'de delete endpoint yok → smoke kayıtları **draft** olarak kalır. Kod prefix'iyle
  bulunabilir: `SMOKE-MOD0151-*` / `TR-*-SMOKE`.
- **Reason:** Hard delete / Mongo hand-edit **yasak**; FU01 delete kapsam dışı. Bu bir problem değildir (verdict PASS).

---

## 8. Evidence Table

| Step | Endpoint | Method | Expected | Actual | Result | CorrelationId | Notes |
|---|---|---|---|---|---|---|---|
| login | /api/tenant-auth/login | POST | 200 + 5 claims | 200, 5/5 | ✅ | — | X-Tenant-Id header |
| contract | /api/crm/territory-management/contract | GET | 200 isReady=true | 200 isReady=True | ✅ | — | flags FU01-correct |
| model create | /api/crm/territory-models | POST | 201 | 201 | ✅ | smoke-fu01-20260725131204 | id=a0e4bb36 |
| model get | /api/crm/territory-models/{id} | GET | 200 draft/v1 | 200 | ✅ | preserved | — |
| model list | /api/crm/territory-models | GET | contains | contains | ✅ | — | — |
| model update | /api/crm/territory-models/{id} | PUT | 200 | 200 | ✅ | — | draft |
| node root | /api/crm/territory-models/{id}/nodes | POST | 201 | 201 | ✅ | — | country |
| node child | …/nodes | POST | 201 | 201 | ✅ | — | zone, level-skip |
| node microzone | …/nodes | POST | 201 | 201 | ✅ | — | +MicroZoneProfile |
| node list | …/nodes | GET | 3 | 200, 3 | ✅ | — | hierarchy |
| node update | …/nodes/{nodeId} | PUT | 200 | 200 | ✅ | — | draft |
| neg (7 senaryo) | …/territory-models[/nodes] | POST | 400/409 | 400/409 | ✅ | — | 7/7 PASS |
| published-values | /api/v1/reference-data/sets/{code}/published-values | GET | 73/73 | 73/73 | ✅ | — | 12 set |

---

## 9. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | **no** |
| MOD-0151 code changed? | **no** |
| MOD-0048 data changed? | **no** |
| Reference set/value changed? | **no** (read-only) |
| RBAC seed changed? | **no** |
| Permission grant changed? | **no** |
| Gateway changed? | **no** |
| UI changed? | **no** |
| Registry changed? | **no** |
| Mongo hand-edit used? | **no** |
| Local/manual DB insert used? | **no** (yalnız API create) |
| Direct 5061 used? | **no** (Gateway-only) |
| Correct tenant? | **yes** (97c59330) |
| X-Tenant-Id used? | **yes** |
| Payload TenantId used? | **no** |
| Login 200? | **yes** |
| Token tenant claim correct? | **yes** |
| 5 permission claims present? | **yes** |
| Forbidden permissions absent? | **yes** |
| Published-values 12/12 and 73/73? | **yes** |
| Contract isReady true? | **yes** |
| Feature flags out-of-scope false? | **yes** |
| Model positive smoke PASS? | **yes** (5/5) |
| Node positive smoke PASS? | **yes** (5/5) |
| Negative validation smoke PASS? | **yes** (7/7) |
| Cleanup safe? | **yes** (draft kayıtlar; hard delete yok) |
| Assignment/Resource/Activation/Evidence/Import-export endpoint called? | **no** (yok zaten) |
| Product/Brand master touched? | **no** |
| Account/Contact touched? | **no** |
| Hardcoded fallback introduced? | **no** |

---

## 10. Final Verdict

**PASS.**

MOD-0151 FU01 backend canlı ortamda **tam doğrulandı**: health OK · login/token/5 permission OK · published-values
**73/73** (rank/metadata/false-false/cross-ref/negatif set kontrolleri dahil) · contract **isReady=True** + FU01 flags
doğru · TerritoryModel pozitif smoke **5/5** · TerritoryNode pozitif smoke **5/5** (root/child level-skip/microzone+profile/
hierarchy/update) · negatif validation **7/7** (409/400 doğru reason'larla) · 2 SKIP by design. Hiçbir kod/seed/reference/
Mongo değişikliği, hiçbir scope-dışı endpoint çağrısı, hiçbir güvensiz workaround yok. Toplam **23/23 smoke PASS**.

MOD-0151 FU01 (Contract + TerritoryModel + TerritoryNode backend) **canlı ortamda çalışır ve kapanış (closeout) PASS**.

---

## 11. Next Recommended Prompt

1. **MOD-0151 FU02 — Territory Hierarchy UI / Territory Model Viewer** — backend + reference + RBAC canlı hazır;
   Golden Reference Compact, 7 dil resx, `_LayoutTenantShell` menü `<li>` (`crm.territory.read` guard), Model Viewer
   (hierarchy tree + level badge + node detail).
2. Alternatif: **MOD-0151 FU03 — Assignment Rules + Preview** (UI kasıtlı ertelenirse; `territory-rule-type` +
   `territory-conflict-policy` publish'li).
