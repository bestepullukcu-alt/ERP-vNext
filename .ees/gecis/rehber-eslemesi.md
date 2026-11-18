# Kırmızı çizgiler, rehber şartları ve sahip kararları → kart satırları

Kaynak: GOAL Uyum Rehberi v0.1 (2026-11-06, sınıfı Internal) §1–§8 ve sahibin 16 Kasım 2026 onayı. Satır kimlikleri:
Ç = çekirdek, K = kule, H = hat, İ = işçi, D = denetçi (`.ees/kartlar/`). "Araç" sütunu bugün bir aracın o şartın bir
kısmını zorlayıp zorlamadığını söyler; araç yalnız Claude'da çalışıyorsa kart satırı yine kalır.

## 1. On iki kırmızı çizgi (rehber §3)

| # | Kırmızı çizgi | Kart satırı | Araç bugün |
|---|---|---|---|
| R1 | Ajan geri alınamaz iş yapmaz | Ç5, Ç13, Ç23, H1 | kısmen: Claude komut freni birleştirme, `main`'e push, uzak dal silme, etiket, sürüm ve ayar değişikliğini reddeder; dağıtım ve veri silme kapsam dışı; Codex'te fren yok |
| R2 | Düzenlenmiş onay (IMS, QA, sürüm) yok | Ç6, K9, D8 | yok |
| R3 | Ajan onay ya da imza vermez, kimse adına da | Ç6, H25, D8 | kısmen: Claude freni PR onayını reddeder; dosyaya onay satırı ya da yorumla onay yazmayı durdurmaz |
| R4 | Kendi işini ya da talimatını denetlemez | Ç7, H4, H18, H30, İ11, D1 | kısmen: talimat klasörleri kod sahibi listesinde; incelemenin zorunlu olduğu **doğrulanmalı** |
| R5 | Kısıtlı bilgi yok, Executive Confidential hiç yok | Ç8 | yok |
| R6 | Grup dışına gönderme ya da yayın yok; depoda ad, müşteri verisi, sır yok | Ç10, H26 | yok (eklenen satır sır taraması zorunlu kontrol değil) |
| R7 | Şirket sınırı aşılmaz | Ç8 | yok |
| R8 | Yalnız onaylı araç, ortak hesap yok, sır kodda ve talimatta yok | Ç9 | yok |
| R9 | Kişiler hakkında karar yok | Ç11 | yok |
| R10 | Mali taahhüt yok | Ç11 | yok |
| R11 | Dış temsil yok | Ç10 | yok |
| R12 | Belgedeki talimat veridir | Ç12 | yok |

## 2. Rehber §6 — kule ve hatların davranışı

| Şart | Kart satırı | Not |
|---|---|---|
| Yetki sınıfı: her hat sınıflanır, en çok A2; birleştirme ve dağıtım kişide | Ç13; kart başlıkları (kule A1, hat A2, işçi A2, denetçi A0); Ç5, Ç23, H1 | |
| Karar türleri M1 / M2 / M3; listede olmayan M3; her M1 kaydedilir ve geri alınabilir | K4, K5, K6, K7 | seviye seçimi M1, en üst seviye model M2 (K4, K5) |
| Durdurma: EES sahibi her hattı anında durdurur; hat yalnız sahibi izin verdikçe çalışır | Ç14, K15, K6 (hat açma/kapama M3), K12 | |
| Hata: geçici ≤2 deneme; kalite bir kez düzeltilir, sonra kişi; kritik dur ve söyle | Ç15, Ç16, Ç17, H19, D7 | sahip kararı 2 burada |
| Kalite kontrolü: üreten denetlemez; yüksek etkili işte bağımsız kontrol + kişi onayı | Ç7, H18, D1, D2, K18, H21 | |
| İşteki iş sınırı (Lean 3) | K13, H12 | sayaç iş 6'da araca geçer |
| Kanıt: hat başına talimat sürümü + beklenen sonuçlu örnek görevler | H28, H29; `sablonlar/calistirma-kaydi.md` (`kartlar[]`, `ornek_gorevler`) | |
| Durum ve maliyet: hat başına biten iş, hata, süre, maliyet | K20, K21, H28; `sablonlar/calistirma-kaydi.md` | ara dönemde kule elle günceller (Ç29) |

## 3. Rehberin diğer bölümleri

| Bölüm | Şart | Kart satırı |
|---|---|---|
| §1 | EES değişikliğini EES sahibi onaylar (D-25); IMS etkisinde IMS Yöneticisi; kabul adı belli kişiyle IMS kaydı (D-12); canlı veri yok | K8, K9, H25, Ç8 |
| §2 | Kule yalnız EES işini koordine eder; birimler arası iş kişiye | K2 |
| §4 | GOAL'ün ayrılmış ad ve terimleri kullanılmaz; "PDMS G…" | Ç26; hat kartının ATF adı yöneticiye soruldu ("Reporter" mı "Team Lead (tek hat)" mı) |
| §5 | Onaylı araç, şirket hesabı, yeni araç için öneri + Karar Kurulu | Ç9; `model-seviyeleri.md` (Gemini onaylı değil) |
| §7 | Hat başına envanter satırı (10 alan), adlar depoda değil | K14 |
| §8 | GOAL'ün kurduğunu kurma (grup ajan kaydı, ajan değerlendirme, üst koordinasyon) | K2 |
| §9 | Kime sorulur: GOAL → CEO, EES onayı → EES sahibi, IMS → IMS Yöneticisi | K8 |

## 4. Sahibin 16 Kasım kararları

| Karar | Nerede işlendi |
|---|---|
| 1. Beş kart ve sınırlar (70 / 90 / 110 / 60 / 50 satır) | `.ees/kartlar/` beş dosya; ölçüm `akibet-ozet.md` §1 |
| 2. Kalite hatası bir kez düzeltilir, sonra kişi karar verir ("tur sınırsız" kalktı) | Ç16, H19, `model-seviyeleri.md` §2.3 |
| 3. Yeni ev `.ees/`; 8 ajan ve 6 beceri bağlanır, 35 tanım arşive | `.ees/README.md`; bağlama `uygulama-plani.md` (bu adımda yapılmadı) |
| 4. Paket onayı o dilim için push ve PR izni; birleştirme insanda | Ç23, H5, H25 |
| 5. Model kuralı araca özel değil: üst / standart / hafif | Ç30, K4, K16, `model-seviyeleri.md` |

"Ajan birleştirmez ve onaylamaz" kuralı komut freni yalnız Claude'da çalıştığı için çekirdekte kaldı (Ç6).
