# DESIGN GAP — W4 rapor v2: mockup 03-rapor ↔ kararlar K-W4…K-W7

> **Tarih:** 2026-10-13 · **Hazırlayan:** CT · **Kural:** kararlar mockup'ın önüne geçer. Mockup, kararlardan önce çizildi (2026-10-08).
>
> **Kaynaklar:**
> - [03-rapor.html](decoded/03-rapor.html) + [00-demo-veri.js](decoded/00-demo-veri.js);
> - [yol haritası](../../ROADMAP-visit-workspace.md) K-W4…K-W7;
> - [rakip ürün brifi](BRIEF-W4-competitor-products.md);
> - W3 yürütme (yakalanan işaretler, imza, kanıt) ve W3 yazım (itiraz kütüphanesi).
>
> **Durum işaretleri:** ✅ mockup kararla aynı · ✏️ değişir · ➕ eklenir · ➖ çıkar · ⏭ başka faza

## 1. Hekim raporu — adım yapısı
Mockup: **Genel → ürün başına bir adım (A Sunum ve ilgi · B Pazar ve rakip · C Talepler ve sonraki adım) → Takip ve güvenlik → Özet ve gönder**. Sol tarafta adım listesi ve eksik sayısı.
✅ Yapı korunur. Ürün adımı üç bölüm ("yana kaydırın") olarak kalır.

### 1.1 Genel
| alan | durum | not |
|---|---|---|
| Sonuç: Yapıldı (Yapılamadı / Ertele takvimden) | ✅ | W2 / W3 kuralı |
| Gerçek başlangıç / bitiş (sunucu saati, değiştirilemez) | ✅ | W3 yürütmeden gelir |
| Ziyaret türü, ortak ziyaret (kim) | ✅ | ortak ziyaretçi araması FU-W3-COVISITOR-LOOKUP ile |
| Görüşülen kişi | ✅ | |
| Genel değerlendirme (sunumdan ön dolar) | ✅ | |
| Plan dışı ürün ekle | ✅ | "plan dışı ürün" etiketi |

### 1.2 A · Sunum ve ilgi (ürün başına)
| alan | durum | not |
|---|---|---|
| Sunulan içerik / aşama ("plana uydu") | ✏️ | Mockup'taki serbest liste (Farkındalık mesajı, Klinik kanıt 1…) yerine **sunumda gerçekten gösterilen yol adımları** (W3 yürütme kaydı) salt okunur özet. "Plana uydu / sapma" sunucu hesabı. |
| Ürüne harcanan süre (dk, sayaçtan; düzeltilebilir) | ✅ | W3 yakalama süresi |
| **Etkinlik ★ "mesaj karşılık buldu mu"** | ✏️ | **K-W4 (4) ziyaret başarısı** ile değişir: Ulaşıldı / Kısmen / Ulaşılamadı. Hedef satırı: "anahtar mesaj" ya da "sonraki aşama" (yolun anahtar mesajı gösterilir). |
| ➕ **Doktor taahhüdü** | ➕ | Deneyecek / Hastasına yazacak / Daha fazla bilgi istiyor / Taahhüt yok (K-W4). Sonraki ziyaret mesajı önerisini besler (ör. Deneyecek → "nasıl gitti"). |
| Doktorun ilgisi (İlgili / Nötr / İlgisiz, sunumdan) | ✅ | W3 işaretinden ön dolar |
| Bağlılık (benimseme) aşaması, 6 seviye + "Önceki ziyaret · Aynısı" | ✅ | **K-W4 (1):** liste = içerik yolculuğu aşamasıyla aynı. Seçim **JourneyProgress**'i ilerletir (SB-3c kapanır) ve sonraki ziyaretin içeriğini seçer. |

