# MOD-0164 / MOD-0048 — Consent Vocabulary Source-of-Truth Reconciliation (2026-08-03)

> **Görev tipi:** Documentation / operator-aid alignment. **Runtime code yok · MOD-0048 publish yok · migration yok · re-smoke yok · UI yok · seed/grant yok · registry yok · Mongo hand-edit yok.**
> **Karar:** **Runtime = Source of Truth.** Shipped MOD-0164-FU02 in-domain vocabulary canonical kabul edildi; MOD-0048 alignment template ve governance dokümanları buna hizalandı.
> **Sonuç:** **PASS** (gerekçe §14).

---

## 1. Preflight

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | MOD-0164-FU02 PASS mı? | ✅ Evet ([memory: mod0164-fu02-consent-preference-runtime]) |
| 2 | Authenticated smoke 65/65 PASS mı? | ✅ Evet (operatör paylaştı) |
| 3 | FU02 runtime vocabulary gerçekten in-domain mi? | ✅ `ConsentPreferenceContract.cs:123` ("validated in-domain (structural); MOD-0048 publish is out of FU02 scope") + `:155` |
| 4 | MOD-0048 publish FU02 için gerçek blocker değil mi? | ✅ Değil (in-domain; visit-frequency deseni) |
| 5 | `consent-legal-basis` runtime ≠ template? | ✅ Farklıydı (F7) — bu task'ta hizalandı |
| 6 | `consent-source` runtime ≠ template? | ✅ Farklıydı (F8) — bu task'ta hizalandı |
| 7 | F7/F8 divergence önceki raporda doğru yakalanmış mı? | ✅ [publish-readiness raporu](mod-0048-publish-consent-required-reference-sets-for-mod-0164-fu02-2026-08-03.md) §6 |
| 8 | Runtime kod değişmeyecek mi? | ✅ Değişmedi |
| 9 | MOD-0048 publish yapılmayacak mı? | ✅ Yapılmadı |
| 10 | Yalnız documentation / operator-aid alignment mı? | ✅ Evet |

**Runtime kaynak teyidi (birebir alıntı):**
- `ConsentLegalBasis.All` — `ConsentRecord.cs:312-325` = `explicit-consent, contract, legal-obligation, legitimate-interest, public-interest, vital-interest, other` (7)
- `ConsentSource.All` — `ConsentRecord.cs:413-427` = `subject-declared, field-capture, portal, consent-center, legacy-import, contract-document, manual, other` (8)

---

## 2. Dependency Confirmation

| Kaynak | Durum |
|---|---|
| MOD-0164-FU02 runtime | **PASS / canlı / 65-65** — canonical vocabulary kaynağı |
| Publish-readiness raporu (2026-08-03) | PARTIAL — F7/F8 yakalandı, publish bilinçli yapılmadı |
| Governance reconciliation (F1) | Bu task ile **superseded** (F1 legal-basis kararı runtime lehine geçersiz) |
| MOD-0164-FU01 §12 boundary legal-basis vocab | Bu task ile **superseded** (override notu §9) |

---

## 3. Scope Confirmation

**Yapıldı:** SoT kararının uygulanması; template `consent-legal-basis` + `consent-source` value listelerinin runtime'a hizalanması; `_status`/`_resolution` güncellemesi; `_meta` SoT notu; operator script temizlik doğrulaması; boundary override notu; publish-readiness güncellemesi; validation checks; evidence report.
**Yapılmadı (kapsam dışı):** runtime code · MOD-0048 publish · migration · re-smoke · UI · seed/grant · registry · Mongo hand-edit · hard delete · consent/preference behavior change · evaluate provider change.

---

## 4. Source-of-Truth Decision

**`Runtime = Source of Truth`.**

Gerekçe: FU02 runtime shipped ve authenticated smoke PASS; runtime in-domain validation ile çalışıyor; MOD-0048 publish FU02 için blocker değil; runtime değerlerini değiştirmek kod + migration + re-smoke gerektirir (canlı kayıtlar `explicit-consent`/runtime source taşıyor). En düşük riskli yol: **documentation + MOD-0048 alignment artifact'lerini runtime'a hizalamak** — bu task tam olarak bunu yaptı.

**Alternatif B (Boundary = SoT) uygulanmadı:** runtime sabitleri `consent`/`public-task`'a çevrilmedi · kod değişmedi · migration yok · canlı kayıt migrate edilmedi · re-smoke gerektiren runtime change açılmadı.

---

## 5. F7 — Consent Legal Basis Reconciliation

**Runtime canonical (kabul edildi):** `explicit-consent · contract · legal-obligation · legitimate-interest · public-interest · vital-interest · other`

