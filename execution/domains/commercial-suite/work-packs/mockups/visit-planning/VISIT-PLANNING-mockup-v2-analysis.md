# Mockup v2 analizi — Ziyaret Planlama: ziyaret ürünleri (K-7)

> **CT, 2026-10-07.**
> - **Kaynak:** `Ziyaret Planlama v2 (standalone).html`; çözülmüş hâli `visit-planning-v2.decoded.html`.
> - **Karşılaştırma:** v1 (`visit-planning.decoded.html`); fark 225 satır eklendi, 44 silindi.
> - **Ölçüt:** [BRIEF-ADDENDUM-K7](BRIEF-ADDENDUM-K7-visit-products.md) + [ana brief](BRIEF-visit-planning-rep-week.md) + [v1 analizi](VISIT-PLANNING-mockup-analysis.md) (geçerli).

## Sonuç
**Ek brief'in dört maddesi de karşılanmış. DOKUNMA alanları korunmuş.** Mockup Faz 4 için kabul edilebilir. Aşağıdaki dört not uygulamada netleşir; S-1 dışındakilerde CT önerisi varsayılan olarak alınır.

## DOKUNMA kontrolü
| Alan | Durum |
|---|---|
| Rota sekmesi | ✓ görsel bayt bayt aynı (yeniden paketlemede yalnız kimliği değişti) |
| Liste | ✓ fark yok |
| Yeni plan paneli | ✓ fark yok |
| Tarih biçimi ("5 Oct, 26") | ✓ değişmedi |

## Ek brief'e göre kontrol
### 04 Hedefler
- ✓ **"Ürünler" sütunu.**
  - Çipler: tanıtım = lacivert zemin, hatırlatma = beyaz + çerçeve.
  - Kaynak simgeleri: önerilen (ampul), son ziyaret (saat), sizin seçiminiz (kişi).
  - Onaylı içeriği olmayan tanıtım ürününde uyarı simgesi.
  - Tabloya simge açıklaması (lejant) eklenmiş.
- ✓ **Ürünü olmayan doktor:** sarı "ürün yok" + "Ürün seç" bağlantısı.
- ✓ **Ürün seçici, doktor panelinin "Ürünler" sekmesinde açılıyor** (MK-1 çekmece deseniyle tutarlı). İçinde:
  - portföy listesi, ad / kod araması, "Tüm ürünler" geçişi;
  - portföy tanımlı değilse tüm ürünler + bilgi notu;
  - ürün başına rol düğmeleri (Tanıtım / Hatırlatma), varsayılan ana veriden;
  - sınır göstergesi "N / 3" ve aşımda kırmızı uyarı;
  - **anlık süre**, ör. "2 tanıtım + 1 hatırlatma + rapor ≈ 34 dk";
  - "Bu ürün için onaylı içerik yok" uyarısı.
- ✓ **K-7b:** önerilen (oyundan gelen) ürün **kilitli**. Kilit ipucu: "yalnız Planlanan Ziyaret ekranında çıkarılabilir". Ekleme serbest.
- ✓ **K-3:** oyunun adı hiçbir yerde yok; yalnız "önerilen".
- ✓ **Toplu uygula:** araç çubuğunda "Ürün uygula (N)" düğmesi (seçim yoksa pasif).
  - Aynı çekmece toplu kipte açılıyor; düğmesi "Seçili N doktora uygula".
  - Ürünler doktorların mevcut ürünlerine **eklenir**, mevcutlar korunur.
- ✓ **Seçim özeti:**
  - "Ürün dağılımı" satırı, ör. `TUTUKON 16 · X 4`.
  - "N seçili doktorda ürün yok" uyarısı.
  - Haftalık süre artık ürünlere göre: "Bu hafta ≈ X saat (ürünlere göre)".
- ✓ **Kurallar kutusu:** kaynak sırası ve "karışık sıra" maddeleri eklenmiş.

### 05 Haftalar
- ✓ **Gün satırları açılıyor:**
  - doktor adı + kurum + ürün çipleri + "≈ N dk";
  - ilk 6 doktor, kalanı "+N doktor daha";
  - ziyaretsiz günde "Bu güne ziyaret düşmüyor".
  - Bu, v1 analizindeki **C6**'yı da (çok ziyaretli durakta doktor adları) Haftalar tarafında karşılıyor.
- ✓ **"Ürün başına haftalık ziyaret"** çipleri (ipucunda tanıtım / hatırlatma kırılımı).
- ✓ **Karışık sıra açıklaması** (A, B, C → B, C, A). Mockup sırayı doktorun dönemdeki kaçıncı ziyareti olduğuna göre kaydırıyor (K-7e ile aynı).
- ✓ **Sığmayan ürünler:** liste "Kaydırılan / sığmayan hedefler ve ürünler" oldu; ürün satırı paket simgeli, ör. "KARDİVA sığmadı → sonraki ziyarete".