### 1.3 B · Pazar ve rakip (ürün başına)
| alan | durum | not |
|---|---|---|
| Endikasyondaki hasta (aylık), endikasyon × hasta profili satırları, sayı + "Önceki · Aynısı", "Bu endikasyonlarda hasta görmüyor" | ✅ ✏️ | Satırlar **KnowledgeConcepts**'ten (CT-002 Endikasyon × CT-003 Hasta profili, ürün konusuna bağlı). Sayı tahmini. **3 ayda bir** sorulur; arada son değer salt okunur gösterilir ("güncelle" ile açılır). |
| ➕ **Doktorun aylık toplam hastası** | ➕ | **Doktor düzeyinde** (ürün başına değil). Doktor profilinde tutulur, 3 ayda bir. Endikasyon toplamı toplamı **geçemez** (anlık uyarı + gönderimde engel). |
| Potansiyel sınıfı | ✏️ | Mockup sabit eşik (≥ 60). **Ürün (endikasyon) başına eşik:** Az < 10 · Orta 10–30 · Çok > 30 (varsayılan), pazarlama değiştirir (standart ayar ekranı). |
| **Reçete payımız (%)** | ✏️ | **K-W4 (3)** seçmeli: Hiç / Ara sıra / Yarı yarıya / Çoğunlukla / Hep. Yüzde girilmez. |
| **"Hesaplanan · Bağlılık modeli · ürün sahibi: A / B / C"** | ➖ | Model seçimi kalkar (karar verildi). |
| Fırsat etiketi | ✏️ | **K-W4 matrisi:** Çok × Hiç–Ara sıra = **Fırsat** · Çok × Yarı yarıya = **Büyüt** · Orta × Hiç–Yarı yarıya = **Büyüt** · Az × Hiç–Yarı yarıya = **Düşük öncelik** · her potansiyel × Çoğunlukla–Hep = **Koru**. Mockup'taki "Düşük potansiyel · yüksek pay → büyüt" yanlış. Sunucu hesaplar; planlamada fırsat doktorları öne çıkar. |
| Rakip · doktorun kullandığı (çipler) + "Rakip kullanmıyor" | ✅ | Liste MDM rakip ürünlerinden: ürünümüzün rakibi + endikasyon. |
| Rakip kartı: etken madde, form / doz, fiyat (kutu), geri ödeme | ✅ | Ülke = temsilcinin ülkesi (MDM ülke tablosundan). |
| Rakip kartı: **pazar payı (kurum / bölge)** | ⏭ | Pazar verisi W7. W4'te gösterilmez. |
| "Listede yok, rakip ekle" (ad + firma) · "temsilci ekledi · onay bekliyor" | ✅ | **Önerilen rakip** kararı: öneren temsilci hemen kullanır; pazarlama / tıbbi onaylar, birleştirir ya da reddeder. |
| **"Bu doktorda bu rakibi kullanan hasta (%)"** | ❓ | Doktorun tutamayacağı bir sayı (bizim pay için yüzde kaldırıldı). CT önerisi: **kaldır** (soru S2). |
| "Rakibe göre: + avantaj / − dezavantaj" (Etkinlik, Güvenlik, Doz kolaylığı…) | ❓ | Kararlarda yok. CT önerisi: **isteğe bağlı kalsın** (soru S2). |

### 1.4 C · Talepler ve sonraki adım (ürün başına)
| alan | durum | not |
|---|---|---|
| İtirazlar (kategori, not, "Giderildi mi?") + "itiraz olmadı" | ✅ ✏️ | Kategoriler W3 BRD setinden. Sunumda açılan **kütüphane yanıtı** (W3 yakalama) satırda gösterilir. |
| Talepler (Literatür, Numune, Hasta materyali, Eğitim daveti, MSL, Diğer) + "talep yok" | ✅ | BRD seti |
| Numune: SKU, adet, lot (opsiyonel), "Teslim imzası alındı · bitiş özeti" | ✅ | W3 imzası; SKU = **GSKU** (Global Ürün kuralı) |
| Sonraki ziyaret mesajı + "Öneri · Öneriyi kullan" | ✅ ✏️ | Öneri = bağlılık aşamasının sonraki adımı + taahhüt kuralı. |

