# MOD-0150 — Professional reference sets (import-ready)

Contact Create/Edit'te **Professional Title / Specialty / Department** alanları artık MOD-0048 (Business Reference Data)
published-values'tan beslenen **select2** dropdown'lardır (opsiyonel; set yayınlanmamışsa alan boş kalır — CRM local
fallback yok). Bu klasör, o üç set için **import-ready** değer dosyalarını içerir; **publish operatör tarafından
yapılır** (SoD gereği submit ≠ approve).

> **Not:** Bu dosyalar governance import formatındadır (parser: `value_code` / `display_name` / `sort_order` /
> `is_deprecated` / `attributes`). Doğrudan Mongo'ya yazılmaz; MOD-0048 governance akışından (create → import →
> validate → submit → approve → publish) geçer. Kod tarafı **hiçbir değeri seed etmez / hardcode etmez.**

## Hedef

- **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` (97c5) · **ScopeType:** `tenant`
- **Sets (hepsi opsiyonel, `blocksCreate=false`):**

| setCode | Name | Dosya | Değer sayısı |
|---|---|---|---|
| `professional-title` | Professional Title | [professional-title.import.json](./professional-title.import.json) | 8 |
| `medical-specialty` | Medical Specialty | [medical-specialty.import.json](./medical-specialty.import.json) | 22 |
| `department-type` | Department Type | [department-type.import.json](./department-type.import.json) | 11 |
| `phone-country-code` | Phone Country Code | [phone-country-code.import.json](./phone-country-code.import.json) | 46 |
| `preferred-language` | Preferred Language | [preferred-language.import.json](./preferred-language.import.json) | 12 |

> `phone-country-code` değerleri dial-code'dur (`value_code` = "+90" gibi; Contact form'da Phone alanının yanındaki
> single-select). `preferred-language` değerleri ISO 639-1 kodlarıdır (`tr`/`en`/… native-name label ile).

`display_name` tek dilli (İngilizce) — import parser tek string alır. İstersen yayınlamadan önce UI'da düzenleyebilirsin.
`valueCode`'lar lowercase-kebab (PKS/authoring konvansiyonu). Metadata boş (`{}`) — bkz. GAP-CRM-04 (aşağıda).

## Publish adımları (her set için, tenant 97c5 oturumu ile)

**MOD-0048 Business Reference Data UI (önerilen):**
1. **Create set** — `setCode` (yukarıdaki tablo), `Name`, `ScopeType = tenant`.
2. **Import** — ilgili `*.import.json` dosyasını yükle (format: JSON) → preview → commit (taslak versiyona değerler yüklenir).
3. **Validate** → **Submit**.
4. **Approve** — **farklı bir steward** ile (SoD: `sod_submitter_cannot_approve`; submitter approve edemez).
5. **Publish** → `published-values?scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93` artık değerleri döndürür; Contact
   formundaki dropdown'lar dolar.

**API alternatifi** (Gateway `http://localhost:5000`, `api/v1/reference-data`; Bearer + `X-Tenant-Id: 97c5…` gerekli):
1. `POST /sets` → `{ "setCode": "...", "name": "...", "scopeType": "tenant" }` (→ setId + draft versionId)
2. `POST /imports/preview` → `{ "targetDraftVersionId": "<draftVersionId>", "fileName": "professional-title.import.json", "format": "json", "contentBase64": "<base64(dosya)>" }` (→ previewId)
3. `POST /imports/{previewId}/commit` (header: `Idempotency-Key: <guid>`)
4. `POST /versions/{versionId}/validate` → `POST /versions/{versionId}/submit`
5. `POST /versions/{versionId}/approve` **(2. steward)** → `POST /versions/{versionId}/publish`

> `contentBase64` = dosyanın base64'ü. PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes('professional-title.import.json'))`.

## Doğrulama (publish sonrası)

```
GET /api/v1/reference-data/sets/professional-title/published-values?scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93
GET /api/v1/reference-data/sets/medical-specialty/published-values?scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93
GET /api/v1/reference-data/sets/department-type/published-values?scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93
```

Beklenen: 8 / 22 / 11 aktif değer. Contact Create → Professional bölümündeki üç select2 dolu gelir. Backend opsiyonel
doğrulama: yayınlanmamışsa tolere (201), yayınlanmış + geçersiz değer → 400.

## GAP-CRM-04 — Contact Type'a göre cascade (sonraki iş)

Bu değerler şu an düz (metadata `{}`). Professional dropdown'ların seçili **Contact Type**'a göre filtrelenmesi için,
değerlere `attributes` içinde bir `contactTypes` (ör. `"doctor,medical"`) etiketi eklenir; sonra frontend bu metadata
ile filtreler (mevcut `data-contact-professional` hook + `IReferenceMetadataReader` seam). Detay:
[mod-0149-followups-backlog.md](../audits/mod-0149-followups-backlog.md) → GAP-CRM-04. Bu taskta hardcoded mapping
**eklenmedi**.
