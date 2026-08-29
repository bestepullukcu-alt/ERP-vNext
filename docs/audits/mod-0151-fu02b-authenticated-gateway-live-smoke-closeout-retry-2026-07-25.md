# MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout RETRY

> **Rapor adı/tarihi:** talepte belirtilen dosya adı korunmuştur (`…-2026-07-25.md`).
> **Gerçek çalıştırma tarihi:** **2026-07-28**
> **Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Kullanıcı:** `bestepullukcu@gmail.com` (Admin User)
> **Korelasyon:** `smoke-fu02b-20260728074528`
> **Karar:** **FAIL** — 40 kontrolün 33'ü PASS, 7'si FAIL. Tek bir kök nedenden kaynaklanan **gerçek bir ürün defect'i** bulundu.

Bu çalışma yalnızca canlı doğrulama ve kanıt toplama kapsamındadır. Runtime kodu, backend, frontend, Gateway,
RBAC, reference data, module pack ve MongoDB **değiştirilmemiştir**. Bu rapor dışında dosya
oluşturulmamış veya güncellenmemiştir.

**Önceki denemeden farkı:** `mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-2026-07-25.md` login 401
nedeniyle hiçbir senaryoyu çalıştıramamıştı (tüm adımlar BLOCKED). Bu retry'da **login başarılı oldu ve zincirin
tamamı gerçekten çalıştırıldı**. FAIL kararı artık "doğrulanamadı" değil, **"doğrulandı ve çalışmıyor"** anlamındadır.

---

## 1. Preflight

