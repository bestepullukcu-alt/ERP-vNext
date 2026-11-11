# MOD-0286 — Control Tower karşılaştırması 11 (Codex inceleme 11, SOP §20.4-4)

- Hedef: paket rev 11 @ bc06039e3 (sha256 `35e035a1…`). İnceleme: Codex, **UYGUN DEĞİL**, 1 ENGEL (F-24, yeni); inceleme 10'un F-23'ü "Kapananlar" satırında kapandı, F-22 (ORTA) `kapandı`. Bulgu dosyası `2026-10-26-35e035a1.md` (Codex'in metni olduğu gibi; `parse_findings`: 2 bulgu, 1 açık ENGEL, sorun 0).
- Beyan: "verilemiyor"; sahip kararı `beyan-yok`.

## CT karşılaştırması (CT ölçümü @ bc06039e3 ve `excerpts/rev11/counts.py`)

- **F-24 — kabul.** Paket `:382-383` satır kontrollerinin desenini CI tablosunun "Red when" sütunundan alır ve Python `re` ile sayar. Tabloda CI-4 `createCrudTable(`, CI-6 JS `.style.`, CI-8 `document.cookie` düz metin olarak yazılı; ölçüm betiği (`counts.py:20`, `:24`, `:26`) kaçışlı biçimleri kullanıyor (`createCrudTable\(`, `\.style\.`, `document\.cookie`). Düz metin `re` ile okunursa CI-4 derlenmez, CI-6 JS ve CI-8'de nokta herhangi bir karakteri eşler. Ölçülen 321 dosya / 743 eşleşme kaçışlı desenlerle alındı; bağlayıcı tablo onlarla aynı değil.
- **Kök neden:** tablo hücresi iki işi birden yapıyor — okunur açıklama ve makine deseni. Düzeltme: satır kontrolleri için ayrı, tek kaynak bir desen listesi (kontrol · dosya türü · Python `re` deseni birebir, ham dize) ve "Red when" sütunu yalnız açıklama; seçicinin öz-testi her deseni derler ve ölçüm betiğiyle aynı sayıyı verdiğini gösterir.
- **CT ek denetimi (rev 12 gönderilmeden önce yapılacak):** pakette yazılı her deseni ayrıştırıp `re.compile` ile derlemek ve `counts.py` ile aynı sayıları (321 dosya / 743 eşleşme @ 0eb90a4ce) verdiğini ölçmek; tutmazsa istek gönderilmez.
- Kapanış notu: inceleme 09'dan bu yana DL-3 tasarımı üç tur sürdü (F-19…F-24); kalan bulgu mekaniktir.
- İtiraz yok. Kaçırılan yok.

## Sonraki adım

Sahip sözüyle: inceleme satırı 11 → rev 12 (paket yazarı: desen listesi tek kaynak) → CT derleme ve sayı eşitliği denetimi → inceleme isteği 12.
