# PR — Ziyaret Planlama yeniden tasarımı (Faz 1–4) + içerik → ziyaret düzeltmeleri

> **Dal:** `test/crm-content-visit-e2e` → `main` (bestepullukcu-alt/ERP-vNext) · aç: https://github.com/bestepullukcu-alt/ERP-vNext/compare/main...test/crm-content-visit-e2e · **Hazırlayan:** CT, 2026-10-08 · **PR'ı açan:** kullanıcı
> Bu dosya PR açıklamasının kaynağıdır. Aşağıdaki "PR açıklaması" bölümü olduğu gibi yapıştırılabilir.

---

## PR açıklaması (yapıştır)

### Özet
Temsilcinin **kendi haftasını planladığı** yeni Ziyaret Planlama (mockup v2) ve arkasındaki planlama motoru. Ayrıca içerik → ziyaret uçtan uca testinde bulunan düzeltmeler (bilgi zinciri, oyun / segment / sıklık, ziyaret yürütme).

Main'deki son birleştirme (PR #135, 6 Ekim) ile bu dal arasındaki **103 commit**; 382 dosya. Yarısından fazlası CRM ve Web, geri kalanı test ve iş paketi belgeleri.

### Ne geliyor
**Planlama motoru (CRM)**
- **Dönem planı, haftalık onay:**
  - temsilci + dönem başına **tek plan**;
  - hafta hafta onay ve gerekçeli yeniden açma (raporsuz ziyaretler `week_reopened` ile iptal);
  - sıklık dönem türüne göre bütün döneme dağıtılır. (WP-VP-3A)
- **Gün dengeleme:** günlük bütçe dönem kapasitesinden; yarım gün; hafta sonu / tatil yok; sığmayan sonraki haftaya; izni engelli doktor planlanmaz. (3B)
- **Ürün listesi:**
  - doktor başına temsilci seçimi + önerilen ürünler;
  - kaynak (play / rep-pick / last-visit / portfolio);
  - dönen sıra;
  - rol sınırı taşması. (3C)
- **Doktor dönem durumu:** gereken / yapılan / kalan / son ziyaret / bu hafta görülmeli; plan hedefleri ve bağlı eczaneler toplu okuma. (3D)
- **Coğrafi gün ataması ve gün sabitleri:**
  - uzak kurum boş ya da az dolu güne;
  - temsilci bir ziyareti ya da kurumu güne sabitler;
  - sabitlenmeyenler yerinde kalır;
  - kümeler arası yol sayılır;
  - doğru kayma nedeni (`capacity_full` / `no_near_day`). (4E, 4G, 4I-BE)
- **Okuma alanları:** ürün adları (MDM, toplu ve hata vermeyen okuma), `resources/me.countryCode`, ziyaret başına `reportStatus`, temsilci adı. (4A, 4G)
- **Faz 2 temeli:**
  - görünen adlar (GUID yok);
  - temsilci = oturumdaki kişi + sahiplik (başkasının kaydı 404);
  - bölge evreni;
  - oyun / kampanya / segment sunucuda türetilir;
  - iş yeri listesi aktif kişi sayısı + süzgeç. (VP-2, VP-2B, VP-FIX-1/2)

**Web — Ziyaret Planlama (mockup v2)**
- **Liste, yeni plan çekmecesi ve detay üstü:** durum, eylem kartı, kapasite kartları. (4B)
- **Hedefler:**
  - bölge hesapları listesi;
  - doktor durumu, hızlı filtreler;
  - ürün çipleri, seçici, toplu uygula;
  - seçim özeti. (4C, 4H, 4I)
- **Haftalar:**
  - dönem şeridi, gün dökümü, boş süre;
  - güne taşıma;
  - doktor dönem paneli;
  - boş hafta boş durumu. (4D, 4H)
- **Rota:** durağı başka güne taşıma. (4F)
- **Dil ve RTL:** tarih ve sayılar uygulama dilinde (tarayıcı dili değil); Arapça RTL yön yalıtımı (adlar, sayı oranları). (4H, 4I)

**İçerik → ziyaret düzeltmeleri**
- Ziyaret yürütme planlanan içeriği gösterir; rapor planlanan yolculuk / aşamayı taşır; ileri tarihli ziyaret kapatılamaz (`409 visit_not_yet_due`). (E2E-FIX-1)
- Bilgi zinciri yazım düzeltmeleri. (E2E-FIX-2)
- Tek geçerli oyun sürümü, segment referans değerleri, Türkçe arama. (E2E-FIX-3)