### Doktor paneli
- ✓ İki sekme: "Dönem görünümü" ve "Ürünler"; panel 440 → 480 px.
- ✓ **Sıradaki ziyaretin ürünleri:** hafta + çipler + kaynak + süre + **"Ürünleri değiştir"**. Ürün yoksa "Ziyaret süresi hesaplanamıyor. Ürün seç".
- ✓ **Ürün geçmişi:** hafta başına çipler, etiketi haftanın durumuna göre "Sunuldu" / "Planlandı" / "Öngörülen".

### Durumlar
- ✓ Yeni durum: "Portföy tanımlı değil".
- ✓ Sınır aşımı, onaylı içerik yok, ürün yok — hepsi çizilmiş.
- ✓ `asim` (kapasite aşımı) düğmesi ürün süresiyle çalışıyor.

## Notlar — uygulamada netleşecekler
| # | Konu | Mockup | CT önerisi |
|---|---|---|---|
| **S-1** (kullanıcı) | Ürün değişikliği **onaylı haftaları** etkiler mi? | "Değişiklikler bu doktorun sonraki ziyaretlerine uygulanır"; geçmiş dışındaki tüm haftalar yeni listeyle görünüyor. | **Hayır.** Yalnız taslak ve öngörülen haftalar değişir. Onaylı haftadaki ziyaret Planlanan Ziyaret ekranından değişir (Faz 6; MK-3 / MK-4 kilidiyle tutarlı). Metin: "…onaylanmamış sonraki ziyaretlerine uygulanır". |
| S-2 | Toplu uygulamada bir doktorda sınır aşılırsa | Ele alınmamış. | Ürünler yine eklenir. Sınırı aşanlar o ziyarette "sığmadı → sonraki ziyarete" listesine düşer (K-7e). Toast'ta "N doktorda sınır aşıldı" bilgisi. |
| S-3 | Önerilen ürünün **rolü** | Kilitli (rol düğmeleri de pasif). | Kabul: rol oyunun satırından gelir. K-7d'deki "temsilci rolü değiştirebilir" yalnız kendi eklediği ürünler için geçerli olur. |
| S-4 | Seçim nerede saklanır? | Çekmece "Tamam" ile kapanıyor; ayrı kaydet yok. | Planlama oturumunda **doktor başına ürün listesi**. Mevcut oturum güncellemesiyle (D9 `MergeSelection`: null = koru) taslağa yazılır, **yeni komut açılmaz**. Uygula (apply) listeyi planlanan ziyaretlere kopyalar. |

**Mockup'taki sabitler gerçekte şuralardan gelir:**
- Sınır "3 / 3" ve uyarı metnindeki 3 → dönem kapasitesindeki `MaxPromoProducts` / `MaxNonPromoProducts`.
- Süre formülü (12 / 5 / 5 dk) → kapasitedeki tipik ziyaret modeli (`VisitMinutes`).
- Ürün kodları (`TTK-100`) → MDM global ürün kodu (`GP-…`), ad + kod gösterilir.

## Bağımlılıklar (sıra)
1. **"Son ziyaret" kaynağı** gerçekleşen içeriğe dayanır → **SB-3c** (`ContentActuals`). SB-3c gelene kadar bu kaynak boş kalır; çip hiç görünmez, diğer kaynaklar çalışır.
2. **Portföy** → bölge atamasındaki "Product Portfolio" kapsamı. Veri bugün boşsa "Portföy tanımlı değil" durumu çalışır (tüm ürünler).
3. **Veri modeli (Faz 3, K-7):** planlanan ziyaretin ürün listesi (ürün, rol, kaynak, yolculuk / aşama), süre listeden, döngü, sığmayan ürün nedeni.
4. **Ekran (Faz 4):**
   - VP-UI-2 Hedefler: Ürünler sütunu, toplu uygula, özet.
   - VP-UI-3 Haftalar + doktor paneli: gün açılımı, ürün başına sayı, ürün geçmişi, Ürünler sekmesi.

## Mockup'ta YOK (beklenen; kapsam dışı)
- Planlanan Ziyaret ekranında tek ziyaret için ürün çıkarma (Faz 6, ayrı mockup).
- Rapor / yürütme ekranında "gerçekte sunulanlar" (SB-3c).
- Ek konular (numune, ziyaret amacı, ortak ziyaret, potansiyel, ürün bazında sıklık): Planlanan Ziyaret / rapor konuşulurken ele alınacak.
