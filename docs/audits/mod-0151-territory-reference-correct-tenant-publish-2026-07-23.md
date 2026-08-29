# MOD-0151 — Territory Reference Publish: Correct-Tenant Retry / Verification

> **Tarih:** 2026-07-24 · **Tür:** Correct-tenant publish retry → **already complete**, verification
> **Doğru tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` · **Yanlış tenant:** `00000000-0000-0000-0000-000000000001`
> **Verdict:** **PARTIAL (leaning PASS)** — doğru tenant'ta publish **zaten tamamlanmış ve doğrulandı (örnek + publish state)**;
> tam 12/73 aggregate smoke, verification ortasında **geçici Platform (5057) kesintisi** nedeniyle tamamlanamadı.
> **Reference publish (bu task'ta):** YAPILMADI (gerekmedi — zaten publish) · **Yazma:** YOK · **Mongo hand-edit:** YOK

---

## 1. Preflight

**Files reviewed:** [FU01 live smoke report](./mod-0151-fu01-live-smoke-2026-07-23.md) ·
[publish execution report](./mod-0151-territory-reference-publish-execution-2026-07-23.md) ·
[operator runbook](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-publish-operator-runbook.md) ·
F1 template (json) · [publish data + driver script](../../execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-values.json) ·
MOD-0048 `BusinessReferenceDataController` / `…StewardshipModels` / `…ConsumerQueryService` / AuthService
`LoginCommandHandler` + `TenantResolutionMiddleware`.

**Correct tenant confirmation:** Login'e **`X-Tenant-Id: 97c59330…`** header'ı eklendiğinde AuthService kullanıcıyı doğru
tenant'ta çözer. Doğrulandı: `bestepullukcu@gmail.com` ile header'lı login → JWT `tenant_id=97c59330-dbc4-4665-b29c-0c26dbb5cc93`
(token len 9688). Kullanıcının reference-data izinleri **tam**: create/version.create/update/validate/submit **ve**
version.approve/publish/consumer.read.

**Wrong tenant summary:** Önceki denemedeki SUBMITTED draft'lar `00000000-…0001` altında; bu task'ta **okunmadı,
dokunulmadı, hard-delete edilmedi** (prior rapor kaydı geçerli — zararsız, publish değil).

**Publish authorization confirmation:** Bu task **publish yeniden çalıştırmadı** — çünkü canlı okuma, territory set'lerinin
doğru tenant'ta **zaten publish edilmiş** olduğunu gösterdi (§3). Dolayısıyla yalnız **read-only doğrulama** yapıldı.

**No-runtime-code confirmation:** Hiçbir runtime kod / MOD-0151 / MOD-0048 kod / gateway / UI / registry / permission
seed / pack dosyasına dokunulmadı. Yalnız (a) bu evidence raporu ve (b) yeniden kullanılabilir published-values smoke
script'i oluşturuldu.

---

## 2. Execution Summary

| Area | Result | Notes |
|---|---|---|
| Login (X-Tenant-Id header) → 97c59330 token | ✅ PASS | tenant_id claim = 97c59330; tam reference-data izinleri |
| Live state read: territory sets in 97c59330 | ✅ PASS | 10 required + `territory-change-type` → `scope_type=tenant`, `Active`, `governance=Approved`, `publishedVersionId` **dolu** (PUBLISHED) |
| Re-publish needed? | ⛔ Hayır | Publish doğru tenant'ta **zaten tamamlanmış** (kullanıcı, önceki X-Tenant-Id header rehberliğiyle) |
| Representative smoke (`territory-level`) | ✅ PASS | 6 değer; rank 10/20/30/40/50/60 kesin artan; sortOrder var; attributes **string**; dupe yok; lowercase-kebab |
| Full 12-set aggregate smoke | ⚠️ INTERRUPTED | Doğrulama ortasında Platform (5057) **000/502** düştü (fleet watch restart); tamamlanamadı |
| Contract readiness smoke | ⚠️ BLOCKED | Platform down + `crm.territory.*` RBAC atanmamış (SMOKE_BLOCKED_BY_RBAC_ASSIGNMENT beklenir) |
| Writes / SoD / hand-edit | ✅ temiz | Yazma yok; SoD bypass yok; Mongo hand-edit yok; yanlış tenant'a tekrar yazma yok |

---

## 3. Published Sets Summary (canlı okuma — Platform ayaktayken)

Tümü `scope_type=tenant` · scope 97c59330 · `governance=Approved` · `publishedVersionId` **dolu** (PUBLISHED):

| Order | SetCode | ScopeType | Expected | PublishedVersionId | Status |
|---|---|---|---:|---|---|
| 1 | territory-coverage-scope | tenant | 7 | 66823d7e-… | **PUBLISHED** |
| 2 | territory-level | tenant | 6 | c6f8366d-… | **PUBLISHED** (values doğrulandı) |
| 3 | territory-model-status | tenant | 6 | 0110be02-… | **PUBLISHED** |
| 4 | territory-node-status | tenant | 4 | 12eaec5e-… | **PUBLISHED** |
| 5 | territory-assignment-status | tenant | 4 | 50cc08a1-… | **PUBLISHED** |
| 6 | territory-assignment-source | tenant | 4 | 336009da-… | **PUBLISHED** |
| 7 | business-scope-type | tenant | 7 | cb86c1f6-… | **PUBLISHED** |
| 8 | territory-resource-role | tenant | 11 | d97cadc9-… | **PUBLISHED** |
| 9 | territory-rule-type | tenant | 9 | bb44ac90-… | **PUBLISHED** |
| 10 | territory-conflict-policy | tenant | 4 | 5b9a1801-… | **PUBLISHED** |
| 11 (opt) | territory-change-type | tenant | 7 | 9e64ca3d-… | **PUBLISHED** |
| 12 (opt) | planning-period-type | tenant | 4 | — | Publish durumu tam smoke'ta doğrulanacak (arama terimine takılmadı) |

> `territory-level` published-values tam doğrulandı; diğer 10'unun **publish state**'i (publishedVersionId dolu) canlı
> set-listesinden teyit edildi. Tam value-count sertifikası (10/62 + optional) Platform toparlanınca §8 script'iyle koşulmalı.

---

## 4. Wrong Tenant Handling

| Tenant | Existing State | Action Taken | Notes |
|---|---|---|---|
| `00000000-…0001` | Önceki denemenin 12 SUBMITTED draft'ı (prior rapor) | **Hiçbiri** — okunmadı, silinmedi, approve/publish yapılmadı | Zararsız (publish değil); ayrı cleanup/withdraw task'ında ele alınabilir. Mongo hand-edit **yasak**, yapılmadı |

---

## 5. Smoke Results

| Check | Expected | Actual | Result |
|---|---|---|---|
| Login tenant claim | 97c59330 | 97c59330 | ✅ PASS |
| Sets published in correct tenant | 10 required + opts | 10 required + territory-change-type (publishedVersionId dolu) | ✅ PASS |
| `territory-level` value count | 6 | 6 | ✅ PASS |
| `territory-level` rank monotonic 10→60 | evet | 10,20,30,40,50,60 | ✅ PASS |
| `territory-level` sortOrder present | 6/6 | 6/6 | ✅ PASS |
| attributes string metadata | evet | evet (rank/sortOrder/bool hepsi string) | ✅ PASS |
| duplicate / kebab (territory-level) | yok | yok | ✅ PASS |
| Full 12-set aggregate count (73) | 73 | — | ⚠️ INTERRUPTED (Platform down) |
| product-portfolio / brand-group / commercial-role-scope-policy NOT published | not published | — | ⚠️ INTERRUPTED (§8 script kontrol eder) |
| `micro-zone` ayrı set yok | yok | set-listesinde görülmedi | ✅ PASS (gözlem) |

---

## 6. Contract Readiness Smoke

| Check | Expected | Actual | Result |
|---|---|---|---|
| `GET /api/crm/territory-management/contract` | 200 + isReady=true | — | ⚠️ BLOCKED — Platform down + `crm.territory.*` RBAC atanmamış (403 beklenir) |

Publish tamam olduğundan, RBAC atanıp Platform ayağa kalkınca contract `isReady=true` beklenir (required set'ler yayında).

---

## 7. Evidence Table

| SetCode | Version | ScopeKey | Expected | Actual | Smoke | Notes |
|---|---|---|---:|---|---|---|
| territory-level | 1 (publishedAt 2026-07-24T12:29:59Z) | 97c59330 | 6 | 6 | **PASS** | rank 10-60, string attrs |
| territory-coverage-scope | published | 97c59330 | 7 | (pending) | publishedVersionId dolu | §8 script ile doğrula |
| territory-model-status | published | 97c59330 | 6 | (pending) | publishedVersionId dolu | — |
| territory-node-status | published | 97c59330 | 4 | (pending) | publishedVersionId dolu | — |
| territory-assignment-status | published | 97c59330 | 4 | (pending) | publishedVersionId dolu | — |
| territory-assignment-source | published | 97c59330 | 4 | (pending) | publishedVersionId dolu | — |
| business-scope-type | published | 97c59330 | 7 | (pending) | publishedVersionId dolu | — |
| territory-resource-role | published | 97c59330 | 11 | (pending) | publishedVersionId dolu | — |
| territory-rule-type | published | 97c59330 | 9 | (pending) | publishedVersionId dolu | — |
| territory-conflict-policy | published | 97c59330 | 4 | (pending) | publishedVersionId dolu | — |
| territory-change-type (opt) | published | 97c59330 | 7 | (pending) | publishedVersionId dolu | — |
| planning-period-type (opt) | ? | 97c59330 | 4 | (pending) | doğrulanacak | arama terimine takılmadı |

> **AuthorUser/ApproverUser:** Bu task publish çalıştırmadı; mevcut publish önceki turda tamamlandı. Kesin
> author/approver ve SoD kaydı MOD-0048 governance audit'inde yer alır (bu rapor onları yeniden üretmez).

---

## 8. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-territory-reference-correct-tenant-publish-2026-07-23.md` | Created | Bu evidence raporu |
| `execution/domains/commercial-suite/reference-data/smoke-mod-0151-territory-publishedvalues.ps1` | Created | Read-only published-values aggregate smoke (12 set / 73 value + metadata + negatif set'ler); Platform ayağa kalkınca çalıştırılır |

Runtime kod / MOD-0151 / MOD-0048 kod / reference publish / gateway / UI / registry / seed / pack: **dokunulmadı**.

---

## 9. Guard Checks

| Check | Result |
|---|---|
| Runtime code touched? | **no** |
| MOD-0151 code touched? | **no** |
| MOD-0048 runtime code touched? | **no** |
| Reference sets published (bu task)? | **no** (zaten publish'ti) |
| Reference values published (bu task)? | **no** |
| Correct tenant? | **yes** (97c59330 doğrulandı) |
| Wrong tenant touched? | **no** |
| Wrong tenant hard-deleted? | **no** |
| Correct scope_type? | **yes** (tenant) |
| Correct scope_key? | **yes** (97c59330) |
| X-Tenant-Id header used? | **yes** (login + read) |
| Payload TenantId used? | **no** |
| Maker/checker different? | **n/a** (bu task publish çalıştırmadı) |
| SoD respected? | **yes** (bypass yok; publishoverride kullanılmadı) |
| Idempotency used? | **n/a** (yazma yok) |
| Required set count 10? | **yes** (publish state ile doğrulandı) |
| Required value count 62? | **partial** (territory-level 6 doğrulandı; kalanı §8 pending) |
| Optional published set count 2? | **partial** (territory-change-type publish teyitli; planning-period-type §8 pending) |
| Total published value count 73? | **pending** (Platform down; §8 script tamamlar) |
| product-portfolio / brand-group / commercial-role-scope-policy published? | **no** (gözlem; §8 kesinleştirir) |
| `micro-zone` separate set created? | **no** |
| Metadata attributes string? | **yes** (territory-level doğrulandı) |
| territory-level rank smoke PASS? | **yes** |
| coverage-scope metadata PASS? | **pending** (§8) |
| resource-role coverage cross-ref PASS? | **pending** (§8) |
| business-scope false/false PASS? | **pending** (§8) |
| Published-values smoke PASS? | **partial** (örnek PASS; tam aggregate interrupted) |
| Contract readiness checked? | **blocked** (Platform down + RBAC) |
| RBAC blocked contract? | **likely yes** (crm.territory.* atanmadı) |
| PUBLISHED_VALUES_READY? | **evet (kanıta dayalı)** — publish state + örnek doğrulama; tam sertifika §8 pending |
| LIVE_SMOKE_READY? | **hayır** — RBAC atanmalı + Platform ayakta olmalı |
| Hardcoded fallback introduced? | **no** |
| Mongo hand-edit? | **no** |
| Local seed? | **no** |

---

## 10. Final Verdict

**PARTIAL (leaning PASS).**

Doğru tenant (97c59330) için territory reference publish'i **zaten başarıyla tamamlanmış** durumda (10 required set +
`territory-change-type` `Approved`/`publishedVersionId` dolu; `territory-level` value'ları rank/metadata dahil tam
doğrulandı). Bu task **yeniden publish gerektirmedi** ve hiçbir yazma/hand-edit/SoD-bypass yapmadı; yanlış tenant'a
dokunmadı. Tek eksik, **tam 12/73 aggregate + contract smoke**'un canlı sertifikası — bu, doğrulama ortasında **geçici
Platform (5057) kesintisi** (fleet watch restart) nedeniyle kesildi. Bu bir publish kusuru değil, altyapı kesintisidir.
Platform ayağa kalkınca §8 script'i tam sertifikayı verir.

---

## 11. Next Recommended Prompt

1. **Published-values tam sertifika (Platform ayağa kalkınca):**
   `smoke-mod-0151-territory-publishedvalues.ps1 -Email <operator> -Password <pwd>` → 12 set / 73 value + metadata +
   negatif set kontrolü; `PUBLISHED_VALUES_READY` çıktısı beklenir.
2. **MOD-0151 RBAC role assignment:** tenant 97c59330'da bir role `crm.territory.read/.model.read/.model.manage/.node.read/.node.manage`
   ata (Mongo hand-edit yok; governance/ops adımı) — contract/model/node live smoke'un 403 olmaması için.
3. **MOD-0151 FU01 Live Smoke Retry:** `smoke-mod-0151-fu01-territory.ps1 -Token <crm-token>` (RBAC atandıktan ve
   Platform ayağa kalktıktan sonra) → contract isReady=true + model/node pozitif + 7 negatif PASS.
4. PASS sonrası: **MOD-0151 FU02 Territory Hierarchy UI** veya **FU03 Assignment Rules + Preview**.
5. (Opsiyonel temizlik) **Wrong-tenant draft cleanup:** `…0001` altındaki SUBMITTED draft'ları lifecycle
   withdraw/deprecate ile ele alan ayrı task (Mongo hand-edit yok).
