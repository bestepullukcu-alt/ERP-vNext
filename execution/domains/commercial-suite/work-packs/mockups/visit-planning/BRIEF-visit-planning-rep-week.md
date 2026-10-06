# MOCKUP BRIEF — Ziyaret Planlama: temsilcinin haftası ve otomatik haftalar

> Tarih: 2026-10-06 · Hazırlayan: CT. Dayanak: [VISIT-PLANNING-current-state-analysis.md](../../VISIT-PLANNING-current-state-analysis.md).
> **Mockup yapana not:** sayfa bugün çalışıyor (`/CRM/VisitPlanning`). Bu bir **yeniden kurgu değil, hedefli değişiklik**. Ürünün mevcut düzenine uy: sol menü, üst çubuk, kart yapısı, açık / koyu tema.
> Aşağıdaki **DOKUNMA** listesi kesindir.

## BAĞLAM
- **Kullanıcı:** ilaç şirketinin **saha temsilcisi** (tıbbi tanıtım). İşi şu: kendisine atanmış doktorları ve eczaneleri planlamak, ziyaret etmek, ne sunacağını bilmek, ziyareti raporlamak.
- **Temsilci şunları bilmez ve görmez:** "strateji şablonu / oyun" ve "kampanya". Bunlar yönetici ve pazarlama kavramlarıdır; sistem doktor bazında kendisi türetir.
- **Zincir:** Dönem (ör. 2026 Q4) → dönem kapasitesi (bir temsilcinin dönemde yapabileceği ziyaret) → **Ziyaret Planlama** (bu ekran) → Planlanan Ziyaretler → Ziyaret Yürütme (gün / hafta takvimi) → ziyaret raporu.
- **Bugünkü akış:**
  - plan kurulur (ülke, dönem, hafta, temsilci, segment, strateji şablonu);
  - **Hedefler** sekmesinde hesap (hastane / klinik) seçilir, altındaki doktorlar ve bağlı eczaneler işaretlenir;
  - **Rota** sekmesinde gün gün duraklar, harita ve sıra görünür;
  - "Uygula" planı Planlanan Ziyaretler'e yazar.
- **Sıklık:** her doktorun / kurumun bir ziyaret sıklığı vardır (ör. dönemde 3 kez; "Ziyaret Sıklığı Politikaları" sayfasından gelir). Sistem 1. hafta seçilen hedefleri bu sıklığa göre sonraki haftalara da yayabiliyor. Ama bugün bu ekranda görünmüyor ve sıklığı bilinmeyen doktorlar yalnız 1. haftada kalıyor.

## KARARLAR (kullanıcı, 2026-10-06)
- **K-1:** Ekranı **temsilci kendi haftası için** kullanır. İleride ayrı bir **yönetici görünümü** gelecek; bu mockup onu çizmez ama yer bırakır (§6).
- **K-2:** Bir hafta planlanıp onaylanınca **sonraki haftalar sıklığa göre otomatik** taslak olarak gelir; temsilci hafta hafta görür ve düzeltir.
- **K-3:** Temsilci **strateji şablonu seçemez**, ekranda bu kavram geçmez.

## ⛔ DOKUNMA (aynen kalır)
1. **Rota sekmesi:** gün sekmeleri, hafta seçici, durak listesi (saat, kurum, doktor, ziyaret türü, yürüme / yol süresi, öğle arası), harita, sürüklenebilir durak sırası. Tasarımı değişmez, mockup'ta yeniden çizilmez; bağlam için ekran görüntüsü kullanılabilir.
2. **Liste ("Taslak planlarım"):** düzen, sütun yapısı ve tarih biçimi ("5 Oct, 26" tarzı) aynı kalır. Yalnız §1'deki küçük değişiklikler yapılır.
3. **Yeni taslak plan formu:** iki sütunlu "Plan kurulumu" kartı ve görünümü aynı kalır. Yalnız §2'deki alan değişiklikleri yapılır.

## 1) LİSTE — küçük değişiklikler
- **"Hedefler"** sütunu gerçek sayıyı gösterir (bugün hep 0, bu bir hata). Biçim: "122 doktor · 13 eczane".
- **"Haftalar"** sütunu eklenir: onaylı / taslak hafta sayısı (ör. "1 onaylı · 3 taslak").
- **Temsilci** e-posta yerine ad soyad olarak görünür. Temsilci görünümünde liste yalnız kendi planlarını içerir; bu yüzden sütun gizlenebilir (yönetici görünümüne yer).
- Boş taslaklar (0 hedef) ayrı işaretlenir: "boş taslak" rozeti ve sil. Aynı dönem ve hafta için ikinci taslak açılmaz; mevcut taslağa yönlendirme önerisi gösterilir.
- Başlık metni temsilci diliyle yazılır. Bugünkü metin yönetici dili ve İngilizce ("Turn a rep's selection…"). Butonlar Türkçe: "Yeni plan", "Rota oluştur" vb.

