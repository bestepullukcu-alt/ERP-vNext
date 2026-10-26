# MOD-0286 — Control Tower karşılaştırması 02 (Codex inceleme 02, SOP §20.4-4)

- Hedef: paket rev 02 @ 19f0b2f7a (sha256 `d15b6c8a…`). İnceleme: Codex, **UYGUN DEĞİL**, 1 ENGEL + 3 ORTA (F-9…F-12). Ham metin `.ct-teslim/MOD-0286-review-02/codex-review-02-raw.txt` (depo dışı); bulgu dosyası taslağı `2026-10-22-d15b6c8a.md` (aynı klasör, commit'siz).
- Beyan yine eksik: "GPT-6 tabanlı Codex; tam kimlik açıklanmıyor; effort istenen high, etkin doğrulanamıyor; oturum kimliği sunulmuyor". İki istekte de Codex bu değerleri veremedi; `reviews.tsv` satırları için sahip kararı gerekir.
- Yöntem: her bulgu ZIP'teki aynı sürüm dosyalarıyla CT tarafından yeniden ölçüldü. Taslak; bulgu dosyasına çevirme ve commit sahibin onayıyla.

## Bulgu bulgu

| Bulgu | CT kararı | Ölçüm |
|---|---|---|
| F-9 ENGEL · UIC-24 Save View istisnası yanlış kaynaktan; UIC-32 istisnası sınırsız | **Kabul** | Paket :167 "exception: special pages with <= 2 data columns … `frontend-js-standard.md:76`"; :76 ise madde 11 **ColReorder** altındaki istisna. Save View :73 (10.1, ZORUNLU, istisnasız) ve `frontend-datatable-template.md:24` (ZORUNLU). UIC-32 (:175) "non-standard column structure" diyor; kaynak :74-76 "standart kolon yapısı … ≤2 veri kolonu olan özel sayfalar". |
| F-10 ORTA · AC-8 / UIC-21 `; true` araç hatasını gizliyor | **Kabul, etiket itirazlı** | `check_pack_nav_keys.py:207-211` paketler okunamazsa "could not read the packs — …" basıp çıkış 2; metin MOD-0286 içermez → `grep -c` 0 → kabul sağlanır. Gerçek açık. İtiraz: 01 F-8'in ana isteği (küresel çıkışa bağımlılığın kaldırılması) rev 02'de karşılandı; "kapanmadı" yerine "kısmen kapandı, yeni açık" doğru okuma. Düzeltme yine gerekli. |
| F-11 ORTA · kapsama tablosunda üç yanlış "kapsanan" eşlemesi | **Kabul, etiket itirazlı** | `frontend-js-standard.md:289` (async init, `loadDefaultView()` await) → UIC-4 fabrika çağrısı ölçmez; `page-help-and-keyboard.md:53` (`Help.{KOD}.Related.*` koşullu zorunlu) → UIC-15 bunu ölçmez; `frontend-datatable-template.md:238` (colVis explicit override) → UIC-24 ölçmez. Üçü doğru. İtiraz: 01 F-4'ün istediği kapsama tablosu ve dört yeni madde var; kalan, üç eşleme hatası (27 → 30 kapsanmayan). |
| F-12 ORTA · iki UIC tablosu AC-4 tekillik komutuyla çelişir | **Kabul** | Paket :135-175 kontrol listesi + :186-211 koşu sözleşmesi aynı `| UIC-n |` anahtarıyla; :479 ikisi DL-2'de; AC-4 (:383) belgenin tamamında `^| UIC-[0-9]*` sayar → 20 tekrar, gruplu satır UIC-3 sayılır. CT bunu REVIEW-REQUEST-02'de "tasarım" diye beyan etmişti; Codex haklı, AC-4 ile çelişiyor. |

## İtirazlar (kaynağıyla)
- F-10 ve F-11'deki "01 F-n kapanmadı" etiketi abartılı: iki bulgunun ana istekleri rev 02'de karşılanmış, kalan açıklar yeni ve dar. Seviye (ORTA) ve düzeltme gereği kabul; etiket düzeltmesi bir sonraki isteğe not düşülür, kaydı değiştirmez.
- Başka itiraz yok; F-9 ve F-12 tam kabul.

## İncelemenin eksik bıraktığı, CT ekliyor
- **C-4:** İstek, koşu sözleşmesindeki beklenen değerlerin (UIC-1, 2, 4, 7, 13, 17, 26, 27, 29) ZIP'te yeniden ölçülmesini istedi; sonuç bunları ölçtüğünü ya da tuttuğunu yazmıyor. Bulgu yokluğu ölçüm kanıtı değildir; REVIEW-REQUEST-03 "ölçülen maddeleri ve sonucu yaz" diye ister.
- **C-5:** Model/effort/oturum beyanı iki istekte de yok; Codex ortamı bunları ajana açmıyor. Kayıt için sahip kararı: beyan metni olduğu gibi (`beyan edilmedi`) ya da sahibin Codex arayüzünden okuduğu değerler.

## Sonraki adım (CT)
Revizyon 03'ü `module-pack-author` yazar (F-9, F-10, F-11, F-12), aynı dosya, commit yok; sonra bulgu dosyası 02 + `review-row` (sahip onayı ve beyan kararı) ve REVIEW-REQUEST-03.
