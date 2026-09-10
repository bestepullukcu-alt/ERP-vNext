# MOD-0048 Operator Authoring — Contact Availability Reference Sets (2026-08-02)

**Görev tipi:** Yalnız MOD-0048 reference-data authoring. Kod / backend / frontend / gateway / RBAC değişimi **yok**.
**Kapsam kararı:** Kullanıcı talimatı "**kaydet, publish'i ben yapacağım**" — 3 set + value'ları **Draft** olarak kaydedildi; **publish yapılmadı** (operatöre bırakıldı, SoD korunur).

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Gateway 5000 | ✅ `/health` 200 |
| Platform 5057 | ✅ `/health` 200 |
| CRM 5061 | ✅ `/health` 200 |
| Auth 5056 | ✅ tenant-auth mevcut |
| Tenant claim | ✅ Token `tenant_id = 97c59330-dbc4-4665-b29c-0c26dbb5cc93` (doğrulandı; ilk denemede header'sız login dev-bypass ile platform tenant …0001'e düşmüştü, `X-Tenant-Id` header'ı ile düzeltildi) |
| Authoring izinleri | ✅ `platform.businessreferencedata.create`, `.version.create`, `.version.update` token'da mevcut |
| Publish yolu | Tümü **Gateway 5000** üzerinden `/api/v1/reference-data/*`. Direct service-port business call yok. |
| Payload TenantId | Gönderilmedi — tenant claim/header'dan çözüldü |
| Baseline | `GET /sets` → `contact-availability-*` **yok** (doğrulandı) |

Token, kullanıcının kendi çalıştırdığı tenant-auth login ile alındı (parola operatör tarafından girildi; Claude parola girmedi).

## 2. Target Tenant

`97c59330-dbc4-4665-b29c-0c26dbb5cc93` — kullanıcı `bestepullukcu@gmail.com` (bu tenant'ta rol: Admin / DocumentMasterRegisterLinker / GQD / QADocumentation).

## 3. Saved Reference Sets (Draft — publish pending)

| set_code | name | scope_type | status | activeDraftVersionId | publishedVersionId |
|---|---|---|---|---|---|
| contact-availability-type | Contact Availability Type | tenant | **Draft** | 9c1489d5-556a-4e53-9fe1-3fb5a54d833f | null |
| contact-availability-source | Contact Availability Source | tenant | **Draft** | 13f2d0d4-5007-4c6f-9fa6-99c8d74d12a5 | null |
| contact-availability-status | Contact Availability Status | tenant | **Draft** | 4047773f-c251-4b8a-a956-b43b3efd763e | null |

setId'ler: type=`53894cd4-a0a6-4526-a955-b4875c45c53f`, source=`6398833b-e55b-46fa-8d46-ea20a6604271`, status=`37ac94a4-c322-46ea-8886-b4499d5c32f7`.
Draft version'lar pristine durumda: `governanceState=Draft`, `approvalState=NotStarted`, `isEditable=true` → operatör submit→approve→publish yürütebilir.

## 4. Values

**contact-availability-type (7):** working-hours, visiting-hours, preferred-window, restricted-window, appointment-only, temporary-exception, other
**contact-availability-source (7):** manual, legacy-import, contact-confirmed, account-confirmed, field-observation, campaign-input, other
**contact-availability-status (3):** active, inactive, archived

- Stable, kebab-case code'lar; her set içinde duplicate yok.
- `is_active=true`, `sort_order` 10..70 artan.
- Display label İngilizce (MOD-0048 value modeli tek `label` alanı taşır; 7-dil lokalizasyonu bu API yüzeyinde yok — consumer tarafı RESX/L10n ile yapılır).

## 5. Save Verification

Her set için Gateway üzerinden geri okundu:
- `GET /api/v1/reference-data/sets` → 3 set mevcut, `status=Draft`, `scopeType=tenant`, `activeDraftVersionId` dolu, `publishedVersionId=null`.
- `GET /api/v1/reference-data/versions/{versionId}/values` → value sayıları 7 / 7 / 3, code'lar yukarıdaki listeyle birebir.
- HTTP: CreateSet 201, CreateVersion 201, ReplaceValues 200 (her set için).

**Publish doğrulaması: YOK** — bilinçli. Setler Draft; `publishedVersionId=null`. Publish operatör (kullanıcı) tarafından yapılacak.

## 6. MOD-0150-FU Unblock Confirmation

**Henüz açılmadı (publish bekliyor).** ContactAvailability validation, `GetPublishedValuesAsync` published değer bulamadığında fail-closed 400 döndürmeye devam eder; setler Draft olduğu sürece published-values boştur. Operatör 3 seti publish ettiğinde blok kalkar. Bu FU'nun pozitif canlı smoke'u publish sonrası koşulmalı.

## 7. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | No |
| Backend/frontend changed? | No |
| Gateway changed? | No |
| RBAC seed/grant changed? | No |
| MOD-0150 code changed? | No |
| MOD-0151 code changed? | No |
| Route/visit/frequency scope opened? | No |
| Patient data opened? | No |
| Hard delete used? | No |
| Mongo hand-edit? | No (yalnız MOD-0048 operator API / Gateway; okuma amaçlı mongoexport yapıldı, yazma yok) |
| Direct 5061 business call? | No (authoring tümü Gateway 5000) |
| TenantId payload? | No (claim/header) |
| Reference sets **published**? | **No — Draft olarak kaydedildi, publish operatöre bırakıldı** |

## 8. Final Verdict

**PARTIAL** — Görevin kendi kriterine göre: *"publish doğrulandı ama MOD-0150-FU smoke henüz koşturulmadı"* satırının bu göreve uyarlanmış hâli: **3 set + tüm value'lar Draft olarak kaydedildi ve doğrulandı; publish bilinçli olarak yapılmadı (kullanıcı talimatı: "publish'i ben yapacağım"), dolayısıyla MOD-0150-FU pozitif smoke hâlâ reference-missing nedeniyle bloklu.** Kod/config/gateway/RBAC değişmedi.

FAIL değil: set'ler doğru tenant'a, doğru code'larla, duplicate'siz kaydedildi.
PASS değil: `status=published` koşulu sağlanmadı (kasıtlı — publish sende).

## 9. Next Recommended Prompt

Operatör (kullanıcı) 3 seti publish ettikten sonra:

```
MOD-0150-FU — Contact Availability Positive Live Smoke Retry
```

Publish için operatör adımı (her set draft version'ı için, Gateway üzerinden): submit → approve → publish (Idempotency-Key gerekli), veya BRD operatör UI `Platform/ReferenceData` → ilgili set → draft version → publish akışı.
