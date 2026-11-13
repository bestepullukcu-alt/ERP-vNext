# NOT — Canlıya çıkış kontrol listesi (ziyaret çalışma alanı + bağlı işler)

> **Durum:** canlıya alırken ürün sahibi tek tek inceler. Kullanıcı kararı (2026-10-14): bu maddeler yol haritasının **sonunda**.
> Canlı kiracı: `be823006` (ditenteknoloji.com). Geçmiş canlı adımlar CT hafızasında `live-golive-be823006`.

## A. Bizim işlerden kalan canlı kararlar

| # | Madde | Kaynak | Ne yapılmalı |
|---|---|---|---|
| A1 | **K-INFRA-1:** doğrulanmış referans verisinin (GSKU birimi, pazar) canlıya yüklenme yolu yok | WP-VW-W4 §37 INFRA | Üretim için yükleme yolu kararı |
| A2 | **K-INFRA-2:** canlı referans kiracı | WP-VW-W4 §37 INFRA | Ayrılmış bir GUID olmalı; `be823006` olamaz |
| A3 | **K-INFRA-3:** canlı gizli kimlik bilgisinin sahibi ve döndürülmesi | WP-VW-W4 §37 INFRA | Sahip kararı |
| A4 | **MDM RabbitMQ** canlıda | W4-CFG | Ayarlanmazsa onaylanan rakip önerisi MDM'ye işlenmez |
| A5 | **QR imza anahtarı** (`crm-visit-qr-signing:<keyId>` + `Crm:VisitQr:ActiveKeyId`) | WP-VW-W3E §2.2 | Anahtar yoksa canlıda `qrMode` açılmamalı. Anahtar eklenince CRM yeniden başlatılmalı (önbellek). Sahiplik A3 ile aynı soru. |
| A6 | **W3E yetkileri** (`crm.visit-qr.*`, `crm.visit-evidence-flags.*`) | W3E-CFG | Canlıda rollere verilmesi |
| A7 | Diğer 7 ülke pozisyonuna kişi ataması | live-golive | Şu an yalnız UZRO dolu |

## B. PR #144 (Kapanış hattı) ile gelenler — main'e girince

| # | Madde | Ne yapılmalı / dikkat |
|---|---|---|
| B1 | **`admin@diten.com` artık yalnız `/platform/login`'den giriyor** (platform ve kiracı hesapları ayrıldı, BL-521) | Kiracı ekranları için bu hesapla giriş yapılmaz. Canlıda kiracı yöneticisi olarak gerçek kullanıcılar (ör. Beste Hanım, Cihan Bey) kullanılır. |
| B2 | **Canlıda `admin@diten.com` silinmemeli, e-postası değiştirilmemeli** | WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) bitene kadar; yoksa seed onu yeniden oluşturur |
| B3 | **`Security:TrustedProxies` (BL-626)** | Canlıda Web ve gateway adresleri bu listeye yazılmalı. Giriş hız sınırı istemci adresini buradan okur. Yazılmazsa herkes aynı adresten geliyor görünür ve sınır yanlış kişileri kilitleyebilir. |
| B4 | **Workflow: onaylayan = başlatan ise akış başlamıyor** (`WORKFLOW_NO_ELIGIBLE_APPROVER`) | Bir onay pozisyonunda tek kişi varsa, o kişi aynı akışı başlatmamalı. Pozisyonlara en az iki kişi atanmalı ya da hazırlayan başka biri olmalı. |
| B5 | Yerel geliştirme (canlı değil): kiracı yöneticisi `kiraci.admin@diten.com` + `"DevSeeds": { "TenantAdmin": true }` | Yalnız Development |
| B6 | MDM GSKU / LSKU / Global Ürün oluşturma Workflow onaylı + servisler arası güven ayarı | Canlıda ilgili Workflow şablonları ve güven anahtarları kurulmalı (PR #144 belgeleri) |

## C. Yalnız geliştirme ortamı (canlıyı etkilemez)
- **BL-645:** iki farklı dev referans kiracı (`00000000-…-0001` ↔ `0b7d0048-…`); tek kiracı seçilmeli.
- **BL-643 / FIX-CRM-SCAFFOLD-MONGO:** yol haritası sıra 13.
