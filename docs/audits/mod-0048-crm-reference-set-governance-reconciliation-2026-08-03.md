# MOD-0048 — CRM Reference Set Governance Reconciliation (Consent / Campaign / Knowledge) — 2026-08-03

> **Görev tipi:** Yalnız **reconciliation / governance decision / publish-readiness**. Runtime implementation **yok**, MOD-0048 publish **yok**, seed/grant **yok**, registry write **yok**, UI **yok**, Mongo hand-edit **yok**.
> **Amaç:** [Authoring planını](mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-plan-2026-08-03.md) PARTIAL bırakan governance çakışmalarını (F1–F6) kapatmak ve **MOD-0164-FU02 Consent runtime** için reference set publish ön koşulunu netleştirmek.
> **Sonuç:** **PASS** (gerekçe §15).

---

## 1. Preflight

| Soru | Cevap |
|---|---|
| Authoring planı neden PARTIAL kaldı? | İki set vocab çakışması (`consent-legal-basis` F1, `knowledge-content-type` F2), bir set üye çakışması (`campaign-target-type` F6), iki taxonomy owner belirsizliği (`therapeutic-area` F3, `atc-code` F4) ve dil-seti farkı (F5) publish-öncesi karara bırakılmıştı. |
| Hangi setler `blocked-pending-reconciliation` idi? | `consent-legal-basis`, `campaign-target-type`, `knowledge-content-type` (template `_status` alanları). |
| Hangi setler runtime-required? | Consent: `consent-channel`, `consent-purpose`, `consent-legal-basis`, `consent-status`, `preference-type`. (Ayrıca campaign/knowledge/concept'te başka runtime-required'lar var ama Consent runtime'ını bloklamaz.) |
| Publish öncesi kesin karar gerektirenler? | F1 (`consent-legal-basis`) ve F6 (`campaign-target-type`). |
| MOD-0164-FU02 Consent runtime'ını **bloklayan** kararlar? | Yalnız **F1** (`consent-legal-basis` vocab kesinleşmeli — set runtime-required ve fail-closed 400 üretir). |
| Knowledge/Campaign runtime'a kadar **bekleyebilecek** kararlar? | F2 (knowledge-content-type), F3 (therapeutic-area), F4 (atc-code), F5 (dil expansion), F6 (campaign — Campaign runtime'a kadar). Hiçbiri Consent'i bloklamaz. |

