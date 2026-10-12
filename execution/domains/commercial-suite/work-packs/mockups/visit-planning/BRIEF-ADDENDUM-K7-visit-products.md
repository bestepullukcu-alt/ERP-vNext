# MOCKUP BRIEF EKİ — Ziyaret Planlama: ziyaret ürünleri (K-7)

> Tarih: 2026-10-07 · Hazırlayan: CT.
> **Mockup yapana not:** Bu bir **güncelleme**, yeni bir ekran değil. Mevcut mockup `Ziyaret Planlama (standalone).html` (ekranlar 04 Hedefler, 05 Haftalar ve doktor paneli) bu eke göre genişletilir.
> Ana brief [BRIEF-visit-planning-rep-week.md](BRIEF-visit-planning-rep-week.md) ve [mockup analizi](VISIT-PLANNING-mockup-analysis.md) geçerliliğini korur.
> **DOKUNMA:** Rota sekmesi, liste, yeni plan paneli.
> Karar belgesi: [VISIT-PRODUCTS-without-play-decision.md](../../VISIT-PRODUCTS-without-play-decision.md) — **K-7a…g kullanıcı tarafından kabul edildi (2026-10-07).**

## Neden
Bugün ziyaretin ürünleri yalnız strateji şablonundan (oyundan) gelir; oyun yoksa ziyarette ürün olmaz ve süre hesaplanamaz.

Yeni kural:
- Her ziyaretin bir **ürün listesi** vardır; süre bu listeden hesaplanır.
- Liste sırayla dolar: oyun (varsa) → temsilcinin seçimi → doktorun son ziyaretinde sunulanlar → temsilcinin ürün portföyü.
- Temsilci **oyunu hiçbir zaman görmez**; yalnız ürünleri görür.

## 04 HEDEFLER — eklenecekler
**Doktor satırı:**
- Yeni sütun **"Ürünler"**: çip listesi, ör. `TUTUKON` `X`.
  - Çip rengi rolü gösterir: tanıtım (promo) / hatırlatma (non-promo).
  - Çipin küçük işareti kaynağı gösterir:
    - "önerilen": sistemden;
    - "son ziyaret";
    - "sizin seçiminiz".
- Ürünü olmayan doktorda: "Ürün seç" bağlantısı ve sarı "ürün yok" rozeti.

**Ürün seçici** (satırdan açılan küçük panel ya da açılır menü):
- Liste temsilcinin **portföyü** (ürün adı + kod). Arama var. "Tüm ürünler" geçişi, portföy tanımlı değilse varsayılan.
- Her seçili ürün için rol seçimi (tanıtım / hatırlatma). Varsayılan ürün ana verisinden gelir.
- **Sınır:** en fazla N tanıtım + M hatırlatma (dönem kapasitesinden, ör. 3 / 3). Aşınca uyarı.
- **Tahmini ziyaret süresi** anında güncellenir, ör. "2 tanıtım + 1 hatırlatma + rapor ≈ 34 dk".
- Onaylı içeriği olmayan tanıtım ürününde uyarı: "Bu ürün için onaylı içerik yok".
- **Toplu seçim:** seçili doktorlara aynı ürünleri uygula ("Seçili 12 doktora uygula").

**Seçim özeti kartı:**
- "Ürün dağılımı" satırı, ör. `TUTUKON 16 · X 4`.
- Ürünsüz doktor sayısı.
- Tahmini toplam süre artık ürünlere göre.

## 05 HAFTALAR — eklenecekler
- **Hafta kartı ayrıntısı:** gün gün listede her ziyaretin yanında ürün çipleri; ürün başına haftalık ziyaret sayısı.
- **"Karışık" sıra:** sonraki haftalarda aynı ürün seti, sıra kayarak gelir. Bir haftadaki A, B, C sırası sonraki haftada B, C, A olur. Haftalar sekmesinde kısa açıklama.
- **Kaydırılan / sığmayan listesi:** kapasite yüzünden düşen ürün de görünsün, ör. "X ürünü sığmadı → sonraki ziyarete".

## Doktor paneli (dönem görünümü) — eklenecekler
- **Ürün geçmişi:** bu dönemde hangi ziyarette hangi ürünler planlandı / sunuldu.
- **Sıradaki ziyaretin ürünleri** ve kaynağı.
- **"Ürünleri değiştir"** düğmesi: bu doktorun sonraki ziyaretleri için seçim.

## Durumlar (çiz)
- Oyunu olan doktor: ürünler "önerilen"; temsilci ürün ekleyebilir. Çıkarmak yalnız tek ziyarette (Planlanan Ziyaret ekranı, Faz 6).
- Oyunu ve seçimi olmayan doktor: "ürün yok" + "Ürün seç".
- Sınır aşımı uyarısı. Onaylı içerik yok uyarısı. Portföy tanımlı değil bilgisi.

## Kapsam dışı (bu mockupta YOK)
- Planlanan Ziyaret ve ziyaret raporu ekranları (Faz 6 / SB-3c, ayrı mockup).
- Ek konular (numune, ziyaret amacı, ortak ziyaret, ürün potansiyeli, ürün bazında sıklık): o ekranlar konuşulurken ele alınacak.

## MOCKUP'TA BEKLENEN GÜNCELLEMELER
1. 04 Hedefler: Ürünler sütunu + ürün seçici + toplu uygula + güncellenen seçim özeti.
2. 05 Haftalar: ziyaret satırlarında ürün çipleri + karışık sıra açıklaması + sığmayan ürün.
3. Doktor paneli: ürün geçmişi + sıradaki ürünler + "Ürünleri değiştir".
4. Durumlar: ürün yok, sınır aşımı, onaylı içerik yok, portföy yok.
