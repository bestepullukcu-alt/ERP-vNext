# MOCKUP BRIEF — Ziyaret Çalışma Alanı: tek takvimde planla, ziyaret et, raporla

> **Tarih:** 2026-10-08 · **Hazırlayan:** CT · **İsteyen:** ürün sahibi.
> **Dayanaklar:**
> - [ziyaret akışı açıklaması](../../VISIT-FLOW-pages-explained.md)
> - [mockup v3 (Hedefler)](../visit-planning/Ziyaret%20Planlama%20v3%20(standalone).html)
> - [Android ziyaret / rapor durum raporu](../../mobile/2026-10-08-visit-planning-contract/ANDROID-VISIT-AND-REPORT-STATUS-2026-10-08.md)
> - [CT değerlendirmesi](../../mobile/2026-10-08-visit-planning-contract/CT-REVIEW-android-visit-report-status-2026-10-08.md)
> - [mobil sözleşme notu](../../mobile/2026-10-08-visit-planning-contract/MOBILE-CONTRACT-2026-10-08-visit-planning.md)
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: sol menü, üst çubuk, kart yapısı, açık / koyu tema. Mockup v2 / v3'teki renkler, rozetler, çip biçimleri aynı.
> - **Üç cihaz** çizilmeli: **masaüstü web**, **tablet** (yatay), **telefon** (dikey). Mobil uygulama (Android / iOS) aynı akışı izler.
> - **Dil:** Türkçe çiz; Arapçada sağdan sola düzen bozulmamalı (aynalanabilir yerleşim).
> - Gerçekçi demo verisi kullan: İstanbul Şişli kurumları, doktor adları, TUTUKON / ALMIBA / GLUKOFİT gibi ürünler.

---

## BAĞLAM

### Kullanıcı
İlaç şirketinin **saha temsilcisi** (tıbbi tanıtım). Günü şöyle geçer:
1. Bu hafta kimi göreceğini planlar.
2. Sabah takvimine bakar.
3. Kuruma gider, ziyarete başlar, doktora tablette sunum yapar, telefona not alır.
4. Çıkınca raporu girer.

Yönetici görünümü bu mockup'ın konusu değil; ama yer bırakılmalı (§11).

### Bugün (değişecek olan)
Üç ayrı sayfa var:
- **Ziyaret Planlama:** haftanın planı. Hedefler, Haftalar, Rota sekmeleri; hafta onayı.
- **Planlanan Ziyaretler:** tek tek ziyaret kayıtları listesi.
- **Ziyaret Yürütme:** gün / hafta takvimi, sonuç işaretleme, rapor yan paneli.

### Hedef
**Planlama ve yürütme tek ekranda: bir takvim.** Temsilci planlar, haftayı onaylar, günü gelince ziyarete başlar, sunar, bitirir, raporlar. Hepsi aynı takvimden.

Planlanan Ziyaretler listesi kayıt / arama ekranı olarak kalabilir; bu mockup onu çizmez.

### Bağlayıcı kurallar (mevcut, değişmez — mockup'ta görünmeli)
| Kural | Ekranda anlamı |
|---|---|
| Temsilci "oyun / strateji / kampanya" görmez, segment seçmez | Bu kelimeler ve seçiciler hiçbir yerde yok. Segment yalnız bilgi rozeti. |
| Dönem başına **tek plan**; hafta hafta **onay**, gerekçeli **yeniden açma** | Takvimde hafta başlığında durum + "Haftayı onayla" / "Haftayı yeniden aç". |
| Sonraki haftalar sıklığa göre **otomatik taslak** | Taslak haftalar takvimde soluk / kesik çizgili; onaylı haftalar dolu. |
| Ziyaretler coğrafyaya ve gün bütçesine göre günlere dağılır; temsilci **güne taşıyabilir** (sabit) | Takvimde sürükle-bırak = güne sabitle; sabit simgesi; "Sabiti kaldır". |
| Doktor başına **ürün listesi** (önerilen + temsilcinin seçimi), rol: tanıtım / hatırlatma | Ziyaret kartında ürün **adları** (kod değil); hatırlatma çipi çerçeveli. |
| **İleri tarihli** ziyaret kapatılamaz / raporlanamaz | Gelecek günde "Ziyarete başla" ve "Rapor gir" yok. |
| **Rapor son tarihi: ziyaretten sonra 48 saat** | Raporsuz yapılmış ziyarette geri sayım rozeti ("Rapor için 31 sa kaldı"); süre geçince "Süre doldu" ve kilit. |
| Gönderilen rapor **60 dakika** içinde değiştirilebilir, sonra yalnız **gerekçeli düzeltme** | Rapor ekranında "Değiştir (54 dk)" → sonra "Düzelt". |
| **İptal edilmiş ziyaret düzenlenemez, raporlanamaz** | İptal kartı salt okunur; neden kategorisi ve notu görünür. |
| Yetkisiz kullanıcıya sayfa iskeleti çizilmez | "Yetkiniz yok" durumu ayrı ekran. |

