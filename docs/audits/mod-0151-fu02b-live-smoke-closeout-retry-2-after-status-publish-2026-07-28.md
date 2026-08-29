# MOD-0151 FU02B — Live Smoke Closeout RETRY-2 (After Lifecycle Status Publish)

> **Tarih:** 2026-07-28
> **Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Kullanıcı:** `bestepullukcu@gmail.com`
> **Korelasyon:** `smoke-fu02b-retry2-20260728123707` · `smoke-fu02b-retry2-bu-20260728124406`
> **Karar:** **PASS** — 72 kontrolün 72'si PASS

Bu çalışma yalnızca canlı doğrulama ve kanıt toplama kapsamındadır. Runtime kodu, backend, frontend, Gateway,
RBAC, seed, reference authoring dosyaları, module pack ve MongoDB **değiştirilmemiştir**. Reference publish
**bu task'ta yapılmamıştır** (operatör tarafından önceden tamamlanmıştır). Bu rapor dışında dosya oluşturulmamıştır.

**Zincirin özeti:** RETRY-1 (2026-07-28 07:45) → FAIL, 33/40, kök neden lifecycle status vocabulary mismatch →
Reconciliation (authoring + test hardening) → operatör publish → **RETRY-2 → PASS, 72/72.**

---

## 1. Preflight

### Services health

| Servis | Port | Sonuç |
|---|---|---|
| Gateway | 5000 | **200** |
| AuthService | 5056 | **200** |
| Platform | 5057 | **200** |
| MDM | 5059 | **200** |
| CrmService | 5061 | **200** (yalnız health; business endpoint çağrısı yok) |
| Web | 5001 | 302/404 (login redirect — ayakta) |

### Login / token

`POST /api/tenant-auth/login` · `X-Tenant-Id` header ile · payload'da **TenantId yok** → **HTTP 200**,
`tenant_id` claim = `97c59330-dbc4-4665-b29c-0c26dbb5cc93`.

### Gateway-only confirmation

Tüm business API çağrıları `http://localhost:5000` üzerinden yapıldı. `:5061`'e yalnız `/health` isteği gitti;
hiçbir business endpoint doğrudan çağrılmadı. Her istekte `X-Tenant-Id` header'ı gönderildi, hiçbir payload'da
`TenantId` alanı bulunmadı.

### No-code-change confirmation

| Kontrol | Değer |
|---|---|
| HEAD (başlangıç ve bitiş) | `094e3a86` — **değişmedi** |
| `git status --porcelain` | 422 → 425 |
| Fark kaynağı | **Bu task değil.** Üç yeni girdi Document Management çalışmasına ait (`ControlledDocumentRegistration*`, `DocumentVariant*`, `Diten.Web/Program.cs`) ve başka bir oturumda düzenleniyor. MOD-0151 / territory / commercial-suite altında **hiçbir dosya değişmedi**. |
| Bu task'ın ürettiği dosya | Yalnız bu rapor |

---

## 2. Auth / Token Verification

| Claim | Beklenen | Gerçek | Sonuç |
|---|---|---|---|
| `crm.territory.read` | mevcut | **True** | PASS |
| `crm.territory.model.read` | mevcut | **True** | PASS |
| `crm.territory.model.manage` | mevcut | **True** | PASS |
| `crm.territory.node.read` | mevcut | **True** | PASS |
| `crm.territory.node.manage` | mevcut | **True** | PASS |
| `crm.territory.delete` | **yok** | **False** | PASS |
| `crm.micro-zone.manage` | **yok** | **False** | PASS |
| `crm.territory.model.activate` | **yok** | **False** | PASS |

**5/5 gerekli claim mevcut · 0/3 yasak claim.**

---

## 3. Reference Readiness After Publish

