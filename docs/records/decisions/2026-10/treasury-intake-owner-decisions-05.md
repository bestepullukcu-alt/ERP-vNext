---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-05
status: approved — D45–D88 (2026-10-29) itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
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
tarihçedir — "YERİNE GEÇTİ — UYGULAMAYIN", D49/5). **D57/4 eki (2026-10-28):** `finance-treasury.payment-sod-settings.read` /
`.update` Hazine yöneticisine eklendi (bölüm 2 ve 4). **D69 eki (2026-10-28):** yeni anahtar `mdm.contracts.versions.approve`
ve yeni rol "Sözleşme onaylayıcı" (bölüm 3 ve 4; açık atama değil). **D70/1 eki (2026-10-28):** bütün onaylayıcı rollere (ödeme,
AP, AR, nakit, sözleşme — ikinci kademe dahil) `platform.workflow.tasks.approve` / `.reject` çapraz modül bağımlılığı olarak rol
şablonuyla gelir (MOD-0018 eki K-4 (b)); matriste yeni olan yalnız Sözleşme onaylayıcı satırıdır (bölüm 3 ve 4), öteki onaylayıcılar
bu iki anahtarı zaten taşıyordu. Bağımlı anahtar olduğu için modül anahtarı sayısı (117) değişmez. **D74 eki (2026-10-28):** yeni
rol "Muafiyet yetkilisi" (D74/3) yalnız iki Auth anahtarını (`auth.payment-sod-waivers.assign`, `.read`) taşır, açık atamayla
verilir; `waive-sod` atamasını onaylayan bu roldür ("Hazine yöneticisi ya da K1" kalktı); iki Auth anahtarı açık atamadır (D74/2)
ama modül anahtarı değildir (bölüm 1 notu ve sayım). Anahtar adları kısaltılmadan yazıldı. "Bağımlı" sütunu başka modülün
anahtarlarıdır (kullanıcı token'ıyla yapılan çağrılar — MOD-0220, MOD-0023); kaydı o modülün sahibindedir.
**D56/2: belge deposu izinleri kullanıcılara verilmez** — MOD-0217 belge deposunu (MOD-0262-FU01) kendi servis kimliğiyle çağırır;
`platform.document-repository.read` / `.manage` hiçbir rol şablonunda yer almaz (D46a'nın bu matristeki etkisi geri alındı; D46 /
D46a tarihçe bölümü değişmedi).
`procurement.*`, `crm.*` ve MOD-0031 / MOD-0140 okuma izinleri (MOD-0119, MOD-0120 Authorization Convention) bu matrisin
anahtar kapsamı dışındadır; ilgili operatör rolüne o modülün adıyla eklenir. Auth'ta kayıt MOD-0018 sahibinin (Ali) işidir (D9).
Bu matris CT'nin D46 + D47 + D49 uygulamasıdır; sahibin yeni bir kararı değildir — D46'dan ayrıldığı yerler aşağıda "D46'dan farklar"da.

**Miras kuralı (D49/1):** her operatör / hazırlayıcı / onaylayıcı / yönetici rolü yalnız **kendi modülünün** görüntüleyicisini
miras alır. Hazine yöneticisi iki modülün (MOD-0126 ödeme, MOD-0124 nakit pozisyonu) yapılandırma ve destek rolü olduğu için bu iki
modülün görüntüleyicisini alır; AP / AR görüntüleyicisini almaz. "Hazine konsolide görüntüleyici"yi hiçbir rol miras almaz.

**Görev ayrılığı (SoD):** rol şablonu tek başına SoD değildir. Bir kişiye birden çok rol verilebilir; bu yüzden her paketin
çalışma anı kuralları ayrıca geçerlidir — MOD-0126 R1–R3 ve R9 (muafiyet yalnız R4), MOD-0119 R1–R3, MOD-0120 A1, A2, A3, A7,
MOD-0124 R1–R3, MOD-0289 dört göz D23/2, MOD-0217 imza kaydedici ayrılığı D47/5 (AC-64), MOD-0217 sürümü
hazırlayanın onay adayı olmaması (W-5, D69; AC-33) ve MOD-0023 `SodViolation` (başlatan
kendi örneğini onaylayamaz). Uzlaştırma (`…reconcile`) yeniden yazmayı yalnız asıl komutun aktörüne açar; bu yüzden hazırlayan ve
karar veren rollerin ikisinde de bulunur, yöneticide yalnız okuyarak çözüm içindir.

### (1) Yalnız açık atama (`ExplicitGrantOnlyPermissions`; hiçbir rol şablonunda yok)

| Anahtar | Dayanak | Not |
|---|---|---|
| `finance-treasury.payment-batches.waive-sod` | **D47/1, D49/4** | atamayı onaylayan: Muafiyet yetkilisi rolünü (**D74/3**; yalnız `auth.payment-sod-waivers.assign` / `.read`, açık atamayla) taşıyan, muafiyetten yararlanmayan kişi (D49/4'teki "Hazine yöneticisi ya da K1" D74/3 ile kalktı); süre en çok 24 saat, bitişte kendiliğinden düşer; gerekçe kodu `ACIL-ODEME` / `TEK-YETKILI` / `DENETIM-ISTEGI`; kapsam yalnız ilgili tek ödeme partisi; her kullanım 5 iş günü içinde K1 incelemesine düşer; onaylayan muafiyetten yararlanamaz; kendi kendine muafiyet yasak; her muafiyette gerekçe, bitiş, etkilenen parti, onaylayan aktör ve atlanamaz K2 olayı. Hangi denetimin servis çalışma anında, hangisinin Auth / süreç denetimi olduğu MOD-0126 Authorization Convention'dadır |
| `finance-treasury.payment-batches.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.ap-invoices.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.receivable-invoices.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `finance-treasury.cash-positions.read-tenant-wide` | D14/1, D46 | yalnız okuma |
| `mdm.bank-accounts.read-tenant-wide` | D23/1, D46 | yalnız okuma, maskeli |
| `mdm.bank-accounts.read-unmasked` | D23/4, D46 | gerekçe kodu + değerden önce denetim |
| `mdm.contracts.read-tenant-wide` | D41/1, D46 | yalnız okuma |

**D74/2 eki:** MOD-0018 Treasury Auth eki ayrıca iki Auth anahtarını açık atama listesine koyar — `auth.payment-sod-waivers.assign`
ve `auth.payment-sod-waivers.read`; açık atama anahtarı toplamı 10 (bu tablodaki 8 Treasury + 2 Auth). Bu tablo yalnız Treasury modül
anahtarlarını listeler; iki Auth anahtarı modül anahtarı değildir ve yalnız Muafiyet yetkilisi rolündedir (bölüm 2), role açık atamayla
bağlanır — hiçbir otomatik yol vermez.

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
| Hazine yöneticisi (**D47/1** — yalnız yapılandırma ve operasyonel destek) | Ödeme görüntüleyici + Nakit pozisyonu görüntüleyici + `finance-treasury.payment-batches.bulk-delete` (yalnız taslak), `finance-treasury.payment-batches.reconcile`, `finance-treasury.cash-position-threshold-policies.create`, `.update`, `.delete`, `.bulk-delete`, `finance-treasury.cash-positions.bulk-delete` (yalnız hiç gönderilmemiş taslak), `finance-treasury.cash-positions.workflow-reconcile`, `finance-treasury.cash-positions.export-reconcile`, `finance-treasury.payment-sod-settings.read`, `finance-treasury.payment-sod-settings.update` (**D57/4** — SoD ayar kaydı ve ekranı; R1–R3 kapatılamaz, yalnız eşik ve R4). **Yok:** approve, reject, request-evidence, release, tier2-approve, waive-sod, publish; AP / AR okuması | `mdm.legal-entities.read` |
| Muafiyet yetkilisi (**D74/3** — Auth rolü, MOD-0018 Treasury Auth eki DL-5) | yalnız `auth.payment-sod-waivers.assign`, `auth.payment-sod-waivers.read` (Auth anahtarları, açık atama; modül anahtarı değil). **Yok:** hiçbir `finance-treasury.*` / `mdm.*` anahtarı, `waive-sod`. Rol açık atamayla verilir; ödeme sürecinde rolü olan kişi kendine muafiyet veremez (kendine atama 403) | — |

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
| Sözleşme görüntüleyici | `mdm.contracts.read`, `mdm.contracts.documents.read` | — (belge deposu izni yok — D56/2) |
| Sözleşme yöneticisi | görüntüleyici + `mdm.contracts.create`, `.update`, `.delete`, `.bulk-delete` (yalnız taslak), `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.submit-approval`, `mdm.contracts.terminate`, `mdm.contracts.archive`, `mdm.contracts.value.read`, `mdm.contracts.value.write` (**D49/2**), `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`. **Yok:** `mdm.contracts.record-signature` (D47/5) | `platform.workflow.instances.start` (belge deposu izni yok — D56/2) |
| Sözleşme imza kaydedici (**D47/5**) | `mdm.contracts.record-signature`, `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create` (imzalı kopya) | — (belge deposu izni yok — D56/2) |
| Sözleşme onaylayıcı (**D69**) | `mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve` | `platform.workflow.tasks.approve`, `.reject` (**D70/1**); belge deposu izni yok (D56/2) |

Sözleşme sürümünün onay **kararı** MOD-0023 görevinde verilir (şablon `MDM-CONTRACT-VERSION-APPROVAL`, O-3 — Ali); MOD-0217'de
onay ucu yoktur. **D69:** onay yetkisi `mdm.contracts.versions.approve` anahtarıdır (Sözleşme onaylayıcı rolü; açık atama değil);
`MDM-CONTRACT-VERSION-APPROVAL` adayları Auth aday sorgusuyla (D66/1, MOD-0018 Treasury Auth eki DL-6) bu anahtardan bulunur, sürümü
hazırlayan hariç. **D70/1:** rol MOD-0023 görev anahtarlarını (`platform.workflow.tasks.approve` / `.reject`) çapraz modül
bağımlılığı olarak taşır (MOD-0018 eki K-4 (b)); iş akışı izni aday şartı değildir, rol şablonuyla gelir (K-A4 kapandı) —
rol şablonu yoluyla tutarlı; elle atamada açık nokta (MOD-0018 eki K-A4b; karar değil).
`mdm.contracts.value.write` (D49/2)
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
| `finance-treasury.payment-sod-settings.read` | Hazine yöneticisi (**D57/4**) |
| `finance-treasury.payment-sod-settings.update` | Hazine yöneticisi (**D57/4**) |
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
| `mdm.contracts.read` | Sözleşme görüntüleyici, Sözleşme yöneticisi, Sözleşme imza kaydedici, Sözleşme onaylayıcı (**D69**) |
| `mdm.contracts.read-tenant-wide` | açık atama |
| `mdm.contracts.create` | Sözleşme yöneticisi |
| `mdm.contracts.update` | Sözleşme yöneticisi |
| `mdm.contracts.delete` | Sözleşme yöneticisi |
| `mdm.contracts.bulk-delete` | Sözleşme yöneticisi |
| `mdm.contracts.value.read` | Sözleşme yöneticisi |
| `mdm.contracts.value.write` | Sözleşme yöneticisi (**D49/2**) |
| `mdm.contracts.documents.read` | Sözleşme görüntüleyici, Sözleşme yöneticisi, Sözleşme imza kaydedici, Sözleşme onaylayıcı (**D69**) |
| `mdm.contracts.documents.create` | Sözleşme yöneticisi, Sözleşme imza kaydedici |
| `mdm.contracts.documents.delete` | Sözleşme yöneticisi |
| `mdm.contracts.submit-approval` | Sözleşme yöneticisi |
| `mdm.contracts.versions.approve` | Sözleşme onaylayıcı (**D69**) |
| `mdm.contracts.record-signature` | Sözleşme imza kaydedici |
| `mdm.contracts.amendments.create` | Sözleşme yöneticisi |
| `mdm.contracts.renewal-decisions.create` | Sözleşme yöneticisi |
| `mdm.contracts.terminate` | Sözleşme yöneticisi |
| `mdm.contracts.archive` | Sözleşme yöneticisi |
| `mdm.legal-entities.read` (bağımlı, MOD-0220) | dört finans paketinin hazırlayan / operatör / karar veren / serbest bırakan rolleri, AP / AR operasyon yöneticileri, AR 2. kademe düzeltme onaylayıcı ve Hazine yöneticisi (görüntüleyicilerde yok) |
| `platform.workflow.instances.start` (bağımlı, MOD-0023) | Ödeme hazırlayıcı, AP operatörü, AP operasyon yöneticisi, AR operatörü, AR operasyon yöneticisi, Nakit pozisyonu hazırlayıcı, Sözleşme yöneticisi |
| `platform.workflow.tasks.approve` / `.reject` (bağımlı, MOD-0023) | Ödeme onaylayıcı (+ 2. kademe), AP onaylayıcı, AR onaylayıcı (+ AR 2. kademe düzeltme onaylayıcı), Nakit pozisyonu onaylayıcı, Sözleşme onaylayıcı (**D70/1** — bütün onaylayıcı roller; MOD-0018 eki K-4 (b), K-A4 kapandı) |
| `platform.workflow.tasks.request-info` (bağımlı, MOD-0023) | Ödeme onaylayıcı (+ 2. kademe), AP onaylayıcı |

**D56/2:** `platform.document-repository.read` / `.manage` (MOD-0262-FU01) bu tablodan çıktı — kullanıcılara verilmez; MOD-0217
belge deposunu servis kimliğiyle çağırır.

Sayım: 117 modül anahtarı (MOD-0126 22 — D57/4 ile iki SoD ayarı anahtarı eklendi, MOD-0119 19, MOD-0120 15, MOD-0124 20, MOD-0289 23, MOD-0217 18 — `mdm.bank-account-purposes.*`
altısı, D49/2'nin `mdm.contracts.value.write`'ı ve D69'un `mdm.contracts.versions.approve`'u dahil) + 5 bağımlı anahtar (D56/2 ile iki belge deposu anahtarı çıktı); hepsi en az bir role ya da açık atama listesine
yerleşti. Açık atamalı sekiz anahtarın hiçbiri bir rolde değildir. **D74:** bu sayıma ek 2 Auth anahtarı (`auth.payment-sod-waivers.assign`,
`.read`) — modül anahtarı sayılmaz (modül anahtarı = altı Treasury paketinin anahtarları; kapsama tablosuna girmez): **117 modül
anahtarı + 2 Auth anahtarı**; açık atama 8 Treasury + 2 Auth = 10 (D74/2). Rol sayısı: 27 (finans 19 — beş görüntüleyici dahil; ana
veri 7 — D69 ile Sözleşme onaylayıcı eklendi; D50/1 ile AP / AR onay yöneticisi onaylayıcılarla birleşti; Auth 1 — D74/3 ile Muafiyet
yetkilisi).

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
15. **D69:** yeni anahtar `mdm.contracts.versions.approve` ve yeni rol "Sözleşme onaylayıcı" (`mdm.contracts.read`,
    `mdm.contracts.documents.read`, `mdm.contracts.versions.approve`; açık atama değil); MOD-0217 onay adayları bu anahtardan
    (Auth aday sorgusu D66/1, hazırlayan hariç). D46'da sözleşme onayı MDM rolünde değildi.
16. **D70/1:** Sözleşme onaylayıcı `platform.workflow.tasks.approve` / `.reject` bağımlı anahtarlarını alır; böylece bütün onaylayıcı
    roller (ödeme, AP, AR, nakit, sözleşme — ikinci kademe dahil) iki görev anahtarını rol şablonuyla taşır. Modül anahtarı
    sayısı 117, bağımlı anahtar sayısı 5 kalır.
17. **D74/3:** yeni rol "Muafiyet yetkilisi" (yalnız `auth.payment-sod-waivers.assign` / `.read`, açık atamayla); `waive-sod`
    atamasını onaylayan bu roldür — D49/4'ün "Hazine yöneticisi ya da K1" kısmının yerine. Rol sayısı 27; modül anahtarı 117
    değişmez (iki Auth anahtarı ayrı sayılır, D74/2).

**Aynı nesne türünde hazırlama + karar (D49/3 ve D50 sonrası denetim):** hiçbir rol aynı nesne türünün hazırlama / oluşturma / gönderme
eylemiyle onay / ret eylemini birlikte taşımaz — ödeme partisi, AP faturası, AR düzeltmesi, nakit pozisyonu ve sözleşme sürümü
için kontrol edildi (sözleşme kararı MOD-0023'te; D69'un Sözleşme onaylayıcısı `versions.approve` taşır, `submit-approval` ve
yazma anahtarı taşımaz; sözleşme yöneticisi `versions.approve` taşımaz). Kalan ve işaretlenen örtüşmeler (onay / ret değil, ama
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

## D53 — Auth sıralaması iki parça (soru kutusu, önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu commit
`e4f9a78e9` (MOD-0217 "Auth kaydı sıralaması — gerilim").

D51/1'in "Auth kaydı paket kapısında" maddesi altı pakette ikiye ayrılır (Ali'nin Y-6 sırasıyla uyumlu):
1. **Paket kapısı:** Auth paket metni main'de — MOD-0018 paketinde anahtarlar, açık atama listesi ve rol şablonları (D46–D50).
2. **DL-1 kabul kapısı:** Auth kayıt kodu main'de; DL-1 testleri gerçek izinle koşar (`mdm.contracts.*` kaydı MOD-0217 onayından
   sonra, DL-1 kabulünden önce).

## D54 — Muafiyet parti bağı (Codex ön incelemesi MOD-0126 F-1; soru kutusu, önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); bulgu
`docs/records/reviews/MOD-0126/2026-10-27-699b0708.md` F-1.

`waive-sod` açık atamasının hedef ödeme partisini taşıması zorunludur: Auth ataması hedef parti kimliğini taşımıyorsa ya da okunamıyorsa
muafiyetli onay 403 ile reddedilir (kapalı-başarısız; yazım ve MOD-0023 çağrısı yok). Atamanın hedef partisi rotadaki partiyle eşleşmezse
aynı ret. Auth (MOD-0018) paketi atamada hedef parti kimliğini taşır — Ali'ye iletilir. "Tek parti" kuralı süreç denetimine bırakılmaz.

## D55 — MOD-0057 parktan çıkar (D19/2'yi değiştirir; soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); sahibin sözü
"MOD-0057 Etiketleme parktan çıkarmamız lazım". Keşif: `verify_module_id.py --discover "Semantic Tagging"` @ec063fee9, çıkış 3, rapor
sha256 `98cf6271…e980` (Blueprint 1 aday MOD-0057; registry 0; paket 0; görev 1 = TRM-18).

1. **Park kalkar:** MOD-0057 Semantic Tagging & Taxonomy Management Treasury intake'e geri döner (D19/2'nin park maddesi kalkar;
   level-1 kaydı `treasury-level1-acceptance-01/MOD-0057-v1.0.md` geçerli).
2. **Yerleşim önerisi: `commercial-suite`** — konu taksonomisi sahipliği MOD-0162-FU01'de ve o paket MOD-0057'yi gelecek bağlantı
   olarak anıyor. Yerleşim commercial-suite domain sahibinin onayına bağlıdır (XMC-001); onay gelene kadar pack taslağı yazılabilir,
   domain-config / registry satırı yazılmaz.
3. **v1 bağımsız:** taksonomi ve etiket yönetimi (sınıf ağaçları, etiketler, varlığa etiket bağlama); arama ve bilgi grafiği bağlantısı
   MOD-0053 / MOD-0056 Recovery'ye gelince sonraki sürüm.
4. **Kule:** Treasury CT yürütür (devir paketi ve level-1 kaydı burada); pack taslağı CT yazar, sahip ZIP denetimi + CT-APR-02.
   Kod alma izni ayrı karardır (D1 deseni).

## D56 — DL-2 hazırlığında çıkan dört karar (soru kutusu, hepsi önerilen, 2026-10-27)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-27; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu commit
`40a1bcc91` (altı DL-2 hazırlık kaydı).

1. **SEC-002 sağlayıcısı:** Platform'daki izin listesi ve MDM istemcisinin modül kodu, Ali'nin MOD-0018 Auth paketine "permission
   onboarding" eki olarak girer. MOD-0289 ve MOD-0217 bunu paket kapısında bekler (paket metni D53/1 deseni; kodu DL-1 kabulünde).
2. **Belge deposu erişimi (D41/10 ve D46a'yı değiştirir):** MOD-0217 belge deposunu (MOD-0262-FU01) kendi servis kimliğiyle çağırır;
   kullanıcılara `platform.document-repository.read` / `.manage` verilmez (D46a geri alınır). Belge indirme yalnız sözleşme yönetimi
   üzerinden (kapsam ve AC-62 indirme denetimiyle). Belge deposunda servis kimliği desteği gerekir — Platform (Beste); MOD-0217 DL-2
   kapısı.
3. **Muafiyet (MOD-0126):** Auth atama alanları (hedef parti, başlangıç / bitiş, onaylayan, gerekçe kodu) "— DL-2 için" kapısında —
   Auth paket metninde tanımlı, kodu DL-2 kabulünde. Muafiyet adayları Auth'tan "bu partiye geçerli `waive-sod` ataması olanlar"
   sorgusuyla okunur (Ali'den uç; MOD-0018).
4. **SoD ayarı:** FinanceTreasuryService'te kiracı başına tek ayar kaydı (tek eşik + R1–R4 açık / kapalı), denetlenir; DL-2'de yazılır.
   Follow-up'tan çıkar.

## D57 — D56 düzeltme turunda kalan dört nokta (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); konu commit
`e5463096c`.

1. **MOD-0289 DL-2 kabul kapısı:** "Treasury tüketici fikstür koşusu" şartı kalkar (D47/4: v1'de MOD-0289'u okuyan finans akışı yok);
   BANK-ACCOUNT-LOOKUP sözleşmesi kendi testiyle doğrulanır; tüketici testi ilk gerçek tüketicinin dilimine girer.
2. **MOD-0120 denetim sınıfı:** Issue (fatura kesme) K2; SetCollectionStage (tahsilat aşaması) K1.
3. **MOD-0124 kanıt paketi:** DL-2'de yalnız biçim doğrulanır; kalemlerin varlığı MOD-0031 bağlantısıyla DL-4'te doğrulanır.
4. **SoD ayar kaydı (D56/4 ayrıntıları):** izinler `finance-treasury.payment-sod-settings.read` / `.update` (Hazine yöneticisi rolü);
   ayar ekranı MOD-0126 ekran diliminde; R1–R3 kapatılamaz (yalnız eşik ve R4 ayarlanır); kayıt yoksa R1–R4 açık ve varsayılan eşik.
5. **Ortak MOD-0023 bağdaştırıcısı:** servis ortak yapı taşıdır; tek yazarı MOD-0126 DL-2. MOD-0119 DL-2, MOD-0120 DL-3 ve MOD-0124 DL-3
   onu bekler (D14/2 deseni; dört paket için ortak karar).

## D58 — MOD-0289 DL-3 tedarikçi listesi (Cihan'ın sorusu; buton, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

MOD-0289 DL-3'te tedarikçi banka hesaplarının görünürlüğü için MOD-0140 1.3.0 `GET /api/suppliers/eligible` (`listEligibleSuppliers`)
listesi **çağıran şirketle ilişkisi Active olan bütün tedarikçileri** döndürmelidir; `eligibleForTransactions = true` şartı aranmaz.
Gerekçe: banka hesabı çoğunlukla onboarding sırasında kaydedilir; ödeme anı uygunluğu ödeme tarafında ayrıca denetlenir.
Kaynak: Cihan'ın yanıtı (MOD-0140 sahibi, 2026-10-28) — 1.3.0, MOD-0140 kararlarından (S-1…S-6, F-3) yaklaşık 3 iş günü sonra main'de.

## D59 — BL-565 kapısı ve MOD-0048 seed'leri (Beste'nin yanıtı Y-8 üzerine; soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); kaynak Y-8
(`docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md`).

1. **BL-565 / RCS-001 servisi paket kapısından DL-1 kabul kapısına taşınır** (altı paket; MOD-0057'de kod gerekmiyorsa uygulanmaz).
   İlk dilim kodu uygulama katmanında `IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey) → code` portuna
   karşı yazılır; testlerde sahte uygulama; üretimde port bağlı değilse Create kapalı (fail-closed), kodsuz kayıt açılmaz; kendi sayaç
   ya da yedek kod üretimi yasak (RCS-001). v0 çağrı sözleşmesi (Beste, 6 Kasım'da kesinleşir, değişiklik yalnız ekleme):
   `POST /api/internal/record-codes/allocate` — gövde `{legalEntityId, recordType, idempotencyKey}` (kiracı güvenilir bağlamdan),
   200 `{code, sequence}` (aynı anahtar aynı kod), 422 `RECORD_TYPE_UNKNOWN`, 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, 404 başka kiracı,
   503 servis yok → Create başarısız. Gerçek servis DL-1 kabulünden önce main'de olmalıdır.
2. **MOD-0048 seed'leri Treasury CT tarafından eklenir:** Platform'da yalnız seed JSON dosyaları ve `ConsumableSets` izin listesi
   (Beste'nin izni, Y-8); değerler MOD-0048-FU01 §21 (#243 ile onaylı); ayrı dal ve PR, Beste onaylar; push / PR sahibe ayrıca sorulur.
3. **Ara PR:** ct-05 dalı main'e ara PR olarak gider (kart S-77 (c): Beste D56/2'yi main'de görmek istiyor).

## D60 — MOD-0048 seed izninin genişlemesi ve değerler (Beste, değerlerin ve Platform'un sahibi; 2026-10-28)

Onay kanıtı: Beste'nin yazılı yanıtı (`Beste → Treasury CT`, 2026-10-28), sahip K1 (`gmg-10`) tarafından Treasury CT oturumuna
(`f8880468-240a-4bd1-8ab8-bbdbecf875dd`) aktarıldı. Doğrulama: main'deki `execution/approvals/approvals.tsv` satırı MOD-0048-FU01
@ `8fc68be8…` (`whole_pack=TRM-17 — yalnız §21`) ve main'deki paket aynı hash'te (ölçüldü, 2026-10-28).

1. **`DefaultConsumableSets` (kod):** `BusinessReferenceDataConsumableSetsOptions.cs` içindeki listeye yalnız `payment-method` ve
   `currency-minor-units` eklenir; o dosyada başka değişiklik yok, test değişmez. §21'deki "kod yok" ifadesi bir ölçüm eksiğiydi
   (izin listesinin kod kopyası `BusinessReferenceDataConsumableSetsTests` ile `appsettings.json`'a eşit tutuluyor). PR açıklamasında
   belirtilir. D59/2'nin "yalnız seed JSON + ConsumableSets" sınırını bu kadar genişletir.
2. **`RequiredSetCodes`:** `appsettings.Development.json`'a iki kod eklenir (§21.3 madde 2; izinli yollar `appsettings*.json`, iki liste
   girdisi, bir catalog-load bloğu).
3. **`currency-minor-units` değerleri:** ISO 4217'nin güncel baskısındaki bütün aktif kodlar; `catalog_version` o baskının yayın tarihi.
   Değerlerin kararı Beste'de, Finance bilgi olarak görür. AC-R2 kontrolü sistemin döndürdüğü listede ISO'da olmayan eski bir kod
   bulursa kod hane sayısıyla eklenir; kontrol kaldırılmaz. Kaynak: SIX Group'un yayımladığı ISO 4217 "List One" (K1 seçimi, soru kutusu,
   önerilen, 2026-10-28).
4. **§21 onaylı; `payment-method` altı değeri kesin** (`BANK_TRANSFER`, `SEPA_CREDIT_TRANSFER`, `SWIFT_TRANSFER`, `DOMESTIC_TRANSFER`,
   `CHEQUE`, `CASH`). §21 başlığındaki "proposal, not approved" onay öncesinden kalmadır; onayı bozmamak için şimdi değişmez, bir sonraki
   MOD-0048-FU01 revizyonunda düzeltilir.
5. **Tek PR:** seed dosyası, iki liste girdisi ve testler (AC-R1…R4; AC-R2 sabotaj kanıtıyla) aynı PR'da; Beste onaylar.

## D61 — MOD-0057 yerleşim onayı (soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik Y-10/5.

commercial-suite için kayıtlı domain sahibi yok (`docs/guides/operations/control-tower-operating-card.md:563` "Belirsiz — sahip ataması
bekliyor"; Ali'nin yanıtı Y-10/5). Sahip atanana kadar **MOD-0057'nin commercial-suite'e yerleşimini K1 yönetim olarak onaylar** (D55
önerisi). Bu yalnız yerleşim onayıdır: domain-config In-Scope satırı ve kimlik kaydı paket onay PR'ında önerilir; MOD-0162-FU01 sınırı ve
servis/port (O-12 dahil açık kararlar) pakette açık kalır. Domain sahibi atandığında onay mesajı ona bilgi olarak gider; pack onayı,
kod ya da commercial-suite sahipliği ataması değildir.

## D62 — Auth dayanağı ve v1'de kısmi onay (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik Y-11, Y-12.

1. **Auth paketi main'e dayanır:** D54 / D56 / D57 ara PR #270 ile main'e girer; Auth paketi main'deki karar dosyasına atıf yapar, daldaki
   commit'e atıf yapılmaz (D53/1 korunur).
2. **`PartiallyApproved` v1'de kapalı:** MOD-0023 şablonları tek adımlı olduğundan durum (kod 9 ve ad) saklanır, v1'de giriş yolu yoktur;
   çok adımlı onay gelince açılır. MOD-0126 ve aynı durumu taşıyan paketlere ready-for-dev revizyonunda işlenir.

## D63 — D62 ara PR, SEC-002 XMC-001 ve waive-sod yapısı (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik Y-15.

1. **Ara PR:** D62 ve Y-11…Y-15 küçük bir ara PR ile main'e gider (kart S-77 (c): Auth kulesi bekliyor).
2. **SEC-002 eki için XMC-001 onayı Beste'den (Platform sahibi) istenir:** dokunulacak Platform dosyaları istekte listelenir; onay gelene
   kadar o dosyalara dokunulmaz.
3. **waive-sod parti bağı (D54) Auth'un ayrı işi kalır** (D64 ile değişti): Treasury Auth veri yapısı yazmaz. Auth bu yeteneği getirene kadar MOD-0126'nın
   muafiyet yolu kapalıdır (hedef partiyi taşıyan atama okunamaz → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, D54); Treasury'nin diğer işleri bunu
   beklemez. Pakete ready-for-dev revizyonunda işlenir.

## D64 — waive-sod Auth veri yapısını Treasury yazar (D63/3'ün yerine; soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik Y-17
(MOD-0018 sahibi Ali: D9 / XMC-001 "(a) bu işte düzelt").

D63/3 geçersizdir. Partiye bağlı muafiyetin (D54) Auth tarafı — atama kaydı, yazma ucu, "bu parti için geçerli atamalar" sorgu ucu ve
denetim — Treasury tarafından MOD-0018 Treasury Auth ekinde ayrı bir dilim olarak yazılır; MOD-0018 sahibi inceler ve teyit eder.
Sahibin kabul şartları paket AC'si olur: kiracı izolasyonu (`TenantId`, kiracılar arası 404); hedef parti yok / okunamıyor / eşleşmiyor →
403; atama en çok 24 saat; onaylayan ve gerekçe kodu zorunlu; denetim kaydı. Dokunulan Platform dosyaları Beste'ye giden XMC-001 isteğine
eklenir (D63/2; o onayı MOD-0018 sahibi vermez). Yapı main'e girene kadar MOD-0126 muafiyet yolu 403 ile kapalı kalır (D54).

## D65 — Sahip denetimi 5cfd67813 sonrası iki nokta (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **MOD-0057 O-13 kapı değildir:** commercial-suite sahibi atanana kadar MOD-0057 paket onayını da K1 verir (D61'in devamı); atanınca
   bilgi olarak ona gider.
2. **MOD-0289 AC-60 üç dilime bölünür** (banka DL-1, şirket hesabı DL-2, tedarikçi hesabı DL-3); şirket hesabı `BAC` için sahip şirketin
   `LegalEntityId`'si yalnız öneridir, O-14'ün parçası olarak sözleşme sahibine (Beste) sorulur.

## D66 — Ready-for-dev 1. tur soruları (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik Y-12
(şablonlar atamasız).

1. **Onaylayıcı aday tabanı Auth aday sorgusundan gelir:** MOD-0126 O-10'daki "şablonun atama kuralı" dayanağı Y-12 ile kalktı. Auth'ta
   "bu izne sahip kullanıcılar" sorgusu MOD-0018 Treasury Auth ekine (MOD-0018-FU-TBD) eklenir (D56/3 aday sorgusu yönünde); tüketici
   servis R1–R4'ü (ya da paketin kendi kurallarını) uygular ve sonucu `CandidatePrincipalIds` olarak MOD-0023'e gönderir; ayrı dizin
   istemcisi taşınmaz. MOD-0126, MOD-0119, MOD-0120, MOD-0124 ve MOD-0217 için geçerli; MOD-0018 sahibinin teyidi gerekir.
2. **"Son adım değil" yanıtı:** v1'de beklenmez; gelirse parti / kayıt `PendingApproval`'da kalır, onaylanmış sayılmaz (MOD-0124 deseni;
   MOD-0126 AC-46).
3. **MOD-0126 DL-2 kabulü muafiyeti beklemez:** Auth DL-5 main'e girene kadar muafiyet yolu 403 ile kapalıdır; bunu kanıtlayan test DL-2
   kabulü için yeterlidir. Muafiyet DL-5 gelince açılır ve ayrıca kabul edilir. D56/3'teki "DL-5 kodu main'de = DL-2 kabul kapısı"
   kuralının yerine geçer.
4. **MOD-0119 çok adımlı dalı kalkar (D62/2):** AC-14'teki iki adımlı fikstür dalı v1'den çıkar; yerine "ara durum yok" testi.

## D67 — D66 uygulamasının teyitleri (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **Aday sorgusu MOD-0018-FU-TBD DL-6'dır** (DL-1 değil): DL-6 paket metni tüketicilerin onay diliminin önkoşulu, kodu o dilimin kabul
   kapısı (MOD-0126 / MOD-0119 DL-2; MOD-0120 / MOD-0124 / MOD-0217 DL-3). Dört finans paketinin DL-1'i DL-6'yı beklemez.
2. **D56/3 muafiyet aday sorgusu DL-6'ya birleşir:** muafiyet adayı = `waive-sod`'u açık atamayla tutan, etkin ve o partiye geçerli DL-5
   ataması olan kişi (D54 parti bağı aday listesine de uygulanır).
3. **`IFinanceTreasuryApproverCandidateSource` servis ortak portudur; tek yazarı MOD-0126 DL-2** (D57/5 deseni); MOD-0119 DL-2 ve
   MOD-0120 / MOD-0124 DL-3 kullanır.
4. **MOD-0120:** düzeltme talebinde "son adım değil" yanıtı → talep `PendingApproval`'da kalır, etki uygulanmaz (D66/2'nin AR karşılığı).
5. **MOD-0124:** onay niyetine `CandidatePrincipalIds` / `ExcludedPrincipalIds` alanları eklenir.

## D68 — MOD-0126 boş aday listesi (soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik sahip
denetimi 61466e504 sonrası ajan notu.

Kesişim, R1–R4, şirket kapsamı ve muafiyet adayından sonra liste boşsa Submit 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` döner;
MOD-0023 çağrılmaz, niyet yazılmaz, parti değişmez (MOD-0120 ile aynı). MOD-0023'ün 400'ü yalnız savunma hattıdır (502).

## D69 — MOD-0217 sözleşme sürümü onay anahtarı ve rolü (O-3 (b); soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

Yeni anahtar **`mdm.contracts.versions.approve`** ve yeni rol **Sözleşme onaylayıcı** (`mdm.contracts.read`, `mdm.contracts.documents.read`,
`mdm.contracts.versions.approve`; belge deposu izni yok — D56/2). `MDM-CONTRACT-VERSION-APPROVAL` adayları Auth aday sorgusuyla (D66/1,
FU DL-6) bu anahtardan bulunur; hazırlayan hariç. Açık atama değildir. İşleyen matris (D47 / D49) buna göre güncellenir: **117 anahtar,
26 rol** (MOD-0217: 18 anahtar, 4 rol). MOD-0217 O-3 (b) kapanır; Auth eki DL-3 ve DCP-011 sayıları güncellenir; MOD-0018 sahibinin teyidi
Auth ekiyle birlikte.
Düzeltme (2026-10-28, ölçüm): DCP-011'de anahtar / rol sayısı geçmiyor; "DCP-011 sayıları" ifadesi uygulanacak bir değişiklik doğurmaz.

## D70 — Onaylayıcı rollerin iş akışı izni, boş aday listesi ve Auth paket kapısı (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **İş akışı izinleri rol şablonuna:** bütün onaylayıcı rollere (ödeme, AP, AR, nakit, sözleşme — ikinci kademe dahil)
   `platform.workflow.tasks.approve` ve `.reject` çapraz modül bağımlılığı olarak eklenir (MOD-0018-FU-TBD K-4 (b)); aday olan karar
   verebilir. K-A4 kapanır: iş akışı izni aday şartı değildir, rol şablonuyla birlikte gelir. MOD-0018 sahibinin teyidi gerekir.
2. **D68 beş pakete:** MOD-0119, MOD-0124 ve MOD-0217'de de aday listesi boşsa yerelde 422 (paketin hata sözlüğü deseniyle "uygun
   onaylayıcı yok" kodu), MOD-0023 çağrılmaz, kayıt değişmez; MOD-0023'ün 400'ü yalnız savunma hattıdır.
3. **D53/1 Auth paket metni kapısı MOD-0018 sahibinin teyidiyle kapanır:** metin main'de olmak tek başına yetmez; teyit karar dosyasına
   kaynak olarak yazılır.

## D71 — Elle atamada iş akışı izni kesişimi (MOD-0018-FU-TBD K-A4b; soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

Tüketici servisler (MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0217) onaylayıcı aday listesini `platform.workflow.tasks.approve`
iznine sahip kullanıcılarla da kesiştirir (DL-6 aday sorgusuna ek çağrı; tier-2 kesişimiyle aynı desen): karar veremeyecek kişi aday olmaz.
Çağrı okunamazsa kapalı-başarısız; kesişim boşsa paketin 422 "uygun onaylayıcı yok" dalı (D68 / D70/2). D70/1 (rol şablonu) değişmez;
K-A4b kapanır. MOD-0018 sahibinin teyidi Auth ekiyle birlikte.

## D72 — MOD-0140 Revizyon 5 sonrası tedarikçi tüketimi (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Tetik: MOD-0140
Revizyon 5 main'de (`status: ready-for-dev`, onay satırı sha `b3259e40…`, 2026-10-28; sözleşme dosyası hâlâ 1.2.0 FROZEN — 1.3.0 MOD-0140
DL-1'de dondurulur, uçlar DL-3'te sunulur).