**Önceki F1/boundary değerleri (`consent`, `public-task`):** shipped runtime ile uyumsuz → **publish-ready DEĞİL**, kullanılmayacak.

Yapılanlar:
1. Template `consent-legal-basis` value listesi runtime canonical 7 değere **hizalandı** (birebir, sıra dahil).
2. `_status: blocked-runtime-divergence` → **`resolved-runtime-sot`**.
3. `_resolution` eklendi: *Runtime accepted as SoT · explicit-consent/public-interest/other retained · consent/public-task not used by shipped runtime*.
4. Boundary override notu (§9) yazıldı.
5. Eski F1 kararının runtime ile çeliştiği açıkça kayıt altına alındı.
6. `consent` ve `public-task` publish-ready kabul **edilmedi**.

---

## 6. F8 — Consent Source Reconciliation

**Runtime canonical (kabul edildi):** `subject-declared · field-capture · portal · consent-center · legacy-import · contract-document · manual · other`

**Önceki template değerleri (`import`, `external-consent-center`, `campaign`, `system`):** runtime'da yok → **publish-ready DEĞİL**, çıkarıldı.

Yapılanlar:
1. Template `consent-source` value listesi runtime canonical 8 değere **hizalandı**.
2. `_status` → **`resolved-runtime-sot`**.
3. `_resolution` eklendi: *Runtime accepted as SoT · runtime source values retained · older template values not publish-ready*.
4. `import`, `external-consent-center`, `campaign`, `system` publish-ready listeden **çıkarıldı**.
5. Açıklama: `consent-center` runtime değeri önceki `external-consent-center` yerine kullanılır; `subject-declared`, `field-capture`, `contract-document` runtime canonical.

---

## 7. MOD-0048 Template Update

`docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` — operator-aid, publish/seed değil:
- `consent-legal-basis`: 6 → **7 value** (runtime canonical); status `resolved-runtime-sot`.
- `consent-source`: 7 → **8 value** (runtime canonical); status `resolved-runtime-sot`.
- `_meta.reconciliation_2026_08_03`: `source_of_truth` alanı + F7/F8 resolved notları eklendi.
- Diğer 31 set (channel/purpose/status/preference-type/campaign-*/visit-frequency-*/knowledge-*/concept-*/product-*/brand-*) **değişmedi**.
- `notAuthoredAsReferenceSet` (therapeutic-area, atc-code) **değişmedi**.

**Value sayısı:** 222 → **224** (+2: legal-basis +1, source +1). JSON parse **valid**; 33 set korunuyor.

---

## 8. Operator Script Alignment

`docs/audits/mod-0048-publish-consent-required-reference-sets.operator.js` — **değişiklik gerekmedi**:
- Script yalnızca **set kodlarını** listeler (`PUBLISH_SCOPE`); tüm value'ları **template'ten okur** → runtime canonical değerler otomatik yansır.
- Hardcoded legal-basis/source value **yok** (grep ile doğrulandı).
- Login/parola **yok**; `TOKEN` env'den okunur; Gateway-only; Idempotency-Key korunur; `DRY_RUN` + `NO_PUBLISH` modları korunur.
- Publish yine **operatöre** ait; bu task publish etmedi.

---

## 9. Boundary Alignment / Override Note

**Seçim: boundary audit dosyası doğrudan DEĞİŞTİRİLMEDİ; override bu raporda kayıt altına alındı.** Gerekçe: `mod-0164-consent-preference-management-boundary-pack-authorization-2026-08-02.md` tarihli bir denetim kanıtıdır; geriye dönük düzenlemek audit bütünlüğünü zedeler. Bunun yerine ileriye dönük açık override:

> **OVERRIDE (2026-08-03, Runtime SoT reconciliation):** MOD-0164-FU01 §12 legal-basis vocabulary kararı (`consent` · `legitimate-interest` · `contract` · `legal-obligation` · `vital-interest` · `public-task`) ve F1 governance reconciliation'ın buna dayalı `consent-legal-basis` kararı, **shipped MOD-0164-FU02 runtime vocabulary'si tarafından supersede edilmiştir**. MOD-0048 alignment için **runtime canonical** değerler (`explicit-consent, contract, legal-obligation, legitimate-interest, public-interest, vital-interest, other`) kullanılır. Aynı şekilde `consent-source` için runtime `ConsentSource.All` canonical'dir. Bu override geriye dönük audit kaydını **silmez**; boundary/F1 kararları tarihsel bağlamda geçerli kalır, ileriye dönük canonical runtime'dır.

---

## 10. Publish Readiness After Reconciliation