## 2) YENİ TASLAK PLAN — alan değişiklikleri (düzen aynı)
| Alan | Bugün | Yeni |
|---|---|---|
| Ülke | seçim | **otomatik** (temsilcinin ülkesi), salt okunur; birden fazla ülkesi varsa seçim |
| Dönem (Cycle period) | seçim | **otomatik**: bugünün aktif dönemi, değiştirilebilir (yalnız aktif / gelecek dönemler) |
| Hafta | seçim | **otomatik**: planı olmayan ilk hafta, değiştirilebilir; bugünden önceki haftalar seçilemez |
| Temsilci (kullanıcı) | kullanıcı listesi | **oturumdaki kişi**, salt okunur (ad soyad) |
| Segmentler | çoklu seçim (yalnız ilki uygulanıyor) | ⚠ **K-4, karar bekliyor** — aşağıya bak |
| Strateji şablonu | seçim | **kaldırılır** (K-3) |

**⚠ K-4: segment ne olacak?** Bugün segment seçmek Hedefler'deki doktor listesini **değiştirmiyor**. Yalnız plan üretilirken seçilen doktorlardan segmentte olmayanları **sessizce düşürüyor** ve sıklığı segmente göre çözüyor (CT kod okuması).
- **(a)** Segment alanı kalır ama anlamı değişir: seçilince segment üyesi doktorlar Hedefler'de **önceden işaretlenir**; temsilci kaldırıp ekleyebilir. Üye olmayan doktor düşürülmez, yalnız rozetle belirtilir.
- **(b) — CT önerisi:** Segment alanı temsilciden kalkar. Bunun yerine Hedefler'de sistem "**Bu hafta görülmesi gerekenler**" listesini önerir (sıklık + son ziyaret + bölge ataması). Segment üyeliği doktor satırında salt okunur rozet olur.
- Mockup **(b)**'yi çizsin; (a) için tek bir alternatif kare yeterli.

## 3) PLAN DETAYI — üst kısım
- **Özet kartı:** aynı alanlar kalır, etiketler Türkçe ("Cycle period" → "Dönem").
- **Eylemler:** durum bazlı, yalnız uygun olan görünür:
  - **taslak:** "Hedefleri kaydet", "Rota oluştur", "**Haftayı onayla**";
  - **onaylı:** salt okunur; yalnız "**Haftayı yeniden aç**" (gerekçe sorulur) ve "Sonraki haftayı aç".
  - Bugün onaylı planda kaydet / düzenle hâlâ açık; bu bir hata.
- **Arz / talep kartları** aynı birimle gösterilir:
  - "Bu haftanın kapasitesi" (dönem kapasitesinin haftaya düşen payı, tatiller düşülmüş) ↔ "Bu hafta planlanan";
  - ayrıca "Dönem kapasitesi" ↔ "Dönemde planlanan" (ilerleme çubuğu).
  - Bugün dönem toplamı (6543) haftalık talep (176) ile yan yana duruyor ve yanıltıcı.

## 4) HEDEFLER SEKMESİ — yeniden düzen (ekran görüntüsündeki bölüm)
**Sol: hesaplar (kurumlar)**
- Liste yalnız **temsilcinin bölgesindeki** hesaplar. Bölge dışı hesap ayrı bir "Bölge dışı ekle" akışıyla, uyarıyla eklenir.
- Tür kodu yerine yerel etiket: "hospital" → "Hastane", "clinic" → "Klinik", "pharmacy" → "Eczane".
- Hesap satırında: seçili doktor sayısı / toplam ve "bu hafta görülmesi gereken" sayısı.

**Orta: seçili hesabın doktorları / bağlı eczaneleri**
- Sütunlar: seçim, doktor, uzmanlık (yerel ad), **sıklık** (ör. "dönemde 3"), **yapılan / kalan** (ör. "1 / 2"), **son ziyaret** (tarih), durum rozetleri:
  - izin yok / iletişim izni engelli (gri, seçilemez, nedeni ipucunda);
  - pasif;
  - segment üyesi (salt okunur).
- **"BAĞLANTI"** sütunu kalkar (bugün GUID gösteriyor).
- **Uzmanlık filtresi:** 21 çipten oluşan satır yerine sayılı çoklu seçim ("Üroloji (12)").
- Hızlı filtreler: "Bu hafta görülmesi gerekenler", "Hiç görülmeyenler", "Tümü".
- "Tümünü seç" filtreye uyanları seçer.

