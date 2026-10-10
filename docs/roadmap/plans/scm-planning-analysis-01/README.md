# SCM planlama analizi — gereksinim paketi (Q74, SCM-PLANNING-ANALYSIS-01)

🤖 Applying knowledge of @business-analyst + @product-manager. Lane AL-SCM-ANALYSIS-01 (INS). Başlangıç 2026-09-26T14:57:26+03:00.
Repo `feature/mvp6-logistics` @ `4a8d4d4b` (yalnız okuma). MVP6 geliştirmesinden ayrı bir analizdir; hiçbir pack, DCP, registry, sözleşme, kod veya MVP6 planı değiştirilmedi; modül ID'si açılmadı; hiçbir şey onaylanmadı.

## Yönetici özeti

İki iç analiz (S&OP hesap kuralları ve Otomatik Sipariş ekranı) resmi, gözden geçirilebilir bir gereksinim paketine dönüştürüldü. Dosyalar **kod için değil, gereksinim girdisi olarak** kullanılabilir. MVP6 bu hesapların hiçbirini içermiyor: MOD-0190 yalnız S&OP imza akışını, MOD-0192 yalnız kapasite senaryolarını kapsıyor. Hesapların doğal sahipleri MOD-0188 (talep), 0189 (MRP/ikmal), 0191 (emniyet stoğu), 0173/0174/0176 (stok, parti, FEFO), 0194/0195/0197 (iş emri, parti/eBR, fason üretici) ve 0148 (portal).

İki alan hiçbir modülde yok: **(1) distribütör kanal stoğu** (sell-in, GIT faturaları, iade/credit note, toptancı stoğu, sell-out) ve **(2) artwork / mock-up onayı**. Sahiplik için yalnız seçenekler verildi (MODULE-MAPPING.md).

| Dosya | İçerik |
|---|---|
| `inputs/SOPCalculation.xlsx`, `inputs/Automatic_Order_v1_2.xlsx` | Bayt-bayt kopyalar (sha256 aşağıda) |
| `RULES.tsv` | 60 kural: 28 S&OP satırı + 32 Otomatik Sipariş kuralı; hedef modül ve güven düzeyi ile |
| `WORKED-EXAMPLE.md` | "Calculation" sayfasının adım adım yeniden hesabı ve bir S&OP zinciri |
| `BEST-PRACTICE-GAPS.md` | 11 konu, uluslararası uygulama, boşluk, seçenekler |
| `OPEN-QUESTIONS.tsv` | 40 açık soru (hücre, soru, neden önemli, cevap seçenekleri) |
| `MODULE-MAPPING.md` | Modül bazında gruplama, MVP6 kapsamı, iki GAP alanı için sahiplik seçenekleri |
| `docx/` | README, BEST-PRACTICE-GAPS, MODULE-MAPPING ve OPEN-QUESTIONS'ın Word kopyaları |

## Sayım mutabakatı

- S&OP: `Sheet1` satır 2–29 = **28 satır** (id 15–41 ve 43; id 42 yok) → RULES.tsv'de SOP-15…SOP-43, **28/28**.
- Otomatik Sipariş: *Calculation* (formül, süre bileşenleri, lot, örnek zamanlama, etiketsiz seri) 6 kural; *Order rows* (filtre, sütun tipleri, statüler, renkler, aksiyonlar, yetkiler, roller, yorumlar) 25 kural; *Prototype* 1 kural; *Order list* boş (0) → birleştirilmiş **32 kural** (AO-01…AO-32; birden çok sayfada geçen kural tek satırda, kaynak hücreleriyle).
- Toplam **60 kural**, **11 boşluk**, **40 açık soru**.

## Girdi hash'leri

| Dosya | sha256 |
|---|---|
| inputs/SOPCalculation.xlsx | `ac94906f10cdcd10218ea27c24062eb178b1806387c7df8f06c5008c0cf98776` |
| inputs/Automatic_Order_v1_2.xlsx | `d338e63dc413e0f2ba0415adb3eb65039c1cdc38ce2327b5c1dac19f66069f32` |

## İlk 5 öneri

1. **Emniyet stoğu:** mevcut formülü geçiş kuralı olarak tut, geçmiş veri oluştukça servis seviyesine dayalı istatistiksel formüle geç (MOD-0191; G1).
2. **Tahmin yuvarlama ve zaman çitleri:** gönderilmeyen tahminin otomatik devrini durdur; teslim süresi içinde dondurulmuş bölge ve tahmin tüketimi kur (MOD-0189/0188; G3).
3. **Lot ve raf ömrü:** minimum partiye ek olarak kat, azami lot ve kalan raf ömrü sınırı; FEFO ve pazar bazlı asgari raf ömrü (MOD-0189/0176; G4, G7).
4. **Aylık S&OP döngüsü:** MVP6'daki MOD-0190 anlık görüntü + imza akışını her ay kullan; talep/arz girdileri 0188/0189'dan (G6).
5. **Kalite ve izlenebilirlik:** parti serbest bırakmayı yetkili kalite rolüne ve stok statüsüne bağla; DataMatrix'i PDF yerine GTIN/seri/parti/SKT verisi olarak tut (MOD-0195/0175/0174; G9, G10).

## VARSAYIMLAR

- **A1** RULES.tsv'deki İngilizce özetler Excel ipuçlarından (tooltip) ve Türkçe notlardan çıkarıldı; ikisi çelişirse ikisi de yazıldı ve açık soru açıldı.
- **A2** Hedef modül eşlemesi registry ve domain-config'teki modül tanımlarına göre yapıldı; karar değildir.
- **A3** Otomatik Sipariş'te aynı kural birden çok sayfada geçiyorsa tek kural sayıldı (sayım yöntemi yukarıda).
- **A4** WORKED-EXAMPLE §2'deki sayılar örnektir; dosyalardan gelmez.
- **A5** Uluslararası uygulamalar genel yöntem olarak anlatıldı; belge alıntısı yapılmadı.
- **A6** Registry'de MOD-0147/0148 satırları bu lane'in aramasında bulunamadı; MOD-0148 kapsamı pack dosyasından okundu.
- **A7** Word dönüşümü pandoc ile, VM yerine bulut çalışma alanında yapıldı (VM'de pandoc gerekmedi); sonuç klasöre kopyalandı.
- **A8** Klasördeki zip dosyası SHA256SUMS'ın dışında bırakıldı (kendi içeriğini kapsadığı için).