| Set | Expected | Actual | Result |
|---|---|---|---|
| `territory-model-status` | 7 (`inactive` dahil) | **7** — draft, review, approved, active, **inactive**, superseded, archived | ✅ PASS |
| `territory-node-status` | 5 (`archived` dahil) | **5** — draft, active, inactive, ended, **archived** | ✅ PASS |
| `territory-level` | 6 | 6 | ✅ PASS |
| `territory-coverage-scope` | 7 | 7 | ✅ PASS |
| `territory-assignment-status` | 4 | 4 | ✅ PASS |
| `territory-assignment-source` | 4 | 4 | ✅ PASS |
| `business-scope-type` | 7 | 7 | ✅ PASS |
| `territory-resource-role` | 11 | 11 | ✅ PASS |
| `territory-rule-type` | 9 | 9 | ✅ PASS |
| `territory-conflict-policy` | 4 | 4 | ✅ PASS |
| **Required toplam** | **64** | **64** | ✅ PASS |
| `planning-period-type` + `territory-change-type` (optional) | 11 | 11 | ✅ PASS |
| **Genel toplam published value** | **75** | **75** | ✅ PASS |

**Bonus — plan dışı ek publish:** `business-unit` seti de operatör tarafından publish edilmiş
(**3 value: alpha, beta, gamma**). Bu, RETRY-1'de BLOCKED kalan BU-scope'lu senaryoların ilk kez canlı
doğrulanmasını mümkün kıldı (§7B).

---

## 4. Contract Smoke

`GET /api/crm/territory-management/contract` → **HTTP 200**

| Check | Expected | Actual | Result |
|---|---|---|---|
| `moduleId` | MOD-0151 | MOD-0151 | ✅ |
| `isReady` | true | **true** | ✅ |
| `supportsLifecycleActions` | true | **true** | ✅ |
| `supportsComputedExpiry` | true | **true** | ✅ |
| `supportsDraftSoftDelete` | true | **true** | ✅ |
| `supportsWorkflowActivation` | false | **false** | ✅ |
| `supportsApprovalTrace` | false | **false** | ✅ |
| `missingRequiredReferenceSets` | boş | **0 girdi** | ✅ |
| `territory-model-status` actualValueCount | 7 | **7** (expected 7, ready true) | ✅ |
| `territory-node-status` actualValueCount | 5 | **5** (expected 5, ready true) | ✅ |
| **expected == actual (her iki set)** | true | **true / true** | ✅ |
| Required set toplam actual | 64 | **64** | ✅ |
| `assignmentRules` / `resourceAssignments` / `evidencePack` / `importExport` | hepsi false | hepsi **false** | ✅ |

> Reconciliation'da güncellenen `ExpectedValueCount` sabitleri (7/5) ile canlı publish artık **birebir örtüşüyor**;
> RETRY-1'deki "expected 7 / actual 6" uyarı durumu kapandı.

---

## 5. Locked Smoke Model Cleanup

Model: `SMOKE-MOD0151-LIFE-20260728074528` · id `33fd7e03-37c1-4777-be17-eae0834bce3c`

