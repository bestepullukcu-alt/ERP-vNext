# Uçtan uca test — TUTUKON: içerik → strateji → ziyaret planı → ziyaret raporu

> **CT, 2026-10-07.** Kullanıcı kararı: "ben giriş yaparım, uçtan uca testi sen yaparsın".
> Yer: [yol haritası](ROADMAP-visit-planning.md) **Faz E2E** — WP-VP-FIX-2 kabulünden sonra. Faz 2b ajan işiyle paralel yürüyebilir.
> Önceki bağlam: memory `crm-content-visit-e2e-test` (2026-09-28 zincir haritası) · SB-3a / SB-3b (oyun ürün satırı → yolculuk, yolculuk ilerlemesi çözücüsü) · WP-VP-2 E4 (oyun türetmesi canlıda veri yüzünden görünmedi).

## Amaç
Bir temsilcinin (Beste) planladığı ziyarette **"ne sunacağım"** bilgisinin uçtan uca gelmesini canlıda görmek:

doktor → aktif segment → aktif oyun (STR-TUTUKON) → ürün satırı → yayımlanmış yolculuk → aşama → yol (path) adımları + içerik → planlanan ziyaret içerik kalemleri → Ziyaret Yürütme → ziyaret raporu.

## Roller
| Kim | Ne yapar |
|---|---|
| **Kullanıcı** | Fleet'i başlatır, gerekli kullanıcıyla **giriş yapar** (Beste = Admin User; onay adımlarında sema). Şifre CT'ye verilmez. |
| **CT** | Adımları ayrı sekmede **kendisi** yürütür, her adımı doğrular, kanıt (ekran / API çıktısı) toplar, bulguyu kaydeder, gerekirse düzeltme WP'si yazar. |

**Yazma kuralı:**
- Bu planda listelenen kayıt adımları kullanıcı tarafından **bu test için onaylıdır** (2026-10-07).
- CT her yazmadan önce ne yazacağını tek satırla söyler.
- Liste dışı bir yazma gerekirse önce sorar.
- Silme / arşivleme yalnız `E2E-` önekli test kayıtlarında yapılır.
- Test kayıtları **`E2E-TUT-`** önekiyle adlandırılır (temizlik için).

## Ürün kararı (2026-09-28, hatırlatma)
- TUTUKON = **sindirim konforu**; hedef uzmanlıklar **gastroenteroloji, aile hekimliği, iç hastalıkları** (üroloji değil).
- Mevcut oyun `STR-TUTUKON-URO` **arşivlenmiş** `SEG-URO-DOCTORS` segmentine bağlı → yeniden hedeflenecek.

## Adımlar
| # | Adım | Kim / nasıl | Beklenen | Durum |
|---|---|---|---|---|
| E0 | **Envanter (yalnız okuma):** TUTUKON konu / başlık / içerik / yol / yolculuk / oyun / segment / sıklık politikası / iddia; Beste'nin 4 ilçesindeki gastro + aile + dahiliye doktor sayısı | CT (Mongo + API, salt okuma) | Eksikler listesi; hangi adımların gerekli olduğu kesinleşir | ☐ |
| E1 | **Segment:** `E2E-TUT-SINDIRIM` (kişi; uzmanlık ∈ {gastroenteroloji, aile hekimliği, iç hastalıkları}) oluştur → değerlendir → **etkinleştir** | CT, Segmentler sayfası (Beste) | Üye sayısı > 0; Beste'nin ilçelerindeki doktorlardan üye var | ☐ |
| E2 | **İçerik:** en az 2 KnowledgeContent (TUTUKON, tr) — promo detaylama + bir non-promo bilgi. Gerekirse iddia bağı. Yayımla (onay akışı varsa sema) | CT (Beste) + kullanıcı sema girişi (onay) | İçerikler `published`, tr | ☐ |
| E3 | **Yol (KnowledgePath):** içerikleri adım olarak içeren yol → gönder → onay → yayımla | CT + sema | Yol `released` / yayımlanmış sürüm | ☐ |
| E4 | **Yolculuk (ContentEngagementJourney):** en az 2 aşama, her aşama yola bağlı → yayımla | CT (+ sema SoD gerekirse) | Yolculuk yayımlanmış, aşamalar sıralı | ☐ |
| E5 | **Oyun:** STR-TUTUKON'un **yeni sürümü** — segment bağlaması `E2E-TUT-SINDIRIM`, ürün satırı TUTUKON → E4 yolculuğu, sıklık politikası → etkinleştir | CT (Beste) | Aktif oyun yeni segmente bağlı; eski sürüm "superseded" | ☐ |
| E6 | **Sıklık:** TUTUKON segmenti için sıklık politikası (ör. dönemde 3) aktif mi; değilse `E2E-TUT-VFP` oluştur + etkinleştir | CT | Önizlemede `frequencyStatus = resolved` | ☐ |
| E7 | **Plan:** Beste ile yeni taslak (aktif dönem, boş ilk hafta) → Hedefler'de bölge araması → uzmanlığı uygun 5–10 doktor + 1–2 eczane → Hedefleri kaydet → **Önizleme** | CT (Beste) | `contentStatus = resolved` (segment üyesi doktorlarda), içerik kalemleri (ürün, aşama, adımlar), süre tipik modelden; tatil / hafta sonu boş; bölge uyarısı yok | ☐ |
| E8 | **Uygula** (haftanın planı) | CT (Beste) | Planlanan Ziyaretler'de kayıtlar, `contentItems` dolu, adlar görünür | ☐ |
| E9 | **Ziyaret Yürütme:** bir ziyareti aç → içerik / sunum bilgisi → sonucu kaydet → rapor gönder | CT (Beste) | Rapor kaydı; yolculuk ilerlemesi bir sonraki aşamaya geçer (SB-3b) | ☐ |
| E10 | **Sonraki ziyaret:** aynı doktor için sonraki haftanın önizlemesi | CT | İçerik bir sonraki aşamadan gelir | ☐ |
| E11 | **Mobil görünüm:** `resources/me`, planlanan ziyaret liste / detay, `my-accounts` yanıtlarını API'den kaydet | CT (salt okuma) | Mobil sözleşme notu için gerçek örnek yanıtlar | ☐ |

## Çıktı
- Her adımın kanıtı ve bulgular bu dosyanın sonunda **§Sonuç** bölümüne yazılır.
- Her hata bir düzeltme WP'sine dönüşür; yol haritasına eklenir.
- Test sonunda `E2E-TUT-` kayıtlarının listesi; temizlik kullanıcı kararıyla.

## Bilinen riskler (önceki analizden)
- Oyun – yolculuk bağı (SB-3a) öncesi kayıtlar yolculuk taşımıyor → `ProductHasNoJourney`.
- Yolculuk aşamasında yol yoksa içerik gelmez (SB-3b).
- İçerik dili / kitle bağlamı uyuşmazsa içerik düşer.
- İçerik yayımı iddia (claim) onayı gerektirebilir (BE-6 yayın kapısı).
- Onay adımları için sema girişi gerekir (kullanıcı).
