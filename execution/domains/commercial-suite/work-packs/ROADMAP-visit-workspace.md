# YOL HARİTASI — Ziyaret Çalışma Alanı (tek takvim: planla · ziyaret et · raporla)

> **CT, 2026-10-09.** Kaynaklar:
> - mockup: `mockups/visit-workspace/Ziyaret Calisma Alani (standalone).html`;
> - çözülmüş parçalar: `mockups/visit-workspace/decoded/` (01 takvim, 02 kanıt + sunum, 03 rapor, 04 satış payı + doktor kartı, 05 ön sipariş, 06 e-postalar, 00 demo veri / tema);
> - ürün sahibinin ekran açıklaması (2026-10-09);
> - [brief](mockups/visit-workspace/BRIEF-visit-workspace-calendar.md);
> - [Android raporları](mobile/2026-10-08-visit-planning-contract/).
>
> **Bağlam:** Ziyaret Planlama ([eski yol haritası](ROADMAP-visit-planning.md), Faz 1–4 + 4J / 4L / 4M) bu çalışma alanının **Planla modu** olur. Planlanan Ziyaretler (eski Faz 6) ve Ziyaret Yürütme bu ekranda birleşir. SB-3c (başlat / tamamla) W3–W4'ün içine girer.

## 1. Mockup analizi — ne var
| Ekran | İçerik | Yeni mi? |
|---|---|---|
| **E1 Takvim** | FullCalendar (Standard, MIT); Planla / Yürüt; hafta başlığı (durum, kapasite, onay / yeniden aç, "N ziyaret sığmadı"); gün sütunu doluluk + boş süre; kart durum rengi, geri sayım, sabit / plan dışı simgesi; süzgeçler; telefonda gün listesi | Web yeni sayfa; motor ve okumalar **var** (Faz 3–4L) |
| **Planla + Hedefler** | mockup v3 Hedefler panelde; işaretle → en boş güne; **sürükle → gün + saat sabiti**; ürün uygula (ilk ürün tanıtım) | panel var (4J / 4L / 4M); **saat sabiti yeni** (bugün yalnız gün) |
| **Ayrıntı paneli** | durum bandı (geri sayım, kaçırıldı penceresi), ne sunacağım, önceki ziyaretten (ilgi, bağlılık, açık talep / itiraz, numune), sıklık, duruma göre eylemler | okuma **kısmen var**; "önceki ziyaretten" rapor v2'ye bağlı |
| **E2** İptal / Yapılamadı / Ertele | neden kategorisi (referans seti), "Diğer"de zorunlu not (500), erteleme 8 iş günü doluluk listesi | neden seti **yeni**; erteleme sonucu **karar** (A / B) |
| **E3** Kanıt | fotoğraf (kamera / dosya / web kamerası, KVKK ipucu), konum (kuruma uzaklık), sunucu saati, kiracı ayarı (zorunlu / isteğe bağlı), ek kanıtlar (imza, QR, süre, anti-fraud), çevrimdışı kuyruk | **tamamen yeni** |
| **E4a** Doktor ekranı | onaylı içerik slaytları (tanıtım tümü, hatırlatma tek), ürün sekmeleri, dipnot, PIN kilidi | içerik var (KnowledgeContent / yol / yolculuk); **tablet sunum modu yeni** |
| **E4b** Kumanda | önizleme, ürün sayacı; **notlar:** anahtar mesaj, onaylı konuşma metni, slayt süre hedefi, doktora özel hatırlatma, **beklenen itiraz + onaylı yanıt**, rakip kartı, kişisel not, geçiş cümlesi; işaretler; not / sesle not; ortak ziyaret | **içerik modeline yeni alanlar** (sunum notları, itiraz kütüphanesi); işaret yakalama yeni |
| **E4c** Bitiş | süreler, işaretler, notlar; numune / onay için **doktor imzası**; Rapor gir / Sonra; eczanede ön sipariş | yeni |
| **E5** Hekim raporu (sihirbaz) | Genel; ürün başına **A** sunum + ilgi (aşama, süre, etkinlik, ilgi, bağlılık) · **B** pazar + rakip (endikasyon × hasta profili satırları + kaynak, reçete payı, potansiyel / bağlılık / fırsat, rakip bilgi kartı, avantaj / dezavantaj) · **C** itiraz / talep / numune / sonraki mesaj; "yok" seçenekleriyle **hepsi zorunlu**; takip + **advers olay** (24 sa, PV) + **endikasyon dışı** (MSL); özet + doktor profili; 48 sa, 60 dk değiştir, gerekçeli düzelt; gönderilen e-postalar | rapor **var ama ince** (sonuç, içerik, numune, geri bildirim); **v2 büyük** |
| **E6** Eczane raporu | görüşülen kişi; ürün başına stok / miat / iade; raf + stand + fotoğraf; ön sipariş özeti; satış tahmini + eğilim; rakip; tavsiye + bağlı hekimler; eğitim / materyal; talepler; advers olay | **yeni** |
| **Ön sipariş** | SKU satırları (adet, MF, iskonto, fiyat), depo, teslim, para birimi + kur, KDV, eczacı imzası, **yönetici onayı** (e-postadan Onayla / Reddet), depoya PDF e-posta, geçmiş siparişler | **tamamen yeni** (sipariş modülü yok) |
| **E-postalar** | rapor özeti, advers olay (PV), endikasyon dışı (MSL), rapor süresi doldu, sipariş onay talebi, sipariş (depo, PDF), eczane kopyası, bilgi | **bildirim / e-posta altyapısı yok** (yalnız Auth OTP) |
| **E7** Satış payı | kurum satışı × doktor payı = tahmini satış; %100 kontrolü; potansiyel × pay matrisi; çeyrek eğilimi; kaynak açık soru | **yeni** + veri kaynağı kararı |
| **E8** Doktor kartı | geçmiş / gelecek ziyaretler, bağlılık / ilgi eğilimi, açık talepler, profil (tipoloji, KOL, en uygun zaman, toplam hasta), satış payı özeti | okuma modeli **yeni** |
| **Durumlar** | taslak · planlı · bugün · devam ediyor · rapor eksik (48 sa) · raporlandı · **kaçırıldı** (48 sa içinde yapılamadı / ertele, sonra kilit + yöneticiye bildirim) · süre doldu · iptal / yapılamadı / ertelendi | "kaçırıldı" ve "devam ediyor" **yeni**; 48 sa / iptal kilidi 4K'da |