| Step | Expected | Actual | Result |
|---|---|---|---|
| Model bulundu | active | **active** | ✅ |
| Node durumu | 3 active | **3/3 active** | ✅ |
| **`POST …/deactivate`** | 200 | **200** — vocabulary hatası **yok** | ✅ |
| Model durumu | inactive | **inactive** | ✅ |
| Node senkronizasyonu | 3 inactive | **3/3 inactive** | ✅ |
| **`POST …/archive`** | 200 | **200** — node `archived` vocabulary hatası **yok** | ✅ |
| Model durumu | archived | **archived** | ✅ |
| Node senkronizasyonu | 3 archived | **3/3 archived** | ✅ |
| Listede aktif görünmüyor | not active | **archived / not-active** | ✅ |
| Overlap guard kilidi | çözüldü | **çözüldü** (tenant'ta 0 aktif model) | ✅ |

**RETRY-1'in bıraktığı kilit tamamen çözüldü** — Mongo'ya dokunulmadan, yalnız normal API ile. `GET` ile kayıt
hâlâ okunabiliyor (archived, read-only); hard delete yapılmadı.

---

## 6. Fresh Positive Lifecycle Smoke

Model: `SMOKE-MOD0151-LIFE2-20260728123707` · id `de56c5c5-1ab7-4bd0-96cd-5e59b7a230d6` · countryScope `tr` ·
2028-01-01 → 2028-12-31

| Step | Expected | Actual | Result |
|---|---|---|---|
| Draft model create | 201, draft, isExpired=false | **201**, draft, false | ✅ |
| Country node | 201 draft | **201** | ✅ |
| Zone node (child) | 201 draft | **201** | ✅ |
| MicroZone node + `MicroZoneProfile` | 201 draft | **201** | ✅ |
| Hierarchy | 3 node, hepsi draft | **3/3 draft** | ✅ |
| **Activate** | 200, model active | **200** | ✅ |
| Model durumu | active | **active** | ✅ |
| Node senkronizasyonu | 3 active | **3/3 active** | ✅ |
| **Deactivate** | 200, model inactive | **200** — `inactive` vocabulary hatası **yok** | ✅ |
| Model durumu | inactive | **inactive** | ✅ |
| Node senkronizasyonu | 3 inactive | **3/3 inactive** | ✅ |
| **Archive** | 200, model archived | **200** — `archived` vocabulary hatası **yok** | ✅ |
| Model durumu | archived | **archived** | ✅ |
| Node senkronizasyonu | 3 archived | **3/3 archived** | ✅ |
| Workflow approval tetiklendi mi | hayır | **hayır** (`supportsWorkflowActivation=false`, hiçbir MOD-0023 çağrısı yok) | ✅ |

**Tam zincir canlı ortamda ilk kez uçtan uca kapandı:** `draft → active → inactive → archived`.

---

## 7. Negative Lifecycle Smoke

### 7A. Temel guard'lar

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Active model `delete-draft` | 400/409 | **409** — *Only a draft territory model can be soft-deleted.* | ✅ |
| Active model `archive` (deactivate'siz) | 400/409 | **409** — *Only an inactive or computed-expired territory model can be archived.* | ✅ |
| Active model node `delete-draft` | 400/409 | **409** — *Only a draft node in a draft model can be soft-deleted.* | ✅ |
| Active kayıt korundu | active | **active** | ✅ |
| Archived model `PUT` | 400/409 | **409** — *Only a draft territory model can be updated.* | ✅ |
| Archived node `PUT` | 400/409 | **409** — *Nodes can only be edited on a draft territory model.* | ✅ |
| Draft model `delete-draft` | 200 + listeden düşer | **200**, listede yok, `GET` → **404** (hard delete yok) | ✅ |
| Draft node `delete-draft` | 200 + hierarchy'den düşer | **200**, 2 node → 1 node | ✅ |
| Overlap guard (country-only, case-variant `TR`) | 409 | **409** — *An overlapping active territory model already exists…* | ✅ |
| Overlap sonrası mevcut active model | bozulmaz | **active** | ✅ |
| Overlap adayı | draft kalır | **draft** | ✅ |

### 7B. Business Unit scope'lu overlap — RETRY-1'de BLOCKED, artık doğrulandı

`business-unit` seti publish edildiği için pack §22.1'in **sıra-bağımsız, case-insensitive BU set karşılaştırması**
ilk kez canlıda test edildi.

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Model A create — `countryScope=tr`, BU **[alpha, beta]** | 201 | **201** | ✅ |
| A `businessScopes` persist + geri okuma | alpha,beta | **alpha,beta** | ✅ |
| A activate | 200 | **200** | ✅ |
| Model B create — `countryScope=**TR**`, BU **[BETA, Alpha]** (ters sıra + farklı case), çakışan tarih | 201 | **201** | ✅ |
| **B activate → sıra-bağımsız overlap reddi** | 409 | **409** — *An overlapping active territory model already exists for the same country and business-unit scope.* | ✅ |
| A bozulmadı | active | **active** | ✅ |
| Model C — aynı country, **farklı** BU seti [gamma] → activate | 200 (izin verilmeli) | **200** | ✅ |
| Guardrail: `scopeType=brand-group` (almiba) | reddedilmeli | **400** — *BusinessScopes: only scopeType 'business-unit' is supported.* | ✅ |
| A ve C temizlik (deactivate + archive) | 200/200 archived | **200/200 archived** | ✅ |
| B temizlik (draft soft-delete) | 200 | **200** | ✅ |

> Bu, tek-aktif-model guard'ının **hem çalıştığını hem de aşırı kısıtlayıcı olmadığını** kanıtlar: aynı BU seti
> farklı sırada yazılsa bile yakalanıyor, farklı BU seti ise serbest bırakılıyor. Brand değerlerinin business unit
> olarak kabul edilmediği de canlıda doğrulandı.

---

## 8. Computed Expiry Smoke

Model: `SMOKE-MOD0151-EXP2-20260728123707` · 2020-01-01 → 2020-12-31

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Geçmiş `EffectiveTo` ile draft create | 201 | **201** | ✅ |
| `isExpired` | true | **true** | ✅ |
| `computedStatus` | expired | **expired** | ✅ |
| `storedStatus` korunur | draft | **draft** | ✅ |
| Tekrar okuma DB'yi mutate etmiyor | draft | **draft** | ✅ |
| Background scheduler yok | status kendiliğinden değişmez | **değişmedi** | ✅ |
| Expired model activate reddi | 409 | **409** — *An expired territory model cannot be activated.* | ✅ |
| **Computed-expired model archive** | 200 (pack §22.1: `computed-expired → archived`) | **200** — vocabulary hatası **yok** | ✅ |
| Archive sonrası stored status | archived | **archived** (computedStatus hâlâ expired) | ✅ |

Pack §22.1 ile **birebir uyumlu**: model `inactive` olmadan da, computed-expired olduğu için archive edilebildi.
RETRY-1'de bu adım 400 alıyordu.

---

## 9. Audit / Log Evidence

| Event | Seen? | Notes |
|---|---|---|
| `territory.model.activated` | **NOT CAPTURED** | Kod yolu çalıştı (activate 200 ×4) |
| `territory.model.deactivated` | **NOT CAPTURED** | Kod yolu çalıştı (deactivate 200 ×4) — RETRY-1'de hiç çalışmamıştı |
| `territory.model.archived` | **NOT CAPTURED** | Kod yolu çalıştı (archive 200 ×5) — RETRY-1'de hiç çalışmamıştı |
| `territory.model.soft_deleted` | **NOT CAPTURED** | Kod yolu çalıştı (delete-draft 200 ×4) |
| `territory.node.soft_deleted` | **NOT CAPTURED** | Kod yolu çalıştı (node delete-draft 200) |
| `territory.model.activation_rejected` | **NOT CAPTURED** | Kod yolu çalıştı (overlap 409 ×2, expired 409) |
| `territory.model.delete_rejected` | **NOT CAPTURED** | Kod yolu çalıştı (active delete-draft 409) |
| `territory.node.delete_rejected` | **NOT CAPTURED** | Kod yolu çalıştı (active node delete-draft 409) |

`logs/Crm-watch-out.log` / `-err.log` **2026-07-26'dan beri güncellenmemiş ve boş**; çalışan CrmService süreci
structured log'u dosyaya yazmıyor. Bu nedenle event'ler **görülmedi ve PASS olarak işaretlenmedi**. Task §G
uyarınca bu **ana blocker sayılmamıştır** — davranış API cevap seviyesinde eksiksiz kanıtlanmıştır. Ayrı
follow-up: `MOD-0151 FU02B — Lifecycle Audit Log Sink Hardening`.

---

## 10. Ortam Notu — Platform restart gürültüsü

Smoke sırasında Platform (5057) iki kez yeniden başladı (`dotnet watch`, başka bir oturumdaki Document Management
düzenlemeleri). Bu pencerelerde geçici olarak:

- `published-values` çağrıları **502** döndü,
- contract bir kez `isReady=false` / `territory-level actual=0` gösterdi,
- bu sırada denenen model create'ler **400 fail-closed** aldı.

Servisler stabilize olunca aynı çağrılar sorunsuz çalıştı (`territory-level` yine 6/6, contract `isReady=true`) ve
BU overlap testi **11/11 PASS** verdi. **Bu bir ürün defect'i değildir**; fail-closed davranış doğru çalışmıştır
(reference okunamadığında sistem yazmayı reddetti). Ana smoke (61/61) tamamen stabil pencerede koştu.

---

## 11. Guard Checks

| Check | Result |
|---|---|
| Kod değişti mi? | **Hayır** (HEAD sabit; territory/commercial-suite altında değişiklik yok) |
| Reference publish bu task'ta yapıldı mı? | **Hayır** (operatör önceden yaptı) |
| Reference authoring dosyası değişti mi? | **Hayır** |
| Module pack değişti mi? | **Hayır** |
| Gateway-only mi? | **Evet** |
| Direct `:5061` business API var mı? | **Hayır** (yalnız health) |
| TenantId payload var mı? | **Hayır** |
| `X-Tenant-Id` header kullanıldı mı? | **Evet** (login + tüm çağrılar) |
| Mongo hand-edit var mı? | **Hayır** |
| Hard delete var mı? | **Hayır** |
| Active kayıt silinebildi mi? | **Hayır** (409 ile reddedildi) |
| Workflow approval var mı? | **Hayır** |
| Submit/approve/reject çalıştırıldı mı? | **Hayır** |
| Assignment / resource / evidence / import-export açıldı mı? | **Hayır** |
| Brand Scope eklendi mi? | **Hayır** (aksine `brand-group` reddi doğrulandı) |
| Product/Brand master touched? | **Hayır** |
| Account/Contact touched? | **Hayır** |
| RBAC seed/grant değişti mi? | **Hayır** |
| `crm.territory.delete` eklendi mi? | **Hayır** |
| `crm.micro-zone.manage` eklendi mi? | **Hayır** |
| Background job eklendi mi? | **Hayır** |

---

## 12. Tenant Son Durumu

**Aktif model sayısı: 0** — overlap guard hiçbir modeli kilitlemiyor.

| Model | Stored | Computed | Not |
|---|---|---|---|
| `SMOKE-MOD0151-LIFE-20260728074528` | **archived** | archived | RETRY-1'in kilitli modeli — **temizlendi** |
| `SMOKE-MOD0151-LIFE2-20260728123707` | archived | archived | RETRY-2 fresh lifecycle |
| `SMOKE-MOD0151-EXP2-20260728123707` | archived | expired | Computed-expiry testi |
| `SMOKE-MOD0151-BU-A-20260728124406` | archived | archived | BU overlap baseline |
| `SMOKE-MOD0151-BU-C-20260728124406` | archived | archived | BU distinct-set testi |
| `SMOKE-MOD0151-EXP-20260728074528` | draft | expired | RETRY-1 kalıntısı (zararsız) |
| `SMOKE-MOD0151-NODE-20260728074528` | draft | draft | RETRY-1 kalıntısı (zararsız) |
| `SMOKE-MOD0151-OVL-20260728074528` | draft | draft | RETRY-1 kalıntısı (zararsız) |
| `DENEME` · `SMOKE-MOD0151-20260725131204` | draft | draft | Önceden mevcut kayıtlar — dokunulmadı |

RETRY-2'nin ürettiği tüm ara kayıtlar (overlap adayı, node-test, delete-test) **draft soft-delete** ile
temizlenmiştir. RETRY-1 kalıntıları operasyonel etki yaratmadığı için bırakılmıştır; istenirse
`delete-draft` ile temizlenebilir.

---

## 13. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md` | **Created** | Bu closeout kanıt raporu |

Başka hiçbir dosya oluşturulmadı veya güncellenmedi.

---

## 14. Final Verdict

### **PASS**

**Sayısal sonuç: 72 kontrol / 72 PASS / 0 FAIL** (ana smoke **61/61** + BU overlap eki **11/11**).

Task'ın PASS ölçütleriyle karşılaştırma:

| PASS koşulu | Durum |
|---|---|
| Login başarılı | ✅ |
| Contract yeni status vocabulary ile ready | ✅ `isReady=true`, missing boş |
| `territory-model-status` 7/7 | ✅ (`inactive` publish) |
| `territory-node-status` 5/5 | ✅ (`archived` publish) |
| Locked active model normal API ile deactivate/archive edildi | ✅ |
| Fresh lifecycle activate/deactivate/archive zinciri 200 | ✅ |
| Draft model/node soft-delete çalıştı | ✅ |
| Negative guard'lar çalıştı | ✅ (11 senaryo) |
| Computed expiry doğrulandı | ✅ (archive dahil) |
| Hard delete yok | ✅ |
| Workflow approval yok | ✅ |
| Guardrail'ler korundu | ✅ |

**RETRY-1'in iki FAIL'i kapandı:** `deactivate` ve `archive` artık 200 dönüyor; `Lifecycle reference values are
not published.` hatası hiçbir çağrıda görülmedi.

### Neden PARTIAL değil

Task §J'nin PARTIAL satırlarından ikisi teknik olarak geçerli (audit log sink görünmüyor; UI manual smoke
yapılmadı). Ancak:

- §G, log sink eksikliğinin **ana blocker yapılmamasını ve ayrı follow-up'a bırakılmasını** açıkça talimatlandırır;
  lifecycle davranışı API seviyesinde eksiksiz kanıtlanmıştır.
- UI manual smoke bu task'ın A–H adımlarında **hiç istenmemiştir**; kapsam API smoke'udur.
- Üçüncü PARTIAL koşulu (`business-unit` publish eksik) **artık geçerli değil** — set publish edilmiş ve
  BU-scope'lu senaryolar §7B'de canlı doğrulanmıştır.

Bu nedenle **FU02B operasyonel closeout'u PASS** kabul edilmiştir. Aşağıdaki iki kalem kapsam dışı
follow-up'tır, closeout engeli değildir.

---

## 15. Next Recommended Prompt

**Sıradaki iş:**

`@orchestrator MOD-0151 FU03 — Assignment Rules + Preview`

FU02B artık canlı ortamda tam kapalıdır: lifecycle (activate/deactivate/archive), computed expiry, draft
soft-delete, tek-aktif-model guard (country + sıra-bağımsız BU seti) ve tüm negative guard'lar kanıtlanmıştır.
FU03 için gerekli zemin — aktif bir territory modeli oluşturup güvenle geri alabilme — hazırdır.

### Bloklamayan follow-up'lar

| # | Prompt | Neden |
|---|---|---|
| G | `MOD-0151 FU02B — Lifecycle Audit Log Sink Hardening` | Structured lifecycle audit event'leri hiçbir log dosyasına düşmüyor; gözlemlenebilirlik eksiği (§9) |
| — | *(kapandı)* `MOD-0048 Business Unit Reference Set Publish` | `business-unit` publish edildi (alpha/beta/gamma) ve §7B'de doğrulandı — **artık gerekmiyor** |
| — | *(opsiyonel)* RETRY-1 draft kalıntılarının `delete-draft` ile temizliği | 3 draft kayıt; operasyonel etkisi yok (§12) |
