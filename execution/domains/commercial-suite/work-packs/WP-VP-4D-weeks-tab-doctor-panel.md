# WORK PACKAGE — WP-VP-4D · Ziyaret Planlama ekranı (3/3): Haftalar sekmesi + doktor dönem paneli + yeniden aç penceresi

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** mockup v2 ekran 05 Haftalar + doktor paneli "Dönem görünümü" · [brief](mockups/visit-planning/BRIEF-visit-planning-rep-week.md) §5 · [K-7 eki](mockups/visit-planning/BRIEF-ADDENDUM-K7-visit-products.md) (Haftalar + doktor paneli) · [v2 analizi](mockups/visit-planning/VISIT-PLANNING-mockup-v2-analysis.md) · 3A / 3B / 3C önizleme alanları.
> - **Kullanıcı:** "Faz 4 … paketlemeye başla" (2026-10-07).
> - **Kapsam:** yalnız Web, Haftalar sekmesi (`weeks.js`) + doktor panelinin dönem sekmesi (`doctor-panel.js`, 4C'nin panel iskeletine).
> - **Ön koşul:** **4A, 4B ve 4C birleşmiş olmalı.**
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4d` (4C kabulünden sonra açılır), dal `wp/vp-4d`. Commit bu dala, push YOK.

## Kullanılacak veriler (hazır)
- Önizleme (`POST …/preview`):
  - `weeks[]` (3A durum + ziyaret sayısı);
  - `scheduled[]` (`weekStart`, `plannedDate`, `isFixed`, `contentItems[]` + `source` / `order`, `frequencyStatus`, `requiredVisitCount`);
  - `weekCapacity[]` (3B; + `productVisitCounts` 3C), `periodCapacity`;
  - `shifted[]`, `unscheduled[]` (`consent_blocked`, `period_exhausted` …), `halfDayDates`, `nonWorkingDates`;
  - `overflowProducts`.
- Doktor durumu: 3D (`my-accounts/{id}/doctors`, `targets`).
- Hafta onayı / yeniden aç: 4B'nin eylemleri (aynı fonksiyonları çağır).

## NE
### 1. Dönem şeridi (brief §5)
- Dönemin tüm haftaları yatay. Her hafta için:
  - durum (geçmiş / onaylı / taslak / boş) ve ziyaret sayısı;
  - kapasite doluluk çubuğu (`plannedMinutes / capacityMinutes`);
  - tatil işareti (ör. 29 Ekim) ve yarım gün; uyarı rozetleri (taşma, sığmayan); BUGÜN işareti.
- Bir haftaya tıklama → hafta ayrıntısı (aşağıda); "Rotayı aç" → Rota sekmesi o haftayla (Rota değişmez).
- **Otomatik taslak kuralı** kısa açıklaması: sıklık, eşit aralık, gün dengesi, hafta sonu / tatil yok, sıklığı bilinmeyen = dönemde 1 + "sıklık yok".

### 2. Hafta ayrıntısı
- **Gün gün** (Pzt–Cum) doluluk çubuğu: "N / günlük sınır"; tatil "Tatil · …", yarım gün "yarım gün".
- **Gün satırı açılır** (mockup v2): doktor adı + kurum + ürün çipleri + "≈ N dk"; ilk 6 + "+N doktor daha"; boş günde "Bu güne ziyaret düşmüyor". Doktora tıklama → doktor paneli.
- **Ürün başına haftalık ziyaret** çipleri (`productVisitCounts`; ipucunda tanıtım / hatırlatma).
- **Karışık sıra** açıklaması (A, B, C → B, C, A).
- **Kaydırılan / sığmayan hedefler ve ürünler:**
  - `shifted[]` (neden: kapasite dolu / tatil / yarım gün → sonuç "X. haftaya");
  - `unscheduled` (izin engelli, dönem sonu);
  - `overflowProducts` ("KARDİVA sığmadı → sonraki ziyarete").
- **Eylemler** (hafta durumuna göre; 4B fonksiyonları): "Haftayı onayla", "Bu haftayı yeniden üret" (önizlemeyi tazele), "Rotayı aç", onaylıda "Haftayı yeniden aç".
- Haftadaki doktorlar listesi + dönem noktaları (mockup).

### 3. Doktor paneli — "Dönem görünümü" sekmesi (4C panelinin ikinci sekmesi)
- Başlık: ad · uzmanlık · kurum; segment rozeti (bilgi).
- **Sıklık hedefi / yapılan / kalan** (3D).
- **Sıradaki ziyaretin ürünleri:** hafta + çipler + kaynak + süre + "Ürünleri değiştir" (→ Ürünler sekmesi). Ürün yoksa "Ziyaret süresi hesaplanamıyor. Ürün seç".
- **Dönem boyunca · ürün geçmişi:** hafta başına durum (Yapıldı / Planlı · onaylı / Taslak / Öngörülen) + çipler; etiket "Sunuldu / Planlandı / Öngörülen".
  - "Sunuldu" SB-3c'ye kadar planlanan ürünlerden; raporlu ziyarette "yapıldı" rozeti.
- "Sıradaki içerik" satırı: mockup'ta "yakında" yeri — SB-3c ertelendi; **gösterme** ya da pasif not (raporla).

### 4. Yeniden aç penceresi
- 4B'deki pencere Haftalar'dan da açılır (aynı bileşen).
- Metin MK-4: "Gerekçe haftanın geçmişine kaydedilir"; yönetici bildirimi yok.
- Onaylı haftanın geçmişi (`history[]`: onay / yeniden açma, kim, ne zaman, gerekçe) hafta ayrıntısında listelenir.

### 5. Eski `committed` planlar
- Şeritte yazılmış haftalar "onaylı (eski plan)", diğerleri boş (4A kuralı).
- Eylem yok (yeniden açma yok, F3-2).

## KORU / YAPMA
- Backend'e dokunma. Eksik alan → rapor.
- **Yeni yazma uç YOK.**
- Rota değişmez (yalnız "Rotayı aç" ile hafta seçimi).
- 4B / 4C bileşenlerini yeniden yazma; çağır.
- Mockup stilleri kopyalanmaz; tema bileşenleri; 7 dil + Arapça sağdan sola; tarih biçimi aynı.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (4C sonrası), CRM (dokunulmaz), mimari (27). Build 0 hata. JS `node --check`.

**Yeni testler:**
1. Şerit hafta durumlarını / sayıları / kapasite çubuğunu `weeks[]` + `weekCapacity[]`'dan üretir; tatil ve yarım gün işaretleri.
2. Gün satırı açılımı: ilk 6 + "+N"; boş gün metni.
3. Kaydırılan / sığmayan listesi `shifted` + `unscheduled` + `overflowProducts` birleşimi, nedenler yerel metinle.
4. Doktor paneli: hedef / yapılan / kalan 3D'den; sıradaki ürünler + kaynak; ürün geçmişi hafta durumuna göre etiket.
5. Eylemler hafta durumuna göre (taslak / onaylı / geçmiş / eski plan).
6. Yeni anahtarlar 7 dilde.

**Sabotaj (kırmızı kanıtla, geri al):**
1. Onaylı haftada "Haftayı onayla"yı göster → test 5 kırmızı.
2. `shifted` nedenini düşür → test 3 kırmızı.

### E4 (CT, fleet; kayıtlar kullanıcı onaylı)
- Q4 şeridi: 29 Eki tatil.
- Hafta ayrıntısında günler, ürün çipleri, kaydırılanlar.
- Doktor paneli (SADAKAT ÖZDİL: 1 yapıldı).
- `23b1706a` 42. hafta reopen penceresi (gerekçeli).
- Arapça sağdan sola.
- **Faz 4 sonu:** ekran mockup v2 ile yan yana kullanıcıya gösterilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (4C kabulünden SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4D · Ziyaret Planlama ekranı (3/3): Haftalar sekmesi + doktor dönem paneli + yeniden aç penceresi
Repository: C:\tmp\vp-4d (worktree) · Branch: wp/vp-4d · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-4D-weeks-tab-doctor-panel.md — önce tamamını oku. Mockup: …/mockups/visit-planning/ (v2 standalone + decoded + BRIEF §5 + BRIEF-ADDENDUM-K7 + iki analiz). 4A, 4B, 4C §37'lerini oku (iskelet, panel, eylemler). Ayrıca: …/WP-VP-3A/3B/3C/3D belgeleri (önizleme alanları) · frontend/Diten.Web/Views/CRM/VisitPlanning/** · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/**.
NE:
(1) Dönem şeridi: tüm haftalar, durum + ziyaret sayısı + kapasite çubuğu + tatil/yarım gün + uyarı rozetleri + BUGÜN; tıklama → hafta ayrıntısı; Rotayı aç → Rota o hafta; otomatik taslak kuralı açıklaması.
(2) Hafta ayrıntısı: gün gün doluluk (N/günlük sınır, tatil, yarım gün), açılır gün satırı (doktor+kurum+ürün çipleri+≈dk, ilk 6 + "+N", boş gün metni, doktora tıkla → panel), ürün başına haftalık ziyaret, karışık sıra açıklaması, kaydırılan/sığmayan (shifted + unscheduled + overflowProducts, yerel nedenler), eylemler (4B fonksiyonları: onayla / yeniden üret / Rotayı aç / yeniden aç), haftadaki doktorlar + dönem noktaları, onaylı haftanın history[] listesi.
(3) Doktor paneli Dönem görünümü sekmesi: başlık + segment rozeti, hedef/yapılan/kalan, sıradaki ziyaretin ürünleri + kaynak + süre + Ürünleri değiştir, ürün geçmişi (Sunuldu/Planlandı/Öngörülen), sıradaki içerik satırı gösterilmez/pasif (SB-3c ertelendi, raporla).
(4) Yeniden aç penceresi Haftalar'dan da (4B bileşeni), MK-4 metni.
(5) Eski committed planlar: yazılmış haftalar "onaylı (eski plan)", eylem yok.
KORU/YAPMA: backend'e dokunma (eksik alan → rapor); yeni yazma ucu YOK; Rota değişmez; 4B/4C bileşenleri yeniden yazılmaz; mockup stilleri kopyalanmaz; 7 dil + RTL; tarih biçimi aynı.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web · CRM (dokunulmaz) · mimari (27); build 0 hata; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–6. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(web): WP-VP-4D — weeks tab with period strip, day breakdown, shifted/overflow lists, doctor period panel, reopen from weeks" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), eksik backend alanları (varsa), elle denenecek durumlar. §22 TÜRKÇE. K13.
```