### Davranış değişiklikleri / mobil
- Mobil ekiplere sözleşme notu iletildi: [MOBILE-CONTRACT-2026-10-08](mobile/2026-10-08-visit-planning-contract/MOBILE-CONTRACT-2026-10-08-visit-planning.md).
- Öne çıkanlar:
  - istemciden gelen oyun / kampanya / segment yok sayılır;
  - onay `weekStart` ile hafta hafta;
  - önizleme slotunda `weekNumber` artık dönem haftası dizini;
  - taslak haftalar planlanan ziyaret değildir;
  - arşivli planlar listede varsayılan gelmez.
- **API yanıt şekli değişmedi.** Yeni alanların hepsi ek alan.

### Yetki / veri / kurulum
- **Yeni izin anahtarları:** `crm.planned-visit.read-all`, `crm.visit-plan.read-all`.
  - Yalnız **açık grant** (`ExplicitGrantOnlyPermissions`); modül eşitlemesi ya da SuperAdmin vermez.
  - Yönetici rolüne vermek için `scripts/rbac/grant_visit_planning_read_all_97c5.py`: kuru çalışma varsayılan, `--role <ad> --apply`.
- **Saha temsilcisi rolü:** ürün adlarını görmek için `mdm.global-products.read` gerekir. Rol yetkilerinden verilecek.
- **Göç / seed / indeks yok.** Yeni alanlar ek alan; eski kayıtlar okunur. Eski tek seferlik planlar "eski plan" olarak sabit hafta gösterilir.
- **Veri (kod dışı):**
  - kurum türü, uzmanlık ve il Türkçe adları referans verisinde girilmeli (MOD-0048 değeri tek etiket taşır);
  - temsilcilerin bölge atamaları;
  - 28 Ekim yarım günü çalışma takviminde.
- **Kurulum:** CRM, Web, Auth ve Platform yeniden başlatılır (Auth yeni izin anahtarlarını kataloğa yazar).

### Testler
| Paket | Sonuç |
|---|---|
| CRM Application | **2457 / 0 / 5** (3 tekrar; tek koşuda görülen `TerritoryImportExportFu08Tests` kırmızısı kararsız, bu PR'da değişmeyen bir test) |
| Web | **802 / 0** |
| Auth Application | **1099 / 0** |
| Platform Application | 5357 / **468** — main'de de aynı **468** (küme birebir aynı; yerel `mongod` / üretim kapısı ortam testleri). Bu PR'ın Platform değişikliği (CRM manifest) testleri **11 / 0** |
| Mimari | 38 / **1** — AUD-001, aşağıda |

### ⚠ Bilinen kırmızı testler — birleştirme yönetici onayıyla (ürün sahibi kararı, 2026-10-08)
1. **`AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`**
   - Denetim kaydına bağlanmamış **27** CRM yazma komutu: KnowledgePath, RegulatoryText ve `ReopenPlanningWeek` komutları.
   - Karar: CRM'in merkezi denetim kaydına bağlanması **ayrı PR** (Faz 8, AUD-CRM-1). Bu PR'da sayı artmadı.
2. **`JwtClockSkewGuard`** (HCM / TEP): main'de de kırmızı; bu PR'dan kaynaklanmıyor.

### Elle doğrulama
- Her iş paketi CT tarafından birim ve mimari testlerle kabul edildi (E2), sonra yerel ortamda canlı denendi (E4, kiracı 97c5): liste, yeni plan, Hedefler, Haftalar, Rota, onay / yeniden açma, güne taşıma, ürün seçimi, Arapça RTL.
- Ayrıntılar iş paketlerinin §37 bölümlerinde: `execution/domains/commercial-suite/work-packs/WP-VP-*`, `WP-E2E-FIX-*`, [yol haritası](ROADMAP-visit-planning.md).

🤖 Generated with [Claude Code](https://claude.com/claude-code)

---

## CT notları (PR açıklamasına girmez)
- **Yedek dal:** `backup/crm-content-visit-e2e-presync-20261008` (senkron öncesi baş).
- **Main senkronu:** `origin/main` = PR #135 birleştirmesi (`b994c813`). İçeriği bu dalın 6 Ekim hali (`fe370c9f`); birleştirme çakışmasız, içerik farkı sıfır (`6bdd77024`).
- **PR'dan sonra:**
  - Faz 6 (Planlanan Ziyaretler sayfası), mobil ekip main üzerinden düzeltmelerini yaparken;
  - SB-3c;
  - Faz 8 (mimari testi yeşile çeviren ayrı PR).
