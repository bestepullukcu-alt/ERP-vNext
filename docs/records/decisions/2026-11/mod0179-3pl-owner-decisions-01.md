# MOD-0179 3PL Entegrasyonu — sahip kararları 01 (2026-11-13)

Kaynak: brief `execution/domains/supply-chain-execution/work-packs/mockups/3pl-integration/BRIEF-MOD-0179-3pl-integration.md` (T-1…T-6) ve
mockup tur 1 (`3PL Entegrasyonu MOD-0179 (tek dosya).html`, M-1…M-16; CT incelemesi 2026-11-13). Paket yok; kararlar paketin girdisidir.
Sahip: Beste (MVP-5 WMS CT oturumu `ca7382cf`).

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| T-1 | EDI standardı | **EDIFACT ve X12 ikisi de**; bir ortakta ikisi birlikte olabilir (sahip mockup sırasında cevapladı). Mesaj listesi paket yazımında kesinleşir — mockup varsayımı: EDIFACT INSDES, DESADV, RECADV, INVRPT · X12 940, 856, 945, 944, 846 | Beste | 2026-11-13 |
| T-2 | Ücret / fatura mutabakatı | **v1'de YOK; tasarım ileride eklenecekmiş gibi yapılır** (genişleme noktası: ücret kalemleri ve fatura mesajları için yer ayrılır, ekran / izin v1'de yok). Fatura verisinin kaynağı, karşılaştırılacak kalemler ve finans bağlantısı için Treasury'ye bilgi notu gider (M-10…M-14 bu kararla v2'ye kalır). Mockup'ın "v1'de var" ifadesi bu kararla değişir | Beste | 2026-11-13 |
| T-3 | 3PL lot / SKT bizdekinden farklı | **İstisna açılır**; çözülene kadar bizdeki kayıt geçerli (mockup K-12) | Beste | 2026-11-13 |
| T-4 | Kanal sırları (API anahtarı, SFTP parolası) | **Platform'un sır kasası** (sahibi merkez / Beste); ekranda yalnız "ayarlı · son değişim" | Beste | 2026-11-13 |
| T-5 | 3PL ortağının tanımı | **MDM'de iş ortağı** olarak; MOD-0179 ona bağlanır. MDM tarafındaki değişikliği **merkez geliştirir — Ali'den izin istenir** (XMC-001; MDM sahibi Ali) | Beste (Ali izni bekleniyor) | 2026-11-13 |
| T-6 | Gateway rotaları | integration-agent'ın işi (`ocelot.json`) — değişmez kural | — | — |
| M-2 | "Gecikmeli" eşiği | **Ayar sayfası:** eşikleri kullanıcılar (yetkiliyle) ortak bazında belirler | Beste | 2026-11-13 |
| M-4 | Yoldaki miktarın hesabı | Açıklama istendi (sahip "anlamadım") — CT sade açıklama ve öneri verdi; karar bekliyor | Beste | açık |
| M-6 | Toplu yeniden işleme üst sınırı | **200 kayıt** (öneri kabul) | Beste | 2026-11-13 |
| M-7 | 7 dil | Kural: **en, tr, fr, es, zh, ar, ru** — mockup'taki Almanca çıkar, Çince girer | kural | — |
| M-15 | "3PL ortağı" terimi | **Kalır** (öneri kabul) | Beste | 2026-11-13 |
| M-16 | Kanal kapatma | **Gerekçe ister**; kapalı kanala gelen mesaj reddedilir ve istisna kutusuna düşer (öneri kabul) | Beste | 2026-11-13 |
| — | Örnek veride Türkiye 3PL'i | Önemli değil, düzeltilmez | Beste | 2026-11-13 |

## Ek (2026-11-12, aynı gün)
- **M-4 kapandı — öneri:** yoldaki miktar = 3PL stok raporunun üretildiği saate kadar gönderilmiş ama onayı gelmemiş sevk talimatları ve mal
  kabul ön bildirimleri (Beste: "önerdiğim gibi").
- **Mockup tur 2 yaptırılmayacak** (Beste: "mockup'ı tekrardan yaptırmaya gerek yok, biz bunları kendimiz düzeltiriz"): CT'nin tur 2
  düzeltme listesi (7 dil en/tr/fr/es/zh/ar/ru; ücret ekranı ve izni v1'de yok, "v2 · planlı" notu; eşik ayar sayfası; kanal kapatma
  gerekçeli, kapalı kanala gelen mesaj ret + "kanal kapalı" istisnası; toplu yeniden işleme ≤ 200 ve uyarı; ortak MDM iş ortağından
  seçilir, ad / adres / iletişim MDM'den, düzenlenmez; sırlar Platform sır kasasında, yardımda bir cümle) **paket yazımında doğrudan
  uygulanır**; mockup v1 bu farklarla paketin girdisidir.
