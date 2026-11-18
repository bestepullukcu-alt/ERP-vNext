# MOCKUP BRIEF — MOD-0180 Dalga Planlama (Wave Planning)

> Tarih: 2026-11-10 · Hazırlayan: MVP-5 CT. **Paket yok** — bu özet paketten önce yazıldı (sahip kuralı: her yeni modülde önce mockup
> özeti). Kaynaklar: Blueprint 8.8 (MOD-0180 satırı: "Wave Planner View; Performance KPIs; Exception Queue", WMS-WAVE-BUNDLE, SoR:
> dalgalar, dalga atamaları, dalga sonuçları), plan `docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md` (W-4),
> MOD-0178 paketi (toplama görevleri, outbound, Q-08 izin namespace kuralı). Kapsam kararı (Beste): **toplama dalgası, tek depo**.
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Mevcut ürün düzenine (sol menü, üst çubuk, kart yapısı, açık / koyu tema) ve
> depo ekranlarının bugünkü görünümüne (Yerleştirme Görevleri, Toplama Görevleri, Lokasyon Rolleri) uy. Kurallar henüz pakette
> yazılı değildir; mockup bu kuralları **önerir**, paket kesinleştirir. Emin olmadığın her yeri "açık soru" olarak yaz.

## BAĞLAM
- **Kullanıcılar:** depo planlayıcısı (dalgayı kurar ve serbest bırakır), vardiya sorumlusu (dalgayı izler, istisnaları çözer), toplayıcı
  (dalgadan gelen görevleri MOD-0178 Toplama Görevleri ekranında yapar; bu modülde ayrı ekranı yok). Çok şirketli, çok depolu ilaç dağıtımı.
- **Ne:** sevke hazırlanacak siparişlerin (MOD-0178 outbound) **tek bir depoda** gruplara (dalga) bölünmesi ve toplama görevlerinin dalga
  dalga serbest bırakılması. Amaç: aynı rafa birçok kez gitmemek, kargo çıkış saatine yetişmek, toplayıcıları dengelemek.
- **Ne değil:** stok tahsisi (rezervasyon) MOD-0178 / MVP-1'in işidir; dalga yeni tahsis yapmaz, tahsisi yapılmış satırları gruplar.
  Depolar arası dalga yok. Rota optimizasyonu ilk sürümde yok (öneri — açık soru).
- **Akış:** Dalga oluştur (Taslak) → siparişleri ekle (elle ya da kurala göre öner) → Serbest bırak (toplama görevleri oluşur) → İzle
  (görevler tamamlandıkça ilerleme) → Kapandı. İptal yalnız serbest bırakmadan önce; serbest bırakılmış dalga için "kalan görevleri geri çek".
- **Görünen kod:** `{ŞİRKET}-WAV-000042` (RCS-001 ortak servisinden; `WAV` öneki `.antigravity` satırı ister — açık soru).

## 1) DALGA PANOSU — `/Warehouse/Waves` (menüde)
### 1.1 KPI kartları
- Bugün serbest bırakılan dalga · Açık (izlenen) dalga · Kesim saatini kaçırma riski olan dalga · Ortalama dalga tamamlanma süresi ·
  İstisna sayısı (açık).

### 1.2 Liste
- **Sütunlar:** kod, depo, dalga türü (Kargo çıkışı / Müşteri / Bölge / Elle), kesim saati, sipariş sayısı, satır sayısı, toplama görevi
  sayısı (tamamlanan / toplam), ilerleme çubuğu, durum, oluşturan, serbest bırakan, tarih.
- **Durumlar:** Taslak · Serbest bırakıldı · Toplanıyor · Tamamlandı · Kısmen tamamlandı · İptal.
- **Süzgeçler:** depo (zorunlu, tek depo), durum, tarih, kesim saati aralığı, tür, "riskli olanlar".
- **Satır menüsü:** Aç, Serbest bırak (taslakta), İptal (taslakta, gerekçe), Kalan görevleri geri çek (serbest bırakılmışta, gerekçe).
  **"Sil" yok.**

### 1.3 Oluştur / düzenle (yan panel — Slim, ≤ 8 alan önerisi)
1. Depo (zorunlu; sonra değişmez)
2. Dalga türü
3. Kesim saati (kargo çıkışı)
4. Taşıyıcı / sevk yöntemi süzgeci (isteğe bağlı)
5. Müşteri ya da teslim bölgesi süzgeci (isteğe bağlı)
6. En çok sipariş / satır sınırı
7. Sorumlu (kullanıcı)
8. Not (≤ 500)
- "Siparişleri öner" düğmesi süzgeçlere uyan, tahsisi tamamlanmış ve başka dalgada olmayan siparişleri listeler; kullanıcı seçer.

