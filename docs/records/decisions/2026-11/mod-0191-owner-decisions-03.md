# MOD-0191 — sahip kararları 03: S-8 cevabı (Beste, 2026-11-25)

Önceki kayıtlar: `mod-0191-owner-decisions-01.md` (K1 / K2) ve `mod-0191-owner-decisions-02.md` (S-1…S-14 cevapları; S-8
açık). O kayıtlar değiştirilmez; S-8 cevabı bu yeni numaralı kayıttadır.

## S-8 cevabı (Beste, 2026-11-25)

Soru MOD-0191 paketinin r3 sürümündeki §24 S-8 satırıdır (r3: `execution/domains/supply-chain-execution/module-packs/MOD-0191-safety-stock-optimization.md`,
LF sha256 `4eea04f567c5ea95050a33d9554714b25fa0f199758cb993e3f9536fc133a7ef`; commit `47b9e5c1a`). Sahip = MVP-4 sahibi (Beste);
sahip 2026-11-24'te önce açıklama istedi, açıklamadan sonra kararı CT sohbetinde verdi. Söz: "S-8 önerilerine göre yap".

| No | Konu | Cevap | Pakete etkisi (r4) |
|---|---|---|---|
| S-8 | Golden Reference sapmaları G-0191-01 / G-0191-02 | **Kabul** — iki sapma da paket önerisindeki gibi kabul edildi (seçilmeyen: "önce FU-0191-GR"). | §10 "Bilinen sapmalar" satırı "sahip kabulü: S-8 kabul (2026-11-25, karar kaydı 03)" olur; §18 C S-8 maddesi, §20 madde 12 ve §24 S-8 satırı kapanır. |

**Kabul edilen sapmalar:**

- **G-0191-01** — r1'in düz `ManualQuantityCommand` / `PolicyMutationCommand` yapısı (tek komut kaydı + işlem enum'u, tek
  handler, tek `ExecuteAsync`) r2 / r3 / r4 dilimlerine taşınmaz ve bu dilimlerin ön koşulu değildir. Yeni komutlar
  (etkinleştirme, geçiş, override, ret) ve okuma uçları Golden Reference Slim backend düzeninde yazılır
  (`Features/ManualQuantities/` ve `Features/Policies/` altında `Commands/`, `Queries/`, `Handlers/CommandHandlers/`,
  `Handlers/QueryHandlers/`, `Validators/`, tek `{Module}Models.cs`; handler ve validator adında `Command` / `Query` soneki yok).
- **G-0191-02** — Golden Reference Slim'in `Update`, `Delete` ve `BulkDelete` komutları bu modülde yoktur. Manuel miktar ve
  politika kayıtları oluşturulduktan sonra değişmez (K2) ve değişmez yerel iz silinemez; düzeltme gerekçeli değişiklik
  (override) ya da yeni politika revision'ı ile yapılır. Soft delete yolu açılmaz; ekranlarda düzenleme, silme, toplu seçim ve
  `/bulk` ucu yoktur.

**Gerekçe:** K2 kayıtların değişmezliğini karar olarak koydu; Update / Delete / BulkDelete eklemek K2 ile ve değişmez yerel iz
(AUD-001 `c` yolu) ile çelişir. r1 yapısının taşınması davranış değiştirmeyen bir yeniden düzenlemedir; DL-2…DL-7'nin önüne
konursa etkinleştirme (K1) ve override (K2) dilimleri gereksiz yere bekler ve r1'in main'deki kanıtlı davranışı aynı dalda
riske girer. Ayrı takip işi bu iki riski ayırır.

**Kapsam:** DL-2…DL-7 (yeni komutlar, okuma uçları ve iki ekran). DL-1 (r1 çekirdeği, main'de) bu kararla değişmez;
DL-8 (BL-501, merkezi ekip, MVP'ler sonrası) yapı kararından bağımsızdır.

## FU-0191-GR — takip işinin tanımı

- **İş:** r1'in düz komut yapısının (`ManualQuantityContracts.cs` `ManualQuantityCommand` + `ManualQuantityOperation`,
  `ManualQuantityHandler.cs`, `PolicyMutationCommand` ve tek `ExecuteAsync` taşıyan depo yöntemleri) Golden Reference Slim
  klasör ve adlandırma düzenine taşınması (G-0191-01).
- **Sınır:** davranış değişmez — durum geçişleri, izinler, hata kodları, aynı işlem içi audit (`c`) ve okuma sözleşmesi aynen
  kalır; Update / Delete / BulkDelete eklenmez (G-0191-02 geçerli kalır).
- **Kanıt:** r1 testleri (AC-S01…AC-S11) ve r2 / r3 dilim testleri değişmeden yeşil; mimari testleri yeni klasör düzenini ölçer.
- **Zaman ve kimlik:** DL-2…DL-7'nin ön koşulu değildir; ayrı WP ya da ayrı dilim olarak planlanır. `FU-0191-GR` bir çalışma
  etiketidir, MOD kimliği değildir; ayrı pack gerekirse kimlik DCP-002 ön kontrolüyle (`verify_module_id.py`) alınır,
  uydurulmaz. Sahip ve tarih: TBD (MVP-4 sahibi Beste karar verir).

**Sonuç:** S-1…S-14'ün hepsi kapandı; MOD-0191 §24'te açık soru yok. Bu cevap kod izni ya da r4 onayı değildir; r4 için
bağımsız inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01, onaylayıcı Beste) ayrıca gerekir. r3 incelenmedi ve
onaylanmadı; r4 ile yer değiştirir.
