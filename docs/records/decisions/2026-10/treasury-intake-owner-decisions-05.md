---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-05
status: approved — D45–D46 (2026-10-27) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-27
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); soru kutusu cevapları (2026-10-27), dört sorunun dördünde önerilen seçenek
scope: Treasury intake — `-04.md` (D41–D44; PR #265 ile main'de) devamı; 2. dalga (geliştirmeye hazır revizyon + onay) engelleri
bound_to: dal `chore/ct/treasury-intake-ct-04`, taban origin/main 7c57e8466
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları 5 (2026-10-27)

Önceki: `treasury-intake-owner-decisions-01.md` (D0–D8), `-02.md` (D9–D35), `-03.md` (D36–D40), `-04.md` (D41–D44).

## D45 — 2. dalga öncesi dört sahip kararı (soru kutusu, hepsi önerilen seçenek)

1. **MOD-0217 denetim kategorisi:** `AuditCategory.MasterData` (Platform'da var, değer 14 —
   `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs@7c57e8466`). MOD-0217 MDM servisinde; yeni Platform
   değeri istenmez. `Finance = 18` MOD-0217'ye uygulanmaz (sahip denetimi 2026-10-27). MOD-0217 pack "AuditCategory değeri" engeli
   bu kararla kapanır; pack revizyonu 2. dalgada.
2. **MOD-0217 O-11 `OTHER`:** sözleşme türü listesinde `OTHER` kalır (D41/8'in yedi türüne ek). Liste sonradan paket revizyonuyla
   daraltılabilir.
3. **MOD-0289 kod alma izni:** verildi — D27/5 ile dört finans paketine verilen izinle aynı koşullarda: kod yalnız onaylı
   (`ready-for-dev` + CT-APR-01 satırı main'de) paket sürümüyle ve dilim sırasıyla yazılır; kaynak koddaki sabit kiracı taşınmaz.
   Bu karar onay satırının yerine geçmez.
4. **Treasury kuyruk kaydı:** `execution/delivery/ct-queue-registry.tsv` `treasury-intake` satırının `owner_confirmation` alanı
   `confirmed` olur (bugün `pending`). Satır değişikliği 2. dalganın tek PR'ına girer (kart S-77 (b)); ayrı PR açılmaz.

## D46 — Rol şablonları (rol → izin anahtarı; sahip onayı, 2026-10-27)

Sahip CT önerisini olduğu gibi onayladı ("Rol şablonu önerisini onaylıyorum"). Kaynak: altı paketin Authorization Convention
tabloları (main @ 7c57e8466); anahtarlar paketlerde karar verilmiş durumdadır (D11/4, D23, D41/1). Bu karar MOD-0126 O-5,
MOD-0119 O-20, MOD-0120 O-15 ve MOD-0124 rol şablonu maddelerinin sahip payını kapatır; Auth'ta kayıt MOD-0018 sahibinin (Ali)
işidir (D9). Görev ayrılığı (SoD R1–R4) her kayıtta çalışma anında ayrıca denetlenir.

Ali'nin sorusu (2026-10-27): `mdm.bank-accounts.read-tenant-wide` açık atamalı listeye girer — yeni karar değil, MOD-0289 D23/1.

### Hiçbir rol şablonunda yer almayanlar (yalnız açık atama — ExplicitGrantOnlyPermissions)

- finance-treasury.payment-batches.read-tenant-wide
- finance-treasury.ap-invoices.read-tenant-wide
- finance-treasury.receivable-invoices.read-tenant-wide
- finance-treasury.cash-positions.read-tenant-wide
- mdm.bank-accounts.read-tenant-wide
- mdm.bank-accounts.read-unmasked
- mdm.contracts.read-tenant-wide

### Finance & Treasury (finance-treasury.*)

| Rol şablonu | İzinler |
|---|---|
| Hazine görüntüleyici | payment-batches.read, payment-evidence.read, payment-exceptions.read, payment-trace.read, ap-invoices.read, ap-invoice-trace.read, receivable-invoices.read, receivable-trace.read, cash-positions.read, cash-position-threshold-policies.read |
| Ödeme hazırlayıcı | görüntüleyici + payment-batches.create, .update, .delete, .validate, .submit, payment-evidence.link, payment-attestations.create |
| Ödeme onaylayıcı | görüntüleyici + payment-batches.approve, .reject, .request-evidence |
| Ödeme onaylayıcı (2. kademe) | ödeme onaylayıcı + payment-batches.tier2-approve |
| Ödeme serbest bırakıcı | görüntüleyici + payment-batches.release, .reconcile |
| Hazine yöneticisi | yukarıdaki ödeme rollerinin hepsi + payment-batches.bulk-delete, .waive-sod |
| Borç (AP) operatörü | görüntüleyici + ap-invoices.create, .update, .delete, .validate, .submit, ap-invoice-exceptions.create, ap-invoice-evidence.link |
| Borç (AP) onaylayıcı | görüntüleyici + ap-invoices.approve, .reject, .request-info |
| Borç (AP) yöneticisi | AP operatörü + AP onaylayıcı + ap-invoices.hold, .release-hold, .reconcile, .bulk-delete, ap-invoice-exceptions.resolve, .reopen |
| Alacak (AR) operatörü | görüntüleyici + receivable-invoices.create, .update, .delete, .issue, .collection-manage, receivable-payments.record, receivable-adjustments.request |
| Alacak (AR) onaylayıcı | görüntüleyici + receivable-adjustments.approve, .reject, .reconcile |
| Alacak (AR) yöneticisi | AR operatörü + AR onaylayıcı + receivable-adjustments.tier2-approve, receivable-invoices.bulk-delete |
| Nakit pozisyonu hazırlayıcı | görüntüleyici + cash-positions.create, .update, .delete, .submit, .attach-evidence, .export-evidence, .export-reconcile, cash-position-threshold-policies.create, .update, .delete |
| Nakit pozisyonu onaylayıcı | görüntüleyici + cash-positions.approve, .reject, .publish, .reconcile, .workflow-reconcile, cash-positions.bulk-delete, cash-position-threshold-policies.bulk-delete |

### Ana veri (mdm.*)

| Rol şablonu | İzinler |
|---|---|
| Banka verisi görüntüleyici | mdm.banks.read, mdm.bank-accounts.read, mdm.bank-account-purposes.read |
| Banka verisi yöneticisi | görüntüleyici + mdm.banks.create, .update, mdm.bank-accounts.create, .update, mdm.bank-accounts.sensitive-reads.read |
| Sözleşme görüntüleyici | mdm.contracts.read |
| Sözleşme yöneticisi | görüntüleyici + mdm.contracts.create, .update, .amendments.create, .renewal-decisions.create, .submit-approval, .record-signature, .terminate, .archive, mdm.contracts.value.read |

Bağımlı servis okumaları (ör. hazine rollerinin MDM banka / şirket okuması) MOD-0119 O-20'ye göre şablona dahil edilir:
Hazine ve AP/AR rollerine mdm.bank-accounts.read ve mdm.banks.read eklenir (maskeli).

## Karar olmayanlar

BL-565 servisi ve çağrı sözleşmesi, `Finance = 18`'in main'e girmesi, MOD-0048 seed'leri, MOD-0149 pack revizyonu, DCP-011 onayı
(Beste); Auth kaydı (rol şablonlarının içeriği D46 ile verildi), MOD-0023 şablon kodları (O-3 / O-8 / O-11 / O-20), MOD-0220 `ApprovalStatus`
(Ali); supplier 1.3.0 (MVP-2).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
