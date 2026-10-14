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
| Dosya / fotoğraf saklama | MOD-0262-FU01 ikili depo **main'de** (2026-10-12 CT kontrolü; SCMM-16A/B tüketiyor) | **karar**: fotoğraf + imza nerede saklanacak (W3) |
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
| K-W3 | Fotoğraf + imza saklama yeri (MOD-0262 ikili depo mu, başka) — ✅ **KARAR (2026-10-12): MOD-0262 ikili depo** (hazır değilse yalnız konum + zaman damgası). CT kontrolü: MOD-0262-FU01 main'de, SCMM-16A canlı E4 8/8, CRM `HttpContentArtifactStore` deseni var → **(a) geçerli**. Yeni kapsam `CrmVisitEvidence`. **İzin kararı (kullanıcı 2026-10-12):** Platform'a yalnız ziyaret kanıtı kapsamıyla sınırlı izin eklenir (kiracı geneli `platform.document-repository.read/manage` temsilciye VERİLMEZ); temsilci kanıtı yalnız kendi ziyareti üzerinden, CRM aracılığıyla görür → W3'te ayrı Platform parçası | W3 |
| K-W4 | Bağlılık modeli — ✅ **KARAR (2026-10-12):** raporda ürün başına (1) **kullanım aşaması** Farkında değil / Farkında / Değerlendiriyor / Deniyor / Düzenli kullanıyor / Savunucu — ürünün içerik yolculuğu aşamasıyla AYNI liste, bir sonraki ziyaretin içeriğini seçer (JourneyProgress ilerletme = SB-3c kapanır); (2) **hasta potansiyeli SAYI (tahmini)**: doktorun aylık toplam hasta sayısı (doktor düzeyi) + endikasyon başına aylık hasta sayısı, endikasyonların toplamı toplamı geçemez; **3 ayda bir** sorulur, arada son değer gösterilir; Az / Orta / Çok eşikleri ürün (endikasyon) başına, varsayılan <10 / 10–30 / >30, pazarlama değiştirir; (3) **bizim payımız** seçmeli: Hiç / Ara sıra / Yarı yarıya / Çoğunlukla / Hep; (4) **ziyaret başarısı** Ulaşıldı / Kısmen / Ulaşılamadı (hedef ekranda: anahtar mesaj ya da sonraki aşama) + **doktor taahhüdü** Deneyecek / Hastasına yazacak / Daha fazla bilgi istiyor / Taahhüt yok (sonraki ziyaret planı buna göre, örn. "nasıl gitti"); **fırsat tablosu**: potansiyel × pay → Fırsat / Büyüt / Koru / Düşük öncelik (Çok × Hiç-Ara sıra = Fırsat; Çok × Yarı = Büyüt; Orta × Hiç-Yarı = Büyüt; Az × Hiç-Yarı = Düşük öncelik; × Çoğunlukla-Hep = Koru); fırsat doktorları planlamada öne çıkar. Seçenek listeleri referans verisinden | W4 |
| K-W5 | Hekim tipolojisi: benimseme · iletişim stili · ikisi — ✅ **KARAR (2026-10-12): ikisi + KOL düzeyi**; doktor profilinde (ürüne değil), seçenekler referans verisinden (kiracı uyarlar), temsilci raporda önerir, değişiklik geçmişi tutulur | W4 |
| K-W6 | Endikasyon × hasta profili ve rakip verisinin sahibi — **kısmi KARAR (2026-10-12):** endikasyon / hasta profili / fayda = **KnowledgeConcepts** (CRM kavram grafiği; türler CT-002 Endikasyon, CT-003 Hasta profili, CT-005 Fayda… ürün konusuna bağlı). Rakip ürün: blueprint'te modül YOK; ürün ana verisi MOD-0290 (MDM) → ✅ **KARAR (b): MOD-0290 içinde ayrı "Rakip ürün" kaydı** (ad, firma, etken madde, form, ülke bazında fiyat + geri ödeme; "hangi ürünümüzün rakibi" + endikasyon ilişkisi; MDM ekibinin ürün / marka değişikliğiyle çakışmaz), **bizim ekibimiz ekler**; veriyi pazarlama / tıbbi girer, temsilci seçer. ✅ **Ek (2026-10-13):** temsilci "listede yok" der → **önerilen rakip** (öneren hemen kullanır) → pazarlama / tıbbi kararı hazırlar (tamamla / birleştir / reddet) → **MDM içinde Workflow onayı, Satış Operasyon (POS-SALES-OPS)**; doğrudan kayıt / fiyat güncellemesi yalnız yetkiyle (onaysız); birleştirilen kayıt silinmez, "birleştirildi → hayatta kalan" işaretçisi; etken madde serbest etiket, geri ödeme durumu BRD seti; rakip yönetim ekranı **mockup** (BRIEF-W4-competitor-products.md), potansiyel eşik ekranı standart | W4 |
| K-W7 | Hasta sayısı / satış payı görünürlüğü — ✅ **KARAR (2026-10-12):** temsilci kendi doktorları; yönetici ekibinin doktorları; pazarlama toplulaştırılmış, doktor kimliği olmadan | W4 / W7 |
| K-W8 | Advers olay → hangi PV ekranı / kaydı; endikasyon dışı → MSL kim | W5 |
| K-W9 | E-posta alıcıları (bölge müdürü kim — bölge / HR hiyerarşisi) ve gönderim altyapısı | W5 |
| K-W10 | Ön sipariş: hangi modül; ERP satış siparişine dönüşecek mi; red kuralları (iskonto / MF sınırları); fiyat kaynağı | W6 |
| K-W11 | Satış verisi kaynağı (distribütör / eczane / IQVIA / hastane alım) | W7 |
| K-W12 | Eski üç sayfanın akıbeti: çalışma alanına yönlendirme + Planlanan Ziyaretler yalnız kayıt listesi mi | W9 |
| K-W13 | **Ticari müşteri (ürün satın alan şirketler)** — ✅ **KARAR (kullanıcı 2026-10-14):** müşteri şirket = mevcut CRM kurumu (blueprint: Customer (Account) SoR = MOD-0149; ayrı "müşteri" kaydı YOK); ticari profil **satan şirketimize (tüzel kişilik) göre** ayrı; **finans (kredi limiti, vade, alacak, tahsilat) bizim sistemimizde** (dış ERP yok → yeni Finans alanı: MOD-0118 GL, MOD-0120 AR, MOD-0169 faturalama, MOD-0291 boyutlar); zamanlama **en sonda**; mockup brief [BRIEF-commercial-customer-profile](mockups/commercial-customers/BRIEF-commercial-customer-profile.md). Modül haritası + temas noktaları notu: [NOTE-commercial-customers-module-map](NOTE-commercial-customers-module-map.md) (plan dışı, yalnız not). **W6 notu:** o zamana kadar ön sipariş depoyu kurum (Account, tür distribütör) kimliğiyle ve eczane↔depo kurum ilişkisinden seçer — ileride ticari profile bağlanır, göç gerekmez | Program T |

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
| **W2** · Takvim çalışma alanı — ▶ [WP-VW-W2](WP-VW-W2-calendar-workspace.md) paketlendi 2026-10-09 (BE-a ∥ BE-b ∥ WEB-a; WEB-b + BE-c (tatil adı, gün dengesi) 2. tur; 4 → 5 prompt) — BE-b ☑ + BE-a ☑ + WEB-a ☑ E2 2026-10-09 (CRM 2543, Web 854) | **BE:** birleşik takvim okuması (taslak önizleme + planlı + rapor durumu tek akışta), neden kategorileri (referans seti), erteleme (K-W1), plan dışı ziyaret yalnız bugün, **saat sabiti** (sürükle → gün + saat). **WEB-a:** E1 takvim (FullCalendar), hafta başlığı, kartlar, süzgeçler, ayrıntı paneli, E2. **WEB-b:** Planla modu + Hedefler paneli gömülü + sürükle-bırak + ürün uygula | W2-BE · W2-WEB-a · W2-WEB-b | 3 |
| **W3** · Ziyaret yürütme — ▶ [WP-VW-W3](WP-VW-W3-visit-execution.md) paketlendi 2026-10-12 (1. tur PLT ∥ BE-a ∥ BE-b — ☑ E2 2026-10-12, CRM 2608, mimari 36, CT düzeltmesi bitmiş → report_missing; + W3-AUTH izin kapsamı ☑ (Auth 1108); 2. tur WEB-a ∥ WEB-b ☑ E2 2026-10-12 (Web 894); kalan: grant + canlı E4, W3-BE-c/WEB-c/WEB-d mockup sonrası, mobil not; W3-WEB-c yazım ekranları + W3-WEB-d kanıt ayarları ekranı (+ W3-BE-c katmanlı ayar kiracı < tüzel kişilik < iş birimi + temsilci süreli istisna, kullanıcı 2026-10-12) mockup GELDİ → ▶ [WP-VW-W3-authoring](WP-VW-W3-authoring.md) paketlendi 2026-10-12: BE-c → WEB-c ∥ WEB-d → CFG (onaylar Workflow + Görev Merkezi; tüzel kişilik / iş birimi katmanları sonra, PLT-ORG-SEAT) (önceki not: kullanıcı 2026-10-12; brief [BRIEF-W3-authoring-and-evidence-settings](mockups/visit-workspace/BRIEF-W3-authoring-and-evidence-settings.md)); 4 → 7 prompt) | **BE-a:** ziyaret yürütme kaydı (başlat / bitir, sunucu saati, kanıt: fotoğraf yükleme, konum uzaklığı, kiracı kanıt ayarı, ürün başına süre, işaretler, notlar, ortak ziyaret, imza). **BE-b:** içerik modeli: sunum notları (anahtar mesaj, konuşma metni, slayt süre hedefi, geçiş), itiraz kütüphanesi + onaylı yanıt, doktora özel hatırlatma (Content Studio). **WEB-a:** E3 kanıt + E4b kumanda + E4c bitiş (masaüstü bölünmüş). **WEB-b:** E4a doktor ekranı (tablet, PIN kilidi) | W3-BE-a · W3-BE-b · W3-WEB-a · W3-WEB-b | 4 |
| **FU-W3-OBJ-MLR** · İtiraz yanıtı MLR onayı (TAKİP — kullanıcı 2026-10-12: "şimdi basit onay, ileride değiştiririz") | W3'te itiraz yanıtı basit onaylı (SoD). İleride yanıt metni Claims'in MLR inceleme akışına bağlanır; mevcut onaylı kayıtlar için geçiş kuralı o zaman belirlenir. Zamanı kullanıcı seçer | FU (BE + küçük WEB) | 1–2 |
| **W3E** · Ek kanıtlar — ✅ **KARAR (kullanıcı 2026-10-12): W4'ten sonra** | **QR:** kurum başına imzalı QR kodu üretimi + yazdırma (kurum kaydından), tarama (tablet / telefon kamerası), sunucuda doğrulama (imza + kiracı + kurum + geçerlilik), kiracı ayarına "Kurum QR kodu". **Anti-fraud:** web tarayıcısı cihaz bütünlüğünü ölçemez → **mobil uygulama işi** (Play Integrity / App Attest + sahte konum tespiti); backend yalnız mobilin gönderdiği doğrulama sonucunu alır, doğrular, işaretler; kiracı ayarına "Anti-fraud check-in". Mobil not ile birlikte | W3E-BE-a ∥ BE-b · W3E-WEB-a ∥ WEB-b (+ mobil not) — paket [WP-VW-W3E-qr-antifraud](WP-VW-W3E-qr-antifraud.md), brief [BRIEF-W3E](mockups/visit-workspace/BRIEF-W3E-qr-antifraud.md) (2026-10-14) | 4 |
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

