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

- **2026-11-13 — T-5 izni verildi (Ali, MDM sahibi; kaynak: Beste'nin sohbete aktarımı "MOD-0179 Ali MDM iznini verdi"):** 3PL ortağı MDM'de iş ortağı olarak tanımlanır; MDM tarafındaki değişikliği merkez geliştirir (XMC-001 izni MDM sahibinden alındı). Kapsam ve dosyalar MOD-0179 paketinde adıyla yazılır; MDM'de yalnız eklemeli değişiklik. Sırada MOD-0179 paketi.
- **2026-11-13 — Paket r1 soruları Q-1…Q-9 = öneri kabul (Beste, "hepsi öneri gibi"; kaynak: sahip sohbet cevabı; paket
  `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` r1 `:1080-1089`):** Q-1 izin alt ad alanı
  `warehouse.third-party-logistics.*` (`.antigravity` PKS-001 satırı onaydan önce); Q-2 EDI taşıma v1 SFTP (AS2 FU-0179-AS2); Q-3 EDIFACT
  INSDES, DESADV (iki yön), RECADV, INVRPT; X12 940, 943, 944, 945, 846; Q-4 v1'de "Düzeltme öner" düğmesi yok, "Çözüldü" yalnız gerekçeyle
  (FU-0179-ADJUST); Q-5 rotalar `/Warehouse/ThirdParty*`; Q-6 varsayılan eşikler sevk onayı 120 dk, giriş onayı 240 dk, stok raporu 26 sa,
  hata hedefi %1; Q-7 askıdayken bekletilenler işlenmez, önce etkinleştirilir; Q-8 giriş ön bildirimi v1 elle (FU-0179-AUTOADVICE); Q-9
  3PL deposunda depo başına tek stoklanabilir lokasyon (MOD-0173-FU02 ile). Açık kalanlar: Q-10…Q-13 (Ali), Q-14…Q-17 (CT), Q-18, Q-19.
- **2026-11-13 — r1 incelemesi sahip kararları (Beste, "hepsi öneri gibi"; inceleme `docs/records/reviews/MOD-0179/2026-11-13-aca86685.md`):**
  F-11 — **v1.0 yalnız API kanalı** (DL-1…DL-7 + DL-10); SFTP dosya kanalı (DL-8) ve EDI kanalı (DL-9) **v1.1**'e alınır (T-1'in EDI kapsamı
  v1.1'de karşılanır); DL-1, DL-1a (sözleşme taslağı, ortak, kanal, eşleme, eşik) ve DL-1b (mesaj defteri, istisna kutusu, bekletilenler)
  olarak bölünür. F-8 — ekranlarda ücret için **"v2 · planlı" notu bulunur** (mockup tur 2 kararıyla aynı). F-5 — ham mesaj içeriğinde
  kişisel veri **90 gün** sonra kaldırılır (içerik kişisel verisiz yeniden yazılır; sha256 ve kanonik özet kalır); aynı kural MDM iş ortağı
  Contacts için ayrıca değerlendirilir. F-10 — sır kasası FU'su gelene kadar **ara çözüm**: kanal kaydı yalnız sır referansı tutar, değeri
  işletim yerleştirir (Platform `TenantMessagingSettings.CredentialSecretRef` + `SecretReferenceResolver` emsali); ekran yalnız "ayarlı"
  bilgisini gösterir; DL-4 kasa FU'sunu beklemez.
- **2026-11-13 — Q-10 MDM iş ortağı kimliği (Beste, "tamam"; Ali'ye bilgi verilecek):** MDM iş ortağı ana verisi **MOD-0049-FU01
  "Business Partner Master Foundation"** (domain master-data-management, servis Diten.MdmService; ebeveyn Blueprint MOD-0049 Master Data
  Management, SoR "golden entities"). Ön kontrol: `verify_module_id.py --check-id MOD-0049-FU01 --name "Business Partner Master Foundation"
  --parent MOD-0049` OK. MOD-0179 köprüdür (Blueprint "Not SoR"), iş ortağını yalnız okur. Kapsam dar: taraf kaydı ve roller (v1 rolü
  ThirdPartyLogistics); eşleştirme / birleştirme, tedarikçi ve müşteri SoR'u kapsam dışı. Ayrı paket gerekir; MOD-0179 DL-3 bu FU'ya
  bağımlılık olarak yeniden yazılır (r2). Kimlik kaydına MOD-0049 ve MOD-0049-FU01 satırları eklenir. Aynı firmanın tedarikçi (SUP-),
  3PL ve CRM müşterisi kayıtlarının bağlanması backlog BL-665.
- **2026-11-13 — MOD-0012-FU01 (Tenant Runtime Secret Vault) sahip soruları (Beste, "öneri gibi olsun"; paket r1 23):** Q-6 3PL gelen
  imza sırrı değişiminde eski ve yeni sır en çok 72 saat birlikte geçerli (geçiş penceresi); Q-7 yönetim (meta) ekranı v1'de **yok**;
  Q-8 resolve okumaları denetim kaydına yazılmaz, yalnız sayaç; Q-9 meta sorgusu kalır; Q-14 LegalEntityId kasada ayrıca doğrulanmaz
  (çağıran servis doğrular); Q-15 SMTP sırrının taşınması ayrı FU. Ali'ye kalan: Q-1 KEK yeri / erişimi, Q-2 yedek / kurtarma, Q-3
  Inventory servis istemcisi, Q-4 servisler arası TLS, Q-13 FU onayının ana paket durumunu bekleyip beklemediği.
