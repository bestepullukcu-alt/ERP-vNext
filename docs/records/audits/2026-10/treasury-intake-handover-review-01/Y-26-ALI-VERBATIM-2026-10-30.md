---
status: source-record
date: 2026-10-30
source: Ali (MOD-0018 / MDM sahibi) — Teams mesajı; K1 (gmg-10) bu oturuma kelimesi kelimesine yapıştırdı (iki parça: önce kısa, sonra tam metin)
related: SOURCE-ANSWERS-2026-10-27.md Y-26; treasury-intake-owner-decisions-05.md D97; PR #311 (Ali'nin PR'da yorumu yok — 2026-10-30 ölçümü)
---

# Y-26 — Ali'nin özgün yanıtı (kelimesi kelimesine)

Kanal: Teams; PR [#311](https://github.com/bestepullukcu-alt/ERP-vNext/pull/311)'de Ali'nin yorumu yok (2026-10-30, sayfa okundu).
Aşağıdaki iki blok K1'in yapıştırdığı metinlerdir; değiştirilmedi. İkinci blok birincinin genişletilmiş hâlidir; Y-26 ikinciye dayanır.

## Parça 1 (ilk yapıştırma)

```text
Ali Tufanoglu, Now
3: izin var (XMC-001, MDM istemcisi). Sıra: Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi; yalnız o dosya ve testleri.


4: 1, 2, 3, 5, 6 kabul. 4. madde (ekran): evet, Roller ekranında salt-okunur atama listesi + "Geçmiş" bölümü. Düzeltmeler: (a) iz belirteci iki komut için ayrı bir yazıcı tipinin metodu olsun; "WriteOnceAsync + komut adı" biçimi tanımsız ve servis kimliği işleyicisinin istisnasını kaydırır. (b) Auth'un iletici anahtarı boşsa okuma satırı sessizce kaybolur; kabul ölçütüne yapılandırma şartı yazılsın. (c) Ekran kiracı tarafındaysa 7 dil.


5: ayrı MOD-0018 eki (FU27'ye eklenmez). Kimlik verify_module_id.py ön kontrolüyle açılır. Rol adlarına "Tag" öneki önerilir (çakışma riski). Açık atama anahtarı sayısı bu kayıtla 21'e çıkar.
```

## Parça 2 (tam metin)

```text
Ali → Treasury (PR #311 kararları + MOD-0220):


3 (XMC-001): izin var. MDM istemcisi (PlatformTrustedLegalEntityScopeProviderClient + testleri). Sıra: Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi; yalnız o dosya ve testleri.


4 (K-W5 okuma yolu): 1, 2, 3, 5, 6 kabul. 4. madde (ekran): evet, Roller ekranında salt-okunur atama listesi + "Geçmiş" bölümü. Düzeltmeler: (a) iz belirteci iki komut için ayrı bir yazıcı tipinin metodu olsun; "WriteOnceAsync + komut adı" biçimi tanımsız ve servis kimliği işleyicisinin istisnasını kaydırır. (b) Auth'un iletici anahtarı boşsa okuma satırı sessizce kaybolur; kabul ölçütüne yapılandırma şartı yazılsın. (c) Ekran kiracı tarafındaysa 7 dil.


5 (MOD-0057): ayrı MOD-0018 eki, FU27'ye eklenmez. Kimlik verify_module_id.py ön kontrolüyle açılır. Rol adlarına "Tag" öneki önerilir. Açık atama anahtarı sayısı bu kayıtla 21'e çıkar.


MOD-0262-FU01 Auth tarafı: MDM servisi için istemci kaydı ve hedef kitle; yeni kullanıcı izni yok. Kapsamı Auth kulesi yazacak. Tahmini tarih (ölçülmedi): paket metni 3 Kasım, kod onaydan 1–2 gün sonra.


MOD-0220 §22: "§22 alanı" = MOD-0220 paketinin §22'si (TRM-14), FU27 değil. Onay satırı main'de (bestepullukcu-alt, 26 Ekim) ama satırı ekleyenle birleştirenin ayrı olup olmadığını Yönetici doğruluyor. Başlığı siz değiştirmeyin. TRM-14'ün DL-1 dilim kapısı olarak kalması yeter; ready-for-dev'i bekletme kararı bu doğrulamanın sonucuna bağlı, cevabı ayrıca vereceğim. 7 dilli rol adı tablosunun sonucu da ayrıca gelecek.
```

Erişilebilir ikinci referans: Ali'den aynı metni PR #311'e yorum olarak yazması istenir (Treasury CT mesaj taslağı); gelirse bu dosyaya
yorum bağlantısı eklenir.