**Sıra ve paralellik:** sıra **(4M ✅) → W1 → W2 → W3 → W4 → W3E → W5 ∥ W6 → W7 → W9 → Faz 8** (kullanıcı 2026-10-09: Faz 8 en sonda).
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
- **Sıra:** W1 → W2 → W3 → W4 → W3E → W5 ∥ W6 → W7 → W9 → Faz 8 → **W10 Marka**.

### W2 durum notu (2026-10-10)
- **1. tur:** BE-a, BE-b ve WEB-a E2 + canlı E4 ☑.
- **2. tur:** BE-c ∥ WEB-b ajanlarda ([WP-VW-W2-round2](WP-VW-W2-round2.md)).
- **3. tur (kullanıcı onayı):** WEB-b kabulünden sonra W2-WEB-c tasarım uyumu ∥ W2-BE-d panel verisi.
  - Farklar: [DESIGN-GAP-W2](mockups/visit-workspace/DESIGN-GAP-W2-2026-10-09.md).
  - Durum süzgeci ÇOKLU seçim.
- **W2 toplam:** 7 prompt.
- **W2 TAMAM (2026-10-12):** canlı E4 + mockup görsel karşılaştırma uyumlu; 3 küçük fark CT tarafından kapatıldı (kısa kart adı, "işaretlendi" metni, iptaller varsayılan gizli; Web 867/0). Sıradaki **W3**, önce K-W3 sorusu.

