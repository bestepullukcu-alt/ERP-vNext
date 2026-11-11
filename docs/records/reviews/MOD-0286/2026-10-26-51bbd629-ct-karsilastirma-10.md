# MOD-0286 — Control Tower karşılaştırması 10 (Codex inceleme 10, SOP §20.4-4)

- Hedef: paket rev 10 @ 8cd2f5d4c (sha256 `51bbd629…`). İnceleme: Codex, **UYGUN DEĞİL**, 1 ENGEL (F-23) + 1 ORTA (F-22, inceleme 09'dan açık kaldı). İnceleme 09'un F-19 ve F-21'i kapandı ("Kapananlar" satırı); F-20 kısmen kapandı, kalan açık F-23 oldu. Bulgu dosyası `2026-10-26-51bbd629.md` (Codex'in metni olduğu gibi; `parse_findings`: 2 bulgu, 1 açık ENGEL, sorun 0).
- Beyan: "verilemiyor"; sahip kararı `beyan-yok`.

## CT karşılaştırması (CT ölçümü @ 8cd2f5d4c)

- **F-22 — kabul.** `platform-search.en.json` ve `.tr.json`: her biri 22 kayıt, 14 tekil `url`; `/Platform/Tenants` iki kez (en: `navigation/Platform Operations` ve `suggestions/Common Tasks`; tr: `navigation/Platform Operasyonları` ve `suggestions/Sık Kullanılanlar`). Paket `:326-330` kaydı yalnız `url` ile anahtarlıyor; aynı `url`'nin iki nesnesi tek nesneye iner ve navigation kaydının silinmesi suggestions kaydı dururken görülmeyebilir.
- **F-23 — kabul.** Paket `:363-375` çok satırlı eşleşmede başlangıç satırını özetliyor, `<k>` normalize satırın tekrarından geliyor; `excerpts/dl3/measure_checks.py:45` düzenli ifadesi (`new DataTable\([^,()]+,\s*\{`) aynı başlangıç satırında iki eşleşme verebilir; ikisi aynı anahtarı paylaşır.
- **Desen notu (CT):** inceleme 09 ve 10, parmak izi tasarımının her yeni köşe durumunda yeni bir kural istediğini gösteriyor (satır, kopya, çok satır, aynı satırda ikinci eşleşme). CT önerisi rev 11'de anahtarı sadeleştirmek: satır kontrolleri için taban satırı **sayfa + kontrol + dosya + ihlal sayısı** (düzenli ifadenin dosyadaki toplam eşleşme sayısı); dosyadaki sayı tabandakinden büyükse `YENİ KIRMIZI`, küçükse `kapandı - satırı güncelle`. Bu, aynı dosyaya eklenen her ihlali (ayrı satır, kopya, aynı satır, çok satır) eşleşme sayısı üzerinden yakalar; bedeli, aynı dosyada bir ihlal kapanıp başka bir ihlal eklenirse sayının değişmemesi — bu durum "borç büyümedi" sayılır ve paket bunu açıkça sınır olarak yazmalı. Arama kayıtları için karşılaştırma `url` başına değil (bölüm yolu + nesne) topluluğu olarak yapılır; yalnız navigation kaydını silen ya da değiştiren bir ölçüm AC-18'e eklenir.
- İtiraz yok. Kaçırılan yok.

## Sonraki adım

Sahip sözüyle: inceleme satırı 10 → rev 11 (paket yazarı; F-22 ve F-23, CT'nin sayı tabanlı önerisi değerlendirilerek) → inceleme 11.
