# MOD-0230 — inceleme 09 Control Tower karşılaştırma notu

- **İnceleme:** `docs/records/reviews/MOD-0230/2026-10-23-7dd6a8da.md`. Yapan: Codex (GPT-6). Kimlik olarak `/root` yazılmış, UUID doğrulanamadı.
- **Hedef:** revizyon 09 @ `7dd6a8dad2362a54df97c4ecdd682ec40d11cf7204a5db5c38f6659b9ed048d2`.
- **Sonuç:** UYGUN. Bulgu satırı yok.
- **CT değerlendirmesi:** Sonucu kabul ediyorum. İnceleyenin iddialarını CT kendi ölçümüyle karşılaştırdı (`fba750a48`, `ct_check.py`):
  - İnceleme 08'in F-1'i kapandı. 33 ölçütün hepsi tek satırda. `parse_pack` ölçüt metinlerini tam alıyor: AC-5 371 karakter, AC-12 715, AC-30 5 587, AC-33 1 709 karakter.
  - Revizyon 08 ile belirteç dizisi aynı; tek fark revizyon 09 notu. Künye bayt bayt aynı.
  - `slices` DL-1…DL-9'u okudu.

## İnceleyenin belirttiği sınır

İnceleyen, revizyon 07'nin tam metni ZIP 09'da olmadığı için 07 → 08 eşlemesini bu incelemede yeniden ölçemediğini yazdı. CT bu eşlemeyi iki yoldan ölçmüştü:

- **İnceleme 08:** O inceleme revizyon 07'nin tam metnini ve 07 → 08 diff'ini ZIP'te aldı. Yalnız F-1'i buldu; eşleme ve içerik için bulgu vermedi.
- **CT'nin kelime denetimi:** `pvg-mod0230-ac-esleme-01.md`, "Ölçüm" bölümü.
  - Silinen sözcükler yalnız 33 ayraç ve beş çapraz atıf.
  - Eklenen sözcükler yalnız işaretler, not ve başlık.

Bu yüzden 07 → 08 eşlemesi iki ayrı ölçümle desteklenmiş oluyor. Bu sınır yeni bir bulgu değildir.

## S-44 notu

İnceleyen, S-44 defter satırının "sahip kararı bekliyor" ifadesinin kararın öncesini gösterdiğini not etti. O satır CT-Setup'ın dosyası (`ct-setup-test-01/FINDINGS.md`); PVG ona dokunmaz (XMC-001). Güncelleme CT-Setup'ındır.

## Sonraki adım

1. `reviews.tsv` satırı eklenir.
2. Sahibin onayıyla, bu sürüme bağlı onay satırı yazılır (CT-APR-01). Onay satırı tek PR'da gider ve PR'ı onaylayan birleştirir (S-41, kart §7a/7).
3. Onaydan sonra WP-0230-DL2A, DL3A ve DL8 için künyeli kabul kayıtları yazılır.
