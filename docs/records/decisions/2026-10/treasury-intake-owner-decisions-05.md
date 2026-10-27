---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-05
status: approved — D45–D52 (2026-10-27) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
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

> **YERİNE GEÇTİ — UYGULAMAYIN.** İşleyen matris: 'D47 rol matrisi (CT04a; D49 ile güncellendi)'.

Sahip CT önerisini olduğu gibi onayladı ("Rol şablonu önerisini onaylıyorum"). Kaynak: altı paketin Authorization Convention
tabloları (main @ 7c57e8466); anahtarlar paketlerde karar verilmiş durumdadır (D11/4, D23, D41/1). Bu karar MOD-0126 O-5,
MOD-0119 O-20, MOD-0120 O-15 ve MOD-0124 rol şablonu maddelerinin sahip payını kapatır; Auth'ta kayıt MOD-0018 sahibinin (Ali)
işidir (D9). Görev ayrılığı (SoD R1–R4) her kayıtta çalışma anında ayrıca denetlenir.

Ali'nin sorusu (2026-10-27): `mdm.bank-accounts.read-tenant-wide` açık atamalı listeye girer — yeni karar değil, MOD-0289 D23/1.

### (TARİHÇE — UYGULAMAYIN) Hiçbir rol şablonunda yer almayanlar (yalnız açık atama — ExplicitGrantOnlyPermissions)

- finance-treasury.payment-batches.read-tenant-wide
- finance-treasury.ap-invoices.read-tenant-wide
- finance-treasury.receivable-invoices.read-tenant-wide
- finance-treasury.cash-positions.read-tenant-wide
- mdm.bank-accounts.read-tenant-wide
- mdm.bank-accounts.read-unmasked
- mdm.contracts.read-tenant-wide

### (TARİHÇE — UYGULAMAYIN) Finance & Treasury (finance-treasury.*)

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

### (TARİHÇE — UYGULAMAYIN) Ana veri (mdm.*)

| Rol şablonu | İzinler |
|---|---|
| Banka verisi görüntüleyici | mdm.banks.read, mdm.bank-accounts.read, mdm.bank-account-purposes.read |
| Banka verisi yöneticisi | görüntüleyici + mdm.banks.create, .update, mdm.bank-accounts.create, .update, mdm.bank-accounts.sensitive-reads.read |
| Sözleşme görüntüleyici | mdm.contracts.read, platform.document-repository.read (D46a) |
| Sözleşme yöneticisi | görüntüleyici + platform.document-repository.manage (D46a) + mdm.contracts.create, .update, .amendments.create, .renewal-decisions.create, .submit-approval, .record-signature, .terminate, .archive, mdm.contracts.value.read |

Bağımlı servis okumaları (ör. hazine rollerinin MDM banka / şirket okuması) MOD-0119 O-20'ye göre şablona dahil edilir:
Hazine ve AP/AR rollerine mdm.bank-accounts.read ve mdm.banks.read eklenir (maskeli).

**D46a — ek (sahip onayı, 2026-10-27):** MOD-0217 belge yükleme / indirme çağrıları kullanıcı token'ıyla MOD-0262-FU01'e gider
(D41/10; ölçüm 2026-10-27: `DocumentRepositoryModels.cs:17,20` @7c57e8466). Bu yüzden sözleşme görüntüleyiciye
`platform.document-repository.read`, sözleşme yöneticisine ayrıca `platform.document-repository.manage` eklenir (tablo güncellendi).

## D47 — Sahip denetimi CT04 (KOŞULLU) üzerine rol ve kontrol kararları (soru kutusu, hepsi önerilen seçenek, 2026-10-27)

