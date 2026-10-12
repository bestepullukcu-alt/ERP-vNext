# MOCKUP BRIEF — W3 yazım ekranları: itiraz kütüphanesi, adım sunum notları, ziyaret kanıtı ayarları

> **Tarih:** 2026-10-12 (güncelleme: katmanlı ayar + süreli istisna) · **Hazırlayan:** CT · **İsteyen:** ürün sahibi (W3-WEB-c ve W3-WEB-d öncesi).
>
> **Dayanaklar:**
> - [W3 paketi](../../WP-VW-W3-visit-execution.md): §0 kararlar, §2.1–2.3 alanlar ve sınırlar.
> - Temsilcinin göreceği sonuç ekranları: [02-ziyaret-kanit-sunum.html](decoded/02-ziyaret-kanit-sunum.html) (E3 kanıt, E4a doktor ekranı, E4b kumanda "Notlar" sekmesi).
> - Mevcut Content Studio görünümü:
>   - [KP Studio mockup'ı](../kp-studio/) (bilgi yolu çalışma alanı, adım düzenleyici);
>   - [Claims v2 mockup'ı](../claims-v2/) (onay akışı, durum çipleri, liste / ayrıntı düzeni).
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: sol menü, üst çubuk, kart yapısı, açık / koyu tema. Liste sayfası düzeni Claims ve Bilgi Yolları ile aynı (süzgeç kartı + tablo + sağ üstte "Yeni").
> - Bu ekranlar **yönetici / içerik ekibi** içindir; **masaüstü öncelikli** çiz. Tablet genişliğinde bozulmasın; telefon gerekmez.
> - **Dil:** Türkçe çiz. Arapçada sağdan sola düzen bozulmamalı.
> - Gerçekçi demo verisi: ÜROFLEKS, TUTUKON, ALMIBA ürünleri; itiraz kategorileri (Fiyat, Geri ödeme, Etkinlik, Yan etki, Doz / kullanım, Erişim / stok, Alışkanlık / rakip memnuniyeti, Kanıt yetersiz, Diğer).

---

## A. İtiraz kütüphanesi (yeni sayfa · Content Studio menüsünde)

**Amaç:** temsilcinin sunum sırasında göreceği "Beklenen itirazlar · onaylı yanıt" kayıtlarını yazmak, onaylatmak, emekliye ayırmak.

### A1. Liste
- **Süzgeçler:** ürün, kategori, durum (Taslak / Onaylı / Emekli), metin arama.
- **Tablo sütunları:**
  - ürün;
  - kategori (renkli çip);
  - itiraz (soru metninin ilk satırı);
  - bağlı iddia sayısı;
  - kullanıldığı adım sayısı;
  - durum çipi;
  - son değişiklik (kişi, tarih);
  - eylemler.
- Sağ üstte "Yeni itiraz yanıtı".
- **Boş durum:** "Henüz itiraz yanıtı yok" + açıklama + "Yeni" düğmesi.

### A2. Oluştur / düzenle (taslak)
**Alanlar:**

| alan | tür | sınır / kural |
|---|---|---|
| Ürün | seçim (MDM ürünleri) | zorunlu |
| Kategori | seçim (9 kategori) | zorunlu |
| İtiraz (doktorun sorusu) | metin | ≤ 500 karakter, sayaç |
| Onaylı yanıt | çok satırlı metin | ≤ 4000, sayaç |
| Dayanak iddialar | çoklu seçim | yalnız **onaylı** iddialar; her birinin metni ve referansı görünür |

- **Önizleme kutusu:** temsilcinin kumanda ekranında göreceği akordiyon satırı, E4b "Beklenen itirazlar" stiliyle (soru, açılınca yanıt + referanslar).
- **Düğmeler:** "Taslağı kaydet", "Onaya gönder" (veya doğrudan "Onayla" yetkisi olan için), "Vazgeç".

### A3. Ayrıntı / onay
- **Durum çipi ve geçmiş:** oluşturan, onaylayan, tarihler.
- **Onaylı kayıt salt okunur.** Üstte bilgi şeridi "Onaylı yanıt değiştirilemez. Değiştirmek için emekliye ayırıp yenisini oluşturun." ve düğmeler "Emekliye ayır", "Kopyasından yeni taslak".
- **Yazar kendi taslağını onaylayamaz (görev ayrılığı).** Onay düğmesi pasif + açıklama: "Bu kaydı siz yazdınız; başka bir onaylayıcı onaylamalı."
- **"Kullanıldığı yerler":** bu yanıtı bekleyen bilgi yolu adımlarının listesi (yol adı, sürüm, adım başlığı).

## B. Bilgi yolu adımı — sunum notları (mevcut KP Studio adım düzenleyicisine yeni bölüm)

**Amaç:** her adım (slayt) için temsilcinin göreceği sunum notlarını yazmak.

- **Yer:** KP Studio'da adım düzenleyicisinin içinde yeni bir "Sunum notları" bölümü (sekme veya katlanır kart; tasarımcı seçer). Yeni sayfa değil.
- **Alanlar:**

| alan | tür | sınır |
|---|---|---|
| Anahtar mesaj | metin | ≤ 500 |
| Konuşma metni (onaylı) | çok satırlı metin | ≤ 4000 |
| Hedef süre | sayı + birim (sn) | 10–3600 sn; boşsa adımın tahmini süresi kullanılır (ipucu metni) |
| Geçiş cümlesi | metin | ≤ 500 |
| Beklenen itirazlar | çoklu seçim | en çok 10; yalnız aynı ürünün **onaylı** itiraz yanıtları; "Kütüphanede yeni oluştur" bağlantısı |

- **Önizleme:** yanyana iki küçük önizleme.
  - "Doktor ekranı": başlık, madde işaretleri, referans. Notlar burada **görünmez**; bunu vurgulayan küçük etiket.
  - "Temsilci kumandası": anahtar mesaj, "Bu slayt 00:00 · hedef ≈ 1 dk", konuşma metni, itirazlar, geçiş.
- **Yayınlanmış sürüm:** alanlar salt okunur. Mevcut "yeni sürüm oluştur" akışı geçerli.
- **Tamlık göstergesi:** yol düzeyinde "Sunum notları: 4 / 6 adım dolu" ve eksik adımlara bağlantı.

## C. Ziyaret kanıtı ayarları (yeni sayfa · CRM Ayarlar / Ziyaret menüsünde)

**Amaç:** yöneticinin temsilcinin "Ziyarete başla" kurallarını ayarlaması (K-W2).

**Katmanlı ayar (kullanıcı kararı 2026-10-12): en özel olan kazanır.**
1. **Kiracı varsayılanı:** herkes için geçerli temel ayar (her zaman var).
2. **Tüzel kişilik ayarı:** varsa o tüzel kişiliğin temsilcileri için kiracıyı ezer (ör. ülke mevzuatı farkı).
3. **İş birimi ayarı:** varsa o iş biriminin temsilcileri için tüzel kişiliği de ezer (ör. hastane ekibi / KAM ekibi).
4. **Temsilci için süreli istisna:** kalıcı ayar DEĞİL, gerekçeli ve süreli muafiyet (§C4).

- Bir katman yalnız **değiştirdiği alanları** tutar; boş bıraktığı alan bir üst katmandan gelir (alan bazında miras). Tasarımcı bunu her alanda göstermeli: "Kiracıdan geliyor: Zorunlu" gibi soluk bir etiket + "Bu kapsamda değiştir" bağlantısı; değiştirilmiş alanın yanında "Üst ayara dön".

### C1. Kapsam listesi (sayfanın sol ya da üst bölümü)
- Ağaç ya da gruplu liste:
  - **Kiracı varsayılanı** (en üstte, silinemez);
  - altında **tüzel kişilikler** (yalnız kendi ayarı olanlar + "Tüzel kişilik ekle");
  - her tüzel kişiliğin altında **iş birimleri** (aynı şekilde).
- Her satırda: ad, kaç alanı değiştirdiği ("3 alan farklı"), etkilediği temsilci sayısı, son değişiklik.
- **Etkin ayar arama:** "Bir temsilcinin etkin ayarını gör" kutusu. Temsilci seçilince her alanın değeri ve **nereden geldiği** gösterilir (Kiracı / Tüzel kişilik: Diten İlaç A.Ş. / İş birimi: Hastane Ekibi / İstisna).

### C2. Seçili kapsamın ayar formu
**Alanlar ve kiracı varsayılanları** (W3 paketi §2.2):

| alan | denetim | varsayılan | açıklama metni (örnek) |
|---|---|---|---|
| Konum kontrolü | Zorunlu / Bilgi | Zorunlu | "Zorunlu: temsilci kuruma yakın değilse ziyareti başlatamaz." |
| İzin verilen mesafe | sayı (m) | 300 | 50–5000 m |
| En düşük konum doğruluğu | sayı (m) | 150 | 10–1000 m |
| Konumu olmayan kurum | Başlatılsın ve işaretlensin / Engellensin | Başlatılsın ve işaretlensin | "Kurum kaydında koordinat yoksa" |
| Fotoğraf | Zorunlu / İsteğe bağlı | İsteğe bağlı | "Kurum girişi ya da bekleme alanı; yüz çekilmez (KVKK)." |
| Doktor onayı (imza) | aç / kapa | açık | "Ziyaret sonunda tablette imza ya da onay dokunuşu. Numune isteğinde her zaman istenir." |
| Süre tutarlılığı | aç / kapa | açık | |
| Kısa ziyaret eşiği | sayı (sn) | 120 | 30–1800 sn; "Bu süreden kısa ziyaret işaretlenir." |

- Kiracı varsayılanında tüm alanlar doludur. Tüzel kişilik / iş birimi formunda her alan "üstten gelen" ya da "bu kapsamda değiştirilmiş" durumundadır (yukarıdaki miras gösterimi).
- **Önizleme kartı:** "Bu kapsamdaki temsilci şunu görecek": E3 ekranının küçük bir kopyası. Etkin değerlere göre "zorunlu / isteğe bağlı" etiketleri ve "Sunumu başlat" düğmesinin ne zaman açılacağını anlatan cümle.
- **Kaydet:** onay iletişim kutusu "Değişiklik hemen bu kapsamdaki N temsilciye uygulanır." Son değişiklik (kişi, tarih) gösterilir.
- **Kapsamı kaldır** (tüzel kişilik / iş birimi): "Bu kapsamdaki temsilciler üst ayara döner" onayı.

### C3. Değişiklik geçmişi
- Seçili kapsam için liste: tarih, kişi, alan, eski → yeni değer. (Denetim için; salt okunur.)

### C4. Süreli istisnalar (temsilci)
**Amaç:** geçici durum (GPS arızası, kurum konumu yanlış kayıtlı, cihaz değişimi) için tek bir temsilciye kısa süreli gevşetme. Kalıcı kişisel ayar yoktur.
- **Liste:** temsilci, gevşetilen kural(lar), başlangıç–bitiş tarihi, gerekçe, onaylayan, durum çipi (Aktif / Planlı / Süresi doldu / İptal).
- **Yeni istisna formu:**
  - temsilci (kişi seçici);
  - gevşetilecek kurallar: yalnız şunlar seçilebilir → "Konum kontrolü: Bilgi" · "Fotoğraf: İsteğe bağlı" · "Konumu olmayan kurum: Başlatılsın" (eşik değerleri istisnayla değiştirilmez);
  - başlangıç ve bitiş tarihi (**en çok 30 gün**; bitişsiz istisna yok);
  - gerekçe (zorunlu metin);
  - onay: oluşturan ile onaylayan farklı kişi (görev ayrılığı). Taslak → Onaylı akışı; yalnız onaylı ve tarihi içindeki istisna uygulanır.
- **İptal:** onaylı istisna erken bitirilebilir (gerekçeyle).
- İstisnayla başlatılan ziyaretler yönetici görünümünde işaretli olur ("istisna ile başlatıldı"); bu ekran bunu yalnız bilgi metniyle anlatır.

- **QR ve anti-fraud bu ekranda YOK.** İleride (W3E) "Ek kanıtlar" bölümü olarak eklenecek; tasarımcı yer bırakabilir (ör. altta katlanmış, pasif "Yakında" kartı) ama işlevsel çizmesin.

## D. Ortak kurallar
- **Yetki:** yetkisiz kullanıcıya sayfa iskeleti çizilmez, yalnız erişim uyarısı (UAS-001). Salt okuma yetkisinde düğmeler gizli.
- **Doğrulama hataları alanın altında;** kaydetme hatası üstte şerit.
- **Durum çipleri:** Taslak (gri), Onaylı (yeşil), Emekli (soluk / üstü çizili değil, gri çerçeveli).
- **Çizilmesi istenen ekranlar:**
  1. A1 liste (dolu + boş);
  2. A2 düzenle (taslak);
  3. A3 onaylı salt okunur + SoD uyarısı;
  4. B adım düzenleyici "Sunum notları" + önizleme;
  5. B yayınlanmış (salt okunur);
  6. C1 + C2 kiracı varsayılanı seçili (tüm alanlar dolu) + önizleme;
  7. C2 bir iş birimi seçili (alan bazında miras etiketleri; 2 alan değiştirilmiş) + kaydet onayı ("N temsilciye uygulanır");
  8. C1 "etkin ayar" araması: bir temsilcinin değerleri ve kaynakları;
  9. C3 değişiklik geçmişi;
  10. C4 istisna listesi + yeni istisna formu + SoD uyarısı;
  11. Arapça RTL'de bir ekran (A1 veya C2).