---

## EKRANLAR

### E1 · Takvim — ana ekran (masaüstü + tablet + telefon)
**Üst çubuk**
- Dönem ("Türkiye 2026 Q4") ve görünüm: **Gün · Hafta · Ay** (varsayılan Hafta; telefonda Gün).
- Önceki / sonraki, **Bugün**.
- Kapasite çubuğu: "Bu hafta 38,3 sa kapasite · 21,6 sa planlı".
- Süzgeç: kurum, durum, ürün.
- **"Plan dışı ziyaret"** (elle tek ziyaret).

**Mod anahtarı: "Planla" | "Yürüt"**
- **Planla:** sağda **Hedefler paneli** (mockup v3 Hedefler aynen: hesaplar, doktor listesi, ürün seç, toplu uygula, seçim özeti) açılır; takvimde sürükle-bırak açık.
- **Yürüt:** bugüne odaklı; eylem düğmeleri öne çıkar. Varsayılan: bugün dönemin içindeyse Yürüt.

**Hafta başlığı**
- "41. Hafta · 5–9 Eki" + durum rozeti (taslak / onaylı / boş / geçmiş).
- Eylem: "Haftayı onayla" ya da "Haftayı yeniden aç".
- Kaydırılan / sığmayan ziyaret sayısı ⚠ (tıklanınca liste).

**Gün sütunu**
- Gün adı + tarih, gün bütçesi çubuğu ("6 / 57", "boş 6,5 sa").
- Kurum grupları altında ziyaret kartları, saat sırasıyla.
- Tatil / yarım gün işareti.

**Ziyaret kartı (takvim olayı)**
- Saat aralığı, doktor adı (ya da eczane / kurum), kurum, ürün çipleri (ad, rol rengi), süre.
- **Durum rengi ve simgesi:**
  - taslak (soluk);
  - planlı / onaylı;
  - **bugün, başlamaya hazır**;
  - **devam ediyor** (canlı nokta + süre);
  - yapıldı, **rapor bekliyor** (geri sayım);
  - raporlandı (✓);
  - yapılamadı;
  - ertelendi;
  - iptal (üstü çizili);
  - süre doldu.
- Sabit simgesi, plan dışı (elle) simgesi.
- Telefonda kart, gün listesinde satır olur.

**Kart tıklanınca: ziyaret ayrıntı paneli** (masaüstünde sağdan çekmece, telefonda tam ekran)
- **Başlık:** doktor, uzmanlık, kurum (harita küçük önizleme), saat; segment rozeti (bilgi).
- **"Ne sunacağım":** ürünler sırayla (ad, rol, aşama adı, içerik adımları sayısı, tahmini süre "1 tanıtım + 1 hatırlatma + rapor ≈ 22 dk").
- **Önceki ziyaretten** (doktor + ürün bazında son rapordan):
  - son ziyaret tarihi;
  - doktorun ürüne ilgisi / bağlılık aşaması;
  - **açık itirazlar ve talepler** ("Literatür istedi — 12 Eki'ye kadar");
  - verilen numune.
- **Sıklık:** "dönemde 3 · 1 / 2".
- **Eylemler** (yalnız uygun olan görünür, kural tablosu aşağıda).

**Eylem kuralları**
| Ziyaret durumu | Eylemler |
|---|---|
| Taslak haftada, gelecek | Ürünleri değiştir · Güne taşı · Hedeften çıkar |
| Onaylı, gelecek gün | Güne taşı yok (hafta onaylı) · **İptal et** |
| Bugün / geçmiş, başlamamış (son tarih içinde) | **Ziyarete başla** · Yapılamadı · Ertele · İptal et |
| Devam ediyor | **Sunuma dön** · **Ziyareti bitir** |
| Bitti, raporsuz (48 sa içinde) | **Rapor gir** (geri sayım) |
| Raporlandı | Raporu gör · Değiştir (≤60 dk) / Düzelt (gerekçeli) |
| İptal / süre doldu | Salt okunur (neden ve tarih) |