**Kanıt dosyaları (okundu/güncellendi):** authoring planı raporu + authoring template JSON (bu task'ta `_status` alanları karar sonuçlarıyla güncellendi — publish/seed/runtime değil, operator-aid). Boundary kararları önceki task'ta doğrulanmıştı: MOD-0164-FU01 §12 (legal-basis vocab), MOD-0165-FU02 (campaign-target-type 7 değer), MOD-0162-FU01 (content-type vocab), MOD-0162-FU01C (concept + therapeutic-area ConceptNode), MOD-0290-FU01.

---

## 2. Dependency Confirmation

| Ön koşul / karar | Durum | Bu reconciliation'a etkisi |
|---|---|---|
| MOD-0164-FU01 Consent Boundary | **PASS** | legal-basis + consent vocab canonical kaynağı (F1) |
| MOD-0164-FU02 Consent Runtime | Planlı | Bu task'ın **unblock hedefi**; F1 çözümü + publish-ready liste ile açılıyor |
| MOD-0165-FU02 Campaign Boundary | **PASS** | campaign-target-type 7-değer canonical (F6) |
| MOD-0165-FU03 Visit Frequency runtime | **PASS** | in-domain vocab; frequency setleri non-blocking; `visit-frequency-target-type` ayrı kalır (F6) |
| MOD-0162-FU01 / FU01C | **PASS** | content-type vocab (F2) + therapeutic-area ConceptNode (F3) canonical kaynağı |
| MOD-0290-FU01 Brand/Product | **PASS** | atc-code external taxonomy sınırı (F4) |
| PSS-012 / MOD-0048 kuralları | Geçerli | tek `displayName`, `SetCode` immutable, hard-delete yok, deprecate-only |

---

## 3. Scope Confirmation

**Yapıldı:** F1–F6 kararları; Consent runtime publish-ready set listesi; blocked/later listesi; template `_status` güncellemesi (operator-aid); evidence report.
**Yapılmadı (kapsam dışı):** MOD-0048 publish · runtime code · backend/frontend/gateway change · seed/grant · registry write · UI · hardcoded enum migration · existing value destructive rename · delete · Mongo hand-edit · Consent/Campaign/Knowledge runtime · visit/route planning.

---

## 4. F1 — Consent Legal Basis Decision

**Karar: RESOLVED — publish-ready.**

- **MOD-0164-FU01 boundary vocab CANONICAL kabul edildi.**
- Canonical 6 değer: `consent` · `legitimate-interest` · `contract` · `legal-obligation` · `vital-interest` · `public-task`.
- `explicit-consent` **eklenmedi** → `consent` kullanılır (GDPR Art.6(1)(a) zaten "consent" dayanağıdır; "explicit" ayrımı özel-nitelikli veri için Art.9'a ait ayrı bir katmandır, flat legal-basis value'su olarak açmak yanıltıcıdır).
- `public-interest` **eklenmedi** → `public-task` kullanılır (GDPR Art.6(1)(e) resmi terimi).
- `other` **publish edilmeyecek** → legal basis kapalı bir hukuki kümedir; "other" fail-closed doğruluğu bozar ve denetim izini zayıflatır. Gerekirse **yalnız EA onayıyla** sonradan eklenir (deprecate-only kuralı korunur).
- **Sonuç:** `consent-legal-basis` set'i 6 canonical value ile **publish-ready**. Template `_status: resolved-publish-ready`.

---

## 5. F2 — Knowledge Content Type Decision

**Karar: DEFERRED — Knowledge owner'a; Consent'i bloklamaz.**

- Boundary canonical 8 değer **korunur:** `knowledge-article` · `pdf` · `html-detail` · `video` · `quiz` · `sop` · `training-material` · `message-script`.
- Task'taki ek değerler (`slide-deck`, `image`, `faq`, `clinical-summary`, `lesson`, `assignment`, `checklist`, `external-link`, `other`) **şimdi publish edilmez**; template'te `_proposedExtras` altında kalır.
- **Knowledge runtime'a geçmeden önce ayrı bir MOD-0162 owner reconciliation** yapılmalı (code çakışmaları çözülmeli: `document`↔`pdf`, `article`↔`knowledge-article`, `script`+`message`↔`message-script`). Bu, ayrı bir follow-up.
- **Bu karar MOD-0164-FU02 Consent runtime'ını BLOKLAMAZ.** Template `_status: blocked-for-knowledge-owner`.

---

## 6. F3 — Therapeutic Area Decision

**Karar: ConceptNode olarak kalır — MOD-0048 reference set açılmaz.**

- MOD-0162-FU01C `therapeutic-area`'yı subject concept graph içinde **ConceptNode** olarak modelliyor (hiyerarşi/ilişki taşıdığı için flat value'ya sığmaz).
- MOD-0048 **flat reference set açılmayacak.**
- MOD-0162 Concept Graph runtime sırasında ele alınır.
- **MOD-0164-FU02 Consent runtime'ını bloklamaz.** Template `notAuthoredAsReferenceSet` altında kalır.

---

## 7. F4 — ATC Code Decision

**Karar: External WHO taxonomy — MOD-0048 local master açılmaz.**

- `atc-code` uluslararası WHO ATC taksonomisidir; local master açmak sapan ikinci-master riski doğurur.
- **MOD-0048 local reference set olarak publish edilmez.**
- **ExternalReferences seam** üzerinden tüketilir; MOD-0290 veya ileride ayrı bir external-taxonomy pack ile ele alınır (MOD-0149 external-reference-gap kararıyla tutarlı).
- **MOD-0164-FU02 Consent runtime'ını bloklamaz.** Template `notAuthoredAsReferenceSet` altında kalır.

