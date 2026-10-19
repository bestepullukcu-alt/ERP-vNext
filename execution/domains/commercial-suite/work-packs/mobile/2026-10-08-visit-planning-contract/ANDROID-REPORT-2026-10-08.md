# Android — Ziyaret Planlama sözleşmesi 2026-10-08 uyum raporu

- **Tarih:** 2026-10-08
- **Kaynak sözleşme:** `MOBILE-CONTRACT-2026-10-08-visit-planning.md` (backend / Control Tower). Not, `test/crm-content-visit-e2e` test dalına dayanıyor; main'e PR ile gelecek. Bu rapor test dalındaki davranışı production'da var saymaz.
- **Dal:** `fix/visit-planning-contract-2026-10-08`, taban `master` @ `18e4509d` (`fix/visit-create-planned` merge'i dahil). Commit bilgisi §9'da.
- **Worktree:** yok (AGENTS.md: tek klasör, `git worktree add` yasak). Checkout, sahibi olan "Android Anti-Fraud Continuation" oturumu işini merge edip master'a dönene kadar beklendi; o süre boyunca yalnız okuma yapıldı.
- **Push / merge / deploy:** yapılmadı.

## 1. Sonuç

| Durum | Kapsam |
|---|---|
| **PASS** | Görüntüleme kapsamındaki zorunlu uyumlar (aşağıdaki 1, 2, 5\*, 6, 7, 8, 9, 11, 12, 13, 14) ve hata kodu eşlemesi; birim + cihaz testleriyle kanıtlı. |
| **DEFERRED** | Mobil planlama akışları (hedef/ürün seçimi, `dayPins`, haftalık `apply`, `reopen`, önizleme, 409 sürüm akışı, `planning_session_exists` ile mevcut planı açma). Android'de planlama ekranı ya da `visit-plan` ucu yok, onaylı kapsam kanıtı da yok (§3). |
| **N/A (planlama yokken)** | 3 (`weekStart` gruplama), 4 (taslak hafta), 5 (hafta rozeti), 10'un kayma/sabit uyarıları: bu veriler yalnız `visit-plan` önizleme/oturum yanıtlarında; Android bu uçları çağırmıyor. |
| **BLOCKED** | Gerçek backend doğrulaması: yeni alanlar test dalında; yetkili test ortamında gerçek yanıtlarla deneme yapılmadı. Arapça yerelleştirme yok (§6). |

**Kanıtlı tamamlanma:** görüntüleme kapsamındaki 11 zorunlu maddenin 11'i kodda ve testte tamam (fixture/MockWebServer ve cihazda bağlanan satırlarla). Gerçek backend E2E: **0** — "uçtan uca tamamlandı" değildir. Planlama akışlarının tamamı DEFERRED.

## 2. Başlangıç denetimi

- Modüller: `:app`, `:core`, `:MyPossibility` (ziyaret), `:CRM` (iş yeri/doktor), `:legacy`, `:antifraud*`, `:hr:*`.
- Ağ: Retrofit + OkHttp, `@ErpApi` istemcisi bearer token, `X-Tenant-Id` ve yenilemeyi ekliyor; zarf `ApiResponse<T>` (`Response<T>`, veri `data` altında) ve `unwrap()`. Serializer: kotlinx.serialization, `NetworkJson.default` (`ignoreUnknownKeys = true`, `explicitNulls = false`). Global ayara dokunulmadı.
- UI: XML + ViewBinding (Fragment, `RecyclerView`/`ListAdapter`), Compose değil. ViewModel'ler `StateViewModel`; ziyaret kaynağı seçimi oturum (tenant + kullanıcı) anahtarlı ve yalnız bellekte.
- `grep` ile doğrulandı: Android kodunda `visit-plan`, `preview`, `apply`, `reopen`, `weekNumber`, `dayPins` yok.

## 3. Kapsam kararı

Android bugün yalnız **planlanan ziyaretleri** (`/api/crm/planned-visits` liste/ayrıntı/oluştur/güncelle/onayla/iptal/arşiv) ve **takvimi** (`/api/crm/visit-report/calendar`) kullanıyor. Haftalık planlama (oturum, önizleme, onay, yeniden açma) için kod, ekran ya da onaylı proje kararı bulunamadı. Bu yüzden talimattaki kurala göre yeni planlama ekranları yapılmadı; bağımsız sözleşme düzeltmeleri tamamlandı.

## 4. Değişiklikler

### MyPossibility (ziyaret)
- **B1 / M1:** `CreatePlannedVisitDto` ve `UpdatePlannedVisitDto`'dan `strategyTemplateId`, `campaignId`, `segmentId` **özellik olarak kaldırıldı** (null değil, anahtar hiç yok). `EditVisitUseCase` artık okunan plandan bu üç değeri geri göndermiyor (bugüne kadar gönderiyordu — 06 Ekim §1 davranışı). Bu alanlar hiçbir ekranda gösterilmiyordu; değişmedi.
- **M8 / §4.2:** yeni `PlannedVisitContentItemDto`; liste ve ayrıntı DTO'larına `contentItems` (yok / `null` / `[]` hepsi boş liste). `order` ve `warnings[]` tür belirsizliği için ham JSON tutuluyor; tutarsız biçim yalnız o değeri kaybettirir, planı değil. Ürün adı `productName ?? productCode`, ikisi de yoksa yerel "Ürün adı yok".
- **Liste satırı:** ürünler satırı (`visitItemProducts`, yoksa gizli); durum rozeti `cancelled` için "İptal edildi" (diğer kodlar mevcut politika gereği olduğu gibi).
- **Ziyaret ekranı (ayrıntı):** salt-okunur plan bloğu (`visitEditPlanDetails`): durum, iptal notu (`week_reopened` için "Haftası yeniden açıldığı için iptal edildi. Raporlanamaz."), sıklık (`unknown` → "Dönemde 1 (varsayılan)", `resolved` → "Dönemde N", `conflict` → "çakışan kurallar"), sıralı ürün listesi, rol (`promo` → Tanıtım, `non-promo` → Hatırlatma, null → Tanıtım) ve ürün uyarıları (`no_products`, `no_approved_content`, `ambiguous_journey`; bilinmeyen kod olduğu gibi).
- **M3:** kaynak seçici artık GUID göstermiyor ("Kaynak 1 / Tür: person").
- **§4.7:** `MyResourcesDto` → `displayName`, `countryCode` (ikisi de opsiyonel; `countryCode` hiçbir kurala bağlanmadı).
- **M13:** `VisitNameText` — `androidx.core.text.BidiFormatter.unicodeWrap`; bağlam yönü, adın yerleştiği etiketin kendi yönünden alınıyor (aşağıdaki not). Hedef adı (kurum/doktor) ve ürün adları sarmalanıyor; istek verisi değişmiyor.
- **§6 hata kodları:** `VisitErrorCodes` + `VisitMessages`: `planning_session_exists`, `invalid_week`/`week_in_past`, `week_already_approved`, `week_not_approved`, `reopen_reason_required`, `invalid_day_pin`, ürün ret kodları, `planning_session_not_empty`/`already_committed`, `visit_not_yet_due`. Kod `errors[1]`'den okunuyor (mevcut eşleyici). 503 `product_lookup_unavailable` yeniden denenebilir; 403 → yetki, 404 → bulunamadı (sahiplik); bilinmeyen kod → genel ret mesajı.

> **BiDi notu:** İlk sürümde bağlam yönü cihazın düzen yönünden alınıyordu. Cihaz görüntüsünde, Arapça cihazda İngilizce etiket içinde virgüllerin kaydığı görüldü ("Products: ,Aspirin…"). Uygulamada Arapça dize olmadığından etiketler İngilizceye düşüyor ve paragraf LTR kalıyor. Düzeltme: bağlam = etiketin ilk güçlü harfinin yönü. Arapça etiket gelirse ad yalıtılır, LTR etikette gereksiz işaret eklenmez.

### CRM
- **§8 referans etiketleri:** `PublishedValueDto.labelFor(dil)`: önce `attributes.label_<arayüz dili>`, sonra `label`, sonra `code`. `attributes` artık `Map<String, JsonElement>`; sayı/bool nitelik tüm seti düşürmez.
- **§8 il:** `CityRefText` — `TR-34-ISTANBUL` → "İstanbul" (son parça, Türkçe büyük harf kuralı). Biçime uymayan değer olduğu gibi. Bu kesin il etiketi değil: kodda noktasız ı olmadığı için `IGDIR` → "İgdir" (testte belgelendi). Müşteri/kişi ayrıntısı ve müşteri formu özetinde kullanılıyor; saklanan/gönderilen değer değişmiyor.

### Değişen dosyalar
MyPossibility: 3 DTO + 1 yeni DTO, `EditVisitUseCase`, `VisitFailure`, `VisitPlanFacts` (yeni), `VisitRows`, `VisitScreens`, `VisitTargetText`, `VisitResourcePicker`, `VisitEditFragment`, `VisitEditViewModel`, `VisitNameText`/`VisitPlanText` (yeni), 2 layout, `values` + `values-tr`. CRM: `ReferenceDataDtos`, `RemoteCustomerReferenceDataSource`, 3 fragment, `CityRefText` (yeni). Testler: §5.

## 5. Doğrulama

| Kontrol | Sonuç |
|---|---|
| `:MyPossibility:testDebugUnitTest` | **359 test, 0 hata** |
| `:CRM:testDebugUnitTest` | **239 test, 0 hata** |
| `:app:assembleDebug` | BAŞARILI (`app-debug.apk`) |
| `:MyPossibility:lintDebug`, `:CRM:lintDebug` | **0 hata**. Uyarılar (UseTomlInstead, UnusedAttribute `accessibilityHeading`, Overdraw, AccessibilityFocus, RtlEnabled, UseKtx) eklenen satırlarda değil; önceden var. |
| `:MyPossibility:connectedDebugAndroidTest` (emulator-5570, AVD AF_Closure) | **84 test, 0 hata** (yeni `VisitPlanningDisplayInstrumentedTest` 3 test dahil; kaynak seçici a11y testleri GUID'siz etikete göre güncellendi) |
| `:app:connectedDebugAndroidTest`, paket `com.diten.ditenmultiply.erp` | **115 testten 114 geçti.** `ErpContactFiltersFlowTest.rowsShowTheTenantsLabels…` paket koşusunda bir kez düştü (rozet "cs-active", beklenen "Aktif"); aynı sınıf tek başına **2/2 geçti**. Fixture `attributes:null`, yani yeni etiket yolu yalnız `label` döndürüyor. Kararsız test olarak değerlendirildi, ayrıca izlenmeli. |

Cihaz: `emulator-5570`, sistem görüntüsü `sdk_gphone16k_arm64`; diğer oturum boşta olduğunu bildirdikten sonra `ANDROID_SERIAL` ile seçildi, kapatılmadı.

**Yeni / güncellenen testler**
- `VisitPlanningContractTest` (gerçek `NetworkJson`): oluşturma/güncelleme gövdelerinde üç anahtarın **hiç olmaması** ve DTO'da özellik olarak bulunmaması; `contentItems` sırası, ad yoksa kod; yok/null/boş liste; bilinmeyen alanlar (`steps`, `claims`, `pathVersion:"v3"`, `future`), `order:"first"`, karışık `warnings`; `week_reopened` iptali; elle iptal; `frequencyStatus` sözlüğü; `resources/me` `countryCode` null/eksik.
- `EditVisitUseCaseTest` (MockWebServer, gerçek Retrofit HTTP gövdesi): okunan planda kampanya/strateji/segment varken PUT gövdesinde yok.
- `PlannedVisitRequestSerializationTest`, `PlannedVisitContractParityTest`: anahtar kümeleri B1'e göre; `contentItems` ek alan.
- `VisitPlanningPresentationTest`: §6 kodları → mesaj; 503 yeniden denenebilir; 403/404; rol/uyarı etiketleri.
- `VisitPlanningDisplayInstrumentedTest` (cihaz): BidiFormatter çıktısı `RLM LRE "018 KLİNİK" PDF RLM`; Arapça ve İngilizce etiket bağlamı; RTL açık/koyu ve Türkçe açık satır bağlama; plan bloğu Türkçe metinleri.
- CRM: `CityRefTextTest`; `CustomerReferenceSetsContractTest` → `label_tr` / `label_ar` / boş `label_tr` / null `attributes` / sayı-bool nitelik.
- Sınır testleri: `VisitDtoBoundaryTest` (yeni DTO dosyası), `VisitCodeDisplayTest` (sözleşme §5 kodları için tek izinli etiket dosyası `VisitPlanText`).

**Cihaz kanıtı** (`docs/reports/android/evidence/visit-planning-contract-2026-10-08/`; fixture verisi, gerçek backend değil):
- `bidi-018-klinik-unwrapped.png` — Arapça etiket, yalıtımsız: "KLİNİK 018" (ters).
- `bidi-018-klinik-wrapped.png` — yalıtımlı: "018 KLİNİK" (doğru).
- `visit-row-rtl-light.png`, `visit-row-rtl-dark.png`, `visit-row-tr-light.png` — uzun kurum adı, "İptal edildi/Cancelled", ürün kodu yedeği, "Ürün adı yok".
- Sınır: satır görüntüleri pencereye bağlanmadan çiziliyor. Metin düzeyindeki BiDi kanıtlı; satırın tam RTL aynalanması (chevron yeri) bu görüntülerle kanıtlanmış değil.

**Çalıştırılmayan kontroller:** gerçek backend/test kiracısı ile sözleşme yanıtları; uygulamaya giriş yapılmış canlı ekranlarda Arapça RTL gezinti (test kimlik bilgisiyle oturum açma yapılmadı); `:app:assembleRelease` ve release lint; iOS (kapsam dışı).

## 6. B1–B9 / M1–M13 uyum tablosu

| # | Kod konumu | Durum | Not |
|---|---|---|---|
| B1 | `CreatePlannedVisitDto`, `UpdatePlannedVisitDto`, `EditVisitUseCase.toUpdateDto` | **PASS** (düzeltildi) | Güncellemede üç alan geri gönderiliyordu; kaldırıldı. Okuma DTO'ları (`selection`, `content.strategyTemplateId`, `campaignId`) yalnız ayrıştırma için duruyor, gösterilmiyor. |
| B2 | `ResourceApi`, `ServerVisitResourceSource`, `VisitResourceSelection` | **PASS** (mevcut) | Kaynak yalnız `GET /api/crm/resources/me`; seçici yalnız bu yanıttaki aktif öğeleri sunuyor; oturum anahtarlı, çıkış/tenant değişiminde siliniyor. 403 `resource_not_caller` → kendi mesajı. |
| B3 | — | DEFERRED | Oturum oluşturma yok. `planning_session_exists` mesajı eşlendi. |
| B4 | — | DEFERRED | `apply` çağrılmıyor; tüm dönemi onaylayan eski yol da kullanılmıyor. |
| B5 | — | N/A | `weekNumber` hiçbir yerde kullanılmıyor (§9-2). |
| B6 | `VisitPlanFacts`, `VisitPlanText`, `VisitRows` | **PASS** (görüntüleme) / DEFERRED (`reopen` ucu) | İptal "İptal edildi" olarak görünüyor; `week_reopened` için özel not. Android'de rapor yazma eylemi yok; iptal edilen planda onay/iptal zaten kapalı (`VisitActions`). |
| B7 | `VisitListFilter.includeArchived`, `VisitRepository` | **PASS** (mevcut) | Planlanan ziyaret listesinde `includeArchived` yalnız açık arşiv süzgecinde gönderiliyor (test var). Oturum listesi yok. |
| B8 | — | **PASS** (mevcut) | Planlanan ziyaret listesi yalnız sunucudaki kayıtları gösteriyor; taslak hafta hiçbir yerde yerel kayıt olarak yazılmıyor (`data/local` dizinleri boş; Room varlığı yok). |
| B9 | — | N/A | Mobil gün hesaplamıyor; sabit gönderimi DEFERRED. |
| M1 | yukarıdaki B1 | **PASS** | |
| M2 | B2 | **PASS** | |
| M3 | `VisitTargetText`, `VisitRows`, `VisitResourcePicker` | **PASS** | Hedef adları sunucudan; ad yoksa CRM'den okuma ya da "Ad bilinmiyor"; kaynak seçicideki GUID kaldırıldı. Ara ad istekleri eski sunucu için duruyor (sunucu adı varsa çağrılmıyor). |
| M4 | — | N/A | Hafta gruplaması ve rozeti yalnız önizlemede. |
| M5 | — | **PASS** | Yalnız onaylı (yazılmış) ziyaretler görünüyor; sözleşmeye göre doğru. |
| M6 | — | DEFERRED | |
| M7 | `VisitPlanText.status`, `VisitPlanFacts` | **PASS** | Gösteriliyor (gizlenmiyor). Mevcut bir gizleme tercihi yok; durum süzgeci aynen duruyor. `reportStatus` ile `planStatus` karıştırılmıyor: takvimdeki rapor durumu ayrı satır (`reportState`). |
| M8 | `PlannedVisitContentItemDto`, `VisitProduct` | **PASS** | |
| M9 | — | DEFERRED | Ürün seçimi isteği yok. |
| M10 | `VisitFrequency` | **PASS** | |
| M11 | — | N/A / DEFERRED | `shifted[]`/`pinOverflow[]`/`pinWarnings[]` yalnız önizlemede. Ürün uyarıları yerelleştirildi. `consent_blocked` hedef için mobil ziyaret üretmiyor (üretim yok). |
| M12 | B7 | **PASS** | |
| M13 | `VisitNameText` | **PASS** (metin düzeyi, cihazda) | Arapça yerelleştirme yok; RTL denetimi `ar` yerel ayarı + İngilizce yedek dizelerle yapıldı. |

**Talimattaki ek maddeler:** 12 — `countryCode` null, eksik ürün adı, bilinmeyen sözlük değerleri çökmeden ayrıştırılıyor (testli). `visitModel`, `calendarStatus`, `portfolioStatus` alanları okunmuyor (önizleme yok); bunlardan iş kuralı üretilmedi. 13 — CRM referans etiketleri ve `cityRef` (yukarıda). Tenant/çıkış: yeni durum yalnız ViewModel örneğinde (`planFacts`); yeni önbellek eklenmedi; mevcut kaynak seçimi temizliği değişmedi.

## 7. Backend §9 yanıtları

1. **B1'e aykırı istek kaldı mı?** Vardı: Android, planlanan ziyaret **güncellemesinde** okunan `campaignId`, `strategyTemplateId`, `segmentId` değerlerini geri gönderiyordu (06 Ekim §1'e göre). Bu dalda kaldırıldı: üç alan ne oluşturma ne güncelleme gövdesinde var (DTO'da özellik bile değil). Plan (oturum) istekleri Android'de hiç yok. `contentSource` hâlâ okunan değerle geri gönderiliyor; B1 listesinde değil, ama strateji türetmesiyle ilgisi varsa bildirin.
2. **`weekNumber` nerede kullanılıyor?** Hiçbir yerde. Takvimin hafta görünümü cihazın yerel hafta başlangıcıyla kendi tarih aralığını hesaplıyor; önizleme verisi kullanılmıyor.
3. **Mobil planlama kapsamı:** ilk aşamada **yalnız görüntüleme** (planlanan ziyaret listesi, ayrıntı, takvim). Hedef/ürün seçme, gün sabitleme, hafta onayı/yeniden açma için onaylı ürün kararı yok; §4.3–4.5 bizim için şimdilik opsiyonel. Karar verilirse DEFERRED maddeler uygulanacak.
4. **`week_reopened` iptalleri:** **göstermeyi** seçtik: listede/takvimde "İptal edildi" rozeti, ayrıntıda "Haftası yeniden açıldığı için iptal edildi. Raporlanamaz." Kullanıcı durum süzgeciyle ayıklayabiliyor.
5. **Örnek yanıt istiyoruz:** test kiracısından gerçek (i) `GET /api/crm/planned-visits` ve `/{id}`, `contentItems` dolu, `productName` null olan bir öğeyle; (ii) `week_reopened` ile iptal edilmiş bir ziyaretin ayrıntısı; (iii) `GET /api/crm/resources/me` (`displayName`, `countryCode`'un **üst düzeyde mi `items[]` öğesinde mi** olduğu belirsiz; Android ikisini üst düzeyde opsiyonel okuyor); (iv) `contentItems[].warnings[]` ve `order` türleri; (v) **takvim** (`visit-report/calendar`) `cancellationReason` ya da `contentItems` taşıyacak mı?

## 8. Açık konular, bağımlılıklar, eksik sözleşmeler

- Backend değişiklikleri main'e gelmedi; gerçek yanıtla doğrulama yok (BLOCKED).
- Takvim satırı `cancellationReason` taşımıyor; takvimde iptal "İptal edildi" olarak görünüyor, `week_reopened` ayrımı yalnız ayrıntı ekranında.
- İptal edilmiş planın formu hâlâ düzenlenebilir görünüyor (önceden de böyleydi); backend'in iptal edilmiş planda güncellemeye izin verip vermediği sözleşmede yok. Rapor yazma eylemi Android'de olmadığı için "raporlanamaz" kuralı ek kod gerektirmedi.
- Arapça yerelleştirme yok; tam ekran RTL gezinti (giriş yapılmış) denenmedi.
- `ErpContactFiltersFlowTest` paket koşusunda kararsız.
- SB-3c ziyaret yap/tamamla ve Faz 6 T1–T3 uçları yok; uydurulmadı. MDM izni eksikken `productName` null başarı sayılıyor; mobilde yetki değişikliği yok.
- Anti-fraud varsayılanları, global serializer, session/auth ve ERP/HR kodu değiştirilmedi.

## 9. Dal / commit / dağıtım

- Dal: `fix/visit-planning-contract-2026-10-08` (taban `master` @ `18e4509d`), tek yerel commit (hash: `git log -1 fix/visit-planning-contract-2026-10-08`).
- Worktree yok. Push, merge, deploy, yayın: **yapılmadı**.
- Sahipsiz yerel değişiklikler (`core/build.gradle.kts` boş satır, `gradlew.bat`, `.idea/*`, `.vscode/`, `.artifacts/`, `ditennewcalender/`) dokunulmadan bırakıldı, commit'e alınmadı.