## 2. Altyapı kontrolü (CT, kod)
| İhtiyaç | Bugün | Sonuç |
|---|---|---|
| Planlama motoru, hafta onayı, gün / sabit, ürün listesi, haftalık varsayılan, ek ziyaret | **var** (Faz 3–4L) | çalışma alanı bunun üzerine kurulur |
| Planlanan ziyaret, takvim okuması, rapor (sonuç / gönder / düzelt, 60 dk) | **var** | genişler (4K + W3 / W4) |
| E-posta / bildirim | **yok** (yalnız Auth SMTP OTP) | **yeni yetenek** (W5) — Platform düzeyinde olmalı |
| Onay akışı | Workflow (MOD-0023) Web'de var, sipariş onayı için kullanılabilir mi doğrulanacak | W6 kararı |
| Dosya / fotoğraf saklama | MOD-0262 ikili depo (WIP, park edilmiş dal) | **karar**: fotoğraf + imza nerede saklanacak (W3) |
| Farmakovijilans | `PvgService` **var** | advers olay yönlendirmesi oraya (W5) |
| Sipariş / satış | **servis yok** | ön sipariş modülü **mimari karar** (W6) |
| Endikasyon × hasta profili | MDM ürününde yalnız referans; endikasyon master **yok** | **yeni veri** (W4) |
| Rakip ürün bilgisi | **yok** | **yeni veri** (W4) |
| Sunum notları / itiraz kütüphanesi | içerik modelinde **yok** | **içerik modeli genişler** (W3) |
| Doktor profili (tipoloji, KOL, en uygun zaman, toplam hasta) | Contact'ta **yok** | **yeni alanlar** (W4) |
| Satış verisi (kurum) | **yok** | kaynak kararı (W7) |

