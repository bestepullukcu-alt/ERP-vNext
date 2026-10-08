# YOL HARİTASI — İçerikten ziyarete (Bilgi Yolu Stüdyosu → Etkileşim Yolculuğu → Ziyaret)

> **CT (SoR), 2026-10-01.** Kullanıcının önerdiği sıra: Stüdyo kalanları (KP-UI-3, SB-4, SB-2b, temizlik) → Etkileşim Yolculuğu → Ziyaret.
> Kaynaklar: `DESIGN-KP-STUDIO-knowledge-path-studio.md` §8-9, `DESIGN-SB-3-multi-product-visit-content.md` §4-5, `SCMM-studio-knowledge-bridge-decision.md` §6.

## 0. Bugün (test/crm-content-visit-e2e)
| Alan | Durum |
|---|---|
| Bilgi Yolu Stüdyosu | KP-1, KP-2, KP-2-CFG (6 MLR şablonu canlı), KP-3, KP-4, KP-UI-1, KP-UI-2 — **E2 kabul, E4 yok** |
| Strateji şablonu | SB-3a (rol + yolculuk), SB-3-UIa (form) — E2 |
| Ziyaret planlama | SB-3b (çok ürün, rotasyon, ürün başına aşama, `JourneyProgress` okunur) — E2 |
| Eksik | KP-5, KP-UI-3, SB-4, SB-2b, temizlik, yolculuk uyumu, SB-3c, SB-3-UIb, SB-3-MOB |

**Risk:** 9 paket canlıda hiç denenmedi (E4 birikiyor). Bir sonraki büyük pakete başlamadan canlı kontrol gerekli.

## 1. Sıra analizi
**Bağımlılıklar (kod ve tasarımdan):**
- **KP-UI-3 → KP-5'e bağlı:** kilitli `safety` bloğu güvenlilik metninden, `legal-footer` ülke yasal profilinden, marka kiti üründen gelir (DESIGN-KP §2.4, §8). KP-5 de **kullanıcı kararını** bekliyor (§9.1: güvenlilik metnini kim onaylar).
- **SB-4 → KP-UI-3'e bağlı:** HTML çıktısı sayfa / blok modelinden üretilir. Ayrıca **altyapı kararı** gerekir (sunucu tarafı tarayıcı motoru ile HTML → PDF).
- **SB-2b yeniden tanımlanmalı:** "Çıktılar paneli" SB-2'nin geri izini okuyacaktı; SB-2 KP-4'te emekli oldu. Yeni anlamı: zincir şablonundan kurulan **Bilgi Yolları** (ülke / dil / sürüm / yayın) + onları kullanan yolculuklar. Bağımsız, küçük.
- **Temizlik:** kodu bağımsız; veri kısmı (canlı "test" kapsamı, demo yolları KP-114 / 201 / 888) kullanıcı onayı ister.
- **Yolculuk → stüdyoya bağlı DEĞİL, KP-3'e bağlı:** ziyaret, aşamanın yolunun **yayındaki adımlarını** okur (SB-3b). Sayfa tasarımı ve HTML çıktısı yalnız yolun *çıktı dosyasını* güzelleştirir; ziyaret sözleşmesini değiştirmez.
- **Ziyaret yürütme (SB-3c) → yolculuğa bağlı:** canlıda aşamalı, onaylı yollara bağlı bir yolculuk olmadan "Ziyaret yap" denenemez.

**Sonuç:** Senin sıran uygulanabilir, ama iki düzeltme öneriyorum:
1. **KP-UI-3'ten önce KP-5** (zorunlu bağımlılık) — KP-5 kararını şimdi vermen gerekiyor.
2. **Önce bir canlı kontrol durağı** (Faz 0) — 9 paketlik E4 borcu, en büyük paket (sayfa tasarımcısı) üstüne binmeden kapanmalı.