Onay kanıtı: onaylayan GitHub hesabı `gmg-10` (rol K1, depo sahibi); zaman 2026-10-27; oturum Treasury Intake CT
(`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); denetlenen belge `treasury-ct04-bfe32cd8b.zip`
(`25167b42eb7d0fac328d21e08e0b73ce46f55b64a86d97f4b445524b7f81c4de`). Resmi onay yine CT-APR-01 satırı ve merge'dür.

1. **Görev ayrılığı (SoD) rolleri:** "Hazine yöneticisi" işlem onayı, serbest bırakma ve muafiyet taşımaz; yalnız yapılandırma
   ve operasyonel destek. `finance-treasury.payment-batches.waive-sod` hiçbir rol şablonunda yer almaz: yalnız açık atama
   (`ExplicitGrantOnlyPermissions`), süreli, gerekçe kodlu, bağımsız onaylı; kendi kendine muafiyet yasak; her muafiyette
   gerekçe, bitiş, etkilenen işlem, onaylayan ve atlanamaz denetim olayı.
2. **Banka hesabı etkinleştirme:** ayrı "Banka hesabı etkinleştirici" rolü (`mdm.bank-accounts.activate` / `.deactivate` +
   okuma); banka verisi yöneticisinde yok. Oluşturanın / son güncelleyenin etkinleştirememesi çalışma anında ayrıca denetlenir (D23/2).
3. **Sözleşme gizliliği (D41/6'yı daraltır):** v1'e belge indirme denetimi ve ticari değer okuma denetimi eklenir (mevcut
   izinlerle, yeni alan yok). Gizlilik düzeyi, kısıtlı gruplar, dış paylaşım ve saklama / yasal tutma Follow-up kalır.
4. **Bağımlı banka okumaları:** `mdm.banks.read` / `mdm.bank-accounts.read` yalnız paketlerde banka verisini gerçekten okuyan
   akışın rollerine verilir (ölçülerek); okumayan rolden kaldırılır.
5. **İmza kaydı:** ayrı "Sözleşme imza kaydedici" rolü (`mdm.contracts.record-signature`); sözleşme yöneticisinde yok. İmzalı kopya
   belgesi zorunlu; kaydeden, sözleşmeyi oluşturan ya da onaya gönderen olamaz; düzeltme yalnız yeni değişiklik sürümüyle (silme yok).
6. **Onay kanıtı alanları:** bu kayıttan itibaren her sahip kararında onaylayan hesap, rol, zaman, oturum ve onaylanan belgenin
   hash'i yazılır; kişi adı yazılmaz.

D46 rol tablosu D47 ile güncellenir; güncel tam rol matrisi aşağıdaki "D47 rol matrisi (CT04a; D49 ile güncellendi)" bölümündedir (CT04a revizyonu, CT04b ile D49 uygulandı).

## D47 rol matrisi (CT04a; D49 ile güncellendi — işleyen tek matris)

Kaynak: altı paketin Authorization Convention tabloları bu daldaki (`chore/ct/treasury-intake-ct-04`, CT04a / CT04b) sürümleriyle —
MOD-0126 `MOD-0126-payments-approvals.md`, MOD-0119 `MOD-0119-accounts-payable.md`, MOD-0120 `MOD-0120-accounts-receivable.md`,
MOD-0124 `MOD-0124-cash-positioning.md`, MOD-0217 `MOD-0217-contract-lifecycle-management-clm.md`, MOD-0289
`MOD-0289-bank-master-bank-account-registry.md` (dört finans paketi `execution/domains/finance-treasury/module-packs/`, MOD-0217 /
MOD-0289 `execution/domains/master-data-management/module-packs/`; satır numaraları CT04b düzenlemesinden sonra kaydığı için
yazılmadı — anahtar adıyla aranır). D46 + D46a + D47 + D49 + D50'yi birleştirir; **D46 tablosunun yerine geçer** (D46 tablosu
tarihçedir — "YERİNE GEÇTİ — UYGULAMAYIN", D49/5). Anahtar adları kısaltılmadan yazıldı. "Bağımlı" sütunu başka modülün
anahtarlarıdır (kullanıcı token'ıyla yapılan çağrılar — MOD-0220, MOD-0023, MOD-0262); kaydı o modülün sahibindedir.
`procurement.*`, `crm.*` ve MOD-0031 / MOD-0140 okuma izinleri (MOD-0119, MOD-0120 Authorization Convention) bu matrisin
anahtar kapsamı dışındadır; ilgili operatör rolüne o modülün adıyla eklenir. Auth'ta kayıt MOD-0018 sahibinin (Ali) işidir (D9).
Bu matris CT'nin D46 + D47 + D49 uygulamasıdır; sahibin yeni bir kararı değildir — D46'dan ayrıldığı yerler aşağıda "D46'dan farklar"da.

**Miras kuralı (D49/1):** her operatör / hazırlayıcı / onaylayıcı / yönetici rolü yalnız **kendi modülünün** görüntüleyicisini
miras alır. Hazine yöneticisi iki modülün (MOD-0126 ödeme, MOD-0124 nakit pozisyonu) yapılandırma ve destek rolü olduğu için bu iki
modülün görüntüleyicisini alır; AP / AR görüntüleyicisini almaz. "Hazine konsolide görüntüleyici"yi hiçbir rol miras almaz.

**Görev ayrılığı (SoD):** rol şablonu tek başına SoD değildir. Bir kişiye birden çok rol verilebilir; bu yüzden her paketin
çalışma anı kuralları ayrıca geçerlidir — MOD-0126 R1–R3 ve R9 (muafiyet yalnız R4), MOD-0119 R1–R3, MOD-0120 A1, A2, A3, A7,
MOD-0124 R1–R3, MOD-0289 dört göz D23/2, MOD-0217 imza kaydedici ayrılığı D47/5 (AC-64) ve MOD-0023 `SodViolation` (başlatan
kendi örneğini onaylayamaz). Uzlaştırma (`…reconcile`) yeniden yazmayı yalnız asıl komutun aktörüne açar; bu yüzden hazırlayan ve
karar veren rollerin ikisinde de bulunur, yöneticide yalnız okuyarak çözüm içindir.

### (1) Yalnız açık atama (`ExplicitGrantOnlyPermissions`; hiçbir rol şablonunda yok)

| Anahtar | Dayanak | Not |
|---|---|---|
| `finance-treasury.payment-batches.waive-sod` | **D47/1, D49/4** | atamayı onaylayan: muafiyetten yararlanmayan Hazine yöneticisi ya da K1; süre en çok 24 saat, bitişte kendiliğinden düşer; gerekçe kodu `ACIL-ODEME` / `TEK-YETKILI` / `DENETIM-ISTEGI`; kapsam yalnız ilgili tek ödeme partisi; her kullanım 5 iş günü içinde K1 incelemesine düşer; onaylayan muafiyetten yararlanamaz; kendi kendine muafiyet yasak; her muafiyette gerekçe, bitiş, etkilenen parti, onaylayan aktör ve atlanamaz K2 olayı. Hangi denetimin servis çalışma anında, hangisinin Auth / süreç denetimi olduğu MOD-0126 Authorization Convention'dadır |
| `finance-treasury.payment-batches.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.ap-invoices.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.receivable-invoices.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.cash-positions.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `mdm.bank-accounts.read-tenant-wide` | D23/1, D46 | yalnız okuma, maskeli |
| `mdm.bank-accounts.read-unmasked` | D23/4, D46 | gerekçe kodu + değerden önce denetim |
| `mdm.contracts.read-tenant-wide` | D41/1, D46 | yalnız okuma |

### (2) Finance & Treasury rolleri (`finance-treasury.*`)

**Görüntüleyiciler (D49/1):**

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Ödeme görüntüleyici | `finance-treasury.payment-batches.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`, `finance-treasury.payment-trace.read` | — (banka okuması yok, D47/4 — bölüm 5) |
| AP görüntüleyici | `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read` | — |
| AR görüntüleyici | `finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read` | — |
| Nakit pozisyonu görüntüleyici | `finance-treasury.cash-positions.read`, `finance-treasury.cash-position-threshold-policies.read` | — |
| Hazine konsolide görüntüleyici (**D49/1** — yalnız gerekçeyle atanır; hiçbir rol miras almaz) | dört görüntüleyicinin birleşimi: `finance-treasury.payment-batches.read`, `finance-treasury.payment-evidence.read`, `finance-treasury.payment-exceptions.read`, `finance-treasury.payment-trace.read`, `finance-treasury.ap-invoices.read`, `finance-treasury.ap-invoice-trace.read`, `finance-treasury.receivable-invoices.read`, `finance-treasury.receivable-trace.read`, `finance-treasury.cash-positions.read`, `finance-treasury.cash-position-threshold-policies.read` | — |

**Ödeme (MOD-0126) ve hazine yapılandırması:**

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Ödeme hazırlayıcı | Ödeme görüntüleyici + `finance-treasury.payment-batches.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.payment-evidence.link`, `finance-treasury.payment-attestations.create` | `mdm.legal-entities.read`, `platform.workflow.instances.start` |
| Ödeme onaylayıcı | Ödeme görüntüleyici + `finance-treasury.payment-batches.approve`, `.reject`, `.request-evidence`, `.reconcile` | `mdm.legal-entities.read`, `platform.workflow.tasks.approve`, `.reject`, `.request-info` |
| Ödeme onaylayıcı (2. kademe) | ödeme onaylayıcı + `finance-treasury.payment-batches.tier2-approve` | ödeme onaylayıcıyla aynı |
| Ödeme serbest bırakıcı | Ödeme görüntüleyici + `finance-treasury.payment-batches.release`, `.reconcile` | `mdm.legal-entities.read` |
| Hazine yöneticisi (**D47/1** — yalnız yapılandırma ve operasyonel destek) | Ödeme görüntüleyici + Nakit pozisyonu görüntüleyici + `finance-treasury.payment-batches.bulk-delete` (yalnız taslak), `finance-treasury.payment-batches.reconcile`, `finance-treasury.cash-position-threshold-policies.create`, `.update`, `.delete`, `.bulk-delete`, `finance-treasury.cash-positions.bulk-delete` (yalnız hiç gönderilmemiş taslak), `finance-treasury.cash-positions.workflow-reconcile`, `finance-treasury.cash-positions.export-reconcile`. **Yok:** approve, reject, request-evidence, release, tier2-approve, waive-sod, publish; AP / AR okuması | `mdm.legal-entities.read` |

**Borç (AP, MOD-0119):**

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Borç (AP) operatörü | AP görüntüleyici + `finance-treasury.ap-invoices.create`, `.update`, `.delete`, `.validate`, `.submit`, `.reconcile`, `finance-treasury.ap-invoice-exceptions.create`, `finance-treasury.ap-invoice-evidence.link` | `mdm.legal-entities.read`, `platform.workflow.instances.start` (+ MOD-0140, MOD-0143, MOD-0031 okuma) |
| Borç (AP) onaylayıcı (karar + mutabakat; **D50/1** — AP onay yöneticisi bununla birleşti) | AP görüntüleyici + `finance-treasury.ap-invoices.approve`, `.reject`, `.request-info`, `.reconcile` | `mdm.legal-entities.read`, `platform.workflow.tasks.approve`, `.reject`, `.request-info` |
| AP operasyon yöneticisi (**D49/3** — hazırlama + operasyon) | AP operatörü + `finance-treasury.ap-invoices.hold`, `.release-hold`, `.bulk-delete`, `finance-treasury.ap-invoice-exceptions.resolve`, `.reopen` (kendi açtığı istisnayı çözemez / yeniden açamaz — **D50/3**, MOD-0119 AC-42). **Yok:** `ap-invoices.approve`, `.reject`, `.request-info` | AP operatörüyle aynı |

**Alacak (AR, MOD-0120):**

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Alacak (AR) operatörü | AR görüntüleyici + `finance-treasury.receivable-invoices.create`, `.update`, `.delete`, `.issue`, `.collection-manage`, `finance-treasury.receivable-payments.record`, `finance-treasury.receivable-adjustments.request`, `.reconcile` | `mdm.legal-entities.read`, `platform.workflow.instances.start` (+ müşteri okuma, MOD-0120 O-3) |
| Alacak (AR) onaylayıcı (karar + mutabakat; **D50/1** — AR onay yöneticisi bununla birleşti) | AR görüntüleyici + `finance-treasury.receivable-adjustments.approve`, `.reject`, `.reconcile` | `mdm.legal-entities.read`, `platform.workflow.tasks.approve`, `.reject` |
| AR operasyon yöneticisi (**D49/3**) | AR operatörü (tahsilat operasyonları `receivable-invoices.collection-manage` ve `receivable-payments.record` dahil) + `finance-treasury.receivable-invoices.bulk-delete`. **Yok:** `receivable-adjustments.approve`, `.reject`, `.tier2-approve` | AR operatörüyle aynı |
| AR 2. kademe düzeltme onaylayıcı (**D49/3, D50/2** — tek başına atanır; ödeme 2. kademe deseni) | AR onaylayıcı + `finance-treasury.receivable-adjustments.tier2-approve` | AR onaylayıcıyla aynı |

**Nakit pozisyonu (MOD-0124):**

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Nakit pozisyonu hazırlayıcı | Nakit pozisyonu görüntüleyici + `finance-treasury.cash-positions.create`, `.update`, `.delete`, `.reconcile`, `.attach-evidence`, `.submit`, `.workflow-reconcile`, `.export-evidence`, `.export-reconcile` | `mdm.legal-entities.read`, `platform.workflow.instances.start` |
| Nakit pozisyonu onaylayıcı | Nakit pozisyonu görüntüleyici + `finance-treasury.cash-positions.approve`, `.reject`, `.publish`, `.workflow-reconcile` | `mdm.legal-entities.read`, `platform.workflow.tasks.approve`, `.reject` |

### (3) Ana veri rolleri (`mdm.*`)

| Rol şablonu | İzinler | Bağımlı (başka modül) |
|---|---|---|
| Banka verisi görüntüleyici | `mdm.banks.read`, `mdm.bank-accounts.read`, `mdm.bank-account-purposes.read` | — |
| Banka verisi yöneticisi | görüntüleyici + `mdm.banks.create`, `.update`, `.activate`, `.deactivate`, `.delete`, `.bulk-delete`, `mdm.bank-accounts.create`, `.update`, `.delete`, `.bulk-delete` (yalnız taslak), `mdm.bank-accounts.sensitive-reads.read`, `mdm.bank-account-purposes.create`, `.update`, `.delete`, `.activate`, `.deactivate`. **Yok:** `mdm.bank-accounts.activate` / `.deactivate` (D47/2) | (MOD-0140 tedarikçi okuma — MOD-0289 Dependencies) |
| Banka hesabı etkinleştirici (**D47/2**) | görüntüleyici + `mdm.bank-accounts.activate`, `mdm.bank-accounts.deactivate` | — |
| Sözleşme görüntüleyici | `mdm.contracts.read`, `mdm.contracts.documents.read` | `platform.document-repository.read` (D46a) |
| Sözleşme yöneticisi | görüntüleyici + `mdm.contracts.create`, `.update`, `.delete`, `.bulk-delete` (yalnız taslak), `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.submit-approval`, `mdm.contracts.terminate`, `mdm.contracts.archive`, `mdm.contracts.value.read`, `mdm.contracts.value.write` (**D49/2**), `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`. **Yok:** `mdm.contracts.record-signature` (D47/5) | `platform.document-repository.read`, `.manage` (D46a), `platform.workflow.instances.start` |
| Sözleşme imza kaydedici (**D47/5**) | `mdm.contracts.record-signature`, `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create` (imzalı kopya) | `platform.document-repository.read`, `.manage` |

Sözleşme sürümünün onay **kararı** MDM rolünde değildir: onaylayıcı MOD-0023 görevinde karar verir (`platform.workflow.tasks.*`,
şablon O-3 — Ali); onaylayıcıya sözleşmeyi görmek için "sözleşme görüntüleyici" verilir. `mdm.contracts.value.write` (D49/2)
ticari değeri yazmak için komutun kendi iznine (`create` / `update` / `amendments.create`) ek olarak gerekir; `value.read` yalnız
görüntüler (MOD-0217 "Ticari değer görünürlüğü").

### (4) Kapsama tablosu — anahtar → rol(ler) / açık atama

"(+ miras)" o görüntüleyiciyi miras alan bütün rolleri anlatır (bölüm 2'deki miras kuralı).

| Anahtar | Rol(ler) / açık atama |
|---|---|
| `finance-treasury.payment-batches.read` | Ödeme görüntüleyici (+ miras: ödeme rolleri, Hazine yöneticisi), Hazine konsolide görüntüleyici |
| `finance-treasury.payment-batches.read-tenant-wide` | açık atama |
| `finance-treasury.payment-batches.create` | Ödeme hazırlayıcı |
| `finance-treasury.payment-batches.update` | Ödeme hazırlayıcı |
| `finance-treasury.payment-batches.delete` | Ödeme hazırlayıcı |
| `finance-treasury.payment-batches.bulk-delete` | Hazine yöneticisi |
| `finance-treasury.payment-batches.validate` | Ödeme hazırlayıcı |
| `finance-treasury.payment-batches.submit` | Ödeme hazırlayıcı |
| `finance-treasury.payment-batches.reconcile` | Ödeme hazırlayıcı, Ödeme onaylayıcı (+ 2. kademe), Ödeme serbest bırakıcı, Hazine yöneticisi |
| `finance-treasury.payment-batches.approve` | Ödeme onaylayıcı, Ödeme onaylayıcı (2. kademe) |
| `finance-treasury.payment-batches.tier2-approve` | Ödeme onaylayıcı (2. kademe) |
| `finance-treasury.payment-batches.waive-sod` | açık atama (D47/1, D49/4) |
| `finance-treasury.payment-batches.reject` | Ödeme onaylayıcı, Ödeme onaylayıcı (2. kademe) |
| `finance-treasury.payment-batches.request-evidence` | Ödeme onaylayıcı, Ödeme onaylayıcı (2. kademe) |
| `finance-treasury.payment-batches.release` | Ödeme serbest bırakıcı |
| `finance-treasury.payment-trace.read` | Ödeme görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.payment-attestations.create` | Ödeme hazırlayıcı |
| `finance-treasury.payment-evidence.link` | Ödeme hazırlayıcı |
| `finance-treasury.payment-evidence.read` | Ödeme görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.payment-exceptions.read` | Ödeme görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.ap-invoices.read` | AP görüntüleyici (+ miras: üç AP rolü), Hazine konsolide görüntüleyici |
| `finance-treasury.ap-invoices.read-tenant-wide` | açık atama |
| `finance-treasury.ap-invoices.create` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.update` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.delete` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.bulk-delete` | AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.validate` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.submit` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.reconcile` | AP operatörü, AP onaylayıcı, AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.approve` | AP onaylayıcı |
| `finance-treasury.ap-invoices.reject` | AP onaylayıcı |
| `finance-treasury.ap-invoices.request-info` | AP onaylayıcı |
| `finance-treasury.ap-invoices.hold` | AP operasyon yöneticisi |
| `finance-treasury.ap-invoices.release-hold` | AP operasyon yöneticisi |
| `finance-treasury.ap-invoice-trace.read` | AP görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.ap-invoice-exceptions.create` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.ap-invoice-exceptions.resolve` | AP operasyon yöneticisi |
| `finance-treasury.ap-invoice-exceptions.reopen` | AP operasyon yöneticisi |
| `finance-treasury.ap-invoice-evidence.link` | AP operatörü, AP operasyon yöneticisi |
| `finance-treasury.receivable-invoices.read` | AR görüntüleyici (+ miras: dört AR rolü), Hazine konsolide görüntüleyici |
| `finance-treasury.receivable-invoices.read-tenant-wide` | açık atama |
| `finance-treasury.receivable-invoices.create` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-invoices.update` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-invoices.delete` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-invoices.bulk-delete` | AR operasyon yöneticisi |
| `finance-treasury.receivable-invoices.issue` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-invoices.collection-manage` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-adjustments.request` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-adjustments.approve` | AR onaylayıcı, AR 2. kademe düzeltme onaylayıcı |
| `finance-treasury.receivable-adjustments.tier2-approve` | AR 2. kademe düzeltme onaylayıcı |
| `finance-treasury.receivable-adjustments.reject` | AR onaylayıcı, AR 2. kademe düzeltme onaylayıcı |
| `finance-treasury.receivable-adjustments.reconcile` | AR operatörü, AR onaylayıcı, AR operasyon yöneticisi, AR 2. kademe düzeltme onaylayıcı |
| `finance-treasury.receivable-payments.record` | AR operatörü, AR operasyon yöneticisi |
| `finance-treasury.receivable-trace.read` | AR görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.cash-positions.read` | Nakit pozisyonu görüntüleyici (+ miras: iki nakit pozisyonu rolü, Hazine yöneticisi), Hazine konsolide görüntüleyici |
| `finance-treasury.cash-positions.read-tenant-wide` | açık atama |
| `finance-treasury.cash-positions.create` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.update` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.delete` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.bulk-delete` | Hazine yöneticisi |
| `finance-treasury.cash-positions.reconcile` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.attach-evidence` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.submit` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.approve` | Nakit pozisyonu onaylayıcı |
| `finance-treasury.cash-positions.reject` | Nakit pozisyonu onaylayıcı |
| `finance-treasury.cash-positions.workflow-reconcile` | Nakit pozisyonu hazırlayıcı, Nakit pozisyonu onaylayıcı, Hazine yöneticisi |
| `finance-treasury.cash-positions.publish` | Nakit pozisyonu onaylayıcı |
| `finance-treasury.cash-positions.export-evidence` | Nakit pozisyonu hazırlayıcı |
| `finance-treasury.cash-positions.export-reconcile` | Nakit pozisyonu hazırlayıcı, Hazine yöneticisi |
| `finance-treasury.cash-position-threshold-policies.read` | Nakit pozisyonu görüntüleyici (+ miras), Hazine konsolide görüntüleyici |
| `finance-treasury.cash-position-threshold-policies.create` | Hazine yöneticisi |
| `finance-treasury.cash-position-threshold-policies.update` | Hazine yöneticisi |
| `finance-treasury.cash-position-threshold-policies.delete` | Hazine yöneticisi |
| `finance-treasury.cash-position-threshold-policies.bulk-delete` | Hazine yöneticisi |
| `mdm.banks.read` | Banka verisi görüntüleyici (ve onu içeren iki banka rolü) |
| `mdm.banks.create` | Banka verisi yöneticisi |
| `mdm.banks.update` | Banka verisi yöneticisi |
| `mdm.banks.activate` | Banka verisi yöneticisi |
| `mdm.banks.deactivate` | Banka verisi yöneticisi |
| `mdm.banks.delete` | Banka verisi yöneticisi |
| `mdm.banks.bulk-delete` | Banka verisi yöneticisi |
| `mdm.bank-accounts.read` | Banka verisi görüntüleyici (ve onu içeren iki banka rolü) |
| `mdm.bank-accounts.read-tenant-wide` | açık atama |
| `mdm.bank-accounts.create` | Banka verisi yöneticisi |
| `mdm.bank-accounts.update` | Banka verisi yöneticisi |
| `mdm.bank-accounts.activate` | Banka hesabı etkinleştirici |
| `mdm.bank-accounts.deactivate` | Banka hesabı etkinleştirici |
| `mdm.bank-accounts.delete` | Banka verisi yöneticisi |
| `mdm.bank-accounts.bulk-delete` | Banka verisi yöneticisi |
| `mdm.bank-accounts.read-unmasked` | açık atama |
| `mdm.bank-accounts.sensitive-reads.read` | Banka verisi yöneticisi |
| `mdm.bank-account-purposes.read` | Banka verisi görüntüleyici (ve onu içeren iki banka rolü) |
| `mdm.bank-account-purposes.create` | Banka verisi yöneticisi |
| `mdm.bank-account-purposes.update` | Banka verisi yöneticisi |
| `mdm.bank-account-purposes.delete` | Banka verisi yöneticisi |
| `mdm.bank-account-purposes.activate` | Banka verisi yöneticisi |
| `mdm.bank-account-purposes.deactivate` | Banka verisi yöneticisi |
| `mdm.contracts.read` | Sözleşme görüntüleyici, Sözleşme yöneticisi, Sözleşme imza kaydedici |
| `mdm.contracts.read-tenant-wide` | açık atama |
| `mdm.contracts.create` | Sözleşme yöneticisi |
| `mdm.contracts.update` | Sözleşme yöneticisi |
| `mdm.contracts.delete` | Sözleşme yöneticisi |
| `mdm.contracts.bulk-delete` | Sözleşme yöneticisi |
| `mdm.contracts.value.read` | Sözleşme yöneticisi |
| `mdm.contracts.value.write` | Sözleşme yöneticisi (**D49/2**) |
| `mdm.contracts.documents.read` | Sözleşme görüntüleyici, Sözleşme yöneticisi, Sözleşme imza kaydedici |
| `mdm.contracts.documents.create` | Sözleşme yöneticisi, Sözleşme imza kaydedici |
| `mdm.contracts.documents.delete` | Sözleşme yöneticisi |
| `mdm.contracts.submit-approval` | Sözleşme yöneticisi |
| `mdm.contracts.record-signature` | Sözleşme imza kaydedici |
| `mdm.contracts.amendments.create` | Sözleşme yöneticisi |
| `mdm.contracts.renewal-decisions.create` | Sözleşme yöneticisi |
| `mdm.contracts.terminate` | Sözleşme yöneticisi |
| `mdm.contracts.archive` | Sözleşme yöneticisi |
| `mdm.legal-entities.read` (bağımlı, MOD-0220) | dört finans paketinin hazırlayan / operatör / karar veren / serbest bırakan rolleri, AP / AR operasyon yöneticileri, AR 2. kademe düzeltme onaylayıcı ve Hazine yöneticisi (görüntüleyicilerde yok) |
| `platform.workflow.instances.start` (bağımlı, MOD-0023) | Ödeme hazırlayıcı, AP operatörü, AP operasyon yöneticisi, AR operatörü, AR operasyon yöneticisi, Nakit pozisyonu hazırlayıcı, Sözleşme yöneticisi |
| `platform.workflow.tasks.approve` / `.reject` (bağımlı, MOD-0023) | Ödeme onaylayıcı (+ 2. kademe), AP onaylayıcı, AR onaylayıcı (+ AR 2. kademe düzeltme onaylayıcı), Nakit pozisyonu onaylayıcı; sözleşme onaylayıcısı MOD-0023 şablonuyla (O-3) |
| `platform.workflow.tasks.request-info` (bağımlı, MOD-0023) | Ödeme onaylayıcı (+ 2. kademe), AP onaylayıcı |
| `platform.document-repository.read` (bağımlı, MOD-0262-FU01) | Sözleşme görüntüleyici, Sözleşme yöneticisi, Sözleşme imza kaydedici (D46a) |
| `platform.document-repository.manage` (bağımlı, MOD-0262-FU01) | Sözleşme yöneticisi, Sözleşme imza kaydedici (D46a) |

Sayım: 114 modül anahtarı (MOD-0126 20, MOD-0119 19, MOD-0120 15, MOD-0124 20, MOD-0289 23, MOD-0217 17 — `mdm.bank-account-purposes.*`
altısı ve D49/2'nin `mdm.contracts.value.write`'ı dahil) + 7 bağımlı anahtar; hepsi en az bir role ya da açık atama listesine
yerleşti. Açık atamalı sekiz anahtarın hiçbiri bir rolde değildir. Rol sayısı: 25 (finans 19 — beş görüntüleyici dahil; ana veri 6;
D50/1 ile AP / AR onay yöneticisi onaylayıcılarla birleşti).

### (5) Banka okuma ölçümü (D47/4)

Soru: dört finans paketinde MOD-0289'u (banka / banka hesabı verisi, `BANK-ACCOUNT-LOOKUP`) gerçekten okuyan akış var mı?
Ölçüm: paketlerde `MOD-0289`, `BANK-ACCOUNT-LOOKUP`, `mdm.bank`, `BankAccountRef`, "banka hesab" araması (bu dal, CT04a).

| Paket | Bulgu (`dosya:satır`, CT04a ölçümü) | Sonuç |
|---|---|---|
| MOD-0126 | `MOD-0126-payments-approvals.md:274` `DefaultSourceBankAccountRef` opak Guid; `:301` `DestinationReferenceId` opak Guid; `:322` "Çözümleme MOD-0289'dan ve yalnız dağıtım anında yapılır (dağıtım bu dilimde kapalı)"; `:876` ekranda opak referans girişi (O-15); `:1487` O-15 KARAR D28 opak alan | v1'de okuma yok |
| MOD-0119 | `MOD-0119-accounts-payable.md:116` "v1'de AP banka referansı **taşımaz**"; `:332` banka verisi yasağı (referans da yok); `:1435` BANK slotu "v1'de yok" | v1'de okuma yok |
| MOD-0120 | `MOD-0120-accounts-receivable.md:336` `CollectionDestinationRef` opak Guid; `:935` ekranda opak referans girişi; `:1532` "yalnız opak Guid; çözümleme yok" | v1'de okuma yok |
| MOD-0124 | `MOD-0124-cash-positioning.md:355` `BankAccountRef` opak Guid, "doğrulama yok"; `:1012` ekranda opak referans girişi; `:1650` O-15 KARAR D28 opak alan | v1'de okuma yok |

**Sonuç:** v1'de hiçbir finans akışı MOD-0289'dan banka ya da banka hesabı verisi okumaz; `mdm.banks.read` ve
`mdm.bank-accounts.read` **hiçbir finans rolüne verilmez** (D46'nın "Hazine ve AP/AR rollerine mdm.bank-accounts.read ve
mdm.banks.read eklenir" cümlesi D47/4 ile kalktı). İki anahtar yalnız MOD-0289'un kendi rollerindedir (banka verisi görüntüleyici,
yöneticisi, banka hesabı etkinleştirici). `BANK-ACCOUNT-LOOKUP` 1.0.0 dondurulup bir finans akışı onu okumaya başladığında
(MOD-0126 / MOD-0124 O-15, MOD-0120 tahsilat hesabı), okuyan rol yeniden ölçülerek bu matrise eklenir (sahip kararı).

### D46'dan farklar (CT uygulaması — sahip incelemesine)

1. Hazine yöneticisi ödeme rollerini içermez; onay / ret / kanıt isteme / serbest bırakma / tier-2 / muafiyet / yayım taşımaz
   (D47/1); `waive-sod` açık atama listesine geçti.
2. Eşik politikası yazmaları (`cash-position-threshold-policies.create` / `.update` / `.delete`) nakit pozisyonu hazırlayıcıdan
   Hazine yöneticisine geçti (yapılandırma — D47/1; MOD-0124 hedef kullanıcı tanımı). `cash-positions.bulk-delete` ve
   `cash-position-threshold-policies.bulk-delete` onaylayıcıdan Hazine yöneticisine geçti (yalnız taslak / yapılandırma).
3. İş mutabakatı `cash-positions.reconcile` onaylayıcıdan hazırlayıcıya geçti (MOD-0124 hedef kullanıcı: operatör mutabakat
   çalıştırır; onaylayıcıda hazırlama eylemi kalmadı). Kurtarma uzlaştırmaları (`…reconcile`, `…workflow-reconcile`)
   hazırlayan ve karar veren rollerin ikisine de verildi (yeniden yazma yalnız asıl aktörle).
4. Banka okuması finans rollerinden kaldırıldı (D47/4, bölüm 5).
5. Banka hesabı etkinleştirme ayrı role geçti; banka / şube / amaç durum ve silme anahtarları ile hesap taslak silme banka
   verisi yöneticisine yazıldı (D46'da yerleşmemişti).
6. İmza kaydı ayrı role geçti (D47/5); sözleşme rollerine modül belge anahtarları (`mdm.contracts.documents.read` /
   `.create` / `.delete`) ve taslak silme (`mdm.contracts.delete` / `.bulk-delete`) yazıldı (D46'da yerleşmemişti).
7. **D49/1:** "Hazine görüntüleyici" kaldırıldı; yerine Ödeme, AP, AR ve Nakit pozisyonu görüntüleyicileri. Her rol yalnız
   kendi modülünün görüntüleyicisini miras alır (Hazine yöneticisi: Ödeme + Nakit pozisyonu). "Hazine konsolide görüntüleyici"
   dört modülün okumalarını taşır, hiçbir rol onu miras almaz, yalnız gerekçeyle atanır.
8. **D49/3:** "Borç (AP) yöneticisi" ve "Alacak (AR) yöneticisi" birleşik rolleri kalktı (D48/2'nin AP / AR kısmının yerine
   geçer); yerine AP operasyon yöneticisi, AP onay yöneticisi, AR operasyon yöneticisi, AR onay yöneticisi ve AR 2. kademe
   düzeltme onaylayıcı. `receivable-adjustments.tier2-approve` artık yalnız AR 2. kademe düzeltme onaylayıcıdadır.
9. **D49/2:** yeni anahtar `mdm.contracts.value.write` sözleşme yöneticisine verildi; `value.read` yalnız görüntüler.
10. **D49/4:** `waive-sod` açık atamasının kuralları (onaylayan, 24 saat, gerekçe kodları, tek parti, K1 incelemesi) bölüm 1'de;
    çalışma anı / Auth / süreç ayrımı MOD-0126 Authorization Convention'da.
11. **D49/5:** D46 tablosu "YERİNE GEÇTİ — UYGULAMAYIN" olarak işaretlendi; işleyen tek matris bu bölümdür.
12. **D50/1:** "AP onay yöneticisi" ve "AR onay yöneticisi" kalktı (anahtar kümeleri onaylayıcılarla birebir aynıydı); karar ve
    mutabakat (`…reconcile`) Borç (AP) onaylayıcı ve Alacak (AR) onaylayıcıdadır (ölçüldü: ikisi de `reconcile` taşır).
13. **D50/2:** AR 2. kademe düzeltme onaylayıcı AR onaylayıcıyı miras alır + `receivable-adjustments.tier2-approve`; tek başına
    atanır (birlikte atama notu kalktı).
14. **D50/3:** MOD-0119'a istisna ayrılığı: istisnayı açan (`RaisedBy`) aynı istisnayı çözemez ve yeniden açamaz — 403
    `AP_EXCEPTION_RAISER_CANNOT_RESOLVE`, AC-42 (DL-3).

**Aynı nesne türünde hazırlama + karar (D49/3 ve D50 sonrası denetim):** hiçbir rol aynı nesne türünün hazırlama / oluşturma / gönderme
eylemiyle onay / ret eylemini birlikte taşımaz — ödeme partisi, AP faturası, AR düzeltmesi, nakit pozisyonu ve sözleşme sürümü
için kontrol edildi (sözleşme kararı MDM rolünde değil, MOD-0023'te). Kalan ve işaretlenen örtüşmeler (onay / ret değil, ama
karara yakın eylemler):

- **AP operasyon yöneticisi — istisna açma + çözme** (`ap-invoice-exceptions.create` + `.resolve` / `.reopen`): rol iki izni de
  taşır, ama aynı istisnada değil — **D50/3** çalışma anı kuralı: istisnayı açan (`RaisedBy`) onu çözemez ve yeniden açamaz
  (MOD-0119, 403 `AP_EXCEPTION_RAISER_CANNOT_RESOLVE`, AC-42). Açık madde kapandı.
- **AP operasyon yöneticisi — gönderme + bekletme / bekletmeyi kaldırma** (`ap-invoices.submit` + `.hold` / `.release-hold`):
  bekletme onay değildir; `Approved` faturayı ödeme hattından geri tutar, kaldırma bekletme öncesi duruma döner. D49/3'ün
  operasyon tanımı; çalışma anı SoD kuralı yok.
- **Banka verisi yöneticisi — banka / amaç oluşturma + etkinleştirme** (`mdm.banks.create` + `.activate`,
  `mdm.bank-account-purposes.create` + `.activate`): referans verisi; MOD-0289'un dört göz kuralı (D23/2) yalnız banka
  **hesabı** etkinleştirmesindedir. D47/2 ile değiştirilmedi; sahip isterse ayrı karar.

**Anahtar kümesi aynı roller:** D50/1 ile kapandı — AP / AR onay yöneticisi onaylayıcılarla birleşti.

## D48 — MOD-0289 denetim kategorisi ve CT04a rol matrisi yorumları (soru kutusu, önerilen seçenek, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`);
onaylanan içerik bu dosyadaki "D47 rol matrisi (CT04a)" bölümü (commit sonrası hash ZIP manifestinde).

1. **MOD-0289 `AuditCategory`:** `MasterData` (14; Platform'da var). MOD-0217 ile aynı servis ve aynı kategori; MOD-0126 O-6
   (Finance = 18) bağı kaldırılır; MOD-0289 `Finance = 18`'i beklemez.
2. **Rol matrisi yorumları onaylandı:** eşik politikası yazmaları hazine yöneticisinde; nakit pozisyonu iş mutabakatı
   hazırlayıcıda; taslak toplu silmeler hazine yöneticisinde; `…reconcile` / `…workflow-reconcile` hem hazırlayan hem karar veren
   rollerde; yayım nakit pozisyonu onaylayıcısında. AP / AR yöneticisi birleşimleri çalışma anı SoD kurallarıyla korunur
   (MOD-0119 R1–R3, MOD-0120 A1, A2, A7).

## D49 — CT04a yeniden denetim (geçti, iki yetki düzeltmesi) üzerine kararlar (soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); denetlenen
belge `treasury-ct04a-9bd6d179b.zip` (`9fd7d04c06ab71c7daecf4a10c9c6e2d22150f820a1cd2ce1e11cedd085738f3`).

1. **Modül bazında görüntüleyiciler:** "Hazine görüntüleyici" kaldırılır; yerine Ödeme, AP, AR ve Nakit pozisyonu görüntüleyicileri.
   Her operatör / onaylayıcı yalnız kendi modülünün görüntüleyicisini miras alır. "Hazine konsolide görüntüleyici" (dört modülün
   okumaları) ayrıca kalır; yalnız gerekçeyle atanır, hiçbir rol onu miras almaz.
2. **Ticari değer yazma izni:** yeni anahtar `mdm.contracts.value.write` (D41/1 listesine ek). `mdm.contracts.value.read` yalnız
   görüntüler; değeri yazmak `mdm.contracts.update` + `mdm.contracts.value.write` ister. Okuma ve yazma ayrı denetim olaylarıdır.
   Sözleşme yöneticisine verilir.
3. **AP / AR yönetici ayrımı:** birleşik yönetici rolleri kalkar (D48/2'nin AP / AR kısmının yerine geçer). AP operasyon yöneticisi
   (hazırlama + operasyon: hold / release-hold / istisna çözümü / toplu silme), AP onay yöneticisi (karar + mutabakat); AR operasyon
   yöneticisi, AR onay yöneticisi; "AR 2. kademe düzeltme onaylayıcı" ayrı rol (`receivable-adjustments.tier2-approve`).
4. **SoD muafiyeti kuralları (`waive-sod` açık atama):** onaylayan muafiyetten yararlanmayan Hazine yöneticisi ya da K1; süre en çok
   24 saat ve süre dolunca kendiliğinden düşer; gerekçe kodları `ACIL-ODEME`, `TEK-YETKILI`, `DENETIM-ISTEGI`; kapsam yalnız ilgili tek
   ödeme partisi; her kullanım 5 iş günü içinde K1 incelemesine düşer; onaylayan muafiyetten yararlanamaz.
5. **D46 tablosu karantinada:** D46 tablosu tarihçedir — "YERİNE GEÇTİ — UYGULAMAYIN"; işleyen tek rol matrisi D47 rol matrisinin
   D49 ile güncellenmiş hâlidir.

## D50 — CT04b rol matrisi üç nokta (soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu commit
`a3288084f` (CT04b).

1. **Onay yöneticileri birleşir:** "AP onay yöneticisi" ve "AR onay yöneticisi" kalkar (izinleri onaylayıcılarla birebir aynıydı);
   AP onaylayıcı ve AR onaylayıcı mutabakatı (`reconcile`) da taşır.
2. **AR 2. kademe düzeltme onaylayıcı** AR onaylayıcının izinlerini miras alır + `receivable-adjustments.tier2-approve`; tek başına
   atanabilir (ödeme 2. kademe deseni).
3. **AP istisna ayrılığı:** MOD-0119'a çalışma anı kuralı — istisnayı açan (`RaisedBy`) aynı istisnayı çözemez ve yeniden açamaz;
   hata kodu ve sabotajlı kabul ölçütüyle.

## D51 — DL-1 hazırlığında çıkan tutarsızlıklar ve prompt soruları (soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu
`treasury-ct05-prep.zip` (`bc162f42e9e2043f7738f36a3f085be17adb4381da38211ea50506afd0b2a6e8`).

1. **Paket kapısı ile dilim kapısı ayrılır:** altı pakette "ready-for-dev engelleri" yalnız bütün dilimleri ilgilendiren maddeleri
   taşır (CT-APR-02/01, DCP-011 onayı, RCS-001 servisi, Auth kaydı, AuditCategory, DL-1'in kendi önkoşulları); DL-2/3/4'e özgü
   önkoşullar "— DL-n için" işaretiyle o dilimin kapısına taşınır. İlk dilim yalnız kendi koşullarını bekler.
2. **SEC-002:** tasarım (Y-4/6 yolu) ve Auth'ta `read-tenant-wide` tanımı hazırlık kapısında kalır; kapsamı soran kod ve kanıt testi
   (MOD-0126 AC-32 ve diğer üç paketteki karşılıkları) DL-1 kabul kapısına taşınır.
3. **WP dalı ve iskelet:** her WP kendi dalında — `feature/fin/wp-0126-dl1`, `feature/mdm/wp-0289-dl1`; `/add-module` servis iskeleti
   DL-1 WP'sinin ilk adımıdır (ayrı ön WP yok).
4. **MOD-0289 DL-1 ayrıntıları:** banka araması `Regex.Escape` ile kaçırılır, büyük / küçük harf duyarsız, en çok 100 karakter;
   `AuditAggregateType`'a DL-1'de yalnız `Bank` ve `BankBranch` eklenir, diğerleri kendi diliminde.
5. **Bağlı düzeltmeler (karar değil, D45/3 ve D44'ün sonucu):** MDM domain-config'teki "no code-take permission (D27/5)" cümlesi
   D45/3'e göre güncellenir (sahip MDM sahibi rolünde, D38); BL-565 backlog satırındaki sahip ("CT") Beste'ye bildirilir — backlog
   onun alanıdır, CT yazmaz.

## D52 — DL-1 hazırlığı ikinci tur (soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu commit
`984d93a0c` (dört DL-1 hazırlık kaydı).

1. **WP dalları (D51/3'ün devamı):** `feature/fin/wp-0119-dl1`, `feature/fin/wp-0120-dl1`, `feature/fin/wp-0124-dl1`,
   `feature/mdm/wp-0217-dl1`.
2. **MOD-0120 S-2 bitiş / gözden geçirme koşulu:** kısıtlı yerel AR MVP'si MOD-0169 (faturalama) paketi `ready-for-dev` olduğunda
   gözden geçirilir; en geç Treasury v1 sonunda (D27/2'yi tamamlar).
3. **MOD-0120 Level-1 koşul 1:** kaynağın sınırlı denetim / çoklu yazma devri Recovery'ye taşınmaz; denetim D35 defter yoluyla, tek
   yazma modeliyle.
4. **Paket metin temizliği:** hazırlıkta bulunan eskimiş metinler (checkbox'lar, "PR #246 ile main'e girer", TBD küsurat, RCS sahibi,
   sabitlemeler) anlam değiştirmeden Codex incelemesinden önce güncellenir.

## Karar olmayanlar

BL-565 servisi ve çağrı sözleşmesi, `Finance = 18`'in main'e girmesi, MOD-0048 seed'leri, MOD-0149 pack revizyonu, DCP-011 onayı
(Beste); Auth kaydı (rol şablonlarının içeriği D46 ile verildi), MOD-0023 şablon kodları (O-3 / O-8 / O-11 / O-20), MOD-0220 `ApprovalStatus`
(Ali); supplier 1.3.0 (MVP-2).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