### 1.4 Ayrıntı
- Üst: kod, durum çizgisi, kesim saati geri sayımı, ilerleme.
- **Siparişler sekmesi:** sipariş no, müşteri, satır sayısı, toplama durumu, paketleme durumu (MOD-0178'den okuma).
- **Görevler sekmesi:** dalgadan oluşan toplama görevleri (MOD-0178 bağlantısı), toplayıcı, raf, durum.
- **Yük sekmesi:** bölge / raf bazında satır yoğunluğu (basit ısı tablosu), toplayıcı başına görev sayısı.
- Olay geçmişi: kim oluşturdu, ekledi / çıkardı, serbest bıraktı, geri çekti (gerekçeyle).

## 2) PLANLAYICI GÖRÜNÜMÜ — `/Warehouse/WavePlanner` (menüde)
- Sol: dalgaya girmemiş, tahsisi tamamlanmış siparişler (süzgeçli tablo: kesim saati, müşteri, taşıyıcı, satır, ağırlık / hacim varsa).
- Sağ: taslak dalgalar (kart). Siparişi dalgaya **sürükle-bırak ya da "Ekle" düğmesi** (klavyeyle de yapılabilmeli).
- Her dalga kartında: sipariş / satır sayısı, sınırlara göre doluluk, kesim saati, uyarılar (ör. "3 sipariş farklı taşıyıcıda").
- Tahsisi eksik sipariş listede gri ve nedeni yazılı ("rezervasyon bekliyor"); dalgaya eklenemez.

## 3) İSTİSNA KUYRUĞU — `/Warehouse/WaveExceptions` (menüde)
- **Satır:** dalga kodu, sipariş, satır, istisna türü, oluşma zamanı, durum (Açık / Çözüldü / Yok sayıldı), çözen.
- **Türler (öneri):** toplamada eksik çıktı (kısa toplama) · rafta stok yok · lot geri çağırmada (`LOT_UNDER_RECALL`) · kesim saati kaçtı ·
  görev uzun süredir bekliyor · sipariş dalgadayken iptal edildi.
- **Eylemler:** Siparişi dalgadan çıkar · Sonraki dalgaya taşı · Yok say (gerekçe). Stok düzeltme bu ekrandan **yapılmaz** (bağlantı ile
  ilgili ekrana gider).

## 4) PERFORMANS — `/Warehouse/WaveKpis` (menüde ya da panoda sekme)
- Zaman serisi: günlük dalga sayısı, ortalama tamamlanma süresi, kesim saatine yetişme oranı, toplayıcı başına satır / saat.
- Süzgeç: depo, tarih aralığı (en çok 92 gün önerisi), dalga türü. CSV dışa aktarma.

## 5) DURUMLAR (her ekranda çiz)
- Boş liste; yükleniyor; hata ("Stok servisine ulaşılamadı").
- Yetkisiz kullanıcı (UAS-001): sol menü ve üst çubuk kalır; içerikte **tek bir açıklama** ("Yöneticinizden yetki isteyin"). İskelet,
  yönlendirme, boş ekran ya da yalnız toast yok.
- Ekranlar rol adına değil izne göre çizilir (izin namespace'i MOD-0180'in kendisinin, ör. `wave.*` — Q-08 kuralı).
- Serbest bırakılmış dalgada sipariş listesi salt okunur (yalnız "geri çek").
- Çakışma: başkası aynı dalgayı değiştirdiyse "Kayıt değişti, yenileyin".

## 6) GENEL KURALLAR
- 7 dil; Arapça sağdan sola. Her sayfada "Yardım" paneli; tur yok.
- Masaüstü öncelikli (planlayıcı geniş ekran); 375 px'te pano ve istisna kuyruğu okunabilir olmalı, yatay sayfa kaydırması yok.
- Klavye ile kullanılabilir; renkler tema değişkenleriyle; risk / durum yalnız renge dayanmaz.
- Saat, tarih, sayı biçimi kullanıcının diline ve deponun saat dilimine göre (kesim saati deponun saatiyle — açık soru).

## 7) İSTENEN ÇIKTI
- Tek dosyalık tıklanabilir HTML prototip (örnek veriyle) + ekran metinleri listesi (anahtar adı önerisiyle).
- Ekranlar: Pano (liste + KPI + oluştur paneli + ayrıntı), Planlayıcı, İstisna kuyruğu, Performans, durum örnekleri.
- Açık sorular listesi. Bilinen açık sorular:
  - **D-1** Dalga kuralı önerisi otomatik mi (her N dakikada) yoksa yalnız elle mi? (öneri: v1 elle + "öner" düğmesi)
  - **D-2** Rota / sıra optimizasyonu v1'de var mı? (öneri: yok, raf sırasına göre basit sıralama)
  - **D-3** Kesim saati deponun saat diliminde mi?
  - **D-4** `WAV` kayıt kodu öneki ve `wave.*` izin namespace'i (`.antigravity` satırları — ayrı onay)
  - **D-5** Paketleme / sevk ilerlemesi dalga ekranında gösterilecek mi, yoksa yalnız toplama mı?
