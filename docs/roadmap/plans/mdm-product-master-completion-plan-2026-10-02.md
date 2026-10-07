# Ürün Ana Verisi Tamamlama Planı (MOD-0290)

**Durum:** TASLAK — Stok ekibi onayladı (2026-10-07, §4a); diğer ekipler bekleniyor · **Tarih:** 2026-10-02 · **Güncelleme:** 2026-10-07 · **Sahibi:** Control Tower (MDM ürün modülleri)
**İş listesi:** BL-518 (bu plan) · BL-519 (elektronik imza) · BL-503 / BL-510 (denetim izi) · BL-502 (onay motoru)
**Ölçüm kaynağı:** `feature/mdm/product-five-takeover` dalı, 2026-10-02

> Bu belge ileriye dönük bir plandır; paketler kabul edildikçe güncellenir. Rakamlar tahmindir ve bugünkü ölçülen
> hıza dayanır (bir paket = tasarım + yapım + ortalama iki düzeltme turu ≈ 4 prompt, 1–2 gün).

---

## 1. Amaç

Blueprint, MOD-0290 için şunları sayar: ürün kaydı, **kalem kaydı**, SKU, **ölçü birimi eşlemesi**, **ürün
tanımlayıcıları**, yaşam döngüsü. Bugün yazılmış olan yalnız ürün kimlik zinciridir. Sahip kararı (2026-10-02):
eksikler çıkarılır ve hepsi yapılır. Bu plan eksikleri, sırayı ve öbür ekiplerle sınırları yazar.

Değişmeyen karar: **Global Ürün bugünkü haliyle kalır** (ad + kod). Yeni her şey onun altındaki katmanlara ya da
yanına eklenir; yazılmış kayda dokunulmaz.

## 2. Hedef model

```text
Marka ─────────────┐                                   Madde (etkin / yardımcı)
  └ Marka tescili  │ (bağ: P8)                              │
                   ▼                                        ▼
Global Ürün ──► Ürün Tanımı Sürümü ──────────────────► Bileşim (P5)
 (ad + kod)      (REV-001: form, yol, güç: P3; yeni sürüm: P4)
                   │
                   ├──► GSKU (ambalaj) ──► LSKU (ülke) ──► Pazar ticari adı (P7)
                   │       ├──► Bitmiş Ürün                └─► Ruhsat (ayrı modül: RIM)
                   │       ├──► Tanımlayıcı / GTIN (P6)
                   │       └──► Ambalaj hiyerarşisi (P9)
                   │
Kalem (hammadde, ambalaj malzemesi, …) (P1) ──► Ölçü birimi + çevrim (P2)
   ▲                         │
   └── hangi maddedir        └──► Stok (MOD-0173), Satın Alma, Üretim reçetesi: yalnız KİMLİKLE bağlanır
```

## 3. Bugün ne var

| Parça | Kod | Ekran | Kabul |
|---|---|---|---|
| Global Ürün | var | var | sürüyor (onay akışı dayanıklılığı 3. tur; denetim izi sırada) |
| GSKU · LSKU · Bitmiş Ürün | var | var | bekliyor |
| Kısaltma Kaydı · Şirket Kapsamı | var | var | bekliyor |
| Marka · Ürünler (CRM tarafı) | var | var | bekliyor |
| Ürün Tanımı Sürümü | yalnız kimlik (hangi Global Ürün, sürüm no) | yok | — |
| Pazar ticari adı · eski sistem kodları | tasarımda var, kod yok | yok | — |
| Kalem / malzeme · ölçü birimi eşlemesi · tanımlayıcı · madde · bileşim · ambalaj hiyerarşisi | yok | yok | — |

Bilinen iki kopukluk: Marka bugün Global Ürüne değil CRM tarafındaki Ürünler ekranına bağlı; onaylarda elektronik
imza yok (§7).

## 4. Paketler

