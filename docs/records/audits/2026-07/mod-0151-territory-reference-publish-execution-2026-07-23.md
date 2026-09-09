# MOD-0151 — Territory Reference Set Publish Execution

> **Tarih:** 2026-07-23 · **Tür:** MOD-0048 reference publish execution attempt (governed maker-checker)
> **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` · **Scope:** `tenant` / `scope_key=97c59330-…`
> **Verdict:** **PARTIAL — publish execution blocked by missing operator authorization**
> **Readiness:** `PUBLISHED_VALUES_PENDING` (değişmedi) · **Live smoke:** `LIVE_SMOKE_BLOCKED`
> **Runtime/pack/gateway/UI/registry:** DEĞİŞTİRİLMEDİ · **Reference publish:** YAPILMADI (yetki yok)

---

## 1. Preflight

**Files reviewed:** [operator runbook](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-publish-operator-runbook.md) ·
[F1 authoring template JSON/MD](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-required-reference-authoring-template.json) ·
[operator checklist](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-operator-checklist.md) ·
[MOD-0151 pack](../../execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md) (§4/§8/§9/§10/§16/§20/§23/§24) ·
[FU00 closeout](./mod-0151-fu00-pack-approval-closeout-2026-07-23.md) ·
[FU01 implementation report](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) ·
`BusinessReferenceDataController.cs` (route + auth attributes).

**Publish authorization confirmation:** **YETKİ YOK.** MOD-0048 stewardship API'sinin **her** aksiyonu `[Authorize]` +
per-action `[HasPermission("Platform.BusinessReferenceData.*")]` gerektirir (`BusinessReferenceDataController.cs:24-27, 43-44`).
Publish akışı SoD gereği **iki farklı** kimliği doğrulanmış operator ister (Maker ≠ Checker; `sod_submitter_cannot_approve`
kod düzeyinde zorunlu). Bu execution bağlamında geçerli operator JWT'si (ne Maker ne Checker) mevcut değildir; smoke
okuması dahi kimlik doğrulaması gerektirir. **Bkz. §3.**

**Tenant / scope confirmation:** TenantId `97c59330-dbc4-4665-b29c-0c26dbb5cc93` ✅ · scope kararı `scope_type=tenant`,
`scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93` (runbook §2) ✅. Bu task publish yapabilseydi bu scope kullanılacaktı.

**No-runtime-code confirmation:** Bu task hiçbir runtime kod, MOD-0151 kodu, TerritoryModel/TerritoryNode, migration,
endpoint, controller, UI, permission seed, registry, module-id-registry, `ocelot.json`, `_LayoutTenantShell`, MOD-0151
pack veya F1 template dosyasına **dokunmadı**. Yalnız bu evidence raporu oluşturuldu (task §I gereği).

---

## 2. Execution Summary

| Area | Result | Notes |
|---|---|---|
| Runbook + template okuması | ✅ | Runtime gerçekleri koddan doğrulanmış; değerler/metadata/sıra net |
| Gateway (5000) reachability | ✅ | `GET /health` → **200** |
| Platform (5057) reachability | ✅ | `GET /health` → **200** |
| MOD-0048 auth model doğrulama | ✅ | Controller `[Authorize]` + `[HasPermission]`; anonim çağrı **401** |
| Published-values read smoke (anonim) | ⛔ 401 | `territory-level`/`-model-status`/`-node-status` → **401 Unauthorized** (token yok) |
| Operator credential (Maker) | ⛔ Yok | `Platform.BusinessReferenceData.Create/.Version.*/.Submit` izinli token yok |
| Operator credential (Checker ≠ Maker) | ⛔ Yok | `.Version.Approve/.Publish` izinli **ikinci** kimlik yok; SoD karşılanamaz |
| Reference set create/version/values | ⛔ Yapılmadı | Yetki yok → mutating çağrı denenmedi |
| Validate/submit/approve/publish | ⛔ Yapılmadı | Yetki yok + SoD iki kimlik gerektirir |
| Published-values smoke | ⛔ Yapılmadı | Publish yok + read 401 |

---

## 3. Operator / Maker-Checker Confirmation

| Kontrol | Beklenen | Gözlemlenen | Sonuç |
|---|---|---|---|
| Gateway + Platform ayakta | Evet | 200 / 200 | ✅ |
| MOD-0048 API auth zorunlu | `[Authorize]` + `[HasPermission]` | Anonim çağrı 401 (kanıt §5) | ✅ (doğrulandı) |
| Maker kimliği (izinli JWT) | 1 kullanıcı | **Yok** | ⛔ |
| Checker kimliği (Maker'dan farklı, izinli JWT) | 1 farklı kullanıcı | **Yok** | ⛔ |
| SoD uygulanabilirliği | Maker ≠ Checker | İki kimlik yok → uygulanamaz | ⛔ |
| Idempotency-Key kullanımı | Publish'te zorunlu | Publish adımına ulaşılamadı | n/a |

**Neden bypass edilmedi:** İki operator'ı taklit etmek için JWT üretmek/imzalamak, SoD kontrolünü etkisiz kılar ve
governance audit izini **sahte approver kimlikleriyle** kirletir. Bu, runbook §11 ve task guardrail'lerinin (SoD bypass
yasağı, "yetki yoksa publish yapma") açıkça yasakladığı bir governance-bütünlüğü ihlalidir. Bu nedenle **hiçbir mutating
çağrı denenmedi** ve token forge edilmedi.

---

## 4. Published Sets Summary

| Order | SetCode | ScopeType | ScopeKey | Version | Expected Values | Actual Values | Status |
|---|---|---|---|---|---|---|---|
| 1 | territory-coverage-scope | tenant | 97c59330-… | — | 7 | — | **NOT PUBLISHED (blocked)** |
| 2 | territory-level | tenant | 97c59330-… | — | 6 | — | **NOT PUBLISHED (blocked)** |
| 3 | territory-model-status | tenant | 97c59330-… | — | 6 | — | **NOT PUBLISHED (blocked)** |
| 4 | territory-node-status | tenant | 97c59330-… | — | 4 | — | **NOT PUBLISHED (blocked)** |
| 5 | territory-assignment-status | tenant | 97c59330-… | — | 4 | — | **NOT PUBLISHED (blocked)** |
| 6 | territory-assignment-source | tenant | 97c59330-… | — | 4 | — | **NOT PUBLISHED (blocked)** |
| 7 | business-scope-type | tenant | 97c59330-… | — | 7 | — | **NOT PUBLISHED (blocked)** |
| 8 | territory-resource-role | tenant | 97c59330-… | — | 11 | — | **NOT PUBLISHED (blocked)** |
| 9 | territory-rule-type | tenant | 97c59330-… | — | 9 | — | **NOT PUBLISHED (blocked)** |
| 10 | territory-conflict-policy | tenant | 97c59330-… | — | 4 | — | **NOT PUBLISHED (blocked)** |

**Required toplam:** 10 set / 62 value — **0 publish edildi** (yetki yok).

> **Not:** Yayın durumu bağımsız olarak doğrulanamadı: published-values okuması da 401 döndürdüğü için "zaten publish
> edilmiş mi?" sorusu bu bağlamda yanıtlanamaz. Rapor, publish'in **bu task tarafından yapılmadığını** kaydeder; önceki
> bir operator aksiyonunun durumu, yetkili bir smoke ile ayrıca doğrulanmalıdır.

---

## 5. Optional Sets Decision

| SetCode | Action | Reason |
|---|---|---|
| `planning-period-type` (4) | ⛔ Publish edilmedi (blocked) | Yetki yok; plan: publish (runbook §4) |
| `territory-change-type` (7) | ⛔ Publish edilmedi (blocked) | Yetki yok; plan: publish (runbook §4) |
| `product-portfolio` | ⛔ Publish edilmez | Tenant gerçek kodları onaylı değil; illüstratif değer publish riski (runbook §4) |
| `brand-group` | ⛔ Publish edilmez | Değerler bilinmiyor; boş set publish yasağı |
| `commercial-role-scope-policy` | ⛔ Publish edilmez | F7 policy tanımsız |

---

## 6. Smoke Results

| Check | Result | Notes |
|---|---|---|
| Gateway/Platform health | ✅ PASS | 200 / 200 |
| Anonim reference-data çağrısı auth wall | ✅ PASS | 401 alındı (auth zorunlu — beklenen) |
| Required set count (10) | ⛔ BLOCKED | Publish yapılmadı |
| Required value count (62) | ⛔ BLOCKED | — |
| Optional set count (2) / value (11) | ⛔ BLOCKED | — |
| Metadata string smoke (rank/bool) | ⛔ BLOCKED | Read 401 |
| territory-level rank 10→60 artan | ⛔ BLOCKED | — |
| coverage-scope 4 bool attribute | ⛔ BLOCKED | — |
| resource-role → coverage-scope çapraz ref | ⛔ BLOCKED | — |
| business-scope-type sales/non-sales defaults | ⛔ BLOCKED | — |
| operational-scope / non-sales false/false | ⛔ BLOCKED | — |
| duplicate / lowercase-kebab / no `micro-zone` set | ⛔ BLOCKED | — |
| wrong-scope / global leak | ✅ N/A | Hiç publish yapılmadı → yanlış scope riski oluşmadı |

---

## 7. Evidence Table

| Environment | TenantId | SetCode | Version | IdempotencyKey | CorrelationId/RequestId | Expected | Actual | Smoke |
|---|---|---|---|---|---|---|---|---|
| local-dev | 97c59330-… | territory-coverage-scope | — | — (not attempted) | — | 7 | — | BLOCKED |
| local-dev | 97c59330-… | territory-level | — | — | — | 6 | — | BLOCKED |
| local-dev | 97c59330-… | territory-model-status | — | — | — | 6 | — | BLOCKED |
| local-dev | 97c59330-… | territory-node-status | — | — | — | 4 | — | BLOCKED |
| local-dev | 97c59330-… | territory-assignment-status | — | — | — | 4 | — | BLOCKED |
| local-dev | 97c59330-… | territory-assignment-source | — | — | — | 4 | — | BLOCKED |
| local-dev | 97c59330-… | business-scope-type | — | — | — | 7 | — | BLOCKED |
| local-dev | 97c59330-… | territory-resource-role | — | — | — | 11 | — | BLOCKED |
| local-dev | 97c59330-… | territory-rule-type | — | — | — | 9 | — | BLOCKED |
| local-dev | 97c59330-… | territory-conflict-policy | — | — | — | 4 | — | BLOCKED |
| local-dev | 97c59330-… | planning-period-type *(opt)* | — | — | — | 4 | — | BLOCKED |
| local-dev | 97c59330-… | territory-change-type *(opt)* | — | — | — | 7 | — | BLOCKED |

**Özet:** required set 0/10 · required value 0/62 · optional 0/2 · metadata smoke n/a · overall **BLOCKED (no operator auth)**.

Idempotency keys önerisi (yetkili operator kullanmalı): `mod-0151-territory-{setCode}-97c59330-dbc4-4665-b29c-0c26dbb5cc93-20260723-v1`.

---

## 8. Failures / Retries

- **Anonim published-values probe:** `territory-level`/`territory-model-status`/`territory-node-status` → **401 Unauthorized**
  (`title: Unauthorized`, RFC9110 §15.5.2). Bu bir hata değil; auth zorunluluğunun doğrulanmasıdır.
- **Mutating çağrılar:** Denenmedi (yetki yok + SoD iki kimlik gerektirir). Retry yok.
- **Rollback:** Gerek yok — hiçbir yazma yapılmadı, hiçbir yanlış-scope publish oluşmadı.

---

## 9. Rollback / Recovery Notes

Bu task hiçbir yazma işlemi yapmadığı için rollback gerektirmez. Yetkili operator koştuğunda geçerli olacak recovery
kuralları runbook §9'da tanımlıdır (özet): published value **hard delete edilmez**, düzeltme **yeni versiyon** ile yapılır,
valueCode **rename edilmez** (deprecate + yeni ekle), yanlış scope → doğru scope'a yeni publish + eskisini deprecate,
`territory-level.rank` değişimi yüksek risk (10'ar aralık yeni ara seviye için bırakıldı).

---

## 10. FU01 Live Smoke Readiness

- **PUBLISHED_VALUES_PENDING** (değişmedi) — 10 required set / 62 value henüz publish edilmedi.
- **LIVE_SMOKE_BLOCKED** — publish tamamlanmadan MOD-0151 FU01 canlı create/update kontrollü **400** döner
  (doğru fail-closed davranış; FU01 kodu ve testleri buna göre yeşil).
- Publish PASS olduğunda denenebilecek FU01 canlı endpoint'leri (bu task kapsamında değil):
  `GET /api/crm/territory-management/contract` → `POST /api/crm/territory-models` → `POST /api/crm/territory-models/{id}/nodes`.

---

## 11. Guard Checks

| Check | Result |
|---|---|
| Runtime code touched? | **no** |
| MOD-0151 code touched? | **no** |
| Gateway touched? | **no** |
| UI touched? | **no** |
| Registry touched? | **no** |
| MOD-0151 pack touched? | **no** |
| F1 template touched? | **no** |
| Reference sets published? | **no** (blocked — no operator auth) |
| Reference values published? | **no** |
| Correct tenant? | **yes** (would be 97c59330-…) |
| Correct scope_type? | **yes** (tenant) |
| Correct scope_key? | **yes** (97c59330-…) |
| Required set count 10? | **n/a** (0 published) |
| Required value count 62? | **n/a** (0 published) |
| Optional published set count 2? | **n/a** (0 published) |
| Total published value count 73? | **n/a** (0 published) |
| product-portfolio published? | **no** (correct) |
| brand-group published? | **no** (correct) |
| commercial-role-scope-policy published? | **no** (correct) |
| `micro-zone` separate set created? | **no** (correct) |
| Metadata attributes string? | **n/a** (not written) |
| territory-level rank smoke PASS? | **n/a** (blocked) |
| resource-role coverage cross-ref PASS? | **n/a** (blocked) |
| business-scope false/false PASS? | **n/a** (blocked) |
| Hardcoded fallback introduced? | **no** |
| Mongo hand-edit? | **no** |
| Local seed? | **no** |
| SoD respected? | **yes** (not bypassed; blocked because it cannot be satisfied) |
| Idempotency used? | **n/a** (no publish attempted) |
| Published-values smoke PASS? | **no** (blocked) |
| PUBLISHED_VALUES_READY? | **no** → PENDING |
| LIVE_SMOKE_READY? | **no** → BLOCKED |

---

## 12. Final Verdict

**PARTIAL: publish execution blocked by missing operator authorization.**

MOD-0048 stewardship API'sinin tüm çağrıları kimlik doğrulaması gerektirir (anonim → 401) ve publish akışı SoD gereği
iki **farklı** yetkili operator kimliği ister. Bu execution bağlamında geçerli Maker/Checker operator kimliği yoktur;
SoD'yi bypass etmek veya operator kimliklerini forge etmek governance-bütünlüğü ihlali olacağından **hiçbir mutating
işlem denenmedi ve hiçbir güvensiz workaround uygulanmadı**. Runbook kullanıma hazır kalır; durum `PUBLISHED_VALUES_PENDING`.

---

## 13. Next Recommended Prompt (PARTIAL follow-up)

Publish'i tamamlamak için gereken **net** aksiyon (insan operator gerektirir):

1. **Yetkili operator publish koşumu** — İki farklı MOD-0048 operator hesabı sağlanmalı:
   - **Maker:** `Platform.BusinessReferenceData.Create` · `.Version.Create/.Update/.Validate/.Submit`
   - **Checker (≠ Maker):** `Platform.BusinessReferenceData.Version.Approve/.Publish` · `.Consumer.Read`
   Operator, [runbook §6 prosedürünü](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-publish-operator-runbook.md)
   sırayla (coverage-scope → level → … → conflict-policy) uygular; Gateway (5000) üzerinden, publish'te
   `Idempotency-Key` ile; her set sonrası tenant-scoped published-values smoke yapar ve bu raporun §7/§8 evidence
   tablosunu doldurur.
2. **Publish PASS sonrası:** `MOD-0151 FU01 Live Smoke` (contract readiness → TerritoryModel create → TerritoryNode
   create; test-data soft-delete/cleanup planıyla) veya doğrudan `MOD-0151 FU02 Territory Hierarchy UI`.

> **Alternatif:** Bu ortamda kullanılabilir iki operator kimliği (dev seed) tanımlanır ve credential'ları güvenli
> biçimde sağlanırsa, bu task yeniden çalıştırılabilir — o durumda gerçek publish + smoke bu raporda tamamlanır.