1. **Tedarikçi kimliği:** MOD-0119 ve MOD-0289 ana anahtar olarak kanonik `supplierId` kodunu (`SUP-…`) saklar; `supplierMasterId` (uuid)
   ikincil alan; eski kod gelirse kanonik koda çevrilip saklanır.
2. **MOD-0140 hata kodlarının karşılığı:** 403 `LEGAL_ENTITY_LOOKUP_FORBIDDEN` ve 503 `DEPENDENCY_UNAVAILABLE` (ve okunamayan / alanı
   eksik yanıt) → Treasury'de 503 `*_SUPPLIER_DIRECTORY_UNAVAILABLE`, kayıt yazılmaz, otomatik yeniden deneme yok; 404 `UNKNOWN_LEGAL_ENTITY`
   → 422 `*_LEGAL_ENTITY_UNKNOWN`. Uygunluk = `known` ∧ `eligibleForTransactions == true`; alan yoksa uygun değil (fail-closed).
3. **Çağrı kimliği ve izinler:** MOD-0289 `/eligible` listesini kullanıcı token'ıyla çağırır (MOD-0140 S-4 servis token'ını kapatır);
   `procurement.suppliers.eligibility.read` ve (BL-652'ye kadar) `mdm.legal-entities.read` ilgili Treasury ve MDM rollerine çapraz modül
   bağımlılığı olarak eklenir (D70/1 deseni; MOD-0018 Treasury eki, MOD-0018 sahibinin teyidi). Servis token'ı MOD-0140 DL-7 / 1.5.0'dan
   sonra ayrıca değerlendirilir.
4. **AP tedarikçi adı:** `GET /api/suppliers/{id}` (`procurement.suppliers.read`) ile alınır; izin AP rollerine bağımlılık olarak eklenir.
5. **D58 metin hizalaması:** liste = `eligibleForTransactions = true` = silinmemiş ve Active ilişki (MOD-0140 S-1);
   `onboardingStatus` / `purchasingEnabled` süzgeci yoktur. D58'in içeriği (Active ilişkili bütün tedarikçiler) değişmez.

## D73 — MOD-0119 O-23 ve MOD-0289 O-15 (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik D72 uygulaması.

1. **MOD-0119 O-23:** MOD-0143 eşleştirmesinden gelen `supplierId` eski kod olabilir; AP gelen kodu `getSupplierEligibility` ile kanonik
   koda çevirip saklı kanonik kodla karşılaştırır (eski kodlu doğru fatura reddedilmez). Çevirme çağrısı okunamazsa D72/2 sınıflaması.
2. **MOD-0289 O-15:** BANK-ACCOUNT-LOOKUP taslak sözleşmesine 422 `MDM_BANK_LEGAL_ENTITY_UNKNOWN` yanıtı eklenir (yalnız ekleme; taslak
   Treasury'nin); kapı MOD-0289 DL-3. Hata kodu adı paket öneki `MDM_BANK_` ile (D72/2'deki `MDM_LEGAL_ENTITY_UNKNOWN` örnek adın yerine).

## D74 — Auth eki inceleme bulguları (Y-18; soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **İç uç kimliği (K-W3 / K-A1):** DL-6 aday sorgusu ve muafiyet iç ucu Auth'un mevcut ortak sır desenini (`X-Internal-Api-Key`)
   kullanır; kiracı istek gövdesinden değil, çağrıyı başlatan kullanıcının iletilen token'ından alınır; yeni servis kimliği doğrulama
   katmanı eklenmez. Token yoksa / kiracı çıkarılamazsa kapalı-başarısız.
2. **Açık atama sayısı (K-W1):** Treasury eki 8 Treasury anahtarı + 2 Auth anahtarı (`auth.payment-sod-waivers.assign`, `.read`) = 10 açık
   atama anahtarı ekler; koddaki liste 10'dan 20'ye çıkar (sayım düzeltmesi).
3. **Muafiyet atayan:** yeni rol **Muafiyet yetkilisi** — yalnız `auth.payment-sod-waivers.assign` ve `.read`; açık atamayla verilir;
   ödeme sürecinde rolü olan kişi kendine muafiyet veremez (kendine atama 403, D64). Rol sayısı 26 → 27. MOD-0018 sahibinin teyidi Auth ekiyle.

## D75 — D73 / D74 uygulamasının açık noktaları (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **K-W7 Muafiyet yetkilisi rolü:** rolü Treasury profili izinsiz oluşturur, izinler açık atamayla verilir; atayan hem
   `auth.payment-sod-waivers.assign` iznini hem Muafiyet yetkilisi rol üyeliğini taşımalıdır (izin başka role elle bağlansa da rolsüz kişi
   atayamaz).
2. **Ortak sırın iki sınırı kabul edilir ve kayda alınır:** (a) sırrı tutan her servis iç uçları çağırabilir (çağıran ayırt edilmez);
   (b) kullanıcı token'ı olmayan arka plan işleri iç uçları çağıramaz — aday listesi yalnız kullanıcının başlattığı gönderimde hesaplanır,
   yeniden deneme ilk listeyi kullanır. Servis kimliği ileride gelirse yeniden değerlendirilir.
3. **MOD-0119 kod çevirmede 404 `UNKNOWN_SUPPLIER`** → 422 `AP_MATCH_REFERENCE_MISMATCH` (eşitlik kanıtlanamaz; D73/1 çıkarımı teyit).
4. **MDM 422 `MDM_BANK_LEGAL_ENTITY_UNKNOWN` tüketicide:** hesap uygun değil; tüketici kendi 422'sini döner, kayıt yazılmaz, yeniden deneme yok.

## D76 — D75 uygulamasının teyitleri (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`).

1. **Rol üyeliği denetimi** muafiyet atama ve geri alma uçlarında (izin + üyelik); okuma ucu (`GET …?paymentBatchId=`) yalnız `.read` izni.
2. **Denetim sırası:** izin ve rol üyeliği → kendine atama → alan doğrulama; rolsüz kişi kendini alıcı yazarsa önce `ASSIGN_FORBIDDEN` (403).
3. **Kullanıcının başlattığı Reconcile da yeniden denemedir:** aday listesi Auth'tan yeniden hesaplanmaz, niyetteki ilk liste kullanılır;
   rol değişikliği gerekiyorsa yeni gönderim yapılır.
4. **MOD-0217 O-16:** ilk aday listesi onay denemesi (`ApprovalAttempt`) kaydında deneme başına saklanır; Submit'in aynı anahtarla tekrarı
   bu listeyi kullanır (MOD-0126 niyet deseni).

## D77 — MOD-0126 R4 kapalı partide muafiyet ve R11 hata kodu (soru kutusu, hepsi önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik CT-APR-02 Codex
bulgu kapanışı (MOD-0126 F-1).

1. **R4 kapalı (`R4Enabled = false`) partide muafiyetli onay komutu** → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; yazım ve MOD-0023 çağrısı yok
   (aşılacak R4 kuralı yoktur; kullanıcı normal onayı kullanır).
2. **Release'de R11 tier-2 şartı tutmazsa** → 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; parti `Approved` kalır, `PaymentDispatchAttempt`
   yazılmaz.

## D78 — R4 açık, tier-2 olmayan partide muafiyetli onay (Codex fark incelemesi MOD-0126 F-1; soru kutusu, önerilen, 2026-10-28)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-28; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); tetik
`docs/records/reviews/MOD-0126/2026-10-28-58976767.md` F-1.

`R4Enabled = true` ama `IsTier2 = false` partide (R4 bu partiye uygulanmaz) muafiyetli onay komutu → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`;
`Waived` kaydı oluşmaz, karar / niyet / makbuz yazılmaz, MOD-0023 çağrılmaz (D77/1 ile aynı mantık: aşılacak R4 kuralı yoktur).

## D79 — Auth eki küçük kararları (Ali'nin yükünü azaltmak için; soru kutusu, hepsi önerilen, 2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Auth ekini Treasury
yazar, MOD-0018 sahibi teyit eder (Y-15); bu kararlar sahip teyidine Auth ekiyle birlikte gider.

1. **K-1b:** paket başına bir modül kodu (kiracı modül bazında alır); değerler tüketici paketlerin manifest adımında.
2. **K-2:** modül hakkıyla Admin / Viewer'a hiçbir Treasury anahtarı verilmez (matris 117 anahtarın hepsini yerleştirdi).
3. **K-3:** varsayılan kiracı SuperAdmin tam katalog yolu bugünkü davranış (açık atama dışı hepsi; FU20 / FU22 deseni).
4. **K-W2:** `waive-sod` izni açık atamayla süresiz; her kullanım ayrıca geçerli atama ister; token üretimine dokunulmaz.
5. **K-W4:** kiracı uçlarının kök yolu `/api/roles/sod-waiver-assignments` (Gateway değişmez).
6. **K-W6:** muafiyet sorgu yanıtı `docs/analysis/contracts/` altında sürümlü sözleşme dosyası; MOD-0126 sahte istemcisi ona karşı.
7. **K-A2 (b):** Auth yalnız kiracı tabanını verir; şirket kapsamını tüketici kendi kapsam portuyla süzer (Platform'a yeni bağımlılık yok).
8. **K-A3:** hizmet hesabı (`AccountKind = Service`) aday değildir; geçici kilitli kullanıcı aday kalır.
9. **K-7:** `RoleName` İngilizce PascalCase sabit kod; görünen ad rol ekranında kod → kaynak eşlemesiyle **7 dilde** (en, tr, fr, es, zh, ar, ru;
   Çekirdek kural 4). 27 rolün 7 dilli adlarını Treasury yazar, MOD-0018 sahibi teyit eder.

Açık kalan (MOD-0018 sahibinde): Auth ekinin teyidi, K-1 FU numarası, K-W5 K2 denetim izi, K-A1 uç adı / sözleşme dosyası, K-5 (Beste ile).

## D80 — Ali'nin şartlı teyidi ve FU27'nin işlenmesi (Y-19; MOD-0018 sahibinin kararı, K1 kayda alır, 2026-10-29)

Kaynak: Y-19 (MOD-0018 sahibi Ali, 2026-10-29). Bu kayıt onay satırı ya da ready-for-dev onayı değildir.

1. **Kimlik:** Treasury Auth eki **MOD-0018-FU27** olur; dosya adı ve `id` FU27'ye çevrilir, `execution/registries/module-id-registry.md`'ye
   kayıt satırı paketle birlikte yazılır; bütün atıflar FU27'ye çekilir.
2. **Auth paket kapısı (D70/3) şartlı kapanır:** MOD-0018 sahibi D69–D78 sürümünü şartla teyit etti. **DL-1 için kapı kapalı değildir**
   (bekletmez). **DL-5 kodundan önce** iki şart kapanmalı: (a) K-W5 denetim izi için defter kararı; (b) rol atama adımlarının (rola izin bağlama,
   kişiye rol verme) denetim borcu. D79'dan sonraki değişiklikler (7 dil tablosu vb.) sahip teyidine ayrıca gider.
3. **K-7 ve DL-6:** sahip D79/9'u (sabit kod + 7 dil kaynak) ve D67/1'i (aday sorgusu DL-6) teyit etti.
4. **Açık (sahipte):** K-W5, K-A1, K-A2, K-A3; D79'daki önerileri kabul listesi (K-1b, K-2, K-3, K-W2, K-W4, K-W6) ve K-5; K-W7, K-A4b teyidi.

## D81 — `Finance = 18` PR'ını Treasury açar (K1 izni, 2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); K1'in sözü: "benim
iznim … bir risk yoksa biz başlatalım".

Platform sahibinin (Beste) hazır dalı `feat/ct/audit-category-finance-01` (`b21afdc24` + main birleştirmesi; 3 dosya: `AuditEnums.cs` `Finance = 18`,
`CentralAuditPlatformValues.cs`, `CentralAuditDispatcherTests.cs`) içeriği değiştirilmeden Treasury'nin yeni dalına (`feat/ct/treasury-audit-category-finance-01`)
alınır, main birleştirilir, testler koşulur ve PR'ı Treasury açar; Beste'nin dalına yazılmaz; **onay ve merge Platform sahibinde (Beste)**.
Risk değerlendirmesi: değişiklik küçük (17 satır), içerik sahibin kendi kodu; Beste aynı değişiklik için ayrı PR açarsa biri kapatılır.

## D82 — Şartlı teyitli maddeler işaretlenir; yalnız DL-1'i bekleten maddeler dilim kapısına taşınır (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevapları: "Şartlı olarak işaretle (Önerilen)", "Dilim kapısına taşı (Önerilen)".

1. **D82/1 — şartlı teyit:** altı paketteki "Auth paket metni main'de + MOD-0018 sahibinin teyidi" maddesi (MOD-0126, MOD-0119,
   MOD-0120, MOD-0124, MOD-0289, MOD-0217) ve beş paketteki "Aday sorgusu DL-6 metni main'de" maddesi (MOD-0126, MOD-0119, MOD-0120,
   MOD-0124, MOD-0217) Ali'nin şartlı teyidiyle (Y-19 / D80/2) işaretlenir. Şart düşmez: K-W5 defteri ve rol atama denetimi
   MOD-0018 Treasury Auth ekinin DL-5 kodundan önce kapanır; FU27'deki D80/2 (a)(b) maddeleri açık kalır.
2. **D82/2 — DL-1 dilim kapısı (D51/1):** TRM-14 (MOD-0220 `ApprovalStatus` + sözleşme belgesi; MOD-0126, MOD-0119, MOD-0120,
   MOD-0124) ve "servis iskeleti MOD-0126 DL-1 ile açıldı" (MOD-0119, MOD-0120, MOD-0124) maddeleri paket kapısından "— DL-1 için"
   dilim kapısına taşınır: DL-1 başlamadan kapanır, paketi `ready-for-dev`'den alıkoymaz.

## D83 — Şartlı teyitle üç madde daha; MOD-0031 ölçüm maddeleri işaretlenir (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevapları: "Şartlı işaretle (Önerilen)", "Test et ve işaretle (Önerilen)".

1. **D83/1 — şartlı teyit (D82/1 deseni):** Ali'nin Y-19 şartlı teyidi Auth ekinin tamamını (main `4c08fc75a`, D69–D78) kapsar. Bu
   gerekçeyle MOD-0217 ve MOD-0289'daki "SEC-002 sağlayıcısı paket metni main'de" maddesi ve FU27'deki "D53/1 tüketici paket kapısı"
   maddesi şartlı olarak işaretlenir. Şart düşmez: K-W5 defteri ve rol atama denetimi DL-5 kodundan önce (FU27 D80/2 (a)(b) açık).
   Teyit D69–D78 sürümünü kapsar; D79'dan sonraki değişiklikler (ör. FU27 Bölüm 14.2 beş dil adları) ayrıca teyide gider
   (FU27 ilgili maddeler açık). Kod tarafı (SEC-002 eki DL-4 kodu, XMC-001 Beste) DL-1 kabul kapısında kalır.
2. **D83/2 — MOD-0031 ölçümü:** `docs/records/audits/2026-10/treasury-intake-handover-review-01/MOD-0031-DEDUPE-OLCUMU-01.md` kaydı ve EvidenceLinking testleri (51 geçti / 0 kaldı (ct-05 @ 29b6f2a51 = main 317a48b13 kodu)) ile MOD-0126 (DL-3), MOD-0120 (DL-3) ve MOD-0124
   (DL-4) ölçüm maddeleri işaretlenir. Sonuç: içerik tekilliği var, uzak anahtarla idempotency yok → kurtarmada yeniden çağrı
   kapalı kalır; paket kuralı değişmez.

## D84 — Ali'nin Y-21 cevapları kaydedilir; FU27 açık kararları kapanır (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevapları: "Kaydet ve kapat (Önerilen)" ve MOD-0220 için "Ali'ye sor". Kaynak: Y-21.

1. **D84/1 — kapanan satırlar (sahip teyidi, paket önerisiyle):** K-1b, K-2, K-3, K-W2, K-W4, K-W6, K-W7, K-A1 (uç adı + sürümlü
   sözleşme), K-A2 (b), K-A3, K-A4b. D80/4'teki "sahip teyidi / kararı açık" notları bu satırlarda kapanır.
2. **D84/2 — K-W5:** yöntem teyitli (`auth-gunlugu` + önce-yaz); CT defter kararı açık, DL-5 kodundan önce (D80/2 (a) açık kalır).
   D80/2 (b) (rol atama denetim borcu) Y-21'de geçmiyor; açık kalır.
3. **D84/3 — K-5:** yalnız yöntem teyitli; anahtar listesi MOD-0289 / MOD-0217 sahibinden, Platform dosyaları Beste'den — açık.
4. **D84/4 — açık kalanlar:** §14.2 27 rolün 7 dilli ad tablosu (Ali kontrol ediyor); MOD-0220 yeniden onayı ve Ali'nin "§22 alanı
   netleşene kadar ready-for-dev yok" notu — Ali'ye soruldu; cevap gelene kadar D82/2 değişmez, ama hiçbir paket `ready-for-dev`'e
   alınmaz.

## D85 — MOD-0119 DL-1 slot maddesi dilim kapısına; EVIDENCE slotu ölçümle kapanır (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevapları: "DL-1 kapısına taşı (Önerilen)", "Kapat (Önerilen)".

1. **D85/1:** MOD-0119'daki "Tüketici onayı slotları — DL-1'in kullandığı gerekli slotlar" maddesi paket kapısından "— DL-1 için"
   dilim kapısına taşınır (D51/1, D82/2 deseni): AUDIT slotu MOD-0126 DL-1 koduyla, LE slotu TRM-14 ile kapanır; ikisi de
   ready-for-dev'den önce kapanamaz. SUPPLIER için "MOD-0140 1.3.0 main'de FROZEN" ayrı paket kapısı olarak kalır.
2. **D85/2:** MOD-0119 EVIDENCE slotu (DL-3) MOD-0031 ölçümüyle kapanır (D83/2 ile aynı kanıt ve sonuç; kural değişmez).
3. **D85/3:** MOD-0119 slot tablosuna satır başına durum notu yazılır (karar değil, durum).

## D86 — **GERİ ALINDI (KARAR D87)** — K-W5 CT defter kararı: DL-5'in iki K2 komutu "K2 borcu" olarak yazılır (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevabı: "K2 borcu olarak yaz (Önerilen)". Yöntem MOD-0018 sahibince teyitli (Y-21: "`auth-gunlugu` + önce-yaz, CT defter kararı").

1. **Durum:** FU27 DL-5'in iki komutu (`CreatePaymentSodWaiverAssignmentCommand`, `RevokePaymentSodWaiverAssignmentCommand`) K2'dir
   ve `auth-gunlugu`'na önce-yaz ile yazar. `auth-gunlugu` `aday`dır: okuma ucu yok, FU27'nin ekranı yok → yol `c`'nin 3. koşulu
   sağlanmaz (audit-trail-standard §5.c-3); CT kararı bir izi `aday`dan `c`'ye çeviremez (kural §5.c, `AcceptedTrails_ArePinned`).
2. **Karar:** iki komut kapalı-başarısızdır (önce-yaz, `IAuthAuditService.WriteOnceAsync` — kural §4.4'ün Auth için gösterdiği
   K2 yolu); eksik olan yalnız izin kabulüdür (§5.c-3). Deftere satır eklemenin kurallı yolu CT kararıdır (kural §1.3; emsal
   `CT-DECISIONS.md` 2026-10-14 ESign K2 borcu satırları): DL-5 değişikliği `tests/architecture/audit-ledger/Diten.AuthService.md`
   `## K2 borcu` listesine iki satır, `CT-DECISIONS.md`'ye bu karara atıflı satır ekler ve `AuditTrailStandardTests.PinnedCounts`'u
   aynı değişiklikte yükseltir. Gerekçe satırı açıkça yazar: borç "en iyi çaba" değil, iz okunabilirliğidir (§4.4'ün "yeni K2
   borcu kabul edilmez" cümlesi kapalı-başarısız olmayan komutlar içindir). Önce-yaz kapalı-başarısız kalır (AC-30). **Ölçüm:**
   `WriteOnceAsync` main'de var (`services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IAuthAuditService.cs`,
   main `317a48b13`; kullanan: `IssueServiceIdentityTokenHandler`) — kuralın "bu dalda henüz yok" cümlesi eskidi.
3. **Kapanış:** D80/2 (a) karar olarak kapanır. Defter dosyaları bugün değişmez (Auth defteri MOD-0018 alanı; kod yokken satır
   eklenmez). D80/2 (b) (rol atama denetim borcu, `auth-rbac-gunlugu`) bu kararın kapsamında değildir; açık kalır.
4. **İleride:** `auth-gunlugu` için izinli okuma ucu gelirse (defter K7) iz `c` olur ve iki satır borçtan çıkar.

## D87 — D86 geri alınır; K-W5 açık, DL-5 kodu için engel; okuma yolu MOD-0018 sahibine önerilir (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevabı: "D86'yı geri al, okuma yolunu öner (Önerilen)". Kaynak: sahip denetimi `8ad42e9b0` — UYGUN DEĞİL, 1 ENGEL.

1. **Neden:** D86'nın yolu üretim testini geçmez. `AuditTrailStandardTests.cs:610-619` K2 borcundaki her komut için `IsAudited`
   ister; `IsAudited` yalnız kabul edilmiş ize yazan komutta doğrudur. `auth-gunlugu` `aday`dır (okuma yok, §5.c-3); önce-yaz
   (§4.4) ile okunabilirlik (§5.c-3) ayrı koşullardır, biri ötekini sağlamaz. 2026-10-14 ESign emsali farklıdır (komutlar kabul
   edilmiş merkezi iletimle denetleniyordu; eksik yalnız kapalı-başarısızlıktı). CT'nin hatası: §1.3 usulünü kabul şartının yerine
   koydu.
2. **Karar:** D86 geri alınır; D80/2 (a) yeniden açılır; K-W5 DL-5 kodu için engel kalır (DL-1…DL-4 ve DL-6'yı bekletmez).
3. **Öneri (karar MOD-0018 sahibinin):** DL-5 kapsamına `auth-gunlugu` için kiracı + izin denetimli okuma yolu ve kaydın ekranından
   erişilebilir geçmiş; yol `c`'nin beş koşulunun kanıtı; iz / çağrı belirtecinin `Diten.AuthService.md` `## İzler`'e ve kabul
   listesine işlenmesi; günlük okumasının meta-denetimi (kural §7); önce-yaz korunur. FU27'de ekran yok (UXH-001) — geçmişin hangi
   ekranda okunacağı sahibin kararıdır. Yalnız API ucu yetmez.

## D88 — Beste'nin Y-22 cevabı; K-5 listesi; DCP-011 onay PR'ı (2026-10-29)

Onay kanıtı: hesap `gmg-10` (K1); zaman 2026-10-29; oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`); soru kutusu
cevapları: DCP-011 PR "Evet, hazırla (Önerilen)"; K-5 "CT önersin, K1 onaylasın (Önerilen)" + liste "Onaylıyorum (Önerilen)"; SEC-002
yolu "Beste'ye sor (Önerilen)". Kaynak: Y-22.

1. **D88/1 — K-5 listesi (K1, MOD-0289 / MOD-0217 için sahip kararı — D19, D20/1):** MOD-0289 (9): `mdm.bank-accounts.read`, `mdm.bank-accounts.create`, `mdm.bank-accounts.update`, `mdm.bank-accounts.activate`, `mdm.bank-accounts.deactivate`, `mdm.bank-accounts.delete`, `mdm.bank-accounts.bulk-delete`, `mdm.bank-accounts.read-unmasked`, `mdm.bank-accounts.sensitive-reads.read`. MOD-0217 (14): `mdm.contracts.read`, `mdm.contracts.create`, `mdm.contracts.update`, `mdm.contracts.delete`, `mdm.contracts.bulk-delete`, `mdm.contracts.documents.read`, `mdm.contracts.documents.create`, `mdm.contracts.documents.delete`, `mdm.contracts.submit-approval`, `mdm.contracts.record-signature`, `mdm.contracts.amendments.create`, `mdm.contracts.renewal-decisions.create`, `mdm.contracts.terminate`, `mdm.contracts.archive`.
   Listede yok: `*.read-tenant-wide`, `mdm.banks.*`, `mdm.bank-account-purposes.*`, `mdm.contracts.versions.approve`,
   `mdm.contracts.value.read` / `.value.write`. Her anahtar yalnız kendi paketinin modül koduyla eşleşir (D79/1). Beste kontrol eder.
2. **D88/2 — K-6 "hayır" (Beste):** FU27 K-6 kapanır; §5.2 koşullu Platform dosyaları gerekmez; finans servisi Platform kapsam ucunu
   doğrudan kullanmaz.
3. **D88/3 — Platform XMC-001 (Beste):** FU27 DL-4 için üç dosya + ortam ayarı adımı, Y-22 (4)'teki kapsamla; kod FU27 onaylandıktan
   sonra; MDM tarafının XMC-001'i ve sıra Ali ile netleşir.
4. **D88/4 — SEC-002 tüketim yolu açık soru:** Y-4/6 "servis içi port + Platform `IDataScopeResolver` bağdaştırıcısı" diyordu;
   `OrgDataScopeResolver` yalnız Platform sürecinde kayıtlı. Y-22 "Platform kapsam ucunu doğrudan kullanmaz". Finans servisinin
   kullanıcının şirket kapsamını nereden okuyacağı Beste'ye soruldu; dört paketteki SEC-002 tüketim yolu maddeleri cevaba kadar açık.
5. **D88/5 — DCP-011:** onay PR'ı Treasury açar: yeni dal `chore/ct/treasury-dcp011-approval-01` (main'den; #304'e dokunulmaz),
   durum `draft` → `approved`, kısa Codex doğrulama incelemesi, inceleme satırı, CT-APR-01 satırı (yazan gmg-10, onaylayan
   bestepullukcu-alt); merge Beste. DCP onayı üye pack'lerin ready-for-dev'i değildir.
6. **D88/6 — tarihler (kayıt):** RCS-001 sözleşmesi 3 Kasım, servis v1 6 Kasım; Excel + kısa kod listesi en geç 2 Kasım (K1);
   MOD-0262-FU01 Platform revizyonu 2 Kasım; Auth tarafı Ali'de. #304 dalı Beste merge edene kadar güncellenmez.

## Karar olmayanlar

BL-565 servisi ve çağrı sözleşmesi, `Finance = 18`'in main'e girmesi, MOD-0149 pack revizyonu, DCP-011 onayı
(Beste); Auth kaydı (rol şablonlarının içeriği D46 ile verildi), MOD-0023 şablon kodları (O-3 / O-8 / O-11 / O-20), MOD-0220 `ApprovalStatus`
(Ali); supplier 1.3.0 (MVP-2).

## Bu kayıt ne değildir

Kod, pack onayı, push / PR ya da başka bir Control Tower'ın dosyasına yazma izni değildir.