Ayrıca bir seçenek: **yolculuğu stüdyonun sunum fazından (KP-UI-3 + SB-4) önce yapmak** saha zincirini daha erken kapatır (sayfa tasarımı sonra yalnız çıktıyı değiştirir). Kullanıcı stüdyoyu önce bitirmeyi seçti; bu belge o sırayı izler, istenirse Faz 4 Faz 2'nin önüne alınabilir (bağımlılık yok).

## 2. Fazlar

### Faz 0 — Canlı kontrol durağı (E4)
- **Kullanıcı:** fleet restart; TPL-ALMIBA-01 zincirini yayınla; giriş.
- **CT:** Bilgi Yolu kur → MLR'ye gönder (`KP-MLR-TR` örneği) → 3 adım onay / ret yorumu (sema) → render → yayın (gönderen ≠ yayınlayan) → kullanım; İddialar "Nerede kullanılıyor"; İçerik Setleri menüde yok; strateji şablonu formunda rol + yolculuk; kapasitede 3 / 3.
- Çıkan hatalar → küçük düzeltme paketleri (FIX-*).

### Faz 1 — Küçük ve paralel
| Paket | Kapsam | Katman | Karar |
|---|---|---|---|
| **SB-2b → KP-CH-1 Zincir çıktıları** | zincir düzenleyicide "Bilgi Yolları" paneli: bu zincirden kurulan yollar (ülke, dil, sürüm, MLR / yayın durumu) + kullanan yolculuklar; bağlantılar | CRM okuma + Web | — |
| **CLN-1 Kod temizliği** | salt okunur set / kapsam repository'leri + class-map `LegacyScope`, set revizyon okuma uçları, deprecated auth açıklamaları; PlannedVisits Web `ReadFallback` kalıntısı | CRM + Web | veri 0 doğrulanınca |
| **CLN-2 Veri temizliği** | canlı "test" kapsamını arşivle; demo yolları KP-114 / 201 / 888 arşivle ya da bırak | betik (kullanıcı çalıştırır) | **kullanıcı** |
| **KP-5 Ana veri** | marka kiti (ürün), ülke yasal profili, güvenlilik metni (ürün × ülke × dil, onaylı), onaylı görsel kütüphanesi (Belge Yönetimi) | CRM + Web | **güvenlilik metni onayı: Regülasyon tek adım mı, tam MLR mı?** |

### Faz 2 — Sayfa tasarımcısı (KP-UI-3)
- Kaynak: mockup v2 sayfa bileşeni (`mockups/kp-studio/kp-studio-page-component-v2.decoded.html`): sayfa şeridi, blok paleti, tuval, özellikler, sayfa uyumu.
- Model: `PathPage` / `Block` (DESIGN-KP §2.4) — CRM tarafı `Pages[]` doldurulur; kilitli bloklar (iddia, güvenlilik, yasal altbilgi, onay kodu).
- Revizyon dondurması sayfaları da kapsar; inceleyici görünümü sayfa bazlı yorum.
- Büyük paket → muhtemelen **KP-UI-3a** (sayfa modeli + CRM) ∥ **KP-UI-3b** (tasarımcı ekranı), sonra **KP-UI-3c** (uyum kuralları, erişilebilirlik, RTL).

### Faz 3 — HTML çıktı (SB-4)
- HTML paketi (saha sunumu) + aynı kaynaktan arşiv PDF'i; aynı parmak izi; idempotent render.
- **Karar:** sunucu tarafında HTML → PDF motoru (ör. başsız Chromium) — kurulum / lisans / sunucu kaynağı.
- Mobil gösterim sözleşmesi (HTML paketi nasıl indirilir) SB-3-MOB ile birlikte.