**Sağ: seçim özeti**
- 122 doktor · 13 eczane · 36 hesap kartı kalır.
- Altına **tahmini süre**: "≈ 31 saat · haftalık kapasitenin %78'i". Kapasiteyi aşarsa uyarı.
- **"Seçilenler"** listesi GUID yerine **doktor adı + kurum + uzmanlık**, hesaba göre gruplu, tek tek kaldırılabilir.

**Etiketler:** tamamı Türkçe ve 7 dil ("Accounts (clinics / hospitals)", "Doctors", "Specialty", "Out-of-territory…" İngilizce kalmış).

## 5) YENİ: HAFTALAR SEKMESİ (Hedefler | **Haftalar** | Rota)
K-2'nin ekranı. Rota sekmesine dokunmaz; bir haftaya tıklayınca o haftanın Rota'sı açılır.
- **Dönem şeridi:** dönemin tüm haftaları yatay. Her hafta için:
  - durum: geçmiş / onaylı / taslak (otomatik) / boş;
  - ziyaret sayısı;
  - kapasite doluluk çubuğu;
  - tatil işareti (ör. 29 Ekim) ve uyarı rozetleri.
- **Otomatik taslak kuralı** (ekranda kısa açıklama):
  - 1. hafta onaylanınca sonraki haftalar her hedefin **sıklığına** göre doldurulur;
  - ziyaretler eşit aralıklarla dağıtılır;
  - **günler dengelenir** (günlük üst sınır); hafta sonu ve tatile ziyaret düşmez;
  - sıklığı bilinmeyen hedef "dönemde 1" sayılır ve **"sıklık yok"** rozetiyle gösterilir.
- **Hafta kartı ayrıntısı:**
  - gün gün ziyaret sayısı (Pzt–Cum);
  - kaydırılan / sığmayan hedefler ve nedenleri: kapasite dolu, doktor müsait değil, izin engelli;
  - eylemler: "Haftayı onayla", "Bu haftayı yeniden üret", "Rotayı aç".
- **Doktor bazında dönem görünümü** (açılır panel): doktor → hangi haftalarda planlı, sıklık hedefi, yapılan / kalan.

## 6) YER BIRAKILACAK, ÇİZİLMEYECEK
- **Yönetici görünümü** (ileride): ekibin haftaları, sıklık uyumu, onay. Bu mockup yalnız şunu gösterir: başlıkta "Benim planlarım" yazar ve bir görünüm anahtarı için yer vardır.
- **"Ne sunacağım"** (her ziyarette sunulacak içerik; ayrı iş SB-3): Rota'ya eklenmez. Mockup, Haftalar'daki doktor panelinde "sıradaki içerik: …" satırı için yer ayırabilir.
- **Mobil uygulama:** aynı veriyi kullanır, bu mockup Web içindir.

## 7) DURUMLAR (her ekranda çiz)
- Boş (henüz plan yok → "Bu hafta için plan oluştur"); yükleniyor; hata.
- Aktif dönem yok; dönemin kapasitesi yok; çalışma takvimi okunamadı (tatiller bilinmiyor uyarısı).
- Bölgesi olmayan temsilci ("Size bölge atanmamış" — yöneticiye başvurun).
- Onaylı hafta (salt okunur); geçmiş hafta.
- Yetkisiz kullanıcı: sayfa görünmez, iskelet çizilmez, yönlendirme yapılmaz.

## 8) GENEL KURALLAR
- 7 dil (tr, en, fr, es, zh, ar, ru); Arapça için sağdan sola düzen.
- Masaüstü öncelikli, tablette de çalışmalı.
- Klavye ile kullanılabilir; renkler tema değişkenleriyle.
- Sayılar yerel biçimde; tarih biçimi bugünkü gibi.

## 9) MOCKUP'TA BEKLENEN EKRANLAR
1. Liste: hedef sayısı, haftalar sütunu, boş taslak rozeti.
2. Yeni taslak plan: otomatik alanlar, strateji şablonu yok; K-4 (b) ve alternatif (a) karesi.
3. Plan detayı üst kısım: durum bazlı eylemler, haftalık ve dönem arz / talep.
4. Hedefler sekmesi: bölge hesapları, sıklık / yapılan / kalan, adlı "Seçilenler", tahmini süre.
5. Haftalar sekmesi: dönem şeridi, hafta kartı ayrıntısı, doktorun dönem paneli.
6. Durumlar (§7).
