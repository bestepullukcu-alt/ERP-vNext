# Temiz turdan sonra düşük riskli değişiklikte incelemenin taşınması — tasarım ve test planı

Durum: **taslak, uygulanmadı.** Sahip 2026-10-31 "başlat" ile iki haftalık denemeyi açtı; bu madde için "önce test planı,
uygulamadan önce bana getir" dedi. Kod ve kural değişmedi.

## Sorun (ölçüldü, `origin/main` reviews.tsv, 2026-10-31)

Temiz (UYGUN, açık ENGEL yok) bir turdan sonra paket değişip yeniden incelenen 89 tur var. Aracın kendi risk ölçümü
(`pack_risk`) ile:

| Değişiklik | Tur | Sonuç | Açık engel |
|---|---|---|---|
| LOW | 50 | 47 UYGUN · 2 KOŞULLU · 1 UYGUN DEĞİL | 1 |
| HIGH | 39 | 30 UYGUN · 6 KOŞULLU · 3 UYGUN DEĞİL | 5 |

Değişen satır: ≤5 → 7 tur, ≤30 → 37, ≤100 → 24, >100 → 21. "Tek harf" kuralı tek başına 7 tur kazandırır; risk kuralı 50.
Bugün `review_exempt` yalnız tarih, boş sahip/inceleyen ve boşluk değişikliğini muaf tutar; 89 turun hiçbiri muaf değildi.
Kaçan 1 engelin kaydı uygulamadan önce okunup sahibe gösterilecek.

## Kural (öneri)

Bir paket sürümünün kendi inceleme satırı yoksa inceleme **taşınır**, şu beş koşulun hepsi tutarsa:

1. Aynı hedefin daha eski bir sürümünde doğrudan (taşınmamış) inceleme satırı var ve sonucu UYGUN, açık ENGEL 0.
2. O sürümden bu sürüme `pack_risk` LOW: künye kimlik alanı, duyarlı başlık (Repo Scope, Protected Paths, yetki, gateway,
   şema/veri, entegrasyon, denetim, güvenlik, kiracı), `.antigravity` ya da `ocelot.json` anan satır değişmemiş.
3. Zincir yok: taşınan bir incelemeden ikinci kez taşınmaz (salam dilimi engeli). Her taşıma doğrudan bir incelemeye dayanır.
4. Daha yeni hiçbir sürümde UYGUN DEĞİL ya da açık ENGEL yok (en kısıtlayıcı okuma korunur).
5. Fark onay PR'ında görünür: onay satırının notuna `taşınan inceleme <eski8>→<yeni8>, N satır` yazılır; onaylayan farkı okur.

Mühür değişmez: onay yine tam sürüm özetine bağlanır. Değişen yalnız "bu sürüm için Codex turu şart" koşulu.

## Değişecek yerler

- `scripts/pack_approval.py`: `review_problems` (satır yoksa taşıma araması), yeni `review_carry`, `make_row` notu.
- `scripts/prompt_gate.py`: dağıtım kapısı taşınan incelemeyi aynı ölçütle kabul eder; "inceleme yok" mesajı taşımanın
  neden olmadığını söyler (hangi koşul düştü).
- SOP §20.4 ve kart §1.1: metin, haftalık kural penceresinde. `.antigravity` değişmez.

## Sabotaj testleri (hepsi kırmızı olmalı)

| # | Sabotaj | Beklenen |
|---|---|---|
| 1 | Repo Scope altında tek satır değişikliği | taşınmaz, "risk HIGH: Repo Scope" |
| 2 | Künye `service` değişikliği | taşınmaz |
| 3 | A→B taşındı, B→C yine LOW | C taşınmaz, "zincir" |
| 4 | Eski sürüm KOŞULLU, ENGEL 1 | taşınmaz |
| 5 | Daha yeni sürümde UYGUN DEĞİL satırı var | taşınmaz |
| 6 | `.antigravity/...` anan satır eklendi | taşınmaz |
| 7 | Taşıma notu silinmiş onay satırı | onay geçersiz (not zorunlu) |
| 8 | Kaynak inceleme satırı başka hedefe ait | taşınmaz |
| 9 | Yeşil: duyarlı olmayan bölümde 12 satır düzeltme | taşınır, not yazılır |

Yeniden oynatma: 89 geçmiş tur aracın yeni fonksiyonuyla koşulur; 50 taşınır / 39 taşınmaz sonucu birebir çıkmalı.

## Yürürlük

Araç + öz testler → Codex incelemesi (araç farkı) → sahip onayı → haftalık kural PR'ı (SOP metniyle birlikte). Deneme
bitiminde (2026-11-14) ölçüm: taşınan inceleme sayısı, taşınan sürümlerde kod aşamasında çıkan hata.
