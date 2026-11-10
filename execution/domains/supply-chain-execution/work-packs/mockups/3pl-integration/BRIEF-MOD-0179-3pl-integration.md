# MOCKUP BRIEF — MOD-0179 3PL Entegrasyonu (Dış Depo)

> Tarih: 2026-11-10 · Hazırlayan: MVP-5 CT. **Paket yok** — bu özet paketten önce yazıldı (sahip kuralı: her yeni modülde önce mockup
> özeti). Kaynaklar: Blueprint 8.8 (MOD-0179: "3PL Integration Health; Reconciliation Workbench; Exception Inbox", Integration Bridge,
> 3PL-BUNDLE, **SoR değil** — stokun sahibi WMS / Inventory), WMS-BUNDLE taslağı §14 (dış kaynaklı görev kapıları: `origin: External`,
> `sourceSystem`, `externalRef`; `operatingMode = external-integrated`), CT kuyruğu `WMS-MVP5-MOD0179` (W-3 ertelemesi kalktı,
> 2026-11-02). Kapsam kararı (Beste): **EDI + API + dosya** (üç kanal).
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Mevcut ürün düzenine ve depo ekranlarına uy. Kurallar henüz pakette yazılı
> değildir; mockup bu kuralları **önerir**, paket kesinleştirir. Emin olmadığın her yeri "açık soru" olarak yaz.

## BAĞLAM
- **Kullanıcılar:** entegrasyon sorumlusu (3PL ortağını ve kanalı tanımlar, mesaj hatalarını çözer), stok / mutabakat sorumlusu (bizim
  stokumuzla 3PL'in bildirdiği stoğu karşılaştırır), depo yöneticisi (3PL'in performansını izler). Çok şirketli ilaç dağıtımı; bazı
  depolar kendi WMS'i olan dış lojistik firmasınca (3PL) işletilir.
- **Ne:** dış depoya sevk / giriş talimatı göndermek, 3PL'den gelen onayları (alındı, toplandı, sevk edildi, stok raporu) almak ve
  bunları bizim stok defterimize doğru yansıtmak. Kanallar: **EDI** (ör. EDIFACT DESADV / RECADV / INVRPT), **API** (REST, imzalı),
  **dosya** (SFTP üzerinden CSV / XML).
- **Ne değil:** 3PL deposunda raf / görev yönetimi (onu 3PL'in WMS'i yapar; o depoda MOD-0178 iç görev üretmez). Ücret / fatura
  mutabakatı ilk sürümde yok (öneri — açık soru).
- **Akış (çıkış):** sipariş sevke hazır → 3PL'e sevk talimatı gider → 3PL "toplandı / sevk edildi" bildirir → MOD-0178 akışında
  Shipped olur ve stok düşer. **Akış (giriş):** bekleyen alım → 3PL'e ön bildirim → 3PL "teslim alındı" (miktar, lot, SKT) → stok girer.
  **Stok raporu:** 3PL günlük stok listesi gönderir → mutabakat masasında farklar çıkar.

## 1) ENTEGRASYON SAĞLIĞI — `/Integrations/ThirdPartyLogistics` (menüde)
### 1.1 KPI kartları
- Bağlı 3PL ortağı · Son 24 saatte mesaj (gelen / giden) · Başarısız mesaj (açık) · Son stok raporu yaşı (saat) · Açık mutabakat farkı.

### 1.2 Ortak listesi (kartlar ya da tablo)
- Ortak adı, işlettiği depo(lar), kanal rozetleri (EDI / API / Dosya), bağlantı durumu (Sağlıklı / Gecikmeli / Hatalı / Kapalı),
  son başarılı mesaj zamanı, açık hata sayısı.
- **Satır menüsü:** Aç, Bağlantıyı sına, Askıya al / Etkinleştir (gerekçe). **"Sil" yok.**

### 1.3 Ortak ayrıntısı (sekmeler)
- **Genel:** ortak bilgisi, bağlı depolar, iletişim, sözleşme notu.
- **Kanallar:** her kanal için ayar özeti (EDI: kimlikler, mesaj türleri; API: uç nokta, kimlik doğrulama türü — **sır gösterilmez**,
  yalnız "ayarlı · son değişim"; Dosya: SFTP klasörü, dosya deseni, zamanlama). Düzenleme yetki ister.