---

## 8. F5 — Language Display Policy Decision

**Karar: Platform canlı locale set canonical; çoklu dil consumer RESX/L10n'da; az/ka/kk/uz future follow-up.**

- MOD-0048 value modeli **tek `displayName`** taşır — API yüzeyinde çoklu-dil yoktur (contact-availability publish raporuyla doğrulandı).
- `displayName` **en-US / platform default** ile tutulur.
- Çoklu dil gösterim **consumer tarafında RESX/L10n** ile yapılır (key deseni: `ReferenceValue.<setCode>.<valueCode>`).
- **Platform canlı 7 locale (`en, fr, es, zh, ar, ru, tr`) canonical** kabul edilir. Task'ın istediği `az-AZ / ka-GE / kk-KZ / uz-UZ` platformda henüz canlı değildir.
- **az/ka/kk/uz için ayrı bir "future localization expansion" follow-up** açılır (EA/platform kararı; yeni SharedResource dil dosyaları + L10n bridge kapsamı).
- **MOD-0164-FU02 Consent runtime'ını bloklamaz.**

---

## 9. F6 — Campaign Target Type Decision

**Karar: RESOLVED — 7 boundary value; `campaign-target` çıkarıldı; setler ortaklaştırılmadı.**

- `campaign-target-type` **boundary canonical 7 değerle** kalır: `account` · `contact` · `account-contact-link` · `segment` · `territory-node` · `concept-node` · `audience-profile`.
- `campaign-target` **çıkarıldı** → bir campaign target'ının kendisini target-tip göstermesi self-referential loop riski taşır.
- `visit-frequency-target-type` **ayrı set kalır** ve `campaign-target` içerebilir (frequency `Source=campaign` senaryosu geçerlidir).
- **İki set ortaklaştırılmaz** (üye kümeleri kasıtlı farklıdır).
- **MOD-0164-FU02 Consent runtime'ını bloklamaz** (Campaign runtime'a kadar bekleyebilir; ama zaten resolved/publish-ready). Template `_status: resolved`.

---

## 10. Consent Runtime Publish-Ready Set List

**MOD-0164-FU02 öncesi publish edilmesi gereken (runtime-required, fail-closed 400 üretir):**

| Set | Value sayısı | Durum |
|---|---|---|
| `consent-channel` | 9 | **Publish-ready** |
| `consent-purpose` | 9 | **Publish-ready** |
| `consent-legal-basis` | 6 (F1 canonical) | **Publish-ready** |
| `consent-status` | 6 | **Publish-ready** |
| `preference-type` | 8 | **Publish-ready** |

**Optional ama önerilen (provenance; runtime-required değil):**
| `consent-source` | 7 | **Publish-ready (optional)** |

**Design-open — Consent runtime BLOCKER DEĞİL (C bölümü kararı):**
- `preference-value`: **publish blocker değildir.** `preference-type` Consent/Preference runtime'ı için yeterlidir. `PreferenceValue` runtime'da tipe göre **string / reference / numeric / boolean-like** olarak tasarlanır; controlled value gerekiyorsa **ileride ayrı split set** açılır. Template `_status: design-open-non-blocker`.

> **Publish blocker sayısı: 0.** F1 çözüldüğü için 5 required (+1 optional) set publish-ready; `preference-value` bilinçli olarak sonraya bırakıldı.

---

## 11. Blocked / Later Set List

| Set / karar | Sınıf | Consent blocker? |
|---|---|---|
| `preference-value` | design-open (non-blocker) | **Hayır** |
| `knowledge-content-type` | blocked-for-knowledge-owner (F2) | **Hayır** |
| `therapeutic-area` | not-authored — ConceptNode (F3) | **Hayır** |
| `atc-code` | not-authored — external taxonomy (F4) | **Hayır** |
| az/ka/kk/uz dil expansion | future follow-up (F5) | **Hayır** |
| Campaign/Knowledge/Concept diğer runtime-required setleri | ilgili runtime FU'suna kadar later | **Hayır** |

