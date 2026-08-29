# MOD-0048 — Publish Consent Required Reference Sets for MOD-0164-FU02 (2026-08-03)

> **Görev tipi:** Consent runtime ön koşulu olan reference set **publish** task'ı (yalnız MOD-0048/PSS-012 publish; runtime/UI/seed/grant/registry/Mongo yok).
> **Sonuç:** **PARTIAL — publish BİLİNÇLİ OLARAK YAPILMADI.** İki bağımsız neden: (1) task'ın önermesi ("FU02'yi unblock etmek için publish şart, yoksa fail-closed 400") **shipped implementasyona göre yanlış** — FU02 vocabulary'i **in-domain** doğruluyor ve zaten canlı/yeşil (65/65 smoke); (2) hazır MOD-0048 değerleriyle publish etmek **canlı runtime ile çelişen** ikinci bir doğruluk-kaynağı yaratırdı (`consent-legal-basis` ve `consent-source` runtime'dan farklı). Publish öncesi vocab reconciliation gerekiyor (yeni F7/F8).

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Reconciliation raporu PASS mı? | ✅ [mod-0048-crm-reference-set-governance-reconciliation-2026-08-03](mod-0048-crm-reference-set-governance-reconciliation-2026-08-03.md) = PASS |
| Authoring template JSON geçerli mi? | ✅ 33 set / 222 value (node parse OK) |
| Publish scope'unda `blocked-pending-reconciliation` kaldı mı? | ⚠️ Publish-readiness kontrolünde **iki set yeni `blocked-runtime-divergence`** oldu (`consent-legal-basis`, `consent-source`) — §6 |
| Publish scope yalnız consent-required/optional mı? | ✅ 5 required + 1 optional; §4 |
| Knowledge/Campaign/Frequency/Concept/BrandProduct yanlışlıkla scope'ta mı? | ✅ Hayır (offline validation ile teyit) |
| MOD-0048/PSS-012 authoring/publish akışı doğrulandı mı? | ✅ `BusinessReferenceDataController.cs` — create-set → create-version → PUT values → validate → submit → approve → publish (ayrı izinler = maker-checker SoD; publish Idempotency-Key) |
| Gateway route üzerinden mi? | ✅ Yalnız Gateway 5000 `/api/v1/reference-data/*` (fleet health: 5000/5057/5061/5056 = 200) |
| Idempotency-Key? | ✅ Operator script her lifecycle çağrısında üretir |
| Tenant scope? | Hedef `97c59330-dbc4-4665-b29c-0c26dbb5cc93` (CRM tenant-scoped) |
| Existing set → duplicate yerine reconcile? | ✅ Operator script `GET /sets` → varsa reuse, yoksa create; draft version'da additive replace |
| **Token elde etme** | ❌ Authoring/publish tenant-scoped Bearer token gerektirir; token yalnız **parola ile login**'den gelir. Parola girerek authenticate etmek **yasak** → publish'i ben yürütemem (operatör kendi login'iyle yürütür). Salt-okunur probe: token'sız `GET /sets` = **401** (dev-bypass reference-data'yı auth'suz geçirmiyor). |

---

## 2. Dependency Confirmation

| Kaynak karar | Durum |
|---|---|
| Authoring Plan | PARTIAL (inventory + governance) |
| Governance Reconciliation | PASS (F1 & F6 resolved; F2/F3/F4/F5 non-blocking) |
| **MOD-0164-FU02 Consent Runtime** | ⚠️ **ZATEN CANLI ve 65/65 PASS** (operatörün paylaştığı authenticated smoke). Bu, publish'in FU02 için ön koşul **olmadığını** kanıtlar (§5). |
| F1 consent-legal-basis | Reconciliation'da "resolved/publish-ready" idi → **§6'da REOPENED** (runtime divergence) |

---

## 3. Scope Confirmation

**Yapıldı:** offline validation (6 set), MOD-0048 API sözleşme doğrulaması, operator publish script'i (idempotent, non-destructive), runtime doğrulama davranışının kod incelemesi, iki kritik divergence bulgusu, evidence report.
**Yapılmadı (kapsam dışı + bilinçli):** MOD-0048 publish · runtime code · backend/frontend/gateway change · seed/grant · registry write · UI · Mongo hand-edit · hard delete · destructive rename.

---

## 4. Publish Set List

| Set | Değer sayısı | RequiredLevel | Offline validation |
|---|---|---|---|
| `consent-channel` | 9 | required | ✅ (runtime ile **birebir**) |
| `consent-purpose` | 9 | required | ✅ (runtime ile **birebir**) |
| `consent-legal-basis` | 6 | required | ⚠️ **runtime'dan farklı** (§6) |
| `consent-status` | 6 | required | ✅ (runtime ile **birebir**) |
| `preference-type` | 8 | required | ✅ (runtime ile **birebir**) |
| `consent-source` | 7 | optional | ⚠️ **runtime'dan farklı** (§6) |

Tüm offline yapısal kontroller (kebab-case, duplicate yok, isDeprecated=false, attributes.description mevcut, legal-basis'te `explicit-consent`/`public-interest`/`other` YOK — F1'e göre) **geçti**. Ancak bu "geçiş" F1 reconciliation değerlerine göredir; §6 bunların **canlı runtime ile çeliştiğini** gösterir.

## 5. Excluded Set List

Publish edilmeyen: `preference-value` (design-open, non-blocker) · tüm `campaign-*` · `visit-frequency-*` · `knowledge-*` · `concept-*` · `product-*`/`brand-*` · `therapeutic-area` (ConceptNode) · `atc-code` (external taxonomy). Offline validation bunların hiçbirinin scope'ta olmadığını doğruladı.

---

## 6. Canonical Value Confirmation — ⚠️ İKİ RUNTIME DIVERGENCE

MOD-0164-FU02 vocabulary'i **in-domain** doğruluyor (kaynak: `ConsentPreferenceContract.cs:123` — *"consent/preference vocabulary is validated in-domain (structural); MOD-0048 publish is out of FU02 scope"*; `:155` — *"authoring is ready without a MOD-0048 publish"*). Runtime sabitlerini hazır MOD-0048 değerleriyle karşılaştırdım:

| Set | Shipped runtime in-domain (SoT, canlı) | MOD-0048 template / F1 reconciliation | Sonuç |
|---|---|---|---|
| `consent-channel` | visit, email, sms, phone, whatsapp, portal, digital-detailing, training, other | aynı | ✅ **eşleşiyor** |
| `consent-purpose` | campaign, medical-visit, product-information, training, marketing, service, compliance, research, other | aynı | ✅ **eşleşiyor** |
| `consent-status` | granted, denied, withdrawn, restricted, unknown, expired | aynı | ✅ **eşleşiyor** |
| `preference-type` | preferred-channel, do-not-contact, do-not-visit, preferred-visit-window, language-preference, content-preference, frequency-cap, topic-interest | aynı | ✅ **eşleşiyor** |
| **`consent-legal-basis`** | **explicit-consent, contract, legal-obligation, legitimate-interest, public-interest, vital-interest, other** (`ConsentRecord.cs:312-325`) | consent, contract, legal-obligation, legitimate-interest, vital-interest, public-task | ❌ **ÇELİŞKİ (F7)** |
| **`consent-source`** | **subject-declared, field-capture, portal, consent-center, legacy-import, contract-document, manual, other** (`ConsentRecord.cs:413-427`) | manual, import, legacy-import, external-consent-center, campaign, system, other | ❌ **ÇELİŞKİ (F8)** |

**F7 — consent-legal-basis inversiyonu:** Shipped runtime, F1 reconciliation'ın **"eklenmesin"** dediği `explicit-consent`/`public-interest`/`other`'ı **kullanıyor** ve F1'in **"kullanılsın"** dediği `consent`/`public-task`'ı **içermiyor**. Yani mühendislik, MOD-0164-FU01 boundary'sinin GDPR-purist terimleri yerine task'ın **orijinal** önerisini shipledı. F1 reconciliation ile canlı runtime **taban tabana zıt**.

**F8 — consent-source uyumsuzluğu:** Tamamen farklı value uzayı.

**Neden publish etmedim:** Runtime in-domain doğruladığı ve canlı SoT olduğu için, MOD-0048 setini F1 değerleriyle publish etmek → authoring UI'nin `consent`/`public-task` sunması ama runtime'ın bunları **400 reddetmesi** demek olurdu (iki doğruluk-kaynağı defekti). Runtime değerleriyle publish etmek ise F1 governance kararını sessizce ezmek olurdu. Her ikisi de yanlış; doğru olan **durup kararı yükseltmek**.

---

## 7. Publish Method

Hazır (operatör çalıştırır): [`docs/audits/mod-0048-publish-consent-required-reference-sets.operator.js`](mod-0048-publish-consent-required-reference-sets.operator.js) — Node, bağımlılıksız (yerleşik `http`). Gateway-only, Idempotency-Key'li, idempotent (existing set reuse, draft'ta additive replace, hard delete yok, value code stable). `DRY_RUN=1` ve `NO_PUBLISH=1` (save-as-draft, SoD-safe) modları var. Token'ı `TOKEN` env'den okur — **login yapmaz**; operatör tenant-auth login (parola operatörde) ile token'ı export eder.