| Sınıf | Setler |
|---|---|
| **Publish-ready — Required** (runtime canonical) | `consent-channel` (9) · `consent-purpose` (9) · `consent-legal-basis` (7) · `consent-status` (6) · `preference-type` (8) |
| **Publish-ready — Optional** | `consent-source` (8) |
| **Still not publish** | `preference-value` (design-open, non-blocker) |
| **Consent publish scope dışı** | `campaign-*` · `visit-frequency-*` · `knowledge-*` · `concept-*` · `product-*`/`brand-*` · `therapeutic-area` · `atc-code` |

> **Kritik not:** Bu publish artık **MOD-0164-FU02 için blocker DEĞİL** (runtime in-domain). Yalnızca **governance / authoring-UI tutarlılığı** için opsiyoneldir. İstenirse ayrı operator task ile yürütülür.

---

## 11. Validation Checks

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | legal-basis template == runtime canonical (birebir) | ✅ |
| 2 | source template == runtime canonical (birebir) | ✅ |
| 3 | `consent` publish-ready listede yok | ✅ |
| 4 | `public-task` publish-ready listede yok | ✅ |
| 5 | `explicit-consent` var | ✅ |
| 6 | `public-interest` var | ✅ |
| 7 | `other` legal basis içinde var | ✅ |
| 8 | `subject-declared` source içinde var | ✅ |
| 9 | `field-capture` source içinde var | ✅ |
| 10 | `consent-center` source içinde var | ✅ |
| 11 | `contract-document` source içinde var | ✅ |
| 12 | eski `external-consent-center` source içinde yok | ✅ |
| 13 | eski `campaign` source içinde yok | ✅ |
| 14 | eski `system` source içinde yok | ✅ |
| 15 | Runtime kod değişmedi | ✅ |
| 16 | MOD-0048 publish yapılmadı | ✅ |
| 17 | Operator script token/login güvenliği korunuyor | ✅ |
| 18 | JSON parse valid | ✅ |
| 19 | 33 set korunuyor; value 222→224 (legal +1, source +1) | ✅ açıklandı |
| 20 | Campaign/Knowledge/Frequency/Concept/BrandProduct setleri değişmedi | ✅ |

(1–14 ve 18–19 `node` ile programatik doğrulandı.)

---

## 12. Explicit Exclusions

Runtime code change · migration · MOD-0048 publish · seed/grant · registry write · UI · Mongo hand-edit · hard delete · backend/frontend/gateway change · consent/preference behavior change · evaluate provider change · campaign/knowledge runtime · visit/route planning · re-smoke gerektiren değişiklik — **hiçbiri yapılmadı**.

---

## 13. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0164-mod-0048-consent-vocabulary-source-of-truth-reconciliation-2026-08-03.md` | **Oluşturuldu** (bu rapor) |
| `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` | **Güncellendi** (legal-basis + source runtime canonical'a hizalandı; `_status: resolved-runtime-sot`; `_meta.source_of_truth`) |

Değişmedi: operator script (kontrol edildi, temiz), boundary audit dosyası (override bu raporda), runtime kod, config, gateway, RBAC, reference data (publish), registry, MOD-0164-FU02 implementation.

---

## 14. Final Verdict

### **PASS**

- **Runtime = SoT kararı uygulandı.**
- `consent-legal-basis` runtime canonical 7 değere **hizalandı** (`explicit-consent`/`public-interest`/`other` var; `consent`/`public-task` yok).
- `consent-source` runtime canonical 8 değere **hizalandı** (eski `import`/`external-consent-center`/`campaign`/`system` çıkarıldı).
- MOD-0048 template JSON **valid** (33 set, 224 value).
- Operator script eski vocab **hardcode etmiyor** (template'ten okuyor; doğrulandı).
- Boundary **override notu** yazıldı (audit bütünlüğü korunarak).
- Publish readiness **güncellendi** (runtime canonical; FU02 blocker değil).
- Runtime code · migration · MOD-0048 publish · UI · seed/grant/registry/Mongo — **hiçbiri yok**.

FAIL kriterlerinin hiçbiri tetiklenmedi (runtime değişmedi · publish yok · Boundary=SoT yanlışlıkla uygulanmadı · `consent`/`public-task` publish-ready bırakılmadı · runtime canonical template'e yansıtıldı · migration yok · JSON valid).

---

## 15. Next Recommended Prompt

```
MOD-0165-FU04 — Campaign / Targeting Runtime + Static Target Snapshot Implementation
```

**Not:** MOD-0048 publish artık MOD-0164-FU02 için **blocker değildir**. Publish istenirse, yalnızca governance/UI authoring alignment amacıyla ayrı operator task olarak açılır:

```
MOD-0048 — Publish Consent Alignment Reference Sets
```
