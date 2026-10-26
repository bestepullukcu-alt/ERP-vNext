# MOD-0286 — Control Tower karşılaştırması 04 (Codex inceleme 04, SOP §20.4-4)

- Hedef: paket rev 04 @ eece75428 (sha256 `75bad532…`). İnceleme: Codex, **KOŞULLU**, 0 ENGEL + 2 ORTA (F-16, F-17). F-13/F-14/F-15 ve 04b kapandı; 01 ve 02'nin bulguları geri gelmedi; "Ölçülenler" satırı tam. Ham metin `.ct-teslim/MOD-0286-review-04/codex-review-04-raw.txt`; bulgu dosyası taslağı `2026-10-22-75bad532.md` (commit'siz).
- Beyan: "verilemiyor" (dördüncü kez); sahip kararı `beyan-yok` uygulanır.

## Bulgu bulgu

| Bulgu | CT kararı | Ölçüm |
|---|---|---|
| F-16 ORTA · RC-22 "yeni hata 0" kabulü anahtar düzeyinde değil | **Kabul** | Checker `:169-175` ve `:178-191`: dil başına tek özet satırı (`Missing N key(s)`, `N key(s) still equal`) + en çok 20 anahtar, sonra `...`. Satır sayısı 35→36'da değişmez; 20 üstü anahtar listesi kesik olduğu için çıktı anahtar düzeyinde baseline bile olamaz. CT'nin rev 04 denetimi de bunu kaçırdı. Düzeltme: eksik anahtar kümesini betik kendisi çıkarır (`en` ve `<lang>` `name="…"` listeleri, `comm`), "İngilizceyle aynı" kümesini ad+değer eşleştirmesiyle çıkarır; baseline kümeleri kayıt dizininde dosya olarak commit edilir; kabul = `comm -13 baseline current` boş (yeni anahtar yok). |
| F-17 ORTA · RC-23 sınır dışı adımlar | **Kabul** | RC-23 ve UIC-15 `Step[6-9]` deseni; döngü 1..5. `Step10`/`Step10Title` hiçbir kontrole girmez. Kural `page-help-and-keyboard.md:50` "en az 1 en çok 5, boşluksuz". Düzeltme: tüm sayısal son ekler çıkarılır (`Step[0-9]*"` → sayılar), küme {1..k} ile ve `StepTitle` kümesiyle birebir karşılaştırılır; k ≤ 5. |

## İtirazlar
- Yok. İki bulgu da dar, doğru ve etiketleri yeni (önceki numaralar "kapanmadı" denmedi).

## İncelemenin eksik bıraktığı
- Yok.

## Sonraki adım (CT)
Revizyon 05'i `module-pack-author` yazar (F-16, F-17; aynı dosya, commit yok). Bulgu dosyası 04 + `review-row` 04 sahibin onayıyla (rev 05 commit'inden önce).
