# MOD-0048 — Publish Consent Alignment Reference Sets (2026-08-03)

> **Görev tipi:** MOD-0048 governance / authoring-UI alignment publish. **Runtime yok · migration yok · UI yok · seed/grant yok · registry yok · Mongo hand-edit yok.**
> **Sonuç:** **PARTIAL** — 6 set **oluşturuldu + değerleri yazıldı + validate + submit** (governanceState=`Submitted`, approvalState=`Pending`). **Publish, maker-checker SoD nedeniyle tamamlanmadı** (`sod_submitter_cannot_approve`); operatör kararı: **"submitted bırak"** — onay/publish'i ayrı bir **checker** kimlik yapacak. Publish artık FU02 için blocker değil.

---

## 1. Preflight

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | SoT reconciliation PASS mı? | ✅ [mod-0164-mod-0048-...-source-of-truth-...](mod-0164-mod-0048-consent-vocabulary-source-of-truth-reconciliation-2026-08-03.md) |
| 2 | `Runtime = SoT` raporda açık mı? | ✅ |
| 3–7 | Template valid / 33 set / value 224 / legal-basis 7 / source 8 runtime canonical | ✅ (§6) |
| 8 | Operator script value'ları template'ten mi okuyor (hardcode yok)? | ✅ yalnız set kodları listeli |
| 9–13 | Gateway-only · Idempotency-Key · TOKEN env · login/parola yok · DRY_RUN/NO_PUBLISH | ✅ |
| 14–15 | Scope yalnız 6 consent set; campaign/knowledge/frequency/concept/brand-product scope dışı | ✅ (offline 19/19) |
| — | Fleet | ✅ 5000/5057/5061 = 200 |
| — | Token | Operatör kendi tenant-auth login'iyle sağladı (parola operatörde; Claude parola girmedi). Token dosyaya yazıldı, iş bitince **silindi**. `sub=bestepullukcu@gmail.com`, tenant `97c5…`, izinler: businessreferencedata create/version.create/update/validate/submit/approve/publish/publishoverride. |

---

## 2. Dependency Confirmation

| Kaynak | Durum |
|---|---|
| MOD-0164/0048 SoT reconciliation | PASS — legal-basis/source runtime canonical'a hizalı |
| MOD-0164-FU02 runtime | PASS/canlı/65-65; **in-domain** → bu publish **blocker değil** |
| MOD-0048/PSS-012 API | Snake_case sözleşme doğrulandı (`set_code`, `scope_type`, value: `code/label/description/is_active/sort_order/parent_value_code/attributes`); yanıtlar `{data,…}` zarfında; **maker-checker SoD** aktif |

---

## 3. Scope Confirmation

**Yapıldı:** offline validation (19/19); DRY_RUN; create-set + draft-version + values + validate + submit (6 set); SoD tespiti; state read-back; token temizliği; evidence report.
**Yapılmadı (kapsam dışı + karar gereği):** publish (SoD + operatör "submitted bırak") · publish-override (mevcut ama kullanılmadı) · runtime · migration · UI · seed/grant · registry · Mongo.

---

## 4. Publish Set List (created + submitted; NOT published)

| Set | Değer | setId | activeDraftVersionId | governanceState / approvalState |
|---|---|---|---|---|
| `consent-channel` | 9 | b95ffb2a-084b-438a-b633-78efdea84080 | a6bf0b29-124c-45c4-bdb8-1c8e8504b3c1 | Submitted / Pending |
| `consent-purpose` | 9 | a13f4d23-4b21-404d-869e-7ee8020ed7a0 | 4057089a-f4fa-48f8-9b6e-67433c53a18f | Submitted / Pending |
| `consent-legal-basis` | 7 | f9a73dc0-797b-485c-a2cd-22f0c7c11653 | 199c0c71-8f63-4ffe-b339-a40017cb9a21 | Submitted / Pending |
| `consent-status` | 6 | 00317856-fc86-4e6f-baca-be2a66ee0ff0 | cdc9c1d1-9bcc-4e7b-9b94-d2daf2a0b06b | Submitted / Pending |
| `preference-type` | 8 | aa8c6ff2-5426-4831-9629-96bef35042dd | 3cd61db0-b115-4682-82c7-ff2d187828e5 | Submitted / Pending |
| `consent-source` | 8 | 0057b8c5-2844-4103-9d68-a501eca2aba4 | 62e61004-d206-46c8-b156-6f129c54a158 | Submitted / Pending |