---

## 12. Template Update Summary

`docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` **operator-aid olarak güncellendi** (publish/seed/runtime değil):

- `consent-legal-basis` → `_status: "resolved-publish-ready"` + `_resolution` (F1; 6 boundary value; `explicit-consent`/`public-interest`/`other` eklenmedi).
- `campaign-target-type` → `_status: "resolved"` + `_resolution` (F6; 7 boundary value; `campaign-target` çıkarıldı; frequency set'i ayrı).
- `knowledge-content-type` → `_status: "blocked-for-knowledge-owner"` + `_resolution` (F2; boundary 8 value korundu; extras `_proposedExtras`'ta; Consent blocker değil).
- `preference-value` → `_status: "design-open-non-blocker"` + `_resolution` (C; publish blocker değil).
- `_meta.reconciliation_2026_08_03` bloğu eklendi (publish-ready / optional / non-blocking / resolved / blocked-for-other-owner / not-authored / future-followups özeti).
- `therapeutic-area` ve `atc-code` → `notAuthoredAsReferenceSet` altında **korundu** (değişmedi).

Doğrulama: JSON geçerli — **33 set / 222 value**; hiçbir value code **destructive rename** edilmedi, hiçbir value silinmedi.

---

## 13. Explicit Exclusions

MOD-0048 publish · runtime code change · backend/frontend/Gateway change · seed/grant · registry write · UI · hardcoded enum migration · existing value destructive rename · delete · Mongo hand-edit · Consent runtime · Campaign runtime · Knowledge runtime · visit planning · route planning — **hiçbiri yapılmadı**.

---

## 14. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0048-crm-reference-set-governance-reconciliation-2026-08-03.md` | **Oluşturuldu** (bu rapor) |
| `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` | **Güncellendi** (operator-aid `_status`/`_resolution` alanları + `_meta.reconciliation_2026_08_03`; value içeriği değişmedi) |

Runtime kod, config, gateway, RBAC, reference data (publish), registry **değiştirilmedi**.

---

## 15. Final Verdict

### **PASS**

- **F1 `consent-legal-basis` RESOLVED** — boundary canonical (6 değer); `explicit-consent`/`public-interest`/`other` eklenmedi; **publish-ready**.
- **F6 `campaign-target-type` RESOLVED** — 7 boundary value; `campaign-target` çıkarıldı; `visit-frequency-target-type` ayrı set kaldı; ortaklaştırma yapılmadı.
- **Consent runtime publish-ready set listesi üretildi** (5 required + 1 optional; blocker sayısı 0).
- **F2 knowledge-content-type Consent'i bloklamadan ertelendi** (MOD-0162 owner reconciliation follow-up).
- **F3 therapeutic-area ConceptNode olarak korundu**; flat reference set açılmadı.
- **F4 atc-code external taxonomy olarak korundu**; local master açılmadı.
- **F5 dil display policy netleşti** — platform canlı locale canonical, çoklu dil consumer RESX'te, az/ka/kk/uz future follow-up.
- **No publish · no runtime code · no seed/grant · no registry · no UI · no destructive change.**

FAIL kriterlerinin hiçbiri tetiklenmedi: publish yok · runtime değişmedi · seed/grant/registry yok · destructive rename yok · `campaign-target` `campaign-target-type` içinde **kalmadı** (gerekçeli çıkarıldı) · `explicit-consent` **eklenmedi** · `therapeutic-area` flat set **açılmadı** · `atc-code` local set **açılmadı**.

---

## 16. Next Recommended Prompt

```
MOD-0048 — Publish Consent Required Reference Sets for MOD-0164-FU02
```

> Kapsam: `consent-channel`, `consent-purpose`, `consent-legal-basis` (6 canonical value), `consent-status`, `preference-type` (+ optional `consent-source`) setlerini doğru tenant'a Draft→submit→approve→**publish** (Idempotency-Key). `preference-value` **publish edilmez** (design-open, non-blocker). Publish tamamlanınca MOD-0164-FU02 Consent runtime unblock olur.
