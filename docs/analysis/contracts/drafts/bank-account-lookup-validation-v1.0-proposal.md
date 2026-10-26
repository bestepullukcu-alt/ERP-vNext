# BANK-ACCOUNT-LOOKUP 1.0.0 — MOD-0289 banka hesabı arama / doğrulama sözleşmesi (TASLAK / öneri)

    contract       : BANK-ACCOUNT-LOOKUP
    version        : 1.0.0
    x-status       : DRAFT — öneri; MOD-0289 pack CT-APR-02 / CT-APR-01'den geçmeden ve DL-2 kabulünden önce dondurulmaz
    x-owner        : MOD-0289 Bank Master & Bank Account Registry (master-data-management, Diten.MdmService)
    x-consumers    : MOD-0126 Payments & Approvals (kaynak hesap), MOD-0119 AP, MOD-0120 AR, MOD-0124 Cash Positioning,
                     ileride MOD-0269 gerçek gönderim
    x-decisions    : D20/1 (yerleşim), D23/1 (tedarikçi kapsamı), D23/3 (şirket Active + Approved), D24 (O-4…O-12),
                     D27/1 (MOD-0003 yok → T32 sapması: sözleşme bu klasörde dondurulur) —
                     docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md
    x-pattern      : LEGAL-ENTITY-LOOKUP 1.1.0 (docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md §1–§2)
    x-source       : MOD-0289 pack Runtime Constraints → "Okuma sözleşmesi — BANK-ACCOUNT-LOOKUP 1.0.0"
                     (execution/domains/master-data-management/module-packs/MOD-0289-bank-master-bank-account-registry.md);
                     çelişkide pack kazanır, bu dosya pack ile hizalanır

Kod yoktur; uç henüz yazılmadı. Bu dosya pack'teki tanımın sözleşme hâlidir.

## 1. Uç

- `GET /api/mdm/bank-accounts/{bankAccountId:guid}/lookup-validation` — izin `mdm.bank-accounts.read`; SEC-002 veri kapsamı
  uygulanır (şirket hesapları `LegalEntityId` kapsamıyla, tedarikçi hesapları D23/1 ile).
- Tedarikçi sahipli hesapta `X-Legal-Entity-Id` başlığı zorunludur (ilişki durumu için); yoksa 400.
- Gateway rotası integration-agent işidir (`ocelot.json` bu sözleşmeyle değişmez).

## 2. 404 (gövde aynı, varlık sızdırmaz)

Hesap yok; silinmiş; başka kiracıda; çağıranın kapsamı dışında; `Status ≠ Active` (Draft, Inactive); tedarikçi sahipli
hesapta isteğin şirketiyle `Active` ilişki yok (D23/1).

## 3. 200 gövdesi (`data`)