> **NOT:** Script şu an template'teki değerleri publish eder. F7/F8 çözülmeden bu script `consent-legal-basis`/`consent-source` için **çalıştırılmamalı**. Eşleşen 4 set (channel/purpose/status/preference-type) istenirse güvenle publish edilebilir (runtime ile birebir), ama FU02 açısından **gereksizdir** (in-domain).

## 8. Validation Results

Offline yapısal doğrulama (token gerektirmez) — **6/6 set, 0 hata**: kebab-case set/value code · duplicate yok · beklenen value sayıları (9/9/6/6/8/7) · `consent-legal-basis` yalnız F1 canonical 6 · `explicit-consent`/`public-interest`/`other` F1'e göre yok · `preference-value` scope dışı · campaign/knowledge/frequency/concept/product scope dışı · isDeprecated=false · attributes.description mevcut · displayName tek alan.
**Runtime-alignment doğrulaması (kod incelemesi):** 4 set eşleşiyor, 2 set çelişiyor (§6).

## 9. Idempotency / Duplicate Guard

Operator script: `GET /sets` → mevcut set reuse (duplicate create yok); yeni **draft** version + `PUT values` (published version'a dokunmaz); publish/submit/approve'da benzersiz `Idempotency-Key`; hard delete/destructive rename yok. Re-run duplicate üretmez. **Canlı doğrulama publish yapılmadığı için koşulmadı.**

## 10. Read-back Verification

**Yapılamadı** — token gerektirir (token'sız `GET /sets` = 401). Publish yapılmadığı için read-back da yok. Operatör publish'i çalıştırınca script otomatik `GET /sets/{code}/published-values` read-back'i basar.

## 11. Runtime Dependency Guard

**Beklenen davranış (task önermesi):** "bu setler publish değilse FU02 fail-closed 400 verir." **Gerçek:** FU02 **in-domain** doğruluyor (`ConsentPreferenceContract.cs:123/155`) — setler publish olmasa da FU02 çalışır ve **zaten canlı/65-65 yeşil**. Yani bu task'ın kaldırmayı hedeflediği blocker **hiç var olmadı** (visit-frequency ile aynı desen). Publish artık runtime ön koşulu değil, MOD-0048 **alignment/governance** adımıdır — ve F7/F8 nedeniyle şu an alignment da bozuk.

## 12. Explicit Exclusions

MOD-0048 publish · runtime code · backend/frontend/gateway change · seed/grant · registry write · UI · Mongo hand-edit · hard delete · destructive rename · Consent/Campaign/Knowledge runtime · visit/route planning — **hiçbiri yapılmadı**.

---

## 13. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0048-publish-consent-required-reference-sets-for-mod-0164-fu02-2026-08-03.md` | **Oluşturuldu** (bu rapor) |
| `docs/audits/mod-0048-publish-consent-required-reference-sets.operator.js` | **Oluşturuldu** (operator publish script; runtime kod değil) |
| `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` | **Güncellendi** (`consent-legal-basis` ve `consent-source` → `_status: blocked-runtime-divergence` + F7/F8 kanıt notları; value dizileri değişmedi) |

Runtime kod, config, gateway, RBAC, reference data (publish), registry **değiştirilmedi**.

---

## 14. Final Verdict

### **PARTIAL** — publish bilinçli olarak yapılmadı; iki yeni blocker (F7/F8) yükseltildi.

**Tespitler:**
- **FU02 runtime in-domain doğruluyor** → consent set publish'i FU02 için **ön koşul değil**; FU02 zaten canlı ve **65/65 PASS** (task önermesi geçersiz).
- **F7 (consent-legal-basis):** shipped runtime (`explicit-consent`/`public-interest`/`other`) ↔ F1 reconciliation (`consent`/`public-task`) **taban tabana zıt**. Publish edilirse iki doğruluk-kaynağı defekti oluşur.
- **F8 (consent-source):** runtime vocab ↔ template vocab uyumsuz.
- **4 set (channel/purpose/status/preference-type) runtime ile birebir** — istenirse güvenle publish edilebilir ama gereksiz.
- Offline validation 6/6 geçti; operator script + tüm operatör adımları hazır; token bariyeri (parola) nedeniyle canlı publish/read-back operatöre bağlı — ama F7/F8 çözülmeden **publish edilmemeli**.

**Neden PASS değil:** required setlerin tamamı publish edilmedi (F7/F8 + in-domain nedeniyle bilinçli); read-back yok.
**Neden FAIL değil:** yanlış/çelişkili değer publish **edilmedi**; `explicit-consent`/`public-interest`/`other` MOD-0048'e **eklenmedi**; `preference-value` publish edilmedi; hiçbir yabancı set publish edilmedi; runtime/seed/registry/UI/Mongo değişmedi; destructive değişiklik yok. Aksine, sessiz bir defekt (runtime↔MOD-0048 sapması) **yakalandı ve raporlandı**.

---

## 15. Next Recommended Prompt

Bu bir **governance/engineering hizalama** kararı gerektiriyor (F7 birincil):

```
MOD-0164 / MOD-0048 — Consent Vocabulary Source-of-Truth Reconciliation (legal-basis & source): shipped in-domain runtime vs boundary/F1 canonical
```

Karar seçenekleri (EA/owner):
- **(A)** Runtime'ı SoT kabul et → MOD-0048 alignment setlerini `explicit-consent`/`public-interest`/`other` ve runtime `consent-source` vocab'ı ile hizala; MOD-0164-FU01 §12 boundary'sini güncelle. (Kod değişmez; en düşük risk.)
- **(B)** Boundary'yi SoT kabul et → FU02 in-domain sabitlerini `consent`/`public-task`'a çevir (kod değişikliği + migration + re-smoke; canlı kayıtlar `explicit-consent` taşıyorsa veri geçişi gerekir).

Karar (A) ise ardından: eşleşen 4 set + hizalanmış 2 set için `MOD-0048 — Publish Consent Alignment Reference Sets (post-reconciliation)` — ama unutulmasın: FU02 runtime bunu **gerektirmez**, yalnız authoring-UI/governance tutarlılığı içindir.