Her set için `publishedVersionId = null`. Set-level `status = Draft`; version-level `businessReferenceDataGovernanceState = Submitted`, `businessReferenceDataApprovalState = Pending`, `isEditable = false` (submit sonrası kilitli).

## 5. Excluded Set List

Publish edilmeyen: `preference-value` (design-open) · `campaign-*` · `visit-frequency-*` · `knowledge-*` · `concept-*` · `product-*`/`brand-*` · `therapeutic-area` · `atc-code`. Offline validation ile scope'ta olmadıkları doğrulandı; runtime'da hiçbiri oluşturulmadı/değiştirilmedi.

## 6. Runtime Canonical Value Confirmation

Draft'lara yazılan değerler runtime in-domain canonical ile birebir (offline 19/19 + submit'e giden PUT değerleri):
- **consent-channel (9):** visit, email, sms, phone, whatsapp, portal, digital-detailing, training, other
- **consent-purpose (9):** campaign, medical-visit, product-information, training, marketing, service, compliance, research, other
- **consent-legal-basis (7):** explicit-consent, contract, legal-obligation, legitimate-interest, public-interest, vital-interest, other — `consent`/`public-task` **yok** ✅
- **consent-status (6):** granted, denied, withdrawn, restricted, unknown, expired
- **preference-type (8):** preferred-channel, do-not-contact, do-not-visit, preferred-visit-window, language-preference, content-preference, frequency-cap, topic-interest
- **consent-source (8):** subject-declared, field-capture, portal, consent-center, legacy-import, contract-document, manual, other — eski `import`/`external-consent-center`/`campaign`/`system` **yok** ✅

## 7. Operator Script / Publish Method

[`mod-0048-publish-consent-required-reference-sets.operator.js`](mod-0048-publish-consent-required-reference-sets.operator.js) — Gateway-only, TOKEN env, login/parola yok, Idempotency-Key (submit/approve/publish), `DRY_RUN`/`NO_PUBLISH` modları. Bu koşuda düzeltilenler (canlı sözleşmeye uyum): yanıt `{data,…}` zarfı unwrap; create-set + values gövdeleri **snake_case**; read-back value kodlarını yazar. Değerleri template'ten okur (hardcoded vocab yok).

## 8. Validation Before Publish

