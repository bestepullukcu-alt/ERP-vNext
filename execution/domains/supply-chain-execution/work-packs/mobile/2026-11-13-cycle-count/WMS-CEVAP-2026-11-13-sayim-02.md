# Depo (WMS) → Mobil (Android): donmuş sözleşme uyumu — cevabımız (MOD-0181)

Tarih: 2026-11-13 · Cevap verilen: `MOBIL-CEVAP-2026-11-13-sayim-donmus-sozlesme.md` (Android dalı `feature/warehouse-count-frozen-contract`, `749a1068`)

Teşekkürler; uyum notunuzu okuduk. Donmuş WMS-COUNT-BUNDLE 1.1.0 ve WMS-BUNDLE 1.1.0'a göre yaptığınız değişiklikler bizim beklediğimizle
aynı: gönderimden hemen önce sayımın yeniden okunması ve onun sürümünün `If-Match` olarak gitmesi, seri kontrollü satırda tam sayı, 200'lük
sayfalama, kör sayımda gönderimden sonra da beklenen miktarın gösterilmemesi, yeniden sayımda yeni anahtar / cevapsızlıkta aynı anahtar,
409 `COUNT_CONFLICT` → yeniden okuma. Toplama ve yerleştirmede 503 `Pending` + `details.seq` ile `…/confirmations/{seq}/retry` yolu doğru.
Çeviri sayısı düzeltmesi (152 − 7 + 43 = 188; bugün 238) not alındı.

## Testler ve master

Sorunuza cevap: **evet, testleri şimdi koşun ve emülatörde örnek modda bir tur atın.** Testler yeşilse dalı kendi master'ınıza almanız
uygundur — bu sizin deponuzun kararı, bize bağlı değil. Lütfen sonucu (test sayıları ve kısa emülatör notu) bize iletin.

## Açık kararlar

- **K-2 ve K-3 — kabul edildi (ürün sahibi, 2026-11-13).** WMS-COUNT-BUNDLE'ın eklemeli **1.2.0** sürümü açılıyor: `CycleCountLine`'a
  `locationCode`, `skuCode`, `lotNumber` (dil kuralıyla `skuName`), liste ucuna `assignedTo` / `unassigned=true` süzgeci. MOD-0181 için
  ayrı bir ek iş olarak yürüyor; 1.1.0 değişmiyor. Sözleşme taslağı hazır olunca size göndereceğiz; o zamana kadar bugünkü davranışınız
  (ayrı okumalar ve uygulama tarafında ayırma) doğru.
- **K-4 — görevlerde raf ve ürün kodu:** görüşünüzü aldık. Bu WMS-BUNDLE (MOD-0178) için ayrı bir karar; K-2 ile aynı desende ele alıp
  size haber vereceğiz.
- **K-5 — MOD-0182 `Count` bağlamı:** anladığınız akış doğru (raf okut → ürün okut → sunucu satırı ya da bulunan stoğu söyler;
  `scanRef` = `scan-session:{sessionId}`). MOD-0182 paketi onaylı; ilk dilimi Dalga Planlama'nın ilk diliminden hemen sonra başlıyor.
  SCAN-BUNDLE taslağı çıkınca paylaşacağız; bulunan stok ekranını o zaman açarsınız.

Teşekkürler.
