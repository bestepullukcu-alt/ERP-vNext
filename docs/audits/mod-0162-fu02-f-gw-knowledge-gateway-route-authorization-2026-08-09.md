# MOD-0162-FU02-F-GW — Gateway `/api/crm/knowledge*` Route Authorization

- **Tarih:** 2026-08-09
- **Task türü:** Gateway route authorization + FU02 ready-for-dev unblock (implementation DEĞİL)
- **Verdict:** **PASS** — 2 knowledge route eklendi, mevcut route'lar korundu, F-GW resolved, MOD-0162-FU02 `ready-for-dev`.

---

## 1. Preflight

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | MOD-0162-FU02 pack mevcut | ✅ |
| 2 | FU02 `status: draft` (başlangıçta) | ✅ (bu task sonunda `ready-for-dev`) |
| 3 | F-BND resolved | ✅ (2026-08-09 boundary review) |
| 4 | FU01 approved | ✅ |
| 5 | FU01B draft = FU02 için non-blocking yazılı | ✅ (FU02 top callout + §20) |
| 6 | `/api/crm/knowledge` route yok (öncesi) | ✅ doğrulandı |
| 7 | Campaigns/Consents/Preferences korunacak | ✅ |
| 8 | Route ekleme yalnız `/api/crm/knowledge` + `{everything}` | ✅ |
| 9 | Downstream `Diten.CrmService:5061` | ✅ |
| 10 | DELETE eklenmeyecek | ✅ |
| 11 | PATCH eklenmeyecek | ✅ |
| 12 | MOD-0155 açılmayacak | ✅ |
| 13 | Runtime/UI kodu yazılmayacak | ✅ |
| 14 | RBAC seed/grant yok | ✅ |

## 2. Source Files Reviewed

- `execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md`
- `docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md`
- `execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md`
- `execution/domains/commercial-suite/domain-config.md`, `AGENTS.md`
- `gateway/Diten.ApiGateway/ocelot.json` — CRM (campaigns/consents/preferences), MDM (brands), legal-entities route blokları.

## 3. Gateway Route Inventory Before

- Toplam route: **114**.
- `/api/crm/knowledge*`: **YOK**.
- Mevcut CRM route'ları: accounts, contacts, territory-management, territory-models, resources,
  visit-frequency-policies, consents, preferences, campaigns (hepsi downstream 5061).
- Precedent (campaigns): collection `GET/POST/OPTIONS`, `{everything}` `GET/POST/PUT/OPTIONS`, iki blok, port 5061.

## 4. Route Authorization Decision

- İzinli dosya: yalnız `gateway/Diten.ApiGateway/ocelot.json`.
- İzinli 2 blok: `/api/crm/knowledge` ve `/api/crm/knowledge/{everything}`.
- Downstream: `localhost:5061` (Diten.CrmService).
- Allowed methods: `GET, POST, PUT, OPTIONS` (FU02 §11 sözleşmesiyle birebir). **DELETE/PATCH yasak.**
- Yerleşim: CRM grubunun sonunda (campaigns `{everything}` bloğundan sonra, `/api/mdm/brands` bloğundan önce) —
  Ocelot convention'a uygun; wildcard exact'ı gölgelemez (Ocelot en-uzun-eşleşme).

## 5. Ocelot Changes

Eklenen iki blok (`UpstreamScheme: http`, `DownstreamScheme: http`):

```json
{ "DownstreamPathTemplate": "/api/crm/knowledge",
  "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5061 } ],
  "UpstreamPathTemplate": "/api/crm/knowledge",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "OPTIONS" ] }
{ "DownstreamPathTemplate": "/api/crm/knowledge/{everything}",
  "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5061 } ],
  "UpstreamPathTemplate": "/api/crm/knowledge/{everything}",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "OPTIONS" ] }
```

`DownstreamPathTemplate == UpstreamPathTemplate` (path korunur). Header/auth/tenant propagation mevcut CRM route
pattern'iyle aynıdır (route-level özel auth override eklenmedi — precedent gibi global pipeline).

