# Tasarım farkları — Ziyaret Çalışma Alanı (W2-WEB-a) ↔ mockup (CT, 2026-10-09)

> **Nasıl karşılaştırıldı:**
> - mockup `Ziyaret Calisma Alani (standalone).html` yerel sunucuda açıldı (launch `mockups-workspace`, port 5098). Ekranlar: E1 Takvim — Yürüt, E1 Ayrıntı — Kaçırıldı, E2;
> - canlı sayfa `/CRM/VisitWorkspace` ayrı sekmede, yalnız okuma;
> - görünen metin + yapı + ekran görüntüsü.
>
> **Not:** İlk canlı kontrolüm işlevseldi; bu görsel kıyaslamayı kullanıcı sorunca yaptım.

## E1 — Takvim (Yürüt)
| # | Mockup | Bizim sayfa | Ne yapılmalı | Veri |
|---|---|---|---|---|
| T1 | Başlıkta dönem çipi "Türkiye 2026 Q4" | yok | dönem adı (takvim yanıtındaki oturumun dönemi) | BE: dönem adı |
| T2 | Planla / Yürüt bölmeli düğme; "+ Plan dışı ziyaret" dolu, büyük birincil düğme; "Ön sipariş" | mod yok (WEB-b); plan dışı ince çerçeveli ve küçük; ön sipariş yok (W6, bilinçli) | Planla / Yürüt WEB-b'de; plan dışı düğmesi mockup stilinde | — |
| T3 | **Hafta şeridi YOK**; tek takvim kartı: ‹ › Bugün + "5 Eki – 9 Eki 2026", sağda kurum / durum, altında ürün + Gün / Hafta / Ay | 5 haftalık çip şeridi (mockup'ta yok), ayrı hafta kartı, süzgeçler takvim dışında, FullCalendar araç çubuğu; "Bugün" iki kez | şerit kaldırılır; tek kart başlığı mockup düzeninde; FullCalendar araç çubuğu gizlenir (kendi başlığımız) | — |
| T4 | Hafta başlığı takvim kartının içinde: "41. **H**afta · 5–9 Eki · Onaylı · Bu hafta 38,3 sa kapasite · 13,9 sa planlı" + ince çubuk + 🔒 "Haftayı yeniden aç" | ayrı kart; "41. hafta" (küçük h); "12,3 sa / 38,3 sa planlandı"; kilit simgesi yok | mockup metni ve yeri | — |
| T5 | Gün başlığı: ad + **"Bugün" rozeti** + **gün doluluk çubuğu** + "4 ziyaret · boş 4,9 sa" | metin var, rozet ve çubuk yok | rozet + mini çubuk | — |
| T6 | "Tüm gün" satırı yok; saat etiketleri :30'da; saat başına uzun satır (≈ 1 sa = 115 px); bugün sütunu açık mavi, şimdi çizgisi kırmızı | "Tüm Gün" satırı var; 15 dk'lık sık ızgara; kartlar sıkışık | `allDaySlot:false`; dilim yüksekliği mockup'a göre; bugün sütunu rengi | — |
| T7 | **Kart:** durum rengine göre pastel zemin + sol kenar; sağ üstte durum simgesi (✓ raporlandı, 🔒 kilitli, 📌 sabit, 📅 planlı, ▶ başlamaya hazır); saat, **doktor adı (kalın)**, **kurum (soluk)**, ürün çipleri (**tanıtım dolu**, **hatırlatma çerçeveli**); alt satırda durum metni ("→ 13 Eki", "İşaretlemek için 3 sa", "Rapor için 31 sa kaldı", "Değiştir (54 dk)") | kartta "Kaçırıldı" yazısı, kurum yok, simgeler farklı; kısa ziyaretlerde metin üst üste | mockup kart düzeni; durum metin yerine renk + simge + alt satır | BE-c: `accountDisplayName` (yolda) |
| T8 | Durum süzgeci tek seçim "Tüm durumlar" | çoklu "Durum" açılır listesi | mockup gibi tek seçim (ya da çoklu kalabilir — **ürün sahibine sor**) | — |
| T9 | Gün / Hafta / **Ay** görünümü | Hafta / Gün | Ay görünümü (ayda kart yerine gün başına sayı / durum noktaları) | — |
| T10 | Altta **açıklama satırı**: Taslak, Planlı, Bugün · başlamaya hazır, Devam ediyor, Rapor eksik, Raporlandı, Kaçırıldı, İptal, sabit, plan dışı, ÜRÜN tanıtım, ÜRÜN hatırlatma | yok | açıklama satırı | — |

## E1 — Ayrıntı paneli
| # | Mockup | Bizim | Ne yapılmalı | Veri |
|---|---|---|---|---|
| D1 | Üstte durum çipi, büyük doktor adı, rozetler (**uzmanlık** "Dahiliye", **potansiyel / segment** "Yüksek potansiyel") | ad + tarih; rozet yok | rozetler | BE: uzmanlık, segment / potansiyel rozeti (3D `badges` var) |
| D2 | **Kurum bloğu:** harita önizleme kutusu, kurum adı, adres, "6 Eki Salı · 14:00–14:25 · 25 dk" | yok | kurum bloğu (harita yer tutucu) | BE: kurum adı, adres, süre |
| D3 | Durum uyarı kutusu ("Ziyaret başlatılmadı. 'Yapılamadı' ya da 'Ertele' için 3 sa kaldı.") | benzer | metin mockup'a | — |
| D4 | **NE SUNACAĞIM:** numaralı ürün kartları (ürün tam adı + rol çipi + "Klinik kanıt · 2. mesaj · 6 içerik adımı · ≈ 10 dk"); sağ üstte "Sıklık: dönemde 3 · 1/2"; altta "1 tanıtım + 1 hatırlatma + rapor ≈ 22 dk" | ürün adı + rol çipi listesi; sıklık ayrı bölüm | kartlı düzen; adım sayısı (`plannedContent[].steps`) ve süre tahmini | süre tahmini kuralı: adım başına dk (**CT önerisi**: içerik adımı sayısı × sabit, ürün sahibine sor) |
| D5 | **ÖNCEKİ ZİYARETTEN:** ürün başına ilgi / bağlılık çipleri, açık talep, "Son: 1 Eki" | son rapor (çoğu zaman gizli) | ürün başına bloğu W4 verisiyle doldur; şimdilik son ziyaret tarihi + sonuç | W4 (ilgi / bağlılık / talep) |
| D6 | Eylemler panelin altında, tam genişlik | var (Yapılamadı / Ertele) | uyumlu | — |

## E2 — İptal / Yapılamadı / Ertele
| # | Mockup | Bizim | Ne yapılmalı |
|---|---|---|---|
| P1 | **Tek pencere, üç sekme** (İptal et · Yapılamadı · Ertele); alt başlık "Dr. Kerem Aslan · 8 Eki Perşembe 11:30"; sekmeye göre kısa açıklama ("Başka güne kaydırın; yeni tarih seçin.") | üç ayrı pencere, sekme yok; alt başlıkta yalnız ad | tek pencere + sekmeler; alt başlıkta tarih / saat |
| P2 | Yeni tarih: **4 sütunlu gün kartı ızgarası**; kartta gün adı, mini çubuk, "4 ziyaret · boş 4,9 sa" | dikey liste; "29 ziyaret · 7,1 sa / 7,7 sa" | ızgara + "boş X sa" metni |
| P3 | (Mockup'taki "Ürün sahibi kararı · ertelemenin sonucu" kutusu tasarım notudur; uygulanmaz, karar A uygulandı) | — | — |

## Önerilen paketleme
- **W2-WEB-c — tasarım uyumu (Web):** T2 (plan dışı düğme), T3–T7, T8 (karara göre), T9, T10, D1–D4, D6, P1–P2.
  - W2-WEB-b kabulünden SONRA. Aynı dosyalarla çakışmaması için WEB-b'ye eklenmez.
- **W2-BE-d — panel verisi (CRM, küçük):** dönem adı (T1), uzmanlık + segment / potansiyel rozeti (D1), kurum adresi (D2), ürün tam adı (D4).
  - W2-WEB-c ile paralel.
- **W4'e bırakılan:** D5 (ilgi / bağlılık / açık talep).
- **Prompt etkisi:** +2 (W2 toplamı 5 → 7).
- **Ürün sahibine sorular:**
  - T8: durum süzgeci tek seçim mi, çoklu mu?
  - D4: süre tahmini kuralı (içerik adımı başına kaç dakika)?

## Kullanıcı kararları
- **D4 süre tahmini (2026-10-09):** "hangisi varsa o".
  - Ürün başına: içerik adımlarının süre hedefi tanımlıysa (adım × dakika; adım süre hedefi W3-BE-b ile gelir) toplamı gösterilir.
  - Toplamda ve adım süresi yoksa: planlanan ziyaret süresi (`durationMinutes`).
  - İkisi de yoksa süre gösterilmez.
- **T8 (2026-10-10): ÇOKLU seçim kalır** (mockup tek seçim; kullanıcı çoklu istedi; görünüm mockup stilinde: "Tüm durumlar" etiketli çoklu seçim).
- **Zamanlama (2026-10-10): CT önerisi kabul** — W2-WEB-b kabulünden sonra W2-WEB-c (tasarım uyumu) ∥ W2-BE-d (panel verisi).
