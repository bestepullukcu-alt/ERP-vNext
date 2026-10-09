# Karar belgesi — Strateji şablonu (oyun) olmadan planlama: temsilcinin ürün seçimi

> **CT, 2026-10-07.** Kullanıcı isteği (E2E E7 sırasında): "illa strateji şablonu olması zorunlu olmasın … şablon yoksa temsilci ürün seçerek devam edebilmeli … seçtiği ürüne göre ziyaret süresi hesaplanmalı … ilk ziyarette seçtiği ürünleri sonraki ziyarette karışık getirebilmeli … başka ürün de eklenebilmeli … nerede olmalı bilmiyorum".
> Yer: [yol haritası](ROADMAP-visit-planning.md) **K-7** (karar bekliyor) → Faz 3 / 4 / 6 + SB-3c.

## 1. Bugün nasıl çalışıyor (kod)
- Ziyaretin ürünleri ve içeriği **yalnız oyundan** gelir: `VisitContentSequenceResolver` → oyunun ürün satırları → satırdaki yolculuk → aşama → yol adımları.
  - Oyun yoksa `contentStatus = no-strategy`: **ürün yok**, promo / non-promo sayısı 0.
  - Süre bu durumda yalnız rapor süresinden ibaret (E2E öncesi tüm ziyaretler 3 dk).
- **Süre:** dönem kapasitesindeki tipik model (`VisitMinutes(promo, nonPromo)`): ürün başına dakika × ürün sayısı + rapor. Ürün sayısı sınırı kapasitede (`MaxPromo / MaxNonPromo`, varsayılan 3 / 3).
- **Sıralama:** oyun satırları arasında ağırlıklı **döngü** var (`VisitContentRotation`); sonraki ziyarette ürünler sırayla döner.
- **Planlanan ziyaret:** `ContentSource = strategy | manual`; elle yolculuk / aşama seçilebiliyor (`IsOverridden`). Elle **ürün listesi yok**.
- **Ziyaret raporu:** bugün ürün bazında "gerçekte ne sunuldu" yok; SB-3c'de (`contentActuals`) planlı.
- **Bölge ataması:** `CoverageScope` içinde "Product Portfolio" seçeneği var → temsilcinin ürün portföyü için hazır bir bağ.

## 2. Öneri — tek kaynak: "ziyaretin ürün listesi"
Ürünler **planlanan ziyaretin üzerinde** bir liste olarak tutulur: her satır ürün + rol (promo / non-promo) + kaynak + varsa yolculuk / aşama. Süre her zaman bu listeden hesaplanır. Oyun bu listeyi dolduran kaynaklardan **yalnız biridir**, zorunlu değildir.

**Liste nereden dolar (öncelik sırası):**
1. **Oyun** (doktorun segmentinden türetilen; varsa).
2. **Temsilcinin bu doktor için seçtiği ürünler** (planlamada).
3. **Doktorun son ziyaretinde gerçekten sunulanlar** (rapordan; "taşıma").
4. **Temsilcinin portföyü** (bölge atamasındaki ürün portföyü; yoksa kiracının tüm ürünleri, aramalı).

**Hangi ekranda ne yapılır:**
| Katman | Ekran | Ne yapılır |
|---|---|---|
| Planlama | Ziyaret Planlama → Hedefler (doktor satırı) | Oyunu olmayan doktorda **"Ürün seç"**: rol + sınır kapasiteden. Tahmini süre anında güncellenir. Oyunu olanda ürünler bilgi olarak görünür, **ekleme** serbest (K-3: oyunun adı görünmez, yalnız ürünler). |
| Sonraki haftalar | Haftalar (otomatik taslak, B-4) | Seçilen ürün seti sonraki ziyaretlere **taşınır**, sırası **döner** ("karışık"; mevcut döngü kuralı, eşit ağırlık). |
| Tek ziyaret | Planlanan Ziyaret (Web + mobil) | Ziyaret öncesi ürün **ekle / çıkar** → süre yeniden hesaplanır. "Elle değiştirildi" işareti. |
| Ziyaret | Ziyaret Yürütme / rapor (SB-3c) | **Gerçekte sunulanlar** kaydedilir; sonraki ziyaretin varsayılanı (madde 3) ve yolculuk ilerlemesi buradan beslenir. |