Offline (token'sız) **19/19 PASS**: JSON valid · scope tam 6 · `preference-value` dışı · legal-basis == runtime 7 · source == runtime 8 · `consent`/`public-task` yok · `explicit-consent`/`public-interest`/`other` var · eski source değerleri yok · excluded aileler scope dışı · value sayıları 9/9/7/6/8/8. DRY_RUN: 6 set "would CREATE" (hiçbiri mevcut değildi).

## 9. Publish Execution

Sıra (her set): create-set **200/201** → create draft-version **200/201** → PUT values **200** → validate **200** → submit **200** → approve **409 `sod_submitter_cannot_approve`** → (publish çalıştırılmadı). SoD kontrolü submitter'ın (`bestepullukcu`) kendi submit'ini onaylamasını engelledi. **Operatör kararı: "submitted bırak"** — publish-override (token'da yetki var) **bilinçli kullanılmadı**; onay+publish ayrı bir checker kimliğe bırakıldı.

**Sonuç:** 6 set + tüm değerler kalıcı, `Submitted/Pending` durumda; publish yok.

## 10. Read-back Verification

- `GET /sets` → 6 set mevcut, `publishedVersionId=null`.
- `GET /versions/{legal-basis-draft}` → `governanceState=Submitted`, `approvalState=Pending`, `isEditable=false`.
- `GET /sets/consent-legal-basis/published-values` → **HTTP 400** (published sürüm yok — beklenen). Published read-back, publish tamamlanınca (checker sonrası) koşulmalı.

## 11. Idempotency / Duplicate Guard

- Create-set idempotent: script önce `GET /sets` ile mevcut arar; bu koşuda 6'sı da yoktu → tek sefer oluşturuldu. **Duplicate set yok.**
- Submit/approve çağrılarında benzersiz `Idempotency-Key` kullanıldı.
- **Re-run uyarısı (dürüst):** submit sonrası draft `isEditable=false` (kilitli). Publish/approval öncesi script tekrar koşulursa set'ler mevcut bulunur (duplicate yaratmaz) ama kilitli draft'a `PUT values` **başarısız** olur — yani re-run duplicate üretmez, sadece putValues'ta durur. Temiz re-run için ya checker approve+publish etmeli, ya da yeni bir draft version açılmalı.

## 12. Runtime Dependency Note

```
Bu publish MOD-0164-FU02 için blocker DEĞİLDİR.
FU02 runtime in-domain validation ile zaten PASS (65/65).
Bu publish yalnız MOD-0048 governance, authoring-UI ve future reference-data alignment içindir.
```

## 13. Explicit Exclusions

Runtime code · consent/preference behavior · evaluate provider · migration · UI · seed/grant · registry · Mongo hand-edit · hard delete · destructive rename · campaign/knowledge runtime · visit/route planning · workflow/approval değişikliği — **hiçbiri yapılmadı**. Publish ve publish-override **yapılmadı**.

## 14. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0048-publish-consent-alignment-reference-sets-2026-08-03.md` | **Oluşturuldu** (bu rapor) |
| `docs/audits/mod-0048-publish-consent-required-reference-sets.operator.js` | **Güncellendi** (envelope unwrap + snake_case gövde + read-back kodları) |

**Runtime durum değişikliği (MOD-0048 reference data, tenant 97c5):** 6 consent set + draft version + değerleri **oluşturuldu ve submit edildi** (Gateway/PSS-012 üzerinden; Draft/Submitted, published değil). Runtime kod/config/gateway/RBAC/registry **değişmedi**. Token dosyası (`C:\tmp\consent-token.txt`) **silindi**.

## 15. Final Verdict

### **PARTIAL**

- 6 consent alignment set **oluşturuldu**, değerleri **runtime canonical** ile yazıldı, **validate + submit** başarılı (Submitted/Pending).
- **Publish tamamlanmadı** — maker-checker **SoD** (`sod_submitter_cannot_approve`); operatör bilinçli olarak **"submitted bırak"** dedi (checker ayrı kimlikle bitirecek). Bu, görevin PARTIAL kriterine uyar ("gerçek publish operatör/kimlik nedeniyle tamamlanmadı").
- `consent-legal-basis` runtime canonical **7** value ile; `consent-source` runtime canonical **8** value ile hazırlandı. `preference-value` publish edilmedi. Campaign/Knowledge/Frequency/Concept/BrandProduct **publish edilmedi**.
- Gateway-only + Idempotency-Key; duplicate set üretilmedi; read-back publish-öncesi durumu doğruladı; runtime/UI/seed/registry/Mongo değişmedi; publish-override kullanılmadı.

**FAIL değil:** `consent`/`public-task` yok · yanlış/eski değer publish edilmedi · yabancı set publish edilmedi · runtime değişmedi · destructive değişiklik/hard delete yok · duplicate yok · read-back yapıldı.
**PASS değil:** setler `published` durumuna gelmedi (SoD onayı bekliyor).

---

## 16. Next Recommended Prompt

Bu publish FU02 için blocker olmadığından ana hat devam edebilir:

```
MOD-0165-FU04 — Campaign / Targeting Runtime + Static Target Snapshot Implementation
```

6 consent set'inin **published** duruma gelmesi istenirse (governance/authoring-UI için), ayrı bir **checker** kimlik (submit etmeyen, `Version.Approve`+`Version.Publish` yetkili) ile:

```
MOD-0048 — Approve & Publish Submitted Consent Alignment Versions (checker identity)
```

> Alternatif break-glass: submitter'ın token'ındaki `Version.PublishOverride` ile `publish-override` (audit'e `IsOverrideAction=true` + `override_reason` işlenir). Bu koşuda operatör **kullanmamayı** seçti.
