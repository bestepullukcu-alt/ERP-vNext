# MOD-0178-FU01 — Çıkış sevkiyatı formu v2 — sahip kararları 01 (2026-11-13)

- Sahip: Beste (`bestepullukcu-alt`, MVP-5 merkez). Kayıt: MVP-5 WMS CT alt ajanı (module-pack-author), sohbetteki sahip sözlerinden; çağıran CT'nin
  iletisiyle. Onaylayan (paket onayı, CT-APR-01): Ali.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0178-FU01-outbound-form-v2.md` (r1, taslak).
- Ebeveyn: `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` (r29 onay bekliyor; r27 onaylı).
- Kaynak gözlem: E3 canlı turu `docs/records/audits/2026-11/mod-0178-e3-live-tour-2026-11-13.md`; düzeltme işi
  `execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0178-E3-FIX-01-ui-l10n.md` (FIX-01 FU01'den önce gider).

| # | Karar | Sahip sözü / özeti | Tarih | Pakette |
|---|---|---|---|---|
| D-1 | **Satırlar DataTable standardında.** Satır ekleme ve düzenleme yan panelde (offcanvas): ürün, SKU, miktar, lot değişimi, FEFO sapma gerekçesi. Tahsis önizlemesi panelde ya da tabloda görünür. | sohbet (çağıran CT'nin iletisi) | 2026-11-13 | §1, §8.2, §8.3, §11, AC-1…AC-9 |
| D-2 | **Alıcı CRM müşteri kaydından (account)** adı ve adresiyle seçilir; kayıt yoksa elle girilir; sevkiyatta account kimliği ve adresin **o anki kopyası** tutulur. Depodaki CRM-ACCOUNT-LOOKUP 1.0 adresi bilerek vermez (U-3 / U-1 yalnız doğrulama alanları; K-9 açık) → CRM tarafında sözleşme eki gerekir; sahibi **Beste (CRM)**. Alıcı seçimi dilimi o eke kapılıdır; **elle giriş her zaman çalışır**. | sohbet | 2026-11-13 | §7, §8.4, §8.5, §19.5 Q-1, DL-3 kapısı |
| D-3 | **Sipariş referansı şimdilik elle.** MOD-0168 Order Capture (commercial-suite, planlı, kod yok) gelince "siparişten seç, yoksa elle gir" olarak bağlanır; v1'de alan buna hazırdır, bağlama ayrı FU'dur. | sohbet | 2026-11-13 | §8.6, §20 FU-0178-FU01-ORDER |
| D-4 | **Önizleme seçilen lotu göstermeli.** Bilinen sorun (FIX-01 madde 4): bugünkü form önizlemeye her satırı `lotId: ''` ile gönderir; v2 tasarımı seçilen lotu önizlemede gösterir. | sohbet | 2026-11-13 | §8.3, AC-5, AC-6 |
| D-5 | **Mockup turu yok.** Bu FU için mockup brief'i yazılmaz; mevcut Golden Reference DataTable + offcanvas desenleri esas alınır. "Direkt düzeltmeyi yapabiliriz yeni FU ile." | sohbet (çağıran CT'nin iletisi) | 2026-11-13 | künye, §1, §11, §19.1 |

## CRM sahibinin cevabı (Beste, CRM oturumu, 2026-11-13; çağıran CT iletti) — D-2'nin CRM eki

| # | Cevap | Pakette |
|---|---|---|
| C-1 | CRM-ACCOUNT-LOOKUP 1.1 (yalnız ekleme): yeni uç `GET /api/crm/accounts/{id}/ship-to`. Kiracı sınırı; 404 (yok / silinmiş / başka kiracı aynı cevap); `tenant_id`'siz token → 400; 5xx → 503 — 1.0 kurallarıyla aynı. Adres U-1 / U-3'e eklenmez. | §7, §8.4, §15, AC-32 |
| C-2 | Ülke / şehir / ilçe **kod** döner (`CountryRef`, `CityRef`, `DistrictRef`). Görünen ad WMS'te Reference Data'dan çözülür; anlık kopyaya kod + ad yazılır (ad dile bağlı). Kod kümesi sözleşmede yazılacak. Posta kodu bugün account'ta yok (yalnız ilçe); gerekiyorsa ayrı bir CRM alan işi — v1'de alıcı CRM'den seçildiğinde posta kodu boş gelir, kullanıcı elle ekleyebilir. | §4.1a, §8.4, Q-10, Q-11 |
| C-3 | v1 tek adres (account adresi); çoklu teslimat adresi ileride 1.x eklemesi (adres listesi + isteğe bağlı adres kimliği); WMS account kimliği + adres kopyası tuttuğu için kırılma yok. | §8.4, FU-0178-FU01-MULTIADDR |
| C-4 | İzin: ayrı okuma anahtarı `crm.account-ship-to.read` (`crm.account.read` değil). Kullanıcının kendi token'ıyla, Gateway üzerinden. Anahtarı CRM manifestine CRM ekler; WMS rollerine atama Auth ile yapılır. | §14, Q-5 |
| C-5 | Tarih TBD (Beste). Sıra: 1.1 sözleşme taslağı → inceleme → kod → donma. Alıcı seçim dilimi bu donmaya kapılı; elle giriş her zaman çalışır. MVP-6 da aynı ucu kullanacak; `customerRef` = `Account.Id` ile uyumlu — WMS'te tutulan account kimliği de `Account.Id`. | §8.7, §24 DL-3 |

**Sahip kararı bekleyen (paket Q-10):** posta kodu — v1'de CRM'den seçilen alıcıda boş gelir ve elle eklenir mi (öneri), yoksa CRM'de ayrı
posta kodu alan işi mi açılır?

## Notlar

- CRM 1.1 sözleşme metni **bu kayıtla yazılmaz**; taslağı, incelemesi, kodu ve donması CRM sahibinindir (C-5). FU01 DL-3 o donmaya kapılıdır.
- D-4'ün en dar düzeltmesi FIX-01'dedir (bugünkü formda). FU01 aynı davranışı yan panele taşır ve AC ile korur; FIX-01'in kodunu tekrar yazmaz.
- D-5 Golden Reference'tan bilinçli sapmaları kaldırmaz: satır tablosunun yerel (kaydedilmemiş) veriyle çalışması paket §11'de G-FU01-01
  olarak yazılıdır ve onayla birlikte kabul edilir.
- Bu kayıt bir onay değildir; paketin onayı `execution/approvals/approvals.tsv` satırıyla (CT-APR-01, onaylayan Ali) olur.
