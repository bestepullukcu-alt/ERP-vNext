# MOD-0175-FU01 DL-2 — Control Tower kabulü

- Tarih: 2026-11-05. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `e768b9b55`.
- İncelenen: `e768b9b55` (feat(sce): MOD-0175-FU01 DL-2 — quarantine screens show Voided and the source-reversal conflict; MOD-0178
  open-predicate fix), ebeveyn `f96f7add6` (iş `b8500cc12` üzerinde ölçüldü; arada yalnız bir karar belgesi). MVP-1 hattında; push / PR yok.
- Paket: `MOD-0175-FU01-quarantine-hold-source-reversal.md` @ sha256 `e070d6b6…5380` (r5, onaylı `79a6fa096`).
- Sonuç: **VERIFIED** (E3 smoke hariç, aşağıda). Açılış DL-1 + DL-2 birlikte (§22 F-9): bu kayıtla iki dilim CT kabulünde.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`f96f7add6..e768b9b55`) | 30 dosya: karantina ekranı (JS, görünüm, `Quarantine.cs`), `Quarantine.*.resx` 7 dil (+13 anahtar), `PageHelpResource.*.resx` 7 dil (3 adım), Web testleri; MOD-0178'de yalnız iki satır (XMC-001, Beste 2026-11-04): `GetPutawayCandidateListHandler.cs`, `WarehouseSerialReader.cs` → `QuarantineRecordStates.IsOpen(h.Status)` (+1 `using`) |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1755 / 1759, 4 atlandı, 0 kırmızı — geliştirici sayısıyla aynı (taban 1752 / 1756) |
| Tam Web takımı (filtresiz) | 4249 / 4253, 4 kırmızı — geliştirici sayısıyla aynı; dördü Planning (`CapacityPlansPageTests`, `SandopPlansPageTests`, iki `NavManifestL10nGuardTests`), tabanda aynı. Tabanın karantina kırmızısı (`HoldNoteLockWebTests`) DL-2 ile yeşil |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 221 / 6 kırmızı, tabanla aynı; uyarı Inventory 0, Web 27 (aynı küme); 17 adlı sabotajın 17'si kırmızı; resx paritesi 464 = 464 |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `WarehouseSerialReader.cs` `QuarantineRecordStates.IsOpen(h.Status)` → `h.Status != HoldStatus.Decided` (eski yüklem) | kırmızı: `SourceReversalWarehouseReadsTests.XMC0178b_the_warehouse_serial_reader_counts_the_serials_of_a_voided_hold_as_available` ve `SourceReversalOpenPredicateScanTests.AC18_the_two_MOD_0178_hold_reads_ask_the_predicate`; `git checkout` ile geri alındı, ağaç temiz |

## AC-27 notu (son bakış 02 F-3, CT kararı 2026-11-03)

AC-27 ikinci sabotajının (`blocked.js:275` → `!['Closed', 'SampleRetained'].includes(status)`) beklenen kırmızısı "Blokeyi Kaldır" ve
"İncelemeyi tamamla" düğmeleridir (`blocked.js:280`, `:281`). Geliştiricinin ölçümü: incelenmiş Voided satırda `[UNBLOCK]`, incelenmemişte
`[UNBLOCK,COMPLETEREVIEW]`. Disposition değildir; o düğme sunucunun `allowedActions` listesine bağlıdır (Voided kayıtta boş,
`QuarantineMapping.AllowedActions`).

## Sapmalar (CT kabul etti)

- `JavaScript/Node/quarantine-blocked.test.mjs` §5 Repo Scope dışında; aynı modülün (MOD-0175) testi, yalnız sahte `ui` nesnesine `conflictBadge` satırı.
- Paketteki satır başvuruları birer kaydı (`details.js:126`, MOD-0178 `:186` / `:37`); davranış aynı.
- MOD-0178 testleri Voided hold'u veritabanına doğrudan yazarak kurar (MOD-0178 AC62 emsali); kaynak geri alma akışı DL-1'de ölçüldü.
- `verify_datatable_page.py` (BL-440) karantina ekranlarını değerlendiremiyor: 8 "eksik" dosya ebeveyn MOD-0175'in adlandırmasıdır
  (`queue.js` / `blocked.js`); çıktı tabanda birebir aynı, DL-2 sapma eklemez. CT kararı: ebeveyn adlandırması kabul; betiğe ekran eşlemesi ayrı iş.

## Açık kalanlar

- **E3 smoke** (§17; DL-1 + DL-2 birlikte, en / tr / ar, `ar` RTL tarayıcıda): fleet kapalıydı, koşulmadı. CT, fleet açılınca koşar ve bu kayda ekler; açılış bu smoke'a bağlıdır.
- Web Planning kırmızıları (4) — main'de aynı gün kontrol (CT kuralı).
- DL-3 (defter bekçisi, INVENTORY 1.8.0; kabul kapısı Q-10 MVP-6) başlamadı.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
