# MOD-0173 — sürüm notu: MOD-0173-FU03'ün kapalı hata kataloğuna eklediği beş kod (CT-02) — 2026-11-12

- CT: MVP-1 CT (oturum `ca7382cf`). Sahip: Beste ("Kabul kaydı ve sürüm notu yapalım", 2026-11-12).
- Dayanak: MOD-0173-FU03 paketi §19 CT-02 — "Beş yeni hata kodunun kapalı kataloğa `InventoryErrorCodes.All` eklenme yetkisi bu FU
  paketidir; ebeveyn MOD-0173 paketine yalnız DL-1 kabulünde sürüm notu düşülür." DL-1 VERIFIED 2026-11-03
  (`mod-0173-fu03-dl1-ct-acceptance-2026-11-03.md`); üç dilim PR #397 ile main'de (`7e8aa77d87`).

## Not
MOD-0173'ün kapalı hata kataloğu (`InventoryErrorCodes.All`, "yeni paket sürümü olmadan başka kod yok") MOD-0173-FU03 ile beş kod kazandı;
yetki FU03 paketidir (r4 onayı `c03ac5511`, r5 yeniden onay #402):

| Kod | Anlam (`InventoryErrorCodes.cs:85-100`) |
|---|---|
| `RESERVATION_NOT_FOUND` | kimlik bu kiracı + şirketin rezervasyonu değil (başka kapsamın kimliği ve UUID olmayan metin aynı cevabı alır) |
| `RESERVATION_NOT_ACTIVE` | rezervasyon RELEASED (bırakılmış / tüketilmiş) ya da EXPIRED |
| `RESERVATION_MISMATCH` | hareketin SKU'su, kaynak deposu ya da lotu rezervasyonunkinden farklı (`details.field`) |
| `RESERVATION_NOT_APPLICABLE` | hareket türü / yönü ayrılmış stoğu ne tüketir ne taşır |
| `RESERVATION_QUANTITY_EXCEEDED` | çıkışın temel miktarı rezervasyonun kalanından fazla (Q-04: kalan serbest stoktan alınmaz) |

Sözleşme: INVENTORY 1.6.0 (DL-1) / 1.7.0 (DL-2).

## Pakete yansıması
MOD-0173 paket metni bu kayıtla değiştirilmedi: paket metnindeki her değişiklik EES 2.14'te yeni revizyon, bağımsız inceleme ve onay ister.
Bu not MOD-0173'ün **bir sonraki revizyonunda** "Sürüm notları" bölümüne aynen taşınır (MOD-0173'ün EES yeniden onayı zaten açık iş).

## FU03'ün kalan açığı
- AC-19 E3 (Gateway 5000 üzerinden gerçek giriş belirteciyle `getReservation`): sahip kararıyla (Beste, 2026-11-12) MOD-0178'in canlı E3
  ölçümüyle birlikte koşulur — rezervasyonu 0178 tahsisi üretir, Web ekranı rezervasyon okumaz. ACCEPTED kaydı o ölçümden sonra.