**İçerik (oyunsuz ürün):**
- Ürünün konusunda (Subject = ürün) dil / kitleye uyan **tek bir yayımlanmış yolculuk** varsa o kullanılır; aşama ilerlemesi aynı kuralla işler.
- Birden fazla ya da hiç yoksa ürün **içeriksiz** gider ("yalnız ürün"; uyarı rozeti).

## 3. Kararlar — **kullanıcı tüm CT önerilerini kabul etti (2026-10-07)**; ek konular (§4) Planlanan Ziyaret / ziyaret raporu (Faz 6 / SB-3c) konuşulurken ele alınacak. Mockup güncellemesi: [BRIEF-ADDENDUM-K7](mockups/visit-planning/BRIEF-ADDENDUM-K7-visit-products.md)
| # | Soru | CT önerisi |
|---|---|---|
| K-7a | Oyun olmadan planlama serbest mi? | **Evet.** Oyun yoksa temsilci ürün seçer. Hiç ürün seçilmezse ziyaret "yalnız rapor süresi" ile plana girer, uyarıyla. |
| K-7b | Oyunu olan doktorda temsilci ürün **ekleyebilir / çıkarabilir** mi? | **Ekleyebilir** (sınır içinde). **Çıkarma** yalnız planlanan ziyarette ve "elle değiştirildi" işaretiyle (yönetici görür). |
| K-7c | Ürün seçicinin kaynağı | **Temsilcinin portföyü** (bölge atamasındaki ürün portföyü). Tanımlı değilse kiracının tüm ürünleri (aramalı). |
| K-7d | Promo / non-promo rolünü kim belirler? | Varsayılan ürün ana verisinden (MDM'de alan yoksa promo); temsilci değiştirebilir. Sınırlar kapasiteden. |
| K-7e | "Karışık getirme" kuralı | Set aynı kalır, **sıra her ziyarette bir kayar** (döngü); kapasite sınırı aşılırsa sıradaki ziyarete kalır. |
| K-7f | Onaylı içeriği olmayan promo ürün | Planlanabilir ama **uyarı** ("onaylı içerik yok"). Uyum ekibi isterse engel. |
| K-7g | Yeri | Veri modeli **Faz 3** (planlanan ziyaret ürün listesi + süre); ekran **Faz 4** (Hedefler / Haftalar) ve **Faz 6** (Planlanan Ziyaretler); gerçekleşen **SB-3c**. Mobil aynı sözleşmeyle. |

## 4. Aklıma gelen ek konular (karar ya da sonraki faz)
- **Numune (sample) dağıtımı:** ürün bazında verilen numune adedi ve parti / son kullanma. İlaç sahasında genelde zorunlu; stok ve yasal izleme gerekir.
- **Ziyaret amacı / tipi süreyi etkiler:** detaylama, hatırlatma, eczane stok kontrolü, etkinlik daveti. Bugün amaç süreye girmiyor.
- **Ortak ziyaret:** yönetici ile birlikte ziyaret (koçluk); yönetici görünümüyle bağlantılı.
- **Doktor-ürün potansiyeli:** her doktor için ürün bazında potansiyel / kullanım (rakip ürün kullanıyor mu). Ürün seçimine öneri ve mikro-hedefleme (MicroTarget) için girdi.
- **İzin ve kanal:** ürün / marka bazında iletişim izni (KVKK); izni olmayan ürün önerilmemeli. Onay sisteminde marka / ürün kapsamı zaten var.
- **Yol dalı seçimi (E7-B1):** uzun ziyarette ana akış, kısa ziyarette kısa akış. Süre ile dal seçimi bağlanabilir.
- **İçerik süresi (E7-B2):** yol adımlarının dakikası süreye eklenmeli mi, yoksa ürün başına sabit süre mi esas? (Bugün sabit.)
- **Ürün bazında sıklık:** "TUTUKON ayda 2, X ürünü ayda 1" — sıklık bugün doktor / segment bazında; ürün sıklığı ayrı bir boyut.
- **Uyum izi:** elle eklenen / çıkarılan ürün, kim, ne zaman (denetim kaydı, Faz 8 ile).