### Faz 4 — Etkileşim Yolculuğu uyumu
Yolculuk (MOD-0162-FU05) Stüdyo'dan önce yazıldı; yeni modele göre açıklar (CT kod okuması):
| # | Açık | Öneri |
|---|---|---|
| J1 | Aşamaya **onaysız eski yol** bağlanabiliyor (yolculuk tarafında `IsLegacyUnapproved` / zincir kontrolü yok). **SB-3b çözücüsü de eski yolu dışlamıyor** (DESIGN-KP §5 "onaysız eski yol ziyarette kullanılmaz" — SB-3b doğrulamasında kaçırıldı, CT 2026-10-01) | Aşama yalnız zincirli + MLR'li + yayındaki yolu kabul eder; çözücü eski yolu `stage_path_unapproved` ile düşürür |
| J2 | Yolculuğun kimliği: ürün (konu) + kitle + dil var, **ülke yok**; aşamaların yolları farklı ülke / dilde olabilir | Yolculuğa ülke + dil kimliği (yolla aynı); aşama yolları aynı ürün / ülke / dil |
| J3 | Web formu: aşama varsayılanı hâlâ `pinned`; yol seçici onaysız yolları da gösteriyor | Varsayılan `latest-published`; seçici yalnız uygun yollar + yayın durumu |
| J4 | Strateji satırı belirli bir yolculuk **sürümüne** bağlı; yolculuk yeni sürümle yayınlanınca ürün ziyaretten düşer | Satır yolculuğun güncel sürümünü izlesin (yol aşamasındaki gibi) — **kullanıcı kararı** |
| J5 | Yolculuk yayını tek kişi + SoD; içerik zaten yollarda MLR'den geçti | Yayın SoD yeterli mi, yolculuk da onay akışına mı girsin — **kullanıcı kararı** |
| J6 | F-RBAC (yolculuk yetki anahtarları), canlı duman testi hiç yapılmadı | grant betiği + duman |
| J7 | Ekran stüdyo diline uymuyor (aşama → yol kartları, ziyaret sırası önizlemesi) | İsteğe bağlı mockup |
| J8 | Veri: CEJ-27 aşamasız, CEJ-ALMIBA-HD taslak | Faz 0 / E2E sırasında ALMIBA yolculuğu kurulur |
- Paketler (öneri): **CEJ-1** (J1 + J2 + J4, CRM) ∥ **CEJ-UI-1** (J3 + J7, Web) → **CEJ-CFG** (J6).

### Faz 5 — Ziyaret
| Paket | Kapsam | Ertelenmiş karar (o sayfaya gelince) |
|---|---|---|
| **SB-3c** | "Ziyaret yap" → `in-progress` + taslak rapor (içerik gerçek ilerlemeyle tazelenir); "Tamamla" → rapor submit + `completed` + anlatılan ürünlerin ilerlemesi (başa dönüş); `ContentActuals[]`; replan sonrası içerik tazeleme | başlatma penceresi; "anlatıldı" varsayılanı; kitle uyuşmazlığı (uyarı / düşür) |
| **SB-3-UIb** | planlama önizlemesinde ürün başına içerik; planlanan ziyarette "Ziyaret yap" / "Tamamla" + "anlatıldı" işaretleri; rapor ürün listesi | — |
| **SB-3-MOB** | mobil sözleşme notu (`contentItems[]`, başlat / tamamla uçları, HTML paketi) | — |
| ARCH GATE | rep-facing ekranda play / kampanya görünmez (memory `rep-facing-visit-play-campaign-invisibility`) | SB-3-UIb öncesi doğrula |

## 3. Bekleyen kullanıcı kararları (faza göre)
| Faz | Karar |
|---|---|
| 1 | KP-5: güvenlilik metni onayı (Regülasyon tek adım / tam MLR); CLN-2: "test" kapsamı + demo yollar arşivlensin mi |
| 1 | İddialar listesi 15 sapma: "bilinen sapma" mı |
| 1 | REQ-WCN-01: "aynı kişi birden çok MLR adımı onaylayamaz" seçeneği (şablon başına, varsayılan kapalı) |
| 3 | SB-4: HTML → PDF motoru |
| 4 | J4 satır yolculuğun güncel sürümünü izlesin mi; J5 yolculuk onayı |
| 5 | başlatma penceresi, "anlatıldı" varsayılanı, kitle uyuşmazlığı (kullanıcı: "o sayfaya gelince") |