## 3. Ürün sahibi kararları (mockup §15 + CT)
| # | Karar | Hangi fazdan önce |
|---|---|---|
| K-W1 | Ertelemenin sonucu: **A** yeni tarihte planlı ziyaret · **B** yalnız not — ✅ **KARAR (2026-10-09): A** — eski ziyaret "ertelendi" kapanır, yeni günde yeni planlı ziyaret oluşur, sıklıkta sayılır | W2 |
| K-W2 | Kanıt: fotoğraf zorunlu mu, konum zorunlu mu; ek kanıtlar (imza / QR / süre / anti-fraud) hangileri — ✅ **KARAR (2026-10-09): kiracı ayarı**, varsayılan konum zorunlu + fotoğraf isteğe bağlı (ek kanıt seçenekleri W3 öncesi netleşir) | W3 |
| K-W3 | Fotoğraf + imza saklama yeri (MOD-0262 ikili depo mu, başka) — ⏸ **ERTELENDİ (2026-10-09)**: W3 öncesi yeniden sorulacak; W1 / W2 etkilenmez | W3 |
| K-W4 | Bağlılık modeli: pay % · aşama · birleşik | W4 |
| K-W5 | Hekim tipolojisi: benimseme · iletişim stili · ikisi | W4 |
| K-W6 | Endikasyon × hasta profili ve rakip verisinin sahibi (MDM mi CRM mi) ve kim girer | W4 |
| K-W7 | Hasta sayısı / satış payı görünürlüğü (temsilci / yönetici / pazarlama) | W4 / W7 |
| K-W8 | Advers olay → hangi PV ekranı / kaydı; endikasyon dışı → MSL kim | W5 |
| K-W9 | E-posta alıcıları (bölge müdürü kim — bölge / HR hiyerarşisi) ve gönderim altyapısı | W5 |
| K-W10 | Ön sipariş: hangi modül; ERP satış siparişine dönüşecek mi; red kuralları (iskonto / MF sınırları); fiyat kaynağı | W6 |
| K-W11 | Satış verisi kaynağı (distribütör / eczane / IQVIA / hastane alım) | W7 |
| K-W12 | Eski üç sayfanın akıbeti: çalışma alanına yönlendirme + Planlanan Ziyaretler yalnız kayıt listesi mi | W9 |