### 1.5 Takip ve güvenlik
| alan | durum | not |
|---|---|---|
| Advers olay bildirildi mi (24 sa) — zorunlu | ✅ | "Evet" → AE kaydı yönlendirmesi (PV entegrasyonu sonra; W4'te bayrak + 24 sa görevi) |
| Endikasyon dışı bilgi istendi mi — zorunlu | ✅ | "Evet" → MSL talebi |
| Takip gerekli, önerilen sonraki ziyaret, takip notu | ✅ | |

### 1.6 Doktor profili (rapor içinde kart)
| alan | durum | not |
|---|---|---|
| **"Ürün sahibi kararı · tipoloji modeli"** seçici | ➖ | **K-W5:** ikisi + KOL kararı verildi; seçici kalkar. |
| Benimseme tipolojisi + iletişim stili + **KOL düzeyi** | ✅ ✏️ | BRD'den (kiracı uyarlar). Temsilci **önerir**; kayda işlenir, önceki değer saklanır (geçmiş). |
| ➕ Aylık toplam hasta (3 ayda bir), en uygun gün / saat, tercih edilen kanal | ➕ | Yol haritası satır 44 (doktor profili alanları). |

### 1.7 Özet ve gönder
✅ Eksik listesi ("3 eksik" → adıma git), gönderim. Gönderildikten sonra 60 dk serbest düzeltme, sonra gerekçeli düzeltme (değişen alanlar geçmişte).
⏭ **"Gönderilen e-postalar · rapordaki yanıtlara göre otomatik"** W5'e (bildirimler) kalır.

## 2. Eczane raporu
Mockup adımları: **Genel → Stok ve raf → Sipariş ve satış → Rakip ve tavsiye → Güvenlik → Özet**.

| bölüm | durum | not |
|---|---|---|
| Genel: görüşülen (Eczacı / kalfa…), ziyaret türü | ✅ | |
| Stok: ürün başına stok düzeyi, miadı yakın, iade sorunu | ✅ | Ürün = GSKU |
| Raf / görünürlük, materyal / stand, **raf fotoğrafı** | ✅ | Fotoğraf W3 kanıt deposu (MOD-0262), aynı yetki deseni |
| Sipariş: "Ön sipariş oluştur / Siparişi aç" | ⏭ | **W6.** W4'te pasif düğme + "yakında" |
| Satış: ürün başına **kutu / ay** + eğilim | ❓ | Eczacıdan sayı almak güvenilir değil. CT önerisi: **yalnız eğilim** (artıyor / aynı / azalıyor), sayı isteğe bağlı (soru S3). |
| Rakip (eczanede öne çıkan) + kart | ✅ | MDM rakip listesi, öneri akışı aynı |
| Tavsiye ve reçete akışı (tavsiye düzeyi, bağlı doktorlar) | ✅ | Bağlı doktor = CRM Contact (bölgedeki) |
| Eğitim / materyal ve talepler | ✅ | BRD seti |
| Güvenlik (advers olay) | ✅ | Eczanede endikasyon dışı sorusu yok |

## 3. Görünürlük (K-W7)
- Temsilci yalnız **kendi doktorlarının** potansiyel / pay / fırsat değerlerini görür.
- Yönetici **ekibinin** doktorlarını görür.
- Pazarlama **toplulaştırılmış** görür (doktor kimliği yok).
- Mockup yalnız temsilciyi gösteriyor. Yönetici ve pazarlama görünümleri **W7 doktor kartı / panolar**; W4'te yalnız sunucu kuralı ve uçlar.

## 4. Veri kaynakları ve yeni referans setleri (W4'te açılır)
| liste | kaynak |
|---|---|
| Kullanım aşaması (6) | içerik yolculuğu aşamasıyla tek liste (MOD-0162 yolculuk aşama seti) |
| Ziyaret başarısı, doktor taahhüdü, pay kovaları, ilgi | **yeni BRD setleri** (kiracı uyarlar) |
| Benimseme tipolojisi, iletişim stili, KOL düzeyi | **yeni BRD setleri** |
| Geri ödeme durumu | **yeni BRD seti** (rakip ürün brifi kararı) |
| Endikasyon / hasta profili | KnowledgeConcepts (CT-002 / CT-003) |
| Rakip ürün | MDM (W4-BE-b) |
| İtiraz / talep kategorileri | mevcut BRD (W3) |

## 5. Paket etkisi
- **W4-BE-a** (rapor modeli): §1.1–1.5, §1.7, §2 alanları; zorunluluk + "yok" seçenekleri; önceki değer; sunumdan ön doldurma; JourneyProgress ilerletme; 60 dk / gerekçeli düzeltme; yeni BRD setleri.
- **W4-BE-b** (MDM rakip ürün): brif + kararlar. Mockup beklenmeden yazılabilir (alanlar brifte sabit).
- **W4-BE-c** (doktor profili + potansiyel + fırsat): §1.3 toplam hasta / eşik / matris, §1.6 profil + geçmiş, §3 görünürlük kuralı; eşik ayar uçları.
- **W4-WEB-a** hekim raporu · **W4-WEB-b** eczane raporu: bu belgedeki ✏️ / ➕ / ➖ ile mockup'a göre.
- **W4-WEB-c** rakip ürün yönetimi: **mockup gelince**.
- **Eşik ayar ekranı:** standart CRM ayar deseni; BE-c'nin Web eşi küçük sayfa (WEB-b ile ya da ayrı küçük paket).

## 6. Açık sorular (ürün sahibi)
- **S1.** Mockup'taki "Etkinlik ★ — mesaj karşılık buldu mu" yerine K-W4'teki **ziyaret başarısı** (Ulaşıldı / Kısmen / Ulaşılamadı) gelir, yıldız kalkar. CT önerisi: evet.
- **S2.** Rakip bölümünde "Bu doktorda bu rakibi kullanan hasta (%)" ve "Rakibe göre + / − avantaj". CT önerisi: yüzde **kalksın** (doktor tutamaz; bizim pay da kovaya döndü); avantaj / dezavantaj **isteğe bağlı kalsın**.
- **S3.** Eczane satışında "kutu / ay" sayısı. CT önerisi: **eğilim zorunlu** (artıyor / aynı / azalıyor), sayı isteğe bağlı.
