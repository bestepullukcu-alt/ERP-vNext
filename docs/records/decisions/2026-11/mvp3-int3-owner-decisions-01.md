# MVP-3 INT3-01…03 — sahip kararları (Beste, 2026-11-24)

Kaynak: `docs/records/audits/2026-11/mvp2-mvp3-ct-handoff-01/MVP-3-DEVIR.md` §3 (satır 27-34) ve CT envanteri (2026-11-24).
MVP-3, MVP-5 ile paralel ilerler; ilk adımlar kodsuzdur.

- **INT3-01 (MOD-0193 → MOD-0290 gerçek ürün doğrulaması):** Active olmayan (taslak ya da pasif) ürün BOM'a **girmez**
  (`lifecycleStatus` ≠ Active → ret). Açık kalan, Ali'ye sorulan iki karar: PRODUCT-MASTER v1.1 (`1.1.0-draft.16`) dondurma tarihi
  ve Manufacturing → MDM doğrulama çağrısının kimliği (kullanıcı token'ı iletimi mi, Auth servis token'ı mı). Bu kararlardan sonra
  MOD-0193 için dar iş paketi (200'lük parçalama, 400 / 503 ayrımı, `getSkuUom`, istemci testleri).
- **INT3-02 (Manufacturing → merkezi denetim, BL-501):** erteleme **kalkmaz**; bağlantı diğer MVP'lerle birlikte, merkezi ekip
  tarafından MVP'lerden sonra yapılır (`730a7fa1f` kararı aynen).
- **INT3-03 (korelasyon, MOD-0040):** Blueprint MOD-0040 (Canonical ID & Correlation Standard) **yalnız standart** olarak kayda
  geçer; yeni modül kimliği açılmaz. Manufacturing'deki `CorrelationMiddleware` + `ICorrelationContext` uygulaması ve Gateway
  korelasyon kuralı (her istek Gateway'de kendi `X-Correlation-Id`'sini alır; çağıranın değeri `X-Client-Correlation-Id`) referans
  davranıştır. F-0193-09 (ad çakışması) bu kayıtla kapanır: repo satırı MOD-0040 = MOD-0288'in takma adı, Blueprint MOD-0040 =
  standart; MOD-0040-FU02 Blueprint anlamına bağlıdır. Kayıt defteri notu ve MOD-0193 satırı (port 5070, durum `done`) düzeltildi.
- **Sınır:** Kod, ortam açma ya da dağıtım izni değildir.

## Ek — INT3-01 için Ali'nin kararları (2026-11-24)

- **PRODUCT-MASTER v1.1:** `1.1.0` olarak dondurulmasına evet. Dondurma adımlarını MDM kulesi yapar; tarih Ali tarafından ayrıca
  bildirilecek.
- **Kimlik:** Manufacturing MDM'ye **Auth servis token'ı** ile gider; kullanıcı token'ı iletilmez. MDM'nin servis kimliğine açılması
  GOAL ve SupplyChain'in aynı ihtiyacıyla tek işte yapılacak (Ali); tarih netleşince bildirilecek.
- **Sonuç:** INT3-01 iş paketi iki ön koşulu bekler: v1.1 dondurma ve MDM servis kimliği işi. Bu arada BOM kullanıcılarına
  `mdm.product-master.read` gerekmez (kullanıcı token'ı iletilmediği için).

## Ek — Beste 2026-11-25 (sohbet): INT3-01 aktif/pasif kontrolü olmadan şimdi başlar

- **Karar (Beste, MVP-3 sahibi):** "bunu şuan aktif mi pasif mi bakmadan yapalım daha sonra Ali o özelliği eklediğinde biz aktif pasif
  kontrolünü ekleriz Aliye de metin gönderelim". INT3-01 iki parçaya ayrılır:
  - **DL-1 (şimdi):** Manufacturing ürün doğrulaması dev ortamında `Permissive` yerine `Http` (yalnız `exists`, PRODUCT-MASTER v1.0
    `/validate`); 200'lük parçalama, hata eşlemesi, kimlik seam'i (`IProductMasterCallerCredentials`) — geçici uygulama kullanıcı
    token'ı iletimi. İş paketi: `execution/domains/supply-chain-execution/work-packs/WP-INT3-01-DL1-manufacturing-product-validation-http.md`
    (MOD-0193 §7 / F-0193-07 kapsamında; yeni MOD kimliği yok).
  - **DL-2 (Ali'nin 1.1.0 dondurmasından sonra):** `lifecycleStatus` ile pasif ürün reddi.
  - **DL-3 (MDM servis kimliği işinden sonra):** kullanıcı token'ı yerine Auth servis token'ı.
- **Ali'nin 2026-11-24 kaydıyla ilişkisi:** Ali'nin "kullanıcı token'ı iletilmez" ve "iş paketi iki ön koşulu bekler" sonuçları DL-2 / DL-3
  için geçerli kalır. DL-1'in geçici kullanıcı token'ı iletimi ve bu sürede BOM kullanıcılarının `mdm.product-master.read` ihtiyacı Ali'ye
  soruldu (S-A1); cevap gelene kadar değişiklik yalnız dev ortamında etkindir (`appsettings.json` `Permissive` kalır).

## Ek — Ali 2026-11-25 cevabı ve Beste'nin K-2…K-4 seçimleri

- **Ali (2026-11-25, Beste'ye mesaj, sohbete yapıştırıldı):** "Lütfen kullanıcı token'ı ile geçici adımı başlatmayın; 24 Kasım kararımda
  Manufacturing'in MDM'ye servis token'ı ile gitmesi ve kullanıcı token'ının iletilmemesi var. Gerçek doğrulamaya MDM servis kimliği hazır
  olunca geçelim; o zamana kadar bugünkü mod kalsın." Teknik sorular (istemci adı, audience, izin / audience kontrolü, kiracı ve tüzel kişi
  bağlamı, Gateway, ITEM_NOT_ACTIVE, 1.1.0 alanları ve tarih) Ali tarafından ölçülüp ayrıca cevaplanacak.
- **Sonuç (CT):** WP-INT3-01-DL1 **beklemede**. Kullanıcı token'ı iletimi yapılmaz; Manufacturing `Permissive` kalır. DL-1 kodu MDM servis
  kimliği işinden sonra servis token'ı ile yazılır (DL-1 ve DL-3 birleşir); DL-2 (lifecycle) 1.1.0 dondurmasından sonra.
- **Beste K seçimleri (2026-11-25), iş paketi açıldığında uygulanır:** K-2 = (a) 503 için yeni `DEPENDENCY_UNAVAILABLE`, ekran mesajı ve 7 dil
  genelleşir; K-3 = (a) MDM 400 → 500 `INTERNAL_ERROR` + ayrı günlük satırı, sözleşme değişmez; K-4 = (a) MDM 401 / 403 → 503.
