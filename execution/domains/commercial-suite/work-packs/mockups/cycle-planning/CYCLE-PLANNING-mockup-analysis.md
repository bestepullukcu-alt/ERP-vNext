# Dönemler + Dönem Kapasitesi — mockup analizi

> **CT, 2026-10-05.**
> - **Kaynak:** `cycle-planning-prototype.html`; çözülmüş şablon + mantık: `cycle-planning-screens.decoded.html` (S — şablon 132–617, mantık 619–925). React / çalışma zamanı betikleri repoya alınmadı.
> - **Ölçüt:** `BRIEF-cycle-periods-capacity.md` §1–§5; mevcut kod `Domain/Entities/{CyclePeriod, CycleCapacity}.cs`, `Features/CyclePeriod/**`, `Features/CycleCapacity/**`.
> - Analiz alt ajanla; kritik iddialar CT tarafından doğrulandı: hesap S:679–682, `UpdateCyclePeriodHandler` (`ScopeImmutable`, `DatesImmutable`), `CycleCapacityCalculator.Unresolved` (kısmi tablo yok).

## 1. Özet
Brief'in 6 ekranı da çizilmiş ve kapsama yüksek: tablo + **yıl zaman çizelgesi** (kapsam satırları, boşluk / çakışma, bugün çizgisi), hızlı tarih şablonları, canlı iş günü, kapsam alt alanları, bağlantılı çakışma uyarısı, "geçerli dönem bul", dönem ayrıntısı (bağlı kayıtlar, takvim özeti), iki sütunlu canlı kapasite düzenleme (aylık tablo, "tüm aylara uygula"), hesap şelalesi, ay ay grafik, arz / talep.

**Ziyaret süresi modeli (brief §2.4) önerilen tek modelle uygulanmış:** `tipik ziyaret = tipik promo × promo dk + tipik non-promo × non-promo dk + rapor dk (ziyaret başına)`; günlük sabit = yol + sınav (S:679). Bu, planlamadaki `ActivityTimeBudgetCalculator.VisitDuration` ile aynı; **kapasite hesabı değişmeli**.

Eksik: 7 dil / sağdan sola / tema (yalnız Türkçe, sabit renk); kapasite düzenlemede yükleme / hata durumu; panelde Esc / odak tuzağı.