## 8. Kalan iş planı — güncel (2026-10-12, CT)

> **Bitenler:**
> - W1 ✅
> - W2 ✅ (7 prompt + CT kapanış)
> - W3 yürütme ✅: PLT, BE-a, BE-b, AUTH, WEB-a, WEB-b; canlı E4 iki oturum; mobil not
> - W3-BE-c ✅ (E2; `0927fab9e` → test dalı)
>
> **Gerçekleşen tempo:** W2 7 + 1 düzeltme; W3 şu ana kadar 7 prompt + 3 CT düzeltmesi. Tahminlerde bu oran kullanıldı.

| sıra | iş | prompt | durum / not |
|---|---|---|---|
| 1 | **W3-WEB-c** itiraz kütüphanesi + adım "Sunum notları" sekmesi | 1 | ✅ E2 (90edf5341) |
| 1 | **W3-WEB-d** ziyaret kanıtı ayarları (kiracı + etkin ayar + geçmiş + süreli istisna) | 1 | ✅ E2 (2d16a835c) |
| 2 | **W3-CFG** Workflow şablonları (CRM-OBJ-RESP → MED-DIR, CRM-VISIT-EVIDENCE-EXC → POS-SALES-OPS) + yetki betiği | 1 | ✅ E2 (b40a3702f); kullanıcı --apply + yeniden giriş |
| 3 | W3 yazım canlı E4 + mockup görsel karşılaştırması + olası düzeltme | 0–1 | ✅ 2026-10-13 (2 CT düzeltmesi f8293c2f5, 9f37e0875; notlar 456608389) |
| 3b | **W3-FIX** canlı E4 küçük düzeltmeleri (F1–F5) | 1 | ✅ E2 (cbdf745f2 → test) |
| 4 | **PLT-ORG-SEAT** Platform "benim pozisyon atamam" ucu (+ bildirim) | 1 | tüzel kişilik katmanı için |
| 5 | **W3-BE-d** katmanlı ayar (tüzel kişilik × iş birimi), çözümleyici dolar | 1 | PLT-ORG-SEAT sonrası |
| 5 | **W3-WEB-e** kanıt ayarında tüzel kişilik / iş birimi ağacı + miras | 1 | BE-d ile |
| — | Veri: temsilcilere Platform pozisyon ataması (İK) + bölge atamasında iş birimi (CRM yöneticisi) | 0 | kullanıcı / veri sahibi |
| 6 | **W4** rapor v2: BE-a rapor modeli (CRM) · BE-b1 MDM Workflow bağlantısı · BE-b2 MDM rakip ürün (öneri / birleştir / ülke fiyat) · BE-c doktor profili + potansiyel + fırsat · WEB-a hekim raporu · WEB-b eczane raporu + eşik ekranı · WEB-c rakip yönetimi (**mockup sonrası**) · CFG (şablon MDM-COMP-PROPOSAL → POS-SALES-OPS + yetki) | 8 | ✅ **8/8 kabul (2026-10-14)**, main senkron PR #141 (WEB-c + CFG test dalında 43e30f6e8); canlı go-live 2026-10-13 (BRD, 3 şablon, 16 ülke pozisyonu, Admin + CrmUser yetkileri; MDM RabbitMQ ertelendi); **sıradaki: W4 canlı E4** |
| 7 | **W3E** ek kanıtlar (QR + anti-fraud alıcı uç + yönetici işaretleri) | 4 | ▶ şimdi: BE-a ∥ BE-b hazır; WEB mockup sonrası |
| 7b | **SYNC-144 · PR #144 (Kapanış hattı) main'e girince CRM tarafı düzeltmeleri** — kullanıcı 2026-10-14. **Süre sınırı: istisnalar 2026-11-13'te biter** (BL-640/641/642, 30 gün). (1) CT: main → test senkronu + takımlar. (2) **W-UXH (Web, 1 istem):** UXH-001 "?" yardım metni (Purpose / Audience / Step1..n + Title, 7 dil, `PageHelpResource` + `PageHelpCatalog`) — BL-642'nin 5 sayfası (COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE) + W3E'nin 2 sayfası (CRMVISITQRCODES, CRMVISITEVIDENCEFLAGS); Enter / chord işareti (`data-dt-primary`) — BL-641'in 3 paneli (KnowledgePaths `_PresentationNotes`, PotentialThresholds, VisitEvidenceSettings) + W3E pencereleri; defter / test istisna satırları çıkar. (3) **BL-640 (MDM, 1 istem):** `CompetitorProduct`, `CompetitorProposalDecision`, `ProcessedEvent` → `MdmDocumentClassMaps`; bütün belge yazısı alan düzeyine ya da katı eşleme; `MdmDocumentClassMapsTests` istisnaları çıkar. (4) BL-645 Kapanış hattında (mesaj iletildi); gelince Pazar listesi yeniden ölçülür. **Kalıcı kural:** bundan sonraki her Web istemi (W5, W6, W7, W9…) yeni sayfayı yardım metniyle ve Enter kuralıyla getirir. **Marka:** PR #144 MDM marka modülünü getiriyor → W10'un "MDM PR main'de" ön şartı karşılanır; ama Marka → Global Ürün bağı MDM'de henüz yok (BL-562, sonraki GP turu). W10 yine en sonda | 2 + CT | PR #144 main'e girdikten hemen sonra |
| 8 | **W5** bildirimler (Platform e-posta + olaylar) | 2 | K-W8, K-W9 |
| 8 | **W6** eczane ön siparişi | 3 | K-W10; W5 ile paralel |
| 9 | **W7** satış payı + doktor kartı | 2 | K-W7, K-W11 |
| 10 | **W9** eski sayfaların geçişi + temizlik | 1–2 | K-W12 |
| 11 | **Faz 8** CRM denetimi (AUD-001; denetimsiz sayı şu an **47**) | 2 | en sonda (karar) |
| 12 | **W10** MDM marka entegrasyonu — tüm CRM sayfaları markayı MDM'den okur (Segment, ziyaret sıklığı, kampanya, bilgi içeriği, ziyaret planlama, StrategyTemplate, ürün adları, Claims; kullanıcı 2026-10-09). Ön şartlar: (a) MDM marka modülü main'de → **PR #144 ile gelir**; (b) Marka → Global Ürün bağı (MDM BL-562) — henüz yok; (c) CRM rollerine `mdm.brands.read` / `mdm.products.read` | 2–3 | en sonda; (b) MDM'de bitince |
| — | Takipler: FU-W3-OBJ-MLR (itiraz MLR), FU-W3-SLIDE-ASSET (slayt görseli), FU-W3-COVISITOR-LOOKUP (ortak ziyaretçi araması), W2 mobil notu (CT), **FU-KP-NEWVERSION-BUTTON** (KP Studio yayınlanmış yoldan yeni taslak sürüm düğmesi; KP-UI-1 yeniden yazımında düştü — kullanıcı: en sonda) | 3–4 | zamanı kullanıcı seçer |
| 13 | **FIX-CRM-SCAFFOLD-MONGO** (Kapanış CT isteği 2026-10-14, onların istisnası BL-643): `ScaffoldSmokeTests` DI'yi kurarken `localhost:27017` / `DitenERP_Test`'e bağlanıp indeks yazıyor (DB-010 / BL-595 ihlali). Çözüm: `AddPersistence`'a varsayılanı açık `Mongo:EnsureIndexesOnStartup` ayarı; test kapatır (MongoClient tembel → hiç bağlanmaz); üretim davranışı değişmez. Kanıt: CRM test projesinde `localhost:27017` araması 0 + sabotaj (ayar yok sayılırsa bağlantı / indeks çağrısı testte kırmızı). Main'den ayrı dal `fix/crm-scaffold-smoke-no-mongo`; commit kimliği Kapanış CT'ye iletilir | 0–1 (CT küçük iş) | **en sonda** (kullanıcı 2026-10-14) |
| 14 | **Program T · Ticari müşteri + finans** (K-W13): **T1** CRM ticari profil (kurum × satan şirket; roller sipariş veren / teslim alan / fatura alan / ödeyen; para birimi, vade, fiyat listesi ref, iskonto grubu, depo, vergi no; sipariş bloğu; geçmiş) + "Ticari müşteriler" görünümü — 2–3 istem, **mockup sonrası** · **T2** fiyat listeleri + iskonto sınırları (MOD-0156) — 2–3 · **T3** sipariş (MOD-0168; W6 ön siparişinin devamı) — 3 · **T4** Finans alanı kuruluşu: EA kararı (yeni domain + `Diten.FinanceService`), MOD-0291 boyutlar, MOD-0118 GL asgari, MOD-0169 faturalama, MOD-0120 alacak / kredi limiti (onaylı) / tahsilat, TR e-fatura + BAE KDV — **ayrı büyük program, ≈ 15–25 istem** | ≈ 22–34 | **en sonda** (kullanıcı 2026-10-14); T1 mockup bekler |
| 15 | **Canlıya çıkış kontrol listesi** — K-INFRA-1/2/3, RabbitMQ, QR imza anahtarı, W3E yetkileri, PR #144 maddeleri (admin@diten.com yalnız /platform/login; canlıda silinmez / e-postası değişmez; Security:TrustedProxies BL-626; tek kişilik onay pozisyonu) → [NOTE-golive-checklist](NOTE-golive-checklist.md) | 0 (kullanıcı inceler) | **en sonda**, canlıya alırken (kullanıcı 2026-10-14) |

**Toplam kalan:**
- W3'ün kalanı: 4–5 prompt.
- Katmanlar: 3.
- W4–W10 + Faz 8: 21–23.
- Takipler: 3–4.
- **≈ 31–35 prompt.**
- Deneyime göre her 4–5 pakette bir düzeltme turu → **gerçekçi ≈ 36–40 prompt.**

**Kritik yol:** W3-WEB-c / d → CFG → canlı E4 → **W4** (rapor). Mobilin rapor yazma işi W4 bitince açılır.
