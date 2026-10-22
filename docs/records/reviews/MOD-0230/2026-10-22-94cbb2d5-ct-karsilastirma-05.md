# MOD-0230 — bağımsız inceleme 05: Control Tower karşılaştırması

> SOP §20.4-4: CT inceleme sonucunu paket ve kodla karşılaştırır, itirazlarını kaydeder, sonucu körü körüne kopyalamaz.
> Bu not onay, paket değişikliği ya da dağıtım izni değildir. İnceleyenin metni
> `docs/records/reviews/MOD-0230/2026-10-22-94cbb2d5.md`'dedir; sahibin yapıştırmasıyla bayt bayt aynıdır (ham kopya
> `.ct-teslim/MOD-0230-review-05/codex-review-05-raw.txt`).

## Künye

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `94cbb2d5c82a…`
  (67fe7446c; çalışma ağacında aynı bayt, ölçüldü).
- İnceleme: Codex; yanıt "sistem tanımı GPT-6" diyor, kesin varyant ve effort doğrulanamadı (istenen high); oturum "/root",
  UUID doğrulanamadı. İnceleme 04'ün oturum beyanıyla aynı biçim; yeni bir oturum olduğu kimlikle kanıtlanamıyor.
- Araç: `pack_approval.parse_findings` → 1 bulgu, 0 sorun; açık ENGEL 0; ORTA 1 (sahip "PVG paket sahibi", son tarih
  2026-10-23); `prompt_gate.named_in` → `4adc6faf`, `59d5a954`, `bfb5c82f`; bulgu dosyası sha256 `11a95e61395a…`.
- Karşılaştıran: PVG Control Tower (Claude Code, Opus 5.5). Ölçüm statik; derleme / test koşulmadı.

## 1. Bulgu karşılaştırması (paket satırları `sed -n` ile okundu, @67fe7446c)

| Bulgu | CT kararı | Ölçülen olgu |
|---|---|---|
| F-1 — bağı kaldırma ile tek seferlik CRM aramasının önceliği (ORTA) | kabul, genişletilmiş | Satır 365–370: `RemoveReporterLink = true` → `ReporterSource = Manual`, `ReporterSourceId` boş, kopya elle girilmiş değer olarak kalır. Satır 417–421: elle girilmiş bildiren, kayıt tamamlanınca Create / Update içinde CRM'de **bir kez** aranır, eşleşirse bağlanır (MOD-0150 sözleşmesinden sonra). Paket iki kuralın önceliğini söylemiyor: bağı kaldırılmış kayıt "elle girilmiş" sayılınca aynı kaydetmede yeniden bağlanabilir. BG-29 (1391–1397) kaldırmayı sözleşmesiz, bellek içi kayıtla ölçer; BG-30 (1423–1425) aramayı ayrı ölçer; ikisinin birleşimini ölçen test yok. |

**CT eki (incelemenin söylemediği):** Sorun yalnız aynı kaydetmeyle sınırlı değil. Satır 417'deki "bir kez" kuralı, aramanın
kaydın ömründe bir kez mi, yoksa elle girilmiş her bildiren için bir kez mi yapılacağını söylemiyor. Bağı seçimle
kurulmuş (arama hiç çalışmamış) bir kayıtta bağ kaldırıldıktan sonra **sonraki** bir kaydetmede de arama çalışıp bağı geri
kurabilir. Düzeltme her iki durumu kapsamalı.

## 2. CT'nin itirazı

Esasa itiraz yok. Seviye yerinde: bağ ancak MOD-0150 okuma sözleşmesi geldikten sonra kurulabildiği için (satır 421; G-22)
bugün ortaya çıkamaz; ama KR-2'nin "sessiz bağ değişikliği yok" ilkesini bozar.

## 3. Önerilen düzeltme (revizyon 06 için; yeni karar değil, KR-2 ve AL-6'nın birlikte okunması)

- Öncelik: `RemoveReporterLink = true` olan kaydetmede tek seferlik CRM araması çalışmaz.
- Kalıcılık: kullanıcının bağını kaldırdığı bir kayıtta tek seferlik arama sonraki kaydetmelerde de çalışmaz; bağ yalnız
  kullanıcının açık seçimiyle (seçim çifti) yeniden kurulur.
- Kabul testi (BG-29 ya da BG-30, MOD-0150 sözleşmesinden sonra): CRM kesin eşleşme döndürürken bağ kaldırma sonucu
  `Manual` / boş kimlik kalır, kopya korunur; sonraki bir kaydetmede de bağ geri kurulmaz.
- İkinci madde (kalıcılık) sahibin onayını ister mi: KR-2 yalnız kaldırma işlemini tanımlar; aramanın kalıcı olarak
  durması bu ilkenin doğal sonucudur, ama kayıtta yazılı değildir. CT önerisi: revizyon 06 öncesi sahibe sorulur.

## 4. Sonuç ve sıradaki adım (öneri — onay değil)

- İnceleme 05 **KOŞULLU**, açık ENGEL yok; 1 ORTA sahibi ve son tarihiyle açık. Araç bu sürüme bir onay satırının
  yazılabileceğini gösterir; onay sahibin ve onaylayanın (K2) kararıdır.
- Bulgu bugün çalışan bir yolu etkilemez: CRM araması MOD-0150 sözleşmesine bağlı ve sözleşme yok.
- Sıradaki iş: bu not, bulgu dosyası ve `reviews.tsv` satırı (satır 6); ardından sahibin onay / revizyon 06 kararı.