| # | Paket | İçerik | Büyüklük | Önce ne bitmeli | Regresyon riski |
|---|---|---|---|---|---|
| P0 | **Kabul** | Yazılmış parçaların kabulü, bu sırayla (sahip kararı 2026-10-03): Global Ürün → GSKU → LSKU → Kısaltma Kaydı (ABB) → Şirket Kapsamı → Marka → Bitmiş Ürün; her birinin denetim izi (BL-503). Farmakovijilans bu yedisinden sonra | sürüyor | — | — |
| P1 | **Kalem / malzeme kaydı** | Hammadde, ambalaj malzemesi ve diğer stoklanabilir kalemler: kod, ad, tür, durum, temel ölçü birimi, lot / son kullanma izlenir mi, saklama koşulu; Bitmiş Ürün ile ortak "stoklanabilir kalem" okuma sözleşmesi. Ölçüm ve tasarım hazır (2026-10-03): [kalem kaydı tasarımı](mdm-item-master-p1-design-2026-10-03.md) — sahibin altı kararı bekleniyor | büyük (2 paket) | stok ekibinin cevabı (§6) + sahibin altı kararı | 🟡 |
| P2 | **Ölçü birimi eşlemesi** | Kalem başına birim çevrimleri (kg ↔ g, kutu ↔ adet); referans listesi MOD-0048'de kalır | küçük–orta (1) | P1 tasarımı | 🟢 |
| P3 | **Ürün tanımı alanları** | Sürüme farmasötik form, veriliş yolu, güç (tek etkin maddeli) | orta (1) | P0'da GSKU kabulü | 🟢 |
| P4 | **İkinci sürüm kuralı** | REV-002 açma, hangi sürüm geçerli, eski sürümdeki kutular | orta (1) | P3 | 🟡 |
| P5 | **Madde + Bileşim** | Madde kaydı (etkin / yardımcı); sürüme bağlı bileşim satırları (madde, rol, miktar, birim); çok etkin maddeli ürünün onayı | büyük (2) | P3, P4; madde kaydının sahibi (§6) | 🟡 |
| P6 | **Tanımlayıcılar** | GTIN / barkod: GSKU ya da ambalaj düzeyinde, yaşam döngüsüyle | orta (1) | P0 | 🟢 |
| P7 | **Pazar ticari adı** | LSKU'ya bağlı, ülke + dil + geçerlilik aralığı; onaylı ad üzerine yazılmaz | orta (1) | P0'da LSKU kabulü | 🟢 |
| P8 | **Marka bağı ve tescil** | Marka ↔ Global Ürün bağı; Markanın altında tescil listesi (ülke / ofis, no, sınıf, sahip şirket, tarihler) | küçük–orta (1) | P0 | 🟡 (CRM'in Ürünler ekranı) |
| P9 | **Ambalaj hiyerarşisi** | Kutu → koli → palet; her düzeyin adedi ve tanımlayıcısı | orta (1) | P2, P6 | 🟢 |
| P10 | **Eski kodlar ve veri taşıma** | Eski sistem kodu ↔ yeni kimlik; toplu içe aktarma | orta (1–2) | gerçek eski veri | 🟡 |
| P11 | **Global Ürün adı** | Adı değiştirme, geçmişi, onayı | küçük (1) | P0 | 🟢 |

Toplam P1–P11: yaklaşık 13 paket ≈ 50 prompt; iki sohbet paralel çalışırsa 3–4 hafta.

**Bu planın dışında, ayrı modül olanlar:** ruhsat (RIM), pazar arz ataması, etiket / prospektüs yaşam döngüsü,
üretim reçetesi (Ürün Ağacı ve Rotalar), dış sistem beslemeleri (ERP / PLM), stok defteri (MOD-0173).

## 4a. Stok paketi — Stok ekibiyle uzlaşma (2026-10-07)

Stok (MOD-0173 / MOD-0174, MVP-1) ekibinin "Inventory Capability Baseline" sayfası ve sorularımıza verdiği cevap. Ekip
önerilerimizin hepsini kabul etti. Bu paket P1 + P2 + P6'nın stoğa dönük kısmını öne alır; kalan P paketleri yerinde.

**Kararlar:**
- **Kimlik (S1):** tek okuma sözleşmesi. Mevcut v1 yolları (`GET /products/{itemId}`, `/skus/{skuId}`,
  `/skus/{skuId}/product`, `/skus/{skuId}/uom`, `POST /validate`) Kalem türünü de cevaplar. Yeni yollar eklenebilir,
  eskilerin yerine geçmez.
  - v1.1 (yalnız ekleme): `ItemKind` (`GlobalProduct` | `Item`), `SkuLevel`'e `Item`, Kalem'de `materialType`.
  - Hammaddede kalem kimliği = SKU kimliği; SKU → kalem çözümlemesi (D-SKU-LINK) Kalem için kendisini döndürür.
  - Stok ekibi kendi sözleşmelerine `skuLevel: Item` ekleyip DEC-INV-17'yi güncelleyecek.
- **Bileşim (S2):** madde kaydı + ruhsat / etiket bileşimi MDM'de (Ürün Tanımı Sürümüne bağlı, P5 daraltıldı); parti
  reçetesi MOD-0193 BOM'da (MVP-3 üretim), MDM kimliğine referansla. BOM sahibi kişi / dal: MVP-3'ten sorulacak.
- **Görünürlük (S6):** Kalem kiracı genelinde. Bakiye ve hareket zaten şirket bazında (DEC-INV-18).
- **Yaşam döngüsü (S5):**
  - Taslak: hareket yok.
  - Etkin: evet.
  - Kullanım dışı: yeni giriş yok; mevcut stok çıkabilir, hurda / iade yapılabilir.
  - Emekli: hareket yok.
  - Her durumda ters kayıt kabul edilir.
  - Stoğu sıfır olmayan kalem emekli yapılamaz: stok ekibinin uygunluk sorgusu çağrılır; ulaşılamazsa emekliye ayırma reddedilir.
  - Sözleşmeye `Deactivated` ve `Retired` durumları eklemeyle girer.
- **Bitmiş Ürünün üst kaydı (D-2):** GSKU. Bugünkü kod `FinishedGood.GskuId`; tutarlı kalır.
- **GTIN (D-1):** Bitmiş Ürün satış kutusu düzeyinde gerekli (İTS karekodundaki (01)), stoklanan SKU'dan okunur. Koli / palet düzeyi MOD-0178 depo dalgasında (P9).

**Alanlar (S3):**

| Alan | Kalem | GSKU | Bitmiş Ürün | Not |
|---|---|---|---|---|
| `materialType` | gerekli | — | — | ACTIVE_INGREDIENT, EXCIPIENT, RAW_MATERIAL, PACKAGING_PRIMARY, PACKAGING_SECONDARY, INTERMEDIATE; mamul = Global Ürün (`ItemKind`) |
| Temel birim | gerekli | gerekli | gerekli | stok her zaman temel birimde; MOD-0048'de yeni birim listesi (UN/ECE Rec 20: en az MGM, GRM, KGM, MLT, LTR, C62 + kutu / koli birimleri) |
| Birim çevrimleri | gerekli | gerekli (kutu ↔ adet) | gerekli (koli ↔ kutu) | yönlü `{from, to, pay, payda}`, ondalık metin, yuvarlamasız; stok 4 adıma kadar zincirler |
| GTIN | sonra | — | gerekli | satış kutusu |
| Raf ömrü (gün) | gerekli | gerekli (ruhsatlı) | gerekli (GSKU'dan gelebilir) | SKT ve FEFO (MOD-0176) |
| Yeniden test (gün) | gerekli | — | — | MOD-0175 kuyruğu |
| Saklama koşulu | gerekli | gerekli | gerekli | MOD-0048 çoklu seçim listesi: 15–25°C, 2–8°C, ≤ −20°C, ışıktan koru, nemden koru |
| Lot takibi | gerekli | gerekli | gerekli | |
| Seri takibi | — | sonra | gerekli | pazara bağlı (İTS) → SKU düzeyinde (LSKU / Bitmiş Ürün); v1.1'de SKU düzeyi alan |
| Stoklanabilir mi | gerekli | gerekli | gerekli | |
| Sonra | tehlike sınıfı · CoA zorunlu mu · girişte varsayılan karantina | | | MOD-0175 |

**Sözleşme ekleri (S4):**
- stoklanabilir kalem / SKU araması (sayfalı, `q`); stoğun geçici "Model A seçici" rotası (W-UI-01) bununla kalkar;
- toplu SKU okuma (kimlik listesi → kimlik + ad + kod).
- Olay MVP-1'de gerekmez (stok her harekette canlı okur, ulaşamazsa reddeder).
- **MDM 64 KB istek başlığını kabul eder:** gelen belirteçler ~35 KB.

**Stok ekibinin isteği (R-07):** geliştirme kiracısında birkaç Global Ürün + GSKU / LSKU + 1 lot takipli + 1 seri
takipli + 1 hammadde Kalem. Paylaşılan dev verisini sahip girer; CT betik / adımları verir. v1.1 hazır olunca stok
ekibine "hazır" denir: Prism köprüsünü kaldırıp gerçek MDM'ye karşı G1 kimlik maddelerini kapatırlar.

**Dilimler (S paketi, yaklaşık 8–10 prompt):**

| # | Dilim | İçerik |
|---|---|---|
| S0 | Belge | Sözleşme v1.1 (yalnız ekleme) + MOD-0048 yeni listeler (birim, saklama koşulu, malzeme türü) + kalem paketi (MOD-0290-FU04) + denetlenen olaylar; dondurma |
| S1 | Kalem sunucu çekirdeği | taslak / düzenle / kod (RCS-001 §4: ürün kimliği ailesi) / denetim; yaşam döngüsü durumları; emekliye ayırmada stok uygunluk denetimi |
| S2 | Etkinleştirme onayı | MOD-0023, paylaşılan bağlantı noktası |
| S3 | Stok davranışı alanları | GSKU / LSKU / Bitmiş Ürün: temel birim, çevrimler, raf ömrü, saklama, lot, seri (SKU düzeyi), stoklanabilir, Bitmiş Ürün GTIN'i |
| S4 | Okuma sözleşmesi | mevcut 5 yolun Kalem'i cevaplaması, D-SKU-LINK, arama, toplu okuma, 64 KB başlık |
| S5 | Ekranlar | **Kalemler** sayfası + GSKU / Bitmiş Ürün "Stok davranışı" bölümü; 7 dil, menü, Ctrl+K, yetkisiz yüz |
| S6 | Birim çevrimi doğrulamaları | zincir, yönlü çevrim, ondalık hassasiyet (P2'nin stoğa dönük kısmı) |
| S7 | Dev verisi + "hazır" | R-07 adımları (sahip girer) + stok ekibine bildirim |

## 5. Sıra

| Faz | Paketler | Neden bu sırada |
|---|---|---|
| 1 | P0 | Kabul edilmemiş kodun üstüne yeni kod yazılmaz |
| 2 | **Stok paketi (§4a: S0–S7)** = P1 + P2 + P6'nın stoğa dönük kısmı | Stok ekibi G1 kimlik maddeleri için bekliyor ve hemen başlayabiliyor; beklerse kendi kalem kaydını yazar ve iki ana veri oluşur |
| 3 | P3 → P4 → P5 | Bileşim sürüme bağlanır; sürüm kuralı olmadan bileşim değişikliği eski kaydın üzerine yazar |
| 4 | P6 · P7 · P8 · P9 · P11 | Birbirinden bağımsız; iki sohbetle paralel |
| 5 | P10 | Gerçek eski veri gelince |

Faz 2 ile faz 3 iki ayrı sohbette paralel yürüyebilir.

## 6. Öbür ekiplerle sınırlar — onayınız ve cevabınız gereken yerler

Ortak kural: **ana verinin tek sahibi MDM'dir.** Diğer servisler kaydı **kimliğiyle** tutar; ad, kod ya da alan
kopyalamaz (ekranda göstermek için tutulan kopya, ana kayıt değişince eski sayılır). SAP'de tek malzeme kaydı,
Oracle'da tek kalem kaydı aynı kuralı uygular.

| Ekip | Sizden istenen | Bizim önerimiz |
|---|---|---|
| **Stok (MOD-0173)** | 1) Stok satırı ürünü / malzemeyi hangi kimlikle tutacak? 2) Kalem kaydında hangi alanlara ihtiyacınız var (lot izlenir mi, raf ömrü, saklama koşulu, temel birim, başka)? 3) Kalem kaydı gelmeden yazdığınız geçici bir kalem tablosu var mı? | Tek "stoklanabilir kalem" kimliği: hammadde ve ambalaj malzemesi P1'deki Kalem'dir, bitmiş ürün mevcut Bitmiş Ürün kaydıdır; ikisi aynı okuma sözleşmesinden döner. Lot, seri, bakiye ve hareket stokta kalır; kalemin tanımı MDM'de. |
| **Madde kaydını yazan geliştirici** (varsa) | Hangi dalda, hangi alanlarla? | Madde kaydı MDM'de durur; bileşim Ürün Tanımı Sürümüne bağlanır (Global Ürüne değil). Yazılmış bir şey varsa P5 ona göre küçülür. |
| **CRM** | Ürünler ekranındaki "Ürün", Global Ürüne mi yoksa Ürün Tanımı Sürümüne mi karşılık geliyor? | P8'de Marka → Global Ürün bağı kurulur; CRM'in Ürünler kaydı Global Ürünü kimliğiyle gösterir, kendi alanlarını (tanıtım amaçlı) korur. |
| **Farmakovijilans** | Vakada "şüpheli ürün" hangi düzeyde tutuluyor? | Ürün için LSKU ya da Global Ürün kimliği; etkin madde için P5'teki Madde kimliği. |
| **Satın Alma** | Sipariş satırı neyi gösterecek? | P1'deki Kalem kimliği + P2'deki birim. |
| **PPM** | Projenin ürünle bağı gerekiyor mu? | Gerekirse Global Ürün kimliği. |

