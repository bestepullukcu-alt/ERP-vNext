# CT değerlendirmesi — Android ziyaret ve rapor durumu (2026-10-08)

> **Kaynak:** [ANDROID-VISIT-AND-REPORT-STATUS-2026-10-08](ANDROID-VISIT-AND-REPORT-STATUS-2026-10-08.md) + [ANDROID-REPORT-2026-10-08](ANDROID-REPORT-2026-10-08.md). İddialar CRM koduyla (main) doğrulandı.
> **Not:** Android raporu, backend değişikliklerini "main'de değil" diye yazıyor; **2026-10-08'de main'e birleşti** (B1 kodda tamam, test ortamına dağıtım ayrı).

## 1. Mobilde ne var, ne yok
| Var | Yok |
|---|---|
| Planlanan ziyaret listesi (7 günlük sayfalar, süzgeçler) | Ziyaret sonucu girme (yapıldı / yapılamadı / ertelendi) |
| Hafta / ay takvimi (rapor durumu satırda) | Rapor yazma / gönderme / 60 dk içinde değiştirme / düzeltme |
| Elle ziyaret oluşturma / düzenleme / onay / iptal / arşiv | "Ne sunacağım" (`plannedContent` / `contentItems`) takvimde. Ayrıntıda contentItems artık var. |
| Raporu **salt okunur** görme | Haftalık planlama ekranı (ürün kararı: şimdilik yok) |
| Sunucu adları, GUID yok, BiDi | Check-in / check-out (anti-fraud backend'ine bağlı) |
| — | Çevrimdışı çalışma |

**Neden rapor yazma yok:**
- Rapor yetkileri hiçbir role verilmemiş; uçlar geçici yedek yetkiyle korunuyor (F-RBAC).
- `outcomeCode` serbest metin.
- SB-3c rapor akışını değiştirecek.

## 2. Backend'den beklenenler (B1–B11) — doğrulanmış durum ve öneri
| # | Beklenti | Kodda durum | Öneri / yer |
|---|---|---|---|
| B1 | WP-VP-2 + E2E-FIX-1 main'de | ☑ **main'de** (2026-10-08) | Test ortamına dağıtım: kullanıcı / ops |
| B2 | Mobil sözleşme notu | ☑ iletildi + [yanıt](MOBILE-ANSWERS-2026-10-08-android-report.md) | — |
| B3 | **F-RBAC**: saha temsilcisi rolü; `crm.visit-report.*` rollere; `crm.territory.*` yedeğinin kaldırılması | ✔ doğrulandı: `VisitReportPermissions.ReadFallback = crm.territory.read`, `ManageFallback = crm.territory.model.manage`; uçlar yalnız yedeği istiyor | **Karar:** rol adı + yetki seti (kullanıcı rol yetkilerinden verir). Sonra küçük backend paketi: uçlar `crm.visit-report.read/record/amend` istesin. |
| B4 | Taslak → planlı ucu | ✔ yok | **Faz 6** (Planlanan Ziyaretler sayfası; Web formu da taslak açıyorsa aynı karar) |
| B5 | Sonuç / sebep kodu referans seti (`outcomeCode` serbest) | ✔ backlog E9-B4b | Referans verisi (veri sahipleri) + SB-3c'de bağlanır |
| B6 | **T-1**: "işyerindeki doktor" hedefinde ad boş | ✔ **hata doğrulandı**: `VisitTargetNameReader.For` bağlantı (`account-contact-link`) hedefini kurumlarda arıyor (bağlantı kimliğiyle) → boş | **4K** (küçük backend) |
| B7 | `my-accounts` + saha temsilcisine `crm.visit-plan.read` | uçlar main'de | B3 rol kararıyla birlikte |
| B8 | İş kuralları: "bugün" saat dilimi, rapor son tarihi, iptal edilmiş ziyarete rapor, raporlayanın doğrulanması | ✔ hepsi doğrulandı (aşağıda) | **Kararlar** (kullanıcı) → 4K / SB-3c |
| B9 | SB-3c | ertelendi (Faz 6 sonrası) | yol haritasında |
| B10 | Check-in / anti-fraud | yok | ayrı modül; kapsam dışı |
| B11 | Ziyaret tipi / amaç / durum etiketleri `{code, label}` | ✔ yok (ham kod) | **Faz 6** (Web Planlanan Ziyaretler de aynı etiketleri ister) |

## 3. CT'nin ek bulguları (rapordan + koddan)
1. **Takvimde ürün adı yok.** `VisitCalendarPlannedContentDto`'da `ProductName` yok; 4G yalnız planlanan ziyaret DTO'larına ekledi. → **4K**
2. **Takvimde iptal nedeni yok.** `VisitCalendarItemDto`'da `CancellationReason` yok. → **4K**
3. **İptal edilmiş planlanan ziyaret güncellenebiliyor.** `UpdatePlannedVisitHandler` yalnız `IsArchived` kontrol ediyor. → karar → 4K
4. **İptal edilmiş ziyarete rapor girilebiliyor.** Rapor işleyicileri planın durumuna bakmıyor. → karar → 4K
5. **⚠ Raporlayan kişi istekten alınıyor (güvenlik).**
   - `RecordVisitOutcome` / `SubmitVisitReport` / `Amend` içinde `ReportedByResourceId = request.ReportedByResourceId ?? …`.
   - Temsilci, kendi ziyaretine başkasının adıyla rapor yazabilir. Sahiplik yalnız planın kaynağıyla kontrol ediliyor.
   - Öneri: raporlayan **her zaman oturumdaki kişi**; yalnız read-all sahibi başkası adına (B-1 kuralının aynısı). Karar gerektirmez, güvenlik düzeltmesi. → **4K**
6. **"Bugün" UTC.** Türkiye'de 00:00–03:00 arası bir önceki gün sayılıyor; hem geçmiş tarih hem `visit_not_yet_due` etkileniyor. CRM'de kiracı saat dilimi yok. → karar
7. **Rapor için son tarih yok.** Geçmişteki her ziyaret aylar sonra raporlanabilir. → karar
8. **Takvim hata kodu adı yanıltıcı.** Eksik tarih → `visit_report_reschedule_date_invalid`. → 4K (ek kod `visit_report_calendar_range_invalid`, eski kod korunur ya da değiştirilir; mobil bilgilendirilir)

## 4. Önerilen sıra
- **WP-VP-4K (backend, küçük, mobil engelini kaldırır; 4J ile paralel):**
  - takvim `productName` + `cancellationReason`;
  - T-1 bağlantı hedefinin adı;
  - raporlayan = oturumdaki kişi;
  - takvim hata kodu;
  - kullanıcı kararlarına göre iptal edilmiş ziyaret kuralları.
- **F-RBAC paketi:** saha temsilcisi rol kararından sonra. Uçlar `crm.visit-report.*` istesin; yedek kalksın. Rolü kullanıcı rol yetkilerinden verir.
- **Faz 6:** B4 taslak → planlı, B11 etiketler, T1–T3, Planlanan Ziyaretler sayfası.
- **SB-3c:** başlat / tamamla, sunulan içerik, yolculuk ilerlemesi, sonuç kodu seti (B5) bağlama.