## 4. Mimari ön koşul — denetim (AUD-001)
- Bu iş **çok sayıda yeni yazma komutu** getirir: ziyareti başlat / bitir, kanıt yükle, rapor v2, eczane raporu, ön sipariş oluştur / onayla / reddet …
- Mimari test bugün 27 denetimsiz CRM komutuyla kırmızı.
- ✅ **KARAR (kullanıcı, 2026-10-09): Faz 8 EN SONDA.** CRM denetim yayıncısı Faz 8'de düzeltileceği için yeni komutlar (W1–W7) şimdilik mimari testin denetimsiz listesine eklenir; her paket §37'de yeni sayıyı yazar (27 → …), Faz 8 hepsini tek seferde bağlar. Main PR'ları bu kırmızı testle (admin-override) açılabilir (kullanıcı 2026-10-08).
- (Önceki CT önerisi: Faz 8 W3'ten önce — kullanıcı seçmedi.)
- Alternatif: her yeni komut kendi paketinde denetimli yazılır. Faz 8 yine en sonda kalır; bu durumda W3+ paketlerinin her biri denetim işini de taşır.

## 5. Fazlar ve paketler (prompt sayısı)
> **Sayım:** bir "prompt" = bir ajan istemi (bir worktree, bir commit, CT K13). BE ve WEB paralel paketler ayrı sayılır.

| Faz | Kapsam | Paketler | Prompt |
|---|---|---|---|
| **W0** · Tasarım + kararlar | SoR haritası, veri modeli, API sözleşmeleri, K-W1…K-W12; mockup → paket bölme | CT belgesi (DESIGN-VW) | 0 (CT) |
| **(süren)** 4M | Hedefler sayıları (planlı, plandakiler başta, seçili hafta) — Planla modunda aynen kullanılır | 4M-BE ∥ 4M-WEB | 2 (hazır) |
| **W1** · Rapor kuralları (4K, güncellenmiş) — ☑ [WP-VW-W1](WP-VW-W1-visit-report-rules-statuses.md) E2 ACCEPTED 2026-10-09 (`6815fd3bc`; CRM 2506, Web 826, mimari 27) — canlı E4 bekliyor | 48 sa son tarih, iptal kilidi, raporlayan = çağıran, takvim adları + iptal nedeni, T-1, rapor yetkileri, **"kaçırıldı" durumu** + 48 sa yapılamadı / ertele penceresi + yöneticiye bildirim işareti | W1-BE | 1 |
| **(en sonda — kullanıcı kararı)** Faz 8 · AUD-CRM-1 | CRM komutlarını merkezi denetime bağla; mimari test yeşil | F8-a (altyapı + ilk grup) · F8-b (kalan komutlar) | 2 |
| **W2** · Takvim çalışma alanı — ▶ [WP-VW-W2](WP-VW-W2-calendar-workspace.md) paketlendi 2026-10-09 (BE-a ∥ BE-b ∥ WEB-a; WEB-b 2. tur; 3 → 4 prompt) — BE-b ☑ E2 2026-10-09 | **BE:** birleşik takvim okuması (taslak önizleme + planlı + rapor durumu tek akışta), neden kategorileri (referans seti), erteleme (K-W1), plan dışı ziyaret yalnız bugün, **saat sabiti** (sürükle → gün + saat). **WEB-a:** E1 takvim (FullCalendar), hafta başlığı, kartlar, süzgeçler, ayrıntı paneli, E2. **WEB-b:** Planla modu + Hedefler paneli gömülü + sürükle-bırak + ürün uygula | W2-BE · W2-WEB-a · W2-WEB-b | 3 |
| **W3** · Ziyaret yürütme | **BE-a:** ziyaret yürütme kaydı (başlat / bitir, sunucu saati, kanıt: fotoğraf yükleme, konum uzaklığı, kiracı kanıt ayarı, ürün başına süre, işaretler, notlar, ortak ziyaret, imza). **BE-b:** içerik modeli: sunum notları (anahtar mesaj, konuşma metni, slayt süre hedefi, geçiş), itiraz kütüphanesi + onaylı yanıt, doktora özel hatırlatma (Content Studio). **WEB-a:** E3 kanıt + E4b kumanda + E4c bitiş (masaüstü bölünmüş). **WEB-b:** E4a doktor ekranı (tablet, PIN kilidi) | W3-BE-a · W3-BE-b · W3-WEB-a · W3-WEB-b | 4 |
| **W4** · Rapor v2 | **BE-a:** hekim raporu modeli (ürün başına A / B / C, zorunluluk + "yok" seçenekleri, güvenlik soruları, önceki değer okuması, sunumdan ön doldurma). **BE-b:** veri: endikasyon × hasta profili, rakip ürün bilgisi (K-W6 SoR'a göre MDM / CRM). **BE-c:** doktor profili alanları (Contact: tipoloji, KOL, kanal, en uygun gün / saat, toplam hasta + kaynak). **WEB-a:** E5 hekim raporu sihirbazı. **WEB-b:** E6 eczane raporu (+ eczane rapor modeli BE kısmı WEB-b'nin eşi olarak BE-a'ya dahil) | W4-BE-a · W4-BE-b · W4-BE-c · W4-WEB-a · W4-WEB-b | 5 |
| **W5** · Bildirimler | e-posta / bildirim yeteneği (Platform), şablonlar (7 dil), olaylar: rapor özeti, advers olay → PV (24 sa), endikasyon dışı → MSL, rapor süresi doldu (zamanlayıcı), sipariş e-postaları (W6); uygulama içi "gönderilen e-postalar" | W5-BE · (W5-WEB: önizleme listesi, küçük) | 2 |
| **W6** · Eczane ön siparişi | **BE-a:** sipariş modülü (K-W10), satırlar / MF / iskonto / KDV / kur, eczacı imzası, durum makinesi (ön sipariş → onay → onaylandı / reddedildi → depoya iletildi), onay (Workflow ya da e-posta eylem bağlantısı, güvenli tek kullanımlık). **BE-b:** depo e-postası + PDF, eczane kopyası, geçmiş siparişler. **WEB:** ön sipariş ekranı (takvimden ve E4c'den) | W6-BE-a · W6-BE-b · W6-WEB | 3 |
| **W7** · Satış payı + doktor kartı | **BE:** kurum satış verisi içe alma (K-W11), doktor × ürün payı (yazma), potansiyel × pay matrisi, eğilim; doktor kartı okuma modeli. **WEB:** E7 + E8 | W7-BE · W7-WEB | 2 |
| **W8** · Mobil sözleşme notları | her fazın sonunda mobil not (W1, W2, W3, W4, W6) | CT belgeleri | 0 (CT) |
| **W9** · Geçiş + temizlik | eski Ziyaret Planlama / Planlanan Ziyaretler / Ziyaret Yürütme menüleri çalışma alanına yönlenir (K-W12); Planlanan Ziyaretler kayıt listesi sadeleşir (Türkçe, ad, T1–T3); menü / izin manifestosu | W9-WEB (+ küçük BE manifest) | 1–2 |

**Toplam:**
- **Ana iş: W1–W7 + W9 = 21–22 prompt.**
- **Süren 4M: 2.**
- **Önerilen Faz 8 (önce): 2.**
- **Genel toplam ≈ 25–26 prompt.**
- Geçmiş turlardan deneyim: her 4–5 pakette bir **E4 düzeltme paketi** çıkıyor (4I, 4J, 4L, 4M gibi) → **+5–7 düzeltme promptu**.
- **Gerçekçi toplam: 30–33 prompt.**

**Sıra ve paralellik:** sıra **(4M ✅) → W1 → W2 → W3 → W4 → W5 ∥ W6 → W7 → W9 → Faz 8** (kullanıcı 2026-10-09: Faz 8 en sonda).
- Her fazda BE ve WEB paralel.
- W5 ve W6 paralel gidebilir (W6'nın e-postaları W5'e bağlanır).
- W7 bağımsız; veri kaynağı kararı erken gelirse öne alınabilir.

**Kritik yol:** W2 (takvim) → W3 (yürütme) → W4 (rapor). Mobilin rapor yazma işi (A2–A4) W1 + W3 + W4 bitince açılır.

## 6. Bilinçli olarak kapsam dışı
- yönetici ekranı (yalnız e-postadaki onay);
- MSL ve PV ekranlarının kendisi;
- içerik / slayt hazırlama (Content Studio'da);
- mobil uygulamanın kodu (mobil ekip; biz sözleşme notu veriyoruz);
- check-in anti-fraud backend'i (mobilin anti-fraud modülü; biz yalnız alanı taşırız).

## 7. Yan iş — MDM Marka değişikliği (2026-10-09)
- Analiz: [MDM-BRAND-CHANGE-crm-impact-2026-10-09](MDM-BRAND-CHANGE-crm-impact-2026-10-09.md).
- **Zorunlu, kodsuz:**
  - kiracı planına "Marka" modülü (MDM ekibi yapar);
  - özel CRM rollerine `mdm.brands.read` / `mdm.products.read` (kullanıcı verir);
  - main senkronunda MDM dosyaları.
- **WP-MDM-BRAND-1 (1 prompt):** MDM PR'ı main'e girdikten sonra, W4'ten önce. Seçici ve kayıt `isLinkable` kuralını uygular, Ziyaret Sıklığı'na marka / ürün doğrulaması eklenir, "Ürünler" bağımlılık envanteri testle korunur.
- **Plan kuralı:** W4 (rakip / endikasyon) ve W6 (sipariş) yalnız **Global Ürün / GSKU** kullanır, Marka altı "Ürünler" kaydını kullanmaz.
- **Toplam prompt:** +1.

### 7.1 Kullanıcı kararı (2026-10-09) — Marka her yerde, planın EN SONUNDA
- **Karar:** Segment, Ziyaret Sıklığı Politikası, Kampanya, Bilgi içeriği, Ziyaret planlama, Strateji şablonu, ürün adları ve İddialar'ın **hepsi Marka kullanmalı**.
- **Şimdi hiçbir şey değişmez.** MDM birleşik PR'ını push edip main'e aldıktan **ve** bizim işimiz (W1…W9 + Faz 8) bittikten sonra yapılır.
- WP-MDM-BRAND-1 (§7) bu fazın içine alındı; ayrıca ve erken yapılmaz.
- **Faz adı:** **W10 · Marka entegrasyonu** (Faz 8'den sonra, en son).
- **Ön koşullar:**
  - MDM PR'ı main'de;
  - MDM'nin "Marka → Global Ürün" birleştirme kararı. Ziyaret planlama, strateji şablonu, ürün adları ve iddialar Global Ürün kullanıyor; markayı Global Ürün'den türetmek için bu bağ gerekir.
- **Kapsam o gün netleşir:**
  - seçicide ve kayıtta `isLinkable` kuralı;
  - her modülde marka alanı veya markanın Global Ürün'den türetilmesi;
  - "Ürünler" bağımlılıklarının geçişi;
  - eski veri.
- **Tahmini prompt:** 3–5. Kesin sayı MDM kararından sonra.
- **Sıra:** W1 → W2 → W3 → W4 → W5 ∥ W6 → W7 → W9 → Faz 8 → **W10 Marka**.