### E2 · İptal et / Yapılamadı / Ertele penceresi
Üç ayrı eylem, aynı pencere tasarımı.

| | Anlamı |
|---|---|
| **İptal et** | Ziyaret yapılmayacak (plan iptali; gün gelmeden de olur). |
| **Yapılamadı** | Gün geldi ama ziyaret olmadı. |
| **Ertele** | Başka güne kaydır; **yeni tarih** seçilir. |

- **Neden kategorisi** (zorunlu, tek seçim; liste referans verisinden gelir, örnek):
  - Doktor yok / izinde;
  - Doktor zamanı yok / görüşmeyi reddetti;
  - Kurum kapalı / girişe izin yok;
  - Temsilci izinli / hasta;
  - Toplantı / eğitim çakışması;
  - Ulaşım / hava koşulu;
  - Doktor kurumdan ayrıldı / hedef pasif;
  - Diğer.
- **Açıklama:** "Diğer"de zorunlu, en fazla 500 karakter.
- **Ertele:** tarih seçici; takvimde hedef günün doluluğu görünür.
- Onay düğmesi metni eyleme göre ("Ziyareti iptal et").

### E3 · Ziyarete başla — kanıt adımı
"Ziyarete başla" basılınca kısa bir **kanıt** ekranı açılır; geçince sunum başlar.

**Kanıt kartı**
- **Fotoğraf** (ürün sahibi isteği):
  - telefonda kamera, webde dosya yükle ya da web kamerası;
  - ipucu metni: "Kurum girişi ya da bekleme alanı. Doktorun ya da hastaların yüzü çekilmez (KVKK).";
  - küçük önizleme, yeniden çek.
