# MVP-5 WMS — sahip kararları (2026-11-13, Beste; kaynak: sahip sohbet cevapları)

- **Dilim sırası (aynı sayaçlar ve tek çalışma dalı):** MOD-0180 DL-1 → MOD-0182 DL-1 → MOD-0179 DL-1 ("MOD-0179 sırası … onayladım").
  Her dilim bir öncekinin CT doğrulamasından sonra başlar; Gateway uç sayısı, merkezi denetim komut sayısı ve kayıt kodu önek listesi
  dilim başında ölçülür.
- **K-4 — görevlere raf ve ürün kodu ("evet eklensin"):** toplama ve yerleştirme görevlerine (WMS-BUNDLE `WarehouseTask`) raf kodu ve
  ürün kodu / adı eklemeli olarak eklenir (mobil isteği, MOD-0181 K-2 deseni). WMS-BUNDLE'ın eklemeli yeni sürümü; MOD-0178 için ayrı FU
  (kimlik DCP-002 ile). Dil kuralı `skuName` için K-2 ile aynı.
- **MVP-1 hattının main'e PR'ı ("tamam"):** MOD-0180 DL-1 kapanınca hat dalının tek PR'ı açılır (merge commit; onay defterleri main satırları
  önce). PR'ı açma ve merge her biri yine sahip onayıyla.
- **Ali'ye bağlı işler (geliştirmeyi bekletmez, unutulmaz):** Auth AC-76 (MOD-0018-FU31; MOD-0178 canlıya çıkışı), sayım rol şablonu
  (MOD-0181 DL-4 ACCEPTED), MDM kısa kod kilidi (MOD-0040-FU02 DL-2 canlıya alma), sır kasası teknik soruları (MOD-0012-FU01 Q-1…Q-5,
  Q-13). Geliştirme bunları beklemeden sürer; her biri ilgili kabul ya da canlıya alma kapısında kontrol edilir.
- **MOD-0049-FU01 `.antigravity` kapısı ("yaz", Beste):** RCS-001 §3'e `BPT` (İş Ortağı, OTOMATİK, grup önekli, ortak servisten) satırı
  yazıldı; aynı adımda X-6: Platform `RecordCodeTypeCatalogTests` `RuleAheadOfCatalogue["BPT"] = "MOD-0049-FU01 DL-1"` — 17/17 yeşil,
  satır silinince AC16 kırmızı, geri alındı. Bu onay öneki `BPT` olarak seçer (Q-1). Q-2…Q-7 açık.
- **MOD-0049-FU01 Q-2…Q-7 ("hepsinde öneri gibi", Beste):** Q-2 = A (denetim: `mdm-merkezi-iletim` en iyi çaba + aynı işlemde yerel
  geçmiş, Brand emsali); Q-3 = iletişim kişilerine 90 gün kuralı uygulanmaz, en aza indirme + kişi silinince kalıcı silme, v1'de zamanlanmış
  temizlik yok; Q-4 = vergi numarası kiracıda tekil, listede maskeli, ayrıntıda yetkiliye açık; Q-5 = Golden Compact sapmaları paketteki gibi,
  son rol kaldırılamaz (önce pasifleştirme); Q-6 = pasif kayıt salt okunur, yalnız etkinleştir; Q-7 = domain-config'e MDM ekran / Gateway
  istisna satırı eklenir (merkez yazar). Q-1 = `BPT` (yukarıda).
- **MOD-0040-FU02 r14 Q-8…Q-16 ("hepsinde öneri gibi"):** hepsi paketteki A önerisi — Q-8 TTY kataloğa girer; Q-9 oluşturmada elle kod
  gerekçe istemez, yalnız sonraki değişiklik; Q-10 gerekçe 10–500 karakter; Q-11 kırp + Türkçe→ASCII + büyük harf, diğer işaretler red;
  Q-12 elle kod 2–40; Q-13 tekillik (kiracı, tür); Q-14 emekli koda geri dönüş yok; Q-15 DL-3'te HTTP / Gateway / ekran yok; Q-16 tüketici
  tutarlılığı DL-4'te. A-20 (dört denetim belirteci `platform-islem-ici` iz satırına) CT kararı: kabul, DL-3 merge kapısı.
- **MOD-0182 panel penceresi ("öneri gibi"):** dashboard "son 24 saat" (çağrı anından geriye); takvim günü yok, paket değişikliği yok.
  Paket "bugünkü" ifadesi bir sonraki revizyonda (r8) hizalanır.