## 2. Kodla çelişen / eklenen kurallar ve CT önerisi
| # | Mockup | Bugünkü kod | CT önerisi |
|---|---|---|---|
| **K-1** | Ziyaret süresi = tipik sayılar × ürün başına süre + **ziyaret başına** rapor | `MinutesPerVisit()` = promo + non-promo; rapor **gün başına** (`DailySpendMinutes`) | **Mockup modeli.** Yeni alanlar `TypicalPromoCount`, `TypicalNonPromoCount` (≤ sınırlar), `ReportMinutesPerVisit`. Eski kayıt: yeni alanlar boşsa **eski hesap** (okuma anında, veri yazılmaz); kayıt düzenlenince yeni modele geçer, eski gün başı rapor sıfırlanır. Planlama ve kapasite aynı formülü tek yerden kullanır. |
| K-2 | Dönem kodu otomatik (`TR-2026-04`, `GM-…`, `L1-…`, `B1-…`), düzenlemede yeniden üretiliyor | Kodu kullanıcı yazar, tekil, **hiç değişmez** | Oluştururken **öneri** (mockup biçimi), kullanıcı değiştirebilir; kayıttan sonra **değişmez**. |
| K-3 | Aktif dönemde tarih / kapsam düzenlenebiliyor | Aktifte tarih / yıl / sıra / kapsam değişmez; kapsam her zaman değişmez | **Kod kuralı korunur**; ekranda salt okunur + "kapat ve yeni dönem aç" yönlendirmesi. |
| K-4 | Takvim çözülemezse hafta içi günlerle **tahmini sayı** | Hiç sayı yok (kısmi tablo yok) | **Kod kuralı korunur**: "hesaplanamadı" + neden (tahmin / yetki yok); girdiler görünür, sonuç yok. |
| K-5 | FTE ay ay girilebilir (adım 0,05, toplu doldurma) | FTE sunucu yazar (kurum içi varsayılan), alan kapalı; `FteSource`'ta "yazar girdi" değeri ayrılmış | **Girilebilsin** (`FteSource = authored`); boşsa kurum içi varsayılan; 0–1 arası. İK kaynağı sonra. |
| K-6 | "Geçerli dönem" aracı kapalı dönemleri de sayıyor; `ambiguous` yok | Yalnız aktifler; sonuç resolved / none / ambiguous | **Kod kuralı**: yalnız aktif; "belirsiz" sonucu ekranda gösterilir. |
| K-7 | Temsilci sayısı, ekip arzı, aylık talep oranı | Varlıkta yok (kapasite = temsilci başına) | Kapasite **temsilci başına** kalır. Arz / talep ayrıntıda planlama oturumlarının talebiyle (planlanan ziyaret) karşılaştırılır; ekip arzı (temsilci sayısı) bölge kaynak atamalarından **sonraki iş**. |
| K-8 | Takvim ülkesi tüzel kişi / iş birimi kapsamında da kilitli | Yalnız ülke kapsamında türetilir; tüzel kişi takvimi daraltır | **Kod kuralı**: ülke kapsamında otomatik + kilitli; diğerlerinde seçilir (tüzel kişinin ülkesi öneri olarak gelir). |
| E3 | Taslakta "Kapat" yok | taslak → kapalı var | Ekrana eklenir. |
| E4 | Bitiş = başlangıç kabul | Bitiş başlangıçtan sonra olmalı | Kod kuralı. |
| E5 | Sıra yalnız öneri; aynı sıra engellenmiyor | Kapsam + yıl içinde tekil (kapalılar dahil) | Kod kuralı; ekranda anlık uyarı. |
| E8 | Mikro-hedefleme günü saha gününe **kırpılıyor** | Kırpma yok | **Mockup** (kırp): saha gününden fazla mikro-hedefleme günü anlamsız. |
| E9 | Üst sınırlar UI'da gevşek | 1440 / ziyaret 480 / tampon 240 | UI sınırları koddan. |
| E10 | Zaman çizelgesi ekseni sabit 2025–2027 | — | Dinamik (filtredeki yıl ± 1). |
| E11 | "Kapasitesi olmayan açık dönemler" bandı | — | Uygun ek. |
| E13–14 | Ay başına yuvarlama, düşülen > iş günü → ay 0 | Aynı | Uyumlu. |
| E15 | `PER-503`, `CAP-503` | Kod sözlüğü farklı | Yerelleştirilmiş metin. |

## 3. Mockup verisinden alan modeli (özet)
- **Period:** `type` (company=tenant / country / legal=legal-entity / bu=business-unit), `country`, `legal`, `bu`, `buSrc` (bölge yapısı / elle), `year`, `seq`, `s`, `e`, `status` (draft / active / closed), `name`, `desc`, bağlı sayılar (`campaigns`, `planned`), `act` / `cls` {by, at}.
- **Capacity:** `pid`, `cal`, günlük `workMin` 480 / `travel` 90 / `exam` 15; ziyaret `promoMin` 12 / `nonMin` 5 / `reportMin` 5 / `typP` 2 / `typN` 1 / `buffer` 10 / `maxP` `maxN` 3; `months{YYYY-MM: meet, train, leave, microD, microM, fte}`; `archived`.
- **Takvim durumu:** resolved / estimate / noaccess ↔ kod resolved / calendar_unresolved / calendar_forbidden.

## 4. Paketleme önerisi
1. **CAP-MODEL (CRM):** K-1 (tipik sayılar + ziyaret başına rapor, eski kayıt okuma uyumu, tek formül), K-5 (yazar FTE), E8 (mikro-hedefleme kırpma), K-2 (kod önerisi ucu), hesap DTO'suna şelale kalemleri (ekranın ihtiyacı).
2. **CYC-UI (Web):** iki sayfanın yeniden kurgusu (mockup + §2 kararları), 7 dil, tema, klavye.
Sıra: CAP-MODEL → CYC-UI (ekran yeni sözleşmeyi kullanır).

## 5. Kullanıcı kararı (2026-10-05)
"kabul CAP-MODEL'i paketle" — §2 CT önerilerinin tümü (K-1..K-8, E3–E15) kabul. Paket: `WP-CAP-MODEL-visit-duration-capacity.md`.