- **Konum** (mobil): "Kuruma 120 m — uygun" yeşil rozeti ya da "Kurumdan 3,4 km uzaktasınız" uyarısı. Kiracı ayarıyla zorunlu ya da yalnız bilgi.
- **Saat:** başlangıç saati sunucudan; cihaz saatine güvenilmez.
- **Önerilen ek kanıtlar** (mockup'ta seçenek olarak göster; ürün sahibi seçecek):
  - **Doktor onayı:** ziyaret sonunda tablette doktorun parmakla imzası ya da "Görüşmeyi onaylıyorum" dokunuşu. Numune tesliminde zaten gerekir.
  - **Kurum QR kodu:** kurumun kabulünde asılı QR, temsilci okutur.
  - **Süre tutarlılığı:** sunumda geçen süre ve slayt etkileşimi; 2 dakikadan kısa ziyaret işaretlenir.
  - **Anti-fraud check-in:** mobilde mevcut anti-fraud modülüyle (cihaz bütünlüğü, sahte konum tespiti).
- **Çevrimdışı:** telefonda bağlantı yoksa fotoğraf ve konum cihazda bekler, "Gönderilecek" rozeti.

### E4 · Ziyaret ekranı — sunum (PowerPoint gibi), iki ekran
Ziyaret başlayınca açılır. **İki yüz** var.

**E4a · Doktor ekranı (tablet, yatay, tam ekran)**
- Ürünün onaylı içeriği **slayt slayt**: ürün sırası plandaki sırayla; tanıtım ürünleri önce, hatırlatma kısa.
- Üstte ince ürün sekmeleri (TUTUKON · ALMIBA), altta ilerleme noktaları.
- Kaydırma ile geçiş. Kaynak / referans dipnotları (onaylı iddia metinleri).
- **Doktor modunda** temsilcinin notları, fiyat, iç bilgi **görünmez**; çıkış için temsilci kilidi (uzun basma / PIN).

**E4b · Temsilci ekranı (telefon, dikey)** — tablet tek cihazsa sağda daraltılabilir panel, webde bölünmüş görünüm
- **Kumanda:** şu anki slayt küçük önizlemesi, ileri / geri, ürün atla.
- **Ürün başına süre:** otomatik sayaç ("TUTUKON 04:12").
- **Hızlı işaretler** (tek dokunuş, ürün ve slayt bazında):
  - doktorun ilgisi: 👍 İlgili · Nötr · 👎 İlgisiz;
  - "Soru sordu";
  - "İtiraz etti" (kategori seç: §E5 itiraz listesi);
  - "Numune istedi";
  - "Literatür / bilgi istedi";
  - "Takip gerekli".
- **Not alanı:** serbest not, sesle not (konuşmayı yazıya çevir).
- **Ortak ziyaret:** "Yöneticiyle birlikte" anahtarı + kişi seçimi.
- **Alt çubuk:** "Ziyareti bitir" → bitiş saati alınır → **E4c**.

**E4c · Bitiş özeti** (her iki ekranda)
- Ziyaret süresi, ürün başına süreler, işaretlenen ilgi / itiraz / talepler, alınan notlar.
- Düğmeler: "**Rapor gir**" (birincil) · "Sonra gireceğim" (48 sa geri sayımıyla takvime döner).
- Numune verildiyse **doktor imzası** burada (tablette).

**Uzaktan ziyaret:** web masaüstünde aynı sunum, ekran paylaşımlı uzaktan görüşme için; tür "Uzaktan".

### E5 · Hekim ziyaret raporu (masaüstü + tablet + telefon)
Ziyaret sırasında yakalananlarla **önceden dolu** gelir. Aynı doktor + ürün için **önceki rapordaki değerler** gri öneri olarak durur: "Önceki ziyaret: Kullanıyor · %30" ve "Aynısı" tek dokunuşla kabul.

Üstte:
- **son tarih bandı:** "Rapor için 31 sa kaldı";
- otomatik taslak kaydı;
- doğrulama özeti ("2 alan eksik").

**1 · Genel**
- Sonuç: Yapıldı (bu ekrandan; Yapılamadı / Ertele E2'de).
- Gerçek başlangıç / bitiş (E3 / E4'ten, salt okunur).
- Ziyaret türü (yüz yüze / uzaktan); ortak ziyaret (kiminle).
- Genel değerlendirme (kısa not).

**2 · Ürünler** — plandaki her ürün için bir kart, plandaki sırayla. "+ Plan dışı ürün sunuldu" eklenebilir. Her kartta:

| Alan | Açıklama |
|---|---|
| Sunulan içerik / aşama | Plandaki aşama varsayılan; değiştirilebilir ("plana uydu" işareti otomatik). |
| Ürüne harcanan süre | Dakika; E4'ten otomatik, düzeltilebilir. |
| Etkinlik | 1–5 yıldız: mesaj ne kadar karşılık buldu. |
| Doktorun ilgisi | İlgili / Nötr / İlgisiz (E4'ten). |
| **Bağlılık (benimseme) aşaması** | Farkında değil → Farkında → Değerlendiriyor → Deniyor → Düzenli kullanıyor → Savunucu. |
| **Endikasyondaki hasta sayısı** | Doktorun bu ürünün endikasyon profilindeki **aylık** hasta sayısı (tahmini). |
| **Toplam hasta sayısı** | Doktorun aylık toplam hasta sayısı (doktor düzeyinde; bir kez girilir, sonraki raporlarda önerilir). |
| **Bizim reçete payımız** | Endikasyondaki hastaların yüzde kaçına bizim ürün yazılıyor (tahmini %). |
| → **Hesaplanan** | **Potansiyel** = endikasyon hastası; **bağlılık** = payımız; "Yüksek potansiyel · düşük pay → fırsat" rozeti. |
| **Rakip** | Doktorun kullandığı rakip ürünler (rakip ürün listesinden çoklu seçim) + rakibe göre **avantajlarımız** ve **dezavantajlarımız** (etiket listesi: etkinlik, güvenlik, doz kolaylığı, fiyat, geri ödeme, erişim / stok, kanıt düzeyi, marka bilinirliği + not). |
| **İtirazlar** | Kategori (fiyat, geri ödeme, etkinlik, yan etki, doz / kullanım, erişim / stok, alışkanlık / rakip memnuniyeti, kanıt yetersiz, diğer) + not + **"Giderildi mi?"** (evet / kısmen / hayır). |
| **Doktorun talepleri** | Literatür / çalışma, numune, hasta materyali, eğitim / toplantı daveti, MSL (tıbbi uzman) görüşmesi, diğer + **teslim tarihi**; açık talepler sonraki ziyarette kartta görünür. |
| **Numune** | Ürün / ambalaj (SKU), adet, lot no (opsiyonel), **teslim imzası** (E4c'den). |
| **Sonraki ziyaret mesajı** | Bir dahaki ziyarette verilecek mesaj / hedef aşama (öneri). |

**3 · Doktor profili** — ziyaret değil doktor bilgisi; değişirse doktor kaydına işlenir, önceki değer gösterilir
- **Hekim tipi (tipoloji):** örnek seçenek setleri, ürün sahibi birini seçecek:
  - (a) benimseme tipi: Yenilikçi / Erken benimseyen / Pragmatik / Muhafazakar;
  - (b) iletişim stili: Analitik / Sonuç odaklı / İlişki odaklı / Dışavurumcu.
- Etki düzeyi (KOL: yerel / bölgesel / ulusal).
- Tercih edilen kanal ve **en uygun ziyaret zamanı** (planlamaya geri besler).

**4 · Takip ve güvenlik**
- Takip gerekli + not + önerilen sonraki ziyaret tarihi.
- **Advers olay sorusu (zorunlu):** "Ziyarette bir yan etki / advers olay bildirildi mi?" Evet → farmakovijilans bildirimine yönlendirme (24 saat kuralı uyarısı). İlaç firmaları için yasal zorunluluk.
- **Endikasyon dışı bilgi talebi:** "Doktor endikasyon dışı bilgi istedi mi?" Evet → MSL'ye yönlendirme; temsilci yanıt vermez.

**Alt çubuk**
- "Taslak kaydet" · "**Raporu gönder**".
- Gönderdikten sonra "Değiştir (60 dk)" → sonra "Düzelt" (gerekçe zorunlu; değişen alanlar geçmişte görünür).

### E6 · Eczane ziyaret raporu
Hedef eczaneyse rapor farklıdır. Önerilen alanlar (ürün sahibi onaylayacak):

| Bölüm | Alanlar |
|---|---|
| Genel | Sonuç, başlangıç / bitiş, görüşülen kişi (eczacı / kalfa / teknisyen) |
| **Ürün başına stok** | Stok durumu (var / az / yok) + adet; miadı yakın / iade sorunu |
| **Raf / görünürlük** | Raf yerleşimi (göz hizası / alt / depo), materyal / stand var mı, raf fotoğrafı |
| **Sipariş** | Sipariş alındı mı, ürün başına adet, depo / distribütör seçimi (transfer siparişi) |
| **Rakip** | Rakip ürün stoku, fiyat / kampanya gözlemi (izin verilen ölçüde) |
| **Satış** | Aylık satış tahmini (kutu), eğilim (artıyor / sabit / düşüyor) |
| **Tavsiye ve reçete akışı** | Eczacının ürünü önerme eğilimi; reçetesi gelen doktorlar (bağlı hekimler listesinden) |
| **Eğitim / materyal** | Personele ürün eğitimi verildi mi, bırakılan materyal |
| **Talepler** | Eczacının talepleri (materyal, numune değil, iade, fiyat bilgisi …) + teslim tarihi |
| **Güvenlik** | Advers olay sorusu (hekimdeki gibi) |

### E7 · Doktor satış payı (potansiyel) ekranı
Doktorun ürün satışını tahmin etmek için. Ürün sahibi: "hastanenin bağlı olduğu satışlara göre yüzde ile doktorun ürün satışı hesabı".
- **Kurum seçilir:** kurumun ürün bazında satışı. Kaynak açık soru: distribütör / eczane satış verisi, IQVIA, hastane alım verisi.
- **Tablo:** kurumun doktorları × ürünler. Her hücrede doktorun **kurum içi payı %** (temsilci tahmini, düzenlenebilir) → **tahmini doktor satışı** (kutu / ₺).
  - Satır toplamı kontrolü: kurumda doktor payları toplamı %100; eksik / fazla uyarısı.
- **Matris görünümü:** potansiyel (endikasyon hastası, E5) × payımız (bağlılık) → dört kutu: Koru · Büyüt · Fırsat · Düşük öncelik. Planlamaya öneri ("Fırsat doktorları bu hafta görülmeli").
- **Geçmiş:** dönem dönem pay ve satış eğilimi.
- **Görünürlük:** temsilci kendi doktorlarını görür; yönetici görünümü ileride (§11).

### E8 · Doktor kartı (takvimden doktor adına tıklayınca)
- Ziyaret geçmişi (tarih, ürünler, sonuç, rapor özeti).
- Ürün başına bağlılık ve ilgi eğilimi (küçük grafik).
- **Açık talepler / itirazlar.**
- Verilen numuneler toplamı.
- Gelecek ziyaretler.
- Doktor profili (tipoloji, en uygun zaman).
- Satış payı özeti (E7).

---

## CİHAZ MATRİSİ
| Ekran | Masaüstü web | Tablet | Telefon |
|---|---|---|---|
| E1 Takvim + Planla modu | ✔ tam | ✔ | Gün listesi; planlama sınırlı (görüntüle + güne taşı) |
| E2 İptal / yapılamadı / ertele | ✔ | ✔ | ✔ |
| E3 Ziyarete başla (kanıt) | ✔ (dosya / web kamerası) | ✔ | ✔ (kamera + konum) |
| E4 Sunum | Bölünmüş görünüm (uzaktan / prova) | **Doktor ekranı** | **Temsilci ekranı** (kumanda + not) |
| E5 / E6 Rapor | ✔ | ✔ | ✔ (bölümler katlanır) |
| E7 Satış payı | ✔ | ✔ | Salt okunur özet |
| E8 Doktor kartı | ✔ | ✔ | ✔ |

## DURUMLAR (her ekran için)
- **Boş:** gün / hafta boş.
- **Yükleniyor.**
- **Çevrimdışı:** mobil; "Gönderilecek" kuyruğu.
- **Yetkiniz yok:** iskelet çizilmez.
- **Gün henüz gelmedi:** başla / rapor yok.
- **Son tarih geçti:** kilit + yöneticiye bildir.
- **İptal edildi:** salt okunur.
- **Hata:** tekrar dene.

## MOBİL TALEPLERİYLE EŞLEŞME (Android raporu §10–§11)
| Mobil iş | Bu brief'te |
|---|---|
| A2 ziyaret sonucu girme | E2 + E4c |
| A3 rapor yazma / gönderme | E5 / E6 |
| A4 rapor düzeltme | E5 alt çubuk |
| A5 "ne sunacağım" | E1 ayrıntı paneli + E4 |
| A10 check-in | E3 (fotoğraf + konum + anti-fraud) |
| A11 çevrimdışı | E3 / E4 / E5 çevrimdışı durumları |
| B5 sonuç / neden kodu seti | E2 neden kategorileri |
| B11 tip / amaç / durum etiketleri | Türkçe etiketlerle çiz |

## ÜRÜN SAHİBİNE AÇIK SORULAR (mockup'tan sonra karar)
1. **Ertele:** yeni tarihte **yeni planlı ziyaret** mi açılsın? Bugün backend erteleme yalnız not tutuyor.
2. **Fotoğraf:** zorunlu mu, kiracı ayarı mı? **Konum** kontrolü zorunlu mu, bilgi mi?
3. **Doktor onayı / imzası:** her ziyarette mi, yalnız numunede mi?
4. **Satış verisi kaynağı** (E7): distribütör, eczane, IQVIA, hastane alım verisi.
5. **Hekim tipolojisi modeli:** (a) benimseme mi, (b) iletişim stili mi, ikisi mi?
6. **Bağlılık hesabı:** pay % mı, benimseme aşaması mı, ikisinin birleşimi mi?
7. **Görünürlük:** hasta sayısı, satış payı, tipoloji kimlere görünür (temsilci / yönetici / pazarlama)?
8. **Eczane siparişi** ERP satış siparişine dönüşsün mü?
9. Advers olay yönlendirmesi hangi PV ekranına gidecek?

## KAPSAM DIŞI (bu mockup)
- Yönetici görünümü (ekip takvimi, onay kuyruğu, performans). Yalnız üst çubukta "Ekip" anahtarına yer bırak (etkin değil).
- MSL ekranları, PV bildirim formunun kendisi.
- Planlanan Ziyaretler kayıt listesi.
- İçerik / slayt hazırlama (Content Studio'da var).

## TESLİM
- Masaüstü:
  - E1 (Planla ve Yürüt modları);
  - E1 ayrıntı paneli (en az 4 durum: taslak, bugün başlamaya hazır, rapor bekliyor, raporlandı, iptal);
  - E2;
  - E3;
  - E4 bölünmüş görünüm;
  - E5 (dolu, önceki değer önerileriyle);
  - E6;
  - E7 (tablo + matris);
  - E8.
- Tablet: E4a doktor ekranı, E5.
- Telefon: E1 gün listesi, E3 kamera + konum, E4b temsilci ekranı, E4c bitiş özeti, E5 katlanır bölümler.
- Koyu tema: en az E1 ve E4a.