## 7. Her pakette değişmeyen kurallar

1. Kiracı izolasyonu: her sorgu ve yazım kiracıyla sınırlı; başka kiracının kimliği "yok" ile aynı cevabı alır.
2. Kodlar sistemden gelir, değişmez, yeniden kullanılmaz; kayıt silinmez (emekliye ayrılır).
3. Onay gereken her şey ortak onay motorundan (MOD-0023) geçer; modül kendi onayını yazmaz.
4. Her yazma işlemi denetim kaydı bırakır (AUD-001); iz bırakmayan değişiklik kabul edilmez.
5. Kiracı ekranları 7 dilde (en, tr, fr, es, zh, ar, ru); liste ekranları ortak liste bileşeniyle.
6. Kabul: teslim → bağımsız gözden geçirme → Control Tower sabotaj koşusu → canlı sayfa kontrolü → modül bağlantıları
   tablosu (katalog, alan, sayfa eylemleri, izinler, denetim, bildirim, menü, KVKK, kurulum, kendini kaydetme).

## 8. Elektronik imza (BL-519) — ayrı kesit

Ölçüm: Doküman Yönetimi'nde imza temeli var; parolayı işlem anında yeniden doğrulayan bir uç yok; onay motorunda
imza kavramı yok. Öneri: tek ortak imza servisi, onay motoru adımında "imza ister" kuralı. Global Ürün kimlik
onayı için onay + denetim kaydı yeterli sayılır; bileşim (P5), ürün tanımı (P3) ve ileride ruhsat verisi imza ister.
Tahmin: 7 dilim ≈ 12 prompt, 5–7 iş günü. P5'ten önce bitmesi gerekir. Kararlar sahip ve kalite birimiyle
konuşulacak.

## 9. Açık kararlar (sahip)

| # | Karar | Öneri |
|---|---|---|
| 1 | Sıra (§5) | Yukarıdaki gibi |
| 2 | Bileşim bugün şirkette nerede tutuluyor? | Stok ekibiyle uzlaşıldı (§4a S2): ruhsat / etiket bileşimi MDM'de, parti reçetesi BOM'da. Kalan: ruhsat bileşimi bizde mi girilir yoksa dış kaynaktan mı beslenir |
| 3 | Hangi onaylar imza ister? | §8 |
| 4 | Marka tescilleri ve Görev Merkezi'nde "Marka tescili" görev türü | P8 içinde |

## 10. Onay

| Kim | Alan | Onay / not | Tarih |
|---|---|---|---|
| Stok geliştiricisi (MVP-1) | Stok (MOD-0173) | S1–S8 kabul (§4a) | 2026-10-07 |
| | Madde kaydı | | |
| | CRM | | |
| | Farmakovijilans | | |
| | Satın Alma | | |
| Sahip | Genel | | |