## 6. Route Validation

`py -c "json.load(...)"` ile doğrulandı:

| Kontrol | Sonuç |
|---|---|
| `ocelot.json` valid JSON | ✅ |
| Toplam route 114 → **116** (+2) | ✅ |
| `/api/crm/knowledge` var, port 5061, `[GET,POST,PUT,OPTIONS]` | ✅ |
| `/api/crm/knowledge/{everything}` var, port 5061, `[GET,POST,PUT,OPTIONS]` | ✅ |
| Knowledge route'larında DELETE | ✅ YOK |
| Knowledge route'larında PATCH | ✅ YOK |
| Downstream port 5061 | ✅ |

> **Canlı doğrulama:** Runtime henüz yok (FU02 implementasyonu yapılmadı) → `/api/crm/knowledge` canlıda 404/502
> dönebilir; task talimatı (F.14) uyarınca bu route authorization için **FAIL değildir**. Gateway restart yapılmadıysa
> 404 **stale gateway** olarak raporlanır. Bu task statik config doğrulamasıyla sınırlıdır.

## 7. Existing Route Preservation

| Route | Durum | Port |
|---|---|---|
| `/api/crm/campaigns` (+`{everything}`) | ✅ korundu | 5061 |
| `/api/crm/consents` (+`{everything}`) | ✅ korundu | 5061 |
| `/api/crm/preferences` (+`{everything}`) | ✅ korundu | 5061 |
| `/api/mdm/brands` (+`{everything}`) | ✅ korundu | 5059 |
| `/api/legal-entities` (+`{everything}`) | ✅ korundu | 5059 |

Hiçbir mevcut blok değiştirilmedi/taşınmadı/silinmedi.

## 8. FU02 Pack Status Update

- `status: draft → ready-for-dev`.
- Top callout: iki blocker (F-BND, F-GW) resolved; pack implementasyona AÇIK.
- Ready-for-dev checklist: F-BND ✅ + F-GW ✅ işaretlendi.
- §11 Gateway kararı: "route eklendi" + endpoint tablosu + 404/stale notu.
- §20 F-GW satırı: RESOLVED.

## 9. Explicit Exclusions

Runtime/UI kodu · Campaign/Consent/Brand-Product runtime/UI · RBAC seed/grant · MOD-0048 publish · registry write ·
Mongo hand-edit · MOD-0155 scope · DELETE/PATCH · `/api/mdm/*` değişikliği · `/api/legal-entities` değişikliği · yeni
downstream service. Hiçbiri yapılmadı.

## 10. Created / Updated Files

- **Created:** `docs/audits/mod-0162-fu02-f-gw-knowledge-gateway-route-authorization-2026-08-09.md` (bu rapor).
- **Updated:** `gateway/Diten.ApiGateway/ocelot.json` — +2 knowledge route bloğu.
- **Updated:** `MOD-0162-FU02-knowledge-content-runtime-ui.md` — `ready-for-dev` + F-GW resolved.
- **Dokunulmadı:** runtime/frontend kodu, registry, Mongo, seed/grant, MOD-0048, diğer route'lar.

## 11. Final Verdict — **PASS**

`/api/crm/knowledge` + `/api/crm/knowledge/{everything}` route authorization tamamlandı; DELETE/PATCH eklenmedi;
mevcut CRM/MDM/legal-entities route'ları korundu; F-GW resolved; MOD-0162-FU02 `ready-for-dev`; runtime/UI/seed/
registry/Mongo değişmedi. FAIL kriterlerinin hiçbiri oluşmadı.

## 12. Next Recommended Prompt

```text
@orchestrator execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md

MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI Implementation
```

> **Not:** RBAC alignment en sona bırakılacak. MOD-0155 beklemede kalacak. FU01B EngagementJourney naming
> reconciliation FU02'yi bloklamaz. Gateway'in yeni route'ları görmesi için restart gerekebilir.