| Kontrol | Gerçekleşen | Sonuç |
|---|---|---|
| Gateway `:5000/health` | HTTP 200 | PASS |
| AuthService `:5056/health` | HTTP 200 | PASS |
| Platform `:5057/health` | HTTP 200 | PASS |
| MDM `:5059/health` | HTTP 200 | PASS |
| CrmService `:5061/health` | HTTP 200 (yalnız health; business API'ye direct çağrı yok) | PASS |
| Web `:5001/` | HTTP 302 (login redirect — çalışıyor) | PASS |
| Gateway route/auth guard | 7 territory route'u tokensız **401**; bilinmeyen route **404** → route'lar mevcut ve auth korumalı | PASS |
| Başlangıç `git status --porcelain` | 420 girdi (kullanıcının mevcut kirli worktree'si) | Kaydedildi |
| Bitiş `git status --porcelain` | **420 girdi (değişmedi)** | PASS |
| HEAD | `094e3a86` (değişmedi) | PASS |

Parola ve token hiçbir log çıktısına, rapora veya repo dosyasına yazılmadı. Token yalnız oturum süresince
scratchpad'de tutuldu.

### Gateway route existence probe (tokensız)

| Route | HTTP | Yorum |
|---|---|---|
| `GET /api/crm/territory-management/contract` | 401 | mevcut + korumalı |
| `GET /api/crm/territory-models` | 401 | mevcut + korumalı |
| `POST /api/crm/territory-models/{id}/activate` | 401 | mevcut + korumalı |
| `POST /api/crm/territory-models/{id}/deactivate` | 401 | mevcut + korumalı |
| `POST /api/crm/territory-models/{id}/archive` | 401 | mevcut + korumalı |
| `POST /api/crm/territory-models/{id}/delete-draft` | 401 | mevcut + korumalı |
| `POST /api/crm/territory-models/{id}/nodes/{nodeId}/delete-draft` | 401 | mevcut + korumalı |
| `GET /api/crm/this-route-does-not-exist` | 404 | kontrol (401 ≠ genel davranış) |

---

## 2. Auth / Token Verification

Login: `POST /api/tenant-auth/login` · `X-Tenant-Id` header ile · payload'da **TenantId yok**.

| Check | Expected | Actual | Result |
|---|---|---|---|
| Login HTTP | 200 | **200** | PASS |
| `tenant_id` claim | `97c59330-…` | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` | PASS |
| `email` claim | hedef kullanıcı | `bestepullukcu@gmail.com` | PASS |
| `crm.territory.read` | mevcut | **True** | PASS |
| `crm.territory.model.read` | mevcut | **True** | PASS |
| `crm.territory.model.manage` | mevcut | **True** | PASS |
| `crm.territory.node.read` | mevcut | **True** | PASS |
| `crm.territory.node.manage` | mevcut | **True** | PASS |
| `crm.territory.delete` | **yok** | **False** | PASS |
| `crm.micro-zone.manage` | **yok** | **False** | PASS |
| `crm.territory.model.activate` | **yok** (FU02B yeni permission açmaz) | **False** | PASS |

**5/5 gerekli claim mevcut, 0/3 yasak claim.** Toplam 159 permission claim'i içinde yalnız beş `crm.territory.*`
anahtarı bulunmaktadır.

---

## 3. Contract Smoke

`GET /api/crm/territory-management/contract` → **HTTP 200**

| Check | Expected | Actual | Result |
|---|---|---|---|
| `moduleId` | `MOD-0151` | `MOD-0151` | PASS |
| `isReady` | true | **true** | PASS |
| `supportsLifecycleActions` | true | **true** | PASS |
| `supportsComputedExpiry` | true | **true** | PASS |
| `supportsDraftSoftDelete` | true | **true** | PASS |
| `supportsWorkflowActivation` | false | **false** | PASS |
| `supportsApprovalTrace` | false | **false** | PASS |
| `assignmentRules` / `accountAssignmentApply` / `resourceAssignments` / `evidencePack` / `importExport` | hepsi false | hepsi **false** | PASS |
| `missingRequiredReferenceSets` | boş | **boş** | PASS |
| 10 required set readiness | 10/10 ready | **10/10 ready, metadataReady=true** | PASS |
| `permissions` | 5 anahtar | 5 anahtar (delete/micro-zone yok) | PASS |
| `runtimeScope` | FU01+FU02+FU02A+FU02B | dördü de listeleniyor | PASS |

**Contract smoke: 8/8 hedef flag doğru.** Contract seviyesinde FU02B "hazır" diyor — asıl uyuşmazlık aşağıda,
lifecycle çalıştırıldığında ortaya çıkıyor.

---

## 4. Positive Lifecycle Smoke

Model M1: `SMOKE-MOD0151-LIFE-20260728074528` · `countryScope=tr` · 2027-01-01 → 2027-12-31 ·
id `33fd7e03-37c1-4777-be17-eae0834bce3c`

| Step | Endpoint | Expected | Actual | Result |
|---|---|---|---|---|
| Draft model create | `POST /territory-models` | 201, draft | **201**, storedStatus=draft, isExpired=false | PASS |
| Country node | `POST /{id}/nodes` | 201 draft | **201** (`TR-C-…`) | PASS |
| Zone node | `POST /{id}/nodes` | 201 draft | **201** (`TR-Z-…`, parent=country) | PASS |
| MicroZone node + profile | `POST /{id}/nodes` | 201 draft | **201** (`TR-M-…`, MicroZoneProfile kabul edildi) | PASS |
| Hierarchy list | `GET /{id}/nodes` | 3 node, hepsi draft | **3 node — draft,draft,draft** | PASS |
| **Activate** | `POST /{id}/activate` | 200, model active | **200** | PASS |
| Get after activate | `GET /{id}` | storedStatus=active | **active** | PASS |
| Node lifecycle sync | `GET /{id}/nodes` | 3 node active | **3/3 active** | PASS |
| **Deactivate** | `POST /{id}/deactivate` | 200, inactive | **HTTP 400** — `Lifecycle reference values are not published.` | **FAIL** |
| Get after deactivate | `GET /{id}` | inactive | **active** (değişmedi) | **FAIL** |
| Node deactivation sync | `GET /{id}/nodes` | 3 inactive | **0/3 inactive** | **FAIL** |
| **Archive inactive** | `POST /{id}/archive` | 200, archived | **HTTP 409** — model hâlâ active olduğu için ön koşul sağlanmıyor (deactivate FAIL'inin ardıl etkisi) | **FAIL** |
| Get after archive | `GET /{id}` | archived | **active** | **FAIL** |
| Node archive sync | `GET /{id}/nodes` | 3 archived | **0/3** | **FAIL** |

> Activation **çalışıyor** ve node senkronizasyonu doğru. Zincir **deactivate adımında kırılıyor**.

---

## 5. Negative Lifecycle Smoke

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Active model `delete-draft` | 400/409, kayıt korunur | **409** | PASS |
| Active model `archive` (deactivate'siz) | 400/409 | **409** | PASS |
| Active model node `delete-draft` | 400/409 | **409** | PASS |
| **Overlap guard** — aynı country, çakışan tarih, ikinci modeli activate | 409 | **409** | PASS |
| Overlap sonrası M1 bozulmadı | active kalır | **active** | PASS |
| Overlap sonrası M2 draft kalır | draft | **draft** | PASS |
| Archived model `PUT` (update) | 400/409 | **409** | PASS |
| Archived node `PUT` (update) | 400/409 | **409** | PASS |
| Expired model activate | 409 | **409** — `An expired territory model cannot be activated.` | PASS |
| Draft model `delete-draft` | 200 + default listeden kaybolur | **200**, listeden yok, `GET` → **404** (hard delete yok) | PASS |
| Draft node `delete-draft` | 200 + hierarchy'den kaybolur | **200**, 2 node → 1 node | PASS |

Overlap testi M2 = `SMOKE-MOD0151-OVL-20260728074528`, `countryScope='TR'` (**büyük harf varyantı**),
2027-06-01 → 2027-08-31. Guard'ın **case-insensitive country normalizasyonu** ve **tarih penceresi kesişimi**
mantığı canlı ortamda doğrulanmıştır.

### Doğrulanamayan alt senaryo

| Senaryo | Durum | Neden |
|---|---|---|
| Business Unit scope **sıra-bağımsız** (reverse BU order) overlap | **BLOCKED (by design)** | `business-unit` reference value seti bu tenant'ta **publish değil** (`published-values` → HTTP 400). Backend fail-closed davrandığı için `BusinessScopes` ile model oluşturulamıyor. Bu, FU02A'nın zaten bilinen açık follow-up'ıdır. Overlap guard **boş BU seti** üzerinden (set-eşitliği geçerli hâli) doğrulanmıştır. |

---

## 6. Computed Expiry Smoke

Model M5: `SMOKE-MOD0151-EXP-20260728074528` · 2020-01-01 → 2020-12-31 · id `db1da0c0-…`

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Geçmiş `EffectiveTo` ile draft create | 201 | **201** | PASS |
| `isExpired` | true | **true** | PASS |
| `computedStatus` | expired | **expired** | PASS |
| `storedStatus` korunur | draft | **draft** | PASS |
| Read işlemi DB'yi mutate etmiyor | evet | tekrar okumada hâlâ **draft** | PASS |
| Background scheduler yok | status kendiliğinden değişmiyor | **değişmedi** | PASS |
| Expired model activate reddi | 409 | **409** | PASS |
| **Computed-expired model archive** | 200 (pack §22.1: expired → archived) | **HTTP 400** — `Lifecycle reference values are not published.` | **FAIL** |

Computed expiry **okuma tarafında tamamen doğru çalışıyor**. Ancak "computed-expired kayıt arşivlenebilir"
kuralı archive defect'i nedeniyle uygulanamıyor.

---

## 7. Root Cause — Lifecycle Vocabulary Mismatch

Yedi FAIL'in tamamı **tek bir kök nedene** iniyor: FU02B lifecycle kodunun kullandığı status sözcükleri,
MOD-0048'de tenant'a **publish edilmiş** MOD-0151 status sözlüğüyle örtüşmüyor. Handler'lar fail-closed
davranıp 400 döndürüyor — yani **kod doğru davranıyor, sözleşme yanlış**.

Kaynak: `execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-values.json`
(toplam **73 value** — tenant'ın publish edilmiş **73/73** değeriyle birebir; contract'ın `actualValueCount`
sayıları da 6 ve 4 ile eşleşiyor).

| Reference set | Publish edilmiş değerler | FU02B'nin ihtiyacı | Sonuç |
|---|---|---|---|
| `territory-model-status` (6) | draft · review · approved · active · **superseded** · archived | `inactive` | ❌ **YOK** |
| `territory-node-status` (4) | draft · active · inactive · **ended** | `archived` | ❌ **YOK** |

Kod tarafındaki kontrol (`TerritoryLifecycleHandlers.cs`):

| Aksiyon | Doğrulanan değer | model-status | node-status | Sonuç |
|---|---|---|---|---|
| `activate` | `active` | ✅ var | ✅ var | **200 — çalışıyor** |
| `deactivate` | `inactive` | ❌ **yok** | ✅ var | **400 fail-closed** |
| `archive` | `archived` | ✅ var | ❌ **yok** | **400 fail-closed** |

Bu yalnızca "eksik publish" değil, **semantik bir tasarım uyuşmazlığıdır**: node sözlüğü tarihsel son durum için
`ended` kullanıyor, model sözlüğü ise pasif durum için `inactive` yerine `superseded`/`review`/`approved`
(workflow-odaklı, FU06 sözlüğü) içeriyor. Dolayısıyla karar yalnız operatöre değil, MOD-0151 pack'ine aittir.

**Neden testler yeşildi?** FU02B implementation raporundaki 63/63 + 232/232 test, sahte (fake) reference
validator ile çalışıyor ve her değeri `Valid` kabul ediyor. Bu nedenle uyuşmazlık **yalnız canlı ortamda**
görünür hâle geldi — bu smoke'un varlık sebebi tam olarak budur.

### Çözüm seçenekleri (bu task'ta uygulanmadı — karar pack sahibinindir)

| # | Seçenek | Etki |
|---|---|---|
| A | `territory-model-status`'a `inactive`, `territory-node-status`'a `archived` değerlerini **ekleyip yeniden publish et** | MOD-0048 operator aksiyonu + pack §16 güncellemesi; en hızlı yol |
| B | FU02B kodunu mevcut sözlüğe **hizala** (node arşivi için `ended`, model pasifi için ayrı bir değer) | Runtime kod değişikliği; UI/RESX/test etkisi |
| C | Pack §22.1 lifecycle state machine'ini ve §16 reference önerisini **birlikte** gözden geçir | En doğru yol; A veya B'yi bilinçli seçmeyi sağlar |

---

## 8. Audit / Log Evidence

| Event | Görüldü mü? | Not |
|---|---|---|
| `territory.model.activated` | **NOT CAPTURED** | Kod yolu çalıştı (activate 200) ama log dosyasına yazılmadı |
| `territory.model.activation_rejected` | **NOT CAPTURED** | Kod yolu 2 kez çalıştı (overlap 409, expired 409) |
| `territory.model.delete_rejected` | **NOT CAPTURED** | Kod yolu çalıştı (active delete-draft 409) |
| `territory.model.soft_deleted` | **NOT CAPTURED** | Kod yolu çalıştı (M3 delete-draft 200) |
| `territory.node.soft_deleted` | **NOT CAPTURED** | Kod yolu çalıştı (node delete-draft 200) |
| `territory.model.deactivated` | **Hayır** | Aksiyon 400 aldı; event üretilmedi |
| `territory.model.archived` | **Hayır** | Aksiyon 400/409 aldı; event üretilmedi |
| `territory.node.delete_rejected` | **NOT CAPTURED** | Kod yolu çalıştı (active node delete-draft 409) |

`logs/Crm-watch-out.log` ve `logs/Crm-watch-err.log` **2026-07-26'dan beri güncellenmemiş ve boş**; çalışan
CrmService süreci structured log'u dosyaya yazmıyor. Bu nedenle audit event'leri **görülmedi ve PASS olarak
işaretlenmemiştir** — davranış yalnız API cevap seviyesinde kanıtlanmıştır. Bu, ayrı bir gözlemlenebilirlik
follow-up'ıdır (log sink yapılandırması).

---

## 9. Side Effects / Data State

Bu smoke tenant'ta gerçek kayıtlar oluşturdu. Hard delete yasak olduğu ve deactivate/archive **defect nedeniyle
çalışmadığı** için temizlik tam yapılamadı.

| Kayıt | Kod | Son durum | Not |
|---|---|---|---|
| M1 | `SMOKE-MOD0151-LIFE-20260728074528` | **ACTIVE (3 active node ile)** | ⚠️ **Kilitli** — API ile pasifleştirilemiyor/arşivlenemiyor |
| M2 | `SMOKE-MOD0151-OVL-20260728074528` | draft (+1 node) | Zararsız |
| M3 | `SMOKE-MOD0151-DEL-20260728074528` | soft-deleted | Beklenen |
| M4 | `SMOKE-MOD0151-NODE-20260728074528` | draft (1 node; 1 node soft-deleted) | Beklenen |
| M5 | `SMOKE-MOD0151-EXP-20260728074528` | draft / computed-expired (+1 node) | Beklenen |

> ⚠️ **Operasyonel uyarı:** M1 tenant `97c5`'te **tek aktif territory model** durumundadır ve
> `countryScope=tr` + 2027-01-01…2027-12-31 penceresiyle çakışan **başka hiçbir modelin aktifleştirilmesine
> izin vermez** (overlap guard doğru çalıştığı için). Bu kilit, §7'deki reference/lifecycle düzeltmesi
> yapıldıktan sonra `deactivate` → `archive` ile normal yoldan çözülecektir. Mongo elle düzenlenmemiştir.

---

## 10. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | **No** (git status 420 → 420, HEAD `094e3a86` sabit) |
| Backend / frontend changed? | **No** |
| Gateway route changed? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 reference publish changed? | **No** (yalnız okundu) |
| Module pack changed? | **No** |
| Mongo hand-edit used? | **No** |
| Gateway-only? | **Yes** (business API için direct `:5061` çağrısı yok; yalnız health) |
| `X-Tenant-Id` header kullanıldı? | **Yes** |
| TenantId payload gönderildi mi? | **No** |
| Workflow approval çalıştı mı? | **No** |
| Submit/approve/reject çalıştı mı? | **No** |
| Assignment / resource / evidence / import-export çalıştı mı? | **No** |
| Brand Scope eklendi mi? | **No** |
| Product/Brand master touched? | **No** |
| Account/Contact touched? | **No** |
| Hard delete kullanıldı mı? | **No** |
| Active kayıt silindi mi? | **No** |
| Forbidden permission eklendi mi? | **No** |
| Background job eklendi mi? | **No** |

---

## 11. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-retry-2026-07-25.md` | **Created** | Bu retry kanıt raporu |

Repo başlangıçta kullanıcının mevcut değişiklikleri nedeniyle kirliydi (420 girdi). Bu çalışma onlara
dokunmadı; yalnız yukarıdaki rapor eklendi.

---

## 12. Final Verdict

### **FAIL**

**Sayısal sonuç: 40 kontrolden 33 PASS / 7 FAIL.**

Task'ın PASS ölçütleriyle karşılaştırma:

| PASS koşulu | Durum |
|---|---|
| Login başarılı | ✅ **PASS** (401 → 200; önceki blocker çözüldü) |
| Gateway smoke tamam | ✅ **PASS** (gateway-only, TenantId payload yok) |
| Contract flags doğru | ✅ **PASS** (8/8) |
| `activate` çalışıyor | ✅ **PASS** |
| `delete-draft` (model + node) çalışıyor | ✅ **PASS** |
| Overlap guard çalışıyor | ✅ **PASS** |
| Computed expiry doğrulandı | ✅ **PASS** (okuma tarafı) |
| Hard delete yok | ✅ **PASS** |
| Workflow approval yok | ✅ **PASS** |
| Guardrails korunuyor | ✅ **PASS** |
| **`deactivate` çalışıyor** | ❌ **FAIL** — HTTP 400 |
| **`archive` çalışıyor** | ❌ **FAIL** — HTTP 400 |

İki zorunlu koşul sağlanmadığı için verdict **FAIL**'dir.

**Ancak bu, önceki denemeden niteliksel olarak farklı bir FAIL'dir.** Önceki raporda hiçbir şey
doğrulanamamıştı; burada zincirin **tamamı çalıştırıldı**, lifecycle'ın büyük kısmı canlı ortamda
**kanıtlandı** ve geriye kalan boşluk **tek, net, tekrarlanabilir ve düzeltilebilir bir kök nedene**
indirgendi. FU02B'nin operasyonel closeout'u bu düzeltme yapılana kadar açık kalır.

**FU03'e geçilmemelidir.** FU03 assignment rule'ları aktif bir territory modeli üzerinde çalışır; model
yaşam döngüsünün geri dönüşü (deactivate/archive) olmadan FU03 üstüne inşa edilecek zemin eksik olur.
Ayrıca §9'daki kilitli M1 kaydı temizlenemez durumdadır.

---

## 13. Next Recommended Prompt

`@orchestrator MOD-0151 FU02B — Lifecycle Status Vocabulary Reconciliation`

Kapsam: MOD-0151 pack §16 (reference proposal) ve §22.1 (lifecycle state machine) arasındaki uyuşmazlığı kapat.
`territory-model-status` sözlüğünde `inactive`, `territory-node-status` sözlüğünde arşiv karşılığı bulunmuyor;
FU02B `deactivate`/`archive` handler'ları bu iki değeri publish edilmiş kabul ederek fail-closed 400 dönüyor.
§7'deki A/B/C seçeneklerinden birini pack sahibi kararıyla seç, uygula, ardından bu retry smoke'unu yeniden
çalıştırıp kilitli `SMOKE-MOD0151-LIFE-20260728074528` modelini normal yoldan deactivate → archive ile temizle.

Bu düzeltme PASS olduktan **sonra** sıradaki feature: `MOD-0151 FU03 — Assignment Rules + Preview`.

İkincil (bloklamayan) follow-up'lar:

1. **Log sink** — CrmService structured lifecycle audit event'leri dosyaya yazmıyor (§8); gözlemlenebilirlik
   kanıtı üretilemiyor.
2. **FU02A `business-unit` publish** — value seti publish olmadığı için BU-scope'lu overlap senaryosu ve
   Alpha/Beta seçimi canlı doğrulanamıyor (§5).
