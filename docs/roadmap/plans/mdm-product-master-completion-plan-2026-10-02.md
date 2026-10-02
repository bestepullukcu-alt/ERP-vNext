# Ürün Ana Verisi Tamamlama Planı (MOD-0290)

**Durum:** TASLAK — geliştirici onayı bekliyor · **Tarih:** 2026-10-02 · **Sahibi:** Control Tower (MDM ürün modülleri)
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
| P0 | **Kabul** | Yazılmış sekiz parçanın kabulü (Global Ürün → GSKU → LSKU → Bitmiş Ürün → Kapsam → Kısaltma → Marka / Ürünler) + denetim izi (BL-503) | sürüyor | — | — |
| P1 | **Kalem / malzeme kaydı** | Hammadde, ambalaj malzemesi ve diğer stoklanabilir kalemler: kod, ad, tür, durum, temel ölçü birimi, lot / son kullanma izlenir mi, saklama koşulu; Bitmiş Ürün ile ortak "stoklanabilir kalem" okuma sözleşmesi | büyük (2 paket) | stok ekibinin cevabı (§6) | 🟡 |
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

## 5. Sıra

| Faz | Paketler | Neden bu sırada |
|---|---|---|
| 1 | P0 | Kabul edilmemiş kodun üstüne yeni kod yazılmaz |
| 2 | P1 + P2 | Stok ekibi kalem kimliğine bağlanmak zorunda; beklerse kendi kalem kaydını yazar ve iki ana veri oluşur |
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
| 2 | Bileşim bugün şirkette nerede tutuluyor? | Cevaba göre P5 "bizde girilir" ya da "dış kaynaktan beslenir" olur |
| 3 | Hangi onaylar imza ister? | §8 |
| 4 | Marka tescilleri ve Görev Merkezi'nde "Marka tescili" görev türü | P8 içinde |

## 10. Onay

| Kim | Alan | Onay / not | Tarih |
|---|---|---|---|
| | Stok (MOD-0173) | | |
| | Madde kaydı | | |
| | CRM | | |
| | Farmakovijilans | | |
| | Satın Alma | | |
| Sahip | Genel | | |