| Alan (JSON) | Tip | Değer |
|---|---|---|
| `bankAccountId` | string (UUID) | istenen kimlik |
| `code` | string | RCS-001 kodu (`BAC`, D24/4) |
| `lifecycleState` | string | `ACTIVE` (200 yalnız aktif hesap için) |
| `referenceable` | bool | `true` (200 yalnız referans verilebilir hesap için) |
| `ownerType` | string | `LEGAL_ENTITY` \| `SUPPLIER` |
| `legalEntityId` | string (UUID) \| null | `ownerType = LEGAL_ENTITY` ise dolu, değilse `null` |
| `supplierId` | string \| null | `ownerType = SUPPLIER` ise dolu, değilse `null` (MOD-0140'ta tedarikçi kimliği string koddur) |
| `ownerState` | string | `ELIGIBLE` \| `NOT_ELIGIBLE` — sahibin o anki durumu (şirket: `Active` + `Approved`, D23/3; tedarikçi: 200 yalnız `Active` ilişkide döner, her zaman `ELIGIBLE`) |
| `ownerStateReason` | string \| null | `NOT_ELIGIBLE` ise `LEGAL_ENTITY_NOT_ACTIVE` \| `LEGAL_ENTITY_NOT_APPROVED` |
| `bankState` | string | `ACTIVE` \| `INACTIVE` (banka ya da şube pasifse `INACTIVE`) |
| `currencyCode` | string | `^[A-Z]{3}$` (MOD-0048 listesinde, D24/8) |
| `identifierType` | string | `IBAN` \| `DOMESTIC` |
| `identifierCheckLevel` | string | IBAN: `FORMAT_AND_CHECKSUM` \| `COUNTRY_FORMAT`; yurt içi: `DOMESTIC_FORMAT` \| `DOMESTIC_VERIFIED`; türle uyuşmayan değer çelişkili yanıttır |
| `maskedDisplay` | string | ör. `TR••••1234` |
| `contractVersion` | string | `bank-account-lookup/1.0` |

Gövde **IBAN, hesap numarası, son 4 hane dışındaki herhangi bir parça, BIC, banka adı, şube adı, hesap sahibi adı
taşımaz.** Tedarikçi ilişkisi okunamazsa (MOD-0140 503 / geçersiz) MDM 200 dönmez: **503**
`MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` (varsayılan `ELIGIBLE` yok).

Örnek (200):

```json
{ "data": { "bankAccountId": "0b6d3c1e-5f2a-4c7d-9e81-3a4b5c6d7e8f", "code": "GMG-CH-BAC-000042",
            "lifecycleState": "ACTIVE", "referenceable": true, "ownerType": "LEGAL_ENTITY",
            "legalEntityId": "6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7", "supplierId": null,
            "ownerState": "ELIGIBLE", "ownerStateReason": null, "bankState": "ACTIVE", "currencyCode": "CHF",
            "identifierType": "IBAN", "identifierCheckLevel": "FORMAT_AND_CHECKSUM",
            "maskedDisplay": "CH••••1234", "contractVersion": "bank-account-lookup/1.0" } }
```

## 4. Uygunluk alanları — `referenceable` tek başına yeterli değildir

`referenceable = true` yalnız hesap kaydının aktif ve silinmemiş olduğunu söyler. Tüketici E1…E9'un **hepsini** kontrol
etmeden işleme devam etmez:

| # | Alan | Tüketici kuralı |
|---|---|---|
| E1 | `bankAccountId` | istenen kimliğe eşit |
| E2 | `lifecycleState` | `ACTIVE` |
| E3 | `referenceable` | `true` |
| E4 | `bankState` | `ACTIVE` |
| E5 | `ownerType` + `legalEntityId` / `supplierId` | tutarlı ve beklenen taraf (ör. MOD-0126 kaynak hesabı: `LEGAL_ENTITY`, `legalEntityId` = ödeyen şirket) |
| E6 | `ownerState` | `ELIGIBLE` |
| E7 | `currencyCode` | biçimli ve tüketicinin kuralına uygun (ör. parti para birimine eşit) |
| E8 | `identifierType` + `identifierCheckLevel` | v1 tüketicileri: IBAN `FORMAT_AND_CHECKSUM`, yurt içi `DOMESTIC_FORMAT` yeterli; MOD-0269 gerçek gönderimi: IBAN `COUNTRY_FORMAT`, yurt içi `DOMESTIC_VERIFIED` |
| E9 | `contractVersion` | `bank-account-lookup/1.` ile başlar |

## 5. Tüketici sınıflandırması (sözleşmenin parçası)

| MOD-0289 yanıtı | Sınıf | Tüketici sonucu |
|---|---|---|
| 200 ve E1…E9'un hepsi sağlanıyor | uygun | işlem sürer |
| 200, tutarlı (E1–E3, E9 sağlanıyor, alanlar tanımlı değerlerde) ama E4–E8'den biri tüketicinin kuralını sağlamıyor | tanımlı, uygun değil | 422 |
| 404 | tanımlı, kullanılamaz | 422 |
| 200 ama `bankAccountId` yok / farklı; `referenceable = false`; `lifecycleState ≠ ACTIVE`; `ownerType` bilinmeyen; `ownerType` ile sahip alanları çelişkili; `ownerState` / `bankState` / `identifierCheckLevel` yok, `null` ya da bilinmeyen; tür ile düzey uyuşmuyor; `contractVersion` yok ya da `1.` değil; gövde ayrıştırılamıyor | geçersiz / çelişkili | 503 |
| 400, 401 / 403, 5xx (MDM 503 dahil), zaman aşımı, ulaşılamıyor | erişilemiyor | 503 |

Geçersiz ya da çelişkili yanıt **503**, tanımlı ama uygun olmayan durum **422**; hiçbir belirsiz yanıt "uygun" sayılmaz;
karşılaştırmalar büyük-küçük harfe duyarlıdır. Tüketici yanıttaki hiçbir alanı (maskeli gösterim dahil) kalıcı kaydına
kopyalamaz; yalnız opak `bankAccountId` saklanır.

## 6. Uyum fikstürleri

Her uygunluk alanı için bir **negatif fikstür** — öteki bütün alanlar uygunken yalnız o alan bozuk:

| Fikstür | Bozuk alan | Beklenen sınıf |
|---|---|---|
| F-E4 | `referenceable = true` + `bankState = INACTIVE` | 422 |
| F-E5a | `ownerType = SUPPLIER` beklenirken `LEGAL_ENTITY` | 422 |
| F-E5b | `legalEntityId` başka şirket | 422 |
| F-E5c | `ownerType = LEGAL_ENTITY` + `supplierId` dolu | 503 |
| F-E6 | `ownerState = NOT_ELIGIBLE` (`LEGAL_ENTITY_NOT_APPROVED`) | 422 |
| F-E7 | `currencyCode` parti para biriminden farklı | 422 |
| F-E8a | `FORMAT_AND_CHECKSUM`, tüketici `COUNTRY_FORMAT` istiyor | 422 |
| F-E8b | `identifierType = DOMESTIC` + `identifierCheckLevel = COUNTRY_FORMAT` | 503 |
| F-E1 | `bankAccountId` farklı | 503 |
| F-E3 | `referenceable = false` | 503 |
| F-E9 | `contractVersion = bank-account-lookup/2.0` | 503 |
| F-503 | MDM 503 `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` | 503 |

MdmService testlerinde bir başvuru sınıflandırıcısı bu fikstürleri koşar (MOD-0289 AC-42); her tüketici paketi aynı
fikstürleri kendi sınıflandırıcısına karşı koşar (o paketlerin Follow-up maddesi).

## 7. Sürüm kuralı

1.x yalnız ekleme; 404 kararını genişletmek büyük sürümdür. MOD-0003 Data Contract Registry yoktur (D27/1): dondurulunca
bu dosya `docs/analysis/contracts/bank-account-lookup.md` olur ve MOD-0003 gelince oraya kaydedilir.

## 8. Değişiklik geçmişi

- 1.0.0 (2026-10-25) — ilk yazılı sürüm; MOD-0289 pack tanımından (rev 6) çıkarıldı.