- **Eşlemeler:** bizim ürün kodu ↔ 3PL ürün kodu; bizim depo / durum ↔ 3PL durumu. Eksik eşleme uyarısı.
- **Mesajlar:** zaman, yön, tür, iş belgesi (sipariş / alım no, bağlantılı), durum (Alındı / İşlendi / Hatalı / Yinelenen — yok sayıldı),
  ham içerik görüntüle (yetkiyle; kişisel veri maskeli).

## 2) MUTABAKAT MASASI — `/Integrations/ThirdPartyLogistics/Reconciliation` (menüde)
- Üst: ortak + depo + rapor tarihi seçimi (en son stok raporu varsayılan).
- **Satır:** ürün, lot, SKT, bizim stok, 3PL stoğu, fark (+/−), yoldaki (gönderilmiş ama onaylanmamış) miktar, açıklanmış fark,
  **açıklanmamış fark**, durum (Uyumlu / Fark / İnceleniyor / Çözüldü).
- **Eylemler:** Not ekle · İnceleniyor işaretle · "Düzeltme öner" (stok düzeltmesi **bu ekranda yazılmaz**; düzeltme ilgili stok ekranında
  onayla yapılır, bağlantı verilir) · CSV dışa aktar.
- Kural (öneri): yoldaki miktar farktan otomatik düşülür; yalnız açıklanmamış fark "Fark" sayılır.

## 3) İSTİSNA KUTUSU — `/Integrations/ThirdPartyLogistics/Exceptions` (menüde)
- **Satır:** ortak, kanal, mesaj türü, iş belgesi, hata türü, ilk görülme, deneme sayısı, durum (Açık / Yeniden deneniyor / Çözüldü / Yok
  sayıldı), sorumlu.
- **Hata türleri (öneri):** biçim hatası · bilinmeyen ürün eşlemesi · bilinmeyen lot · miktar uyuşmazlığı (talimattan fazla / az) ·
  geç gelen onay (belge kapanmış) · yinelenen mesaj · bağlantı / kimlik hatası.
- **Eylemler:** Yeniden işle · Eşleme ekle ve yeniden işle · Yok say (gerekçe) · Sorumlu ata. Toplu yeniden işleme yalnız aynı hata türünde.

## 4) DURUMLAR (her ekranda çiz)
- Boş liste ("Henüz 3PL ortağı tanımlı değil" + yetkiliyse "Ortak ekle"); yükleniyor; hata ("Entegrasyon servisine ulaşılamadı").
- Yetkisiz kullanıcı (UAS-001): sol menü ve üst çubuk kalır; içerikte **tek bir açıklama**. İskelet, yönlendirme, yalnız toast yok.
- Ekranlar izne göre çizilir (ham mesaj görüntüleme ve kanal ayarı ayrı izin — öneri).
- Askıya alınmış ortak: gri rozet, mesaj gönderilmez, gelenler "bekletildi" olarak tutulur (öneri).
- Sır / anahtar / parola **hiçbir ekranda** görünmez.

## 5) GENEL KURALLAR
- 7 dil; Arapça sağdan sola. Her sayfada "Yardım" paneli; tur yok.
- Masaüstü öncelikli; 375 px'te liste ve istisna kutusu okunabilir, yatay sayfa kaydırması yok.
- Klavye ile kullanılabilir; durumlar yalnız renge dayanmaz.

## 6) İSTENEN ÇIKTI
- Tek dosyalık tıklanabilir HTML prototip (örnek veriyle: 2 ortak — biri EDI, biri API + dosya) + ekran metinleri listesi.
- Ekranlar: Entegrasyon sağlığı (liste + KPI + ortak ayrıntısı sekmeleri), Mutabakat masası, İstisna kutusu, durum örnekleri.
- Açık sorular listesi. Bilinen açık sorular:
  - **T-1** İlk sürümde hangi EDI standardı (EDIFACT mi, X12 mi, ikisi mi) ve hangi mesajlar?
  - **T-2** Ücret / fatura mutabakatı v1'de var mı? (öneri: yok)
  - **T-3** 3PL'in bildirdiği lot / SKT bizdekinden farklıysa hangisi esas — otomatik ret mi, istisna mı? (öneri: istisna)
  - **T-4** Kanal sırları (API anahtarı, SFTP parolası) nerede tutulur — Platform sır kasası mı? (Platform sahibi, XMC-001)
  - **T-5** Ortak tanımı MDM'de mi (iş ortağı) yoksa bu modülde mi?
  - **T-6** Gateway rotaları integration-agent'ın işi (`ocelot.json`).
