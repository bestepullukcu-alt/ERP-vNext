# Diten ERP — Android (Kotlin) Mobil Uygulama · Yol Haritası (SoR)

**Sahibi (Owner):** gm@grandmedical.eu · **CT:** CONTROL TOWER · **Oluşturma:** 2026-09-10
**Durum:** BAŞLADI — **M0.1** (5ae82588) · **M0.2 `:core:network`** (f256df89) · **M0.3 `:core:database` + M0.5 `:core:design`** (0456e913, paralel) CT-ACCEPTED + commit (2026-09-11). Sonraki: **M0.4 `:core:auth`** (M0.2✓ + M0.3✓ hazır), sonra M0.6 (`:core:sync`+`:core:common` genişletme) → M0.7 (`:app` shell). Toplam 5 modül, 35 mobil unit test yeşil.

> **M0.3 (`:core:database`):** `ScopeKeys(tenantId, legalEntityId)` her satıra `@Embedded` · `DitenDatabase` v1 + schema export (VCS'te) · `CachedReadinessDao` backend legal-entity semantiği: write-scope izolasyon + roll-up (`IN`) · Hilt `DatabaseModule` · yıkıcı-fallback yok, `MigrationTestHelper` · Robolectric testleri sızıntısızlığı **kanıtlıyor** (tenantB aynı `le-1`'i paylaşsa da tenantA scope'una sızmıyor). 11 test.
> **M0.5 (`:core:design`):** ölçülen web paleti (primary `#696CFF`…) → Material3 light/dark, `dynamicColor=false` · readiness durum→renk sistemi (saf-Kotlin JVM-test, 11 test) · bileşenler StatusChip/Scaffold/TextField/DropdownField/Button/`UiStateContainer`(→`UiResult`)/ListRow, hepsi `@PreviewLightDark`.
>
> **Bilinen düzeltilecek:** ktlint eklentisi bu AGP'de yalnız `.kts` denetliyor (Kotlin kaynak-set görevi kaydetmiyor); Kotlin lint şu an **detekt** ile sağlanıyor (temiz). Ayrı küçük WP ile ktlint Kotlin kapsamı düzeltilecek.

> **M0.2'de teslim edilenler (ölçülen backend sözleşmesine birebir):** Retrofit 2.11 + OkHttp 4.12 + kotlinx.serialization · `NetworkEnvelope<T>` `{data,statusCode,isSuccessful,errors}` → `UiResult` + tipli `NetworkError` · HeaderInterceptor (Bearer + `X-Tenant-Id` + `X-Legal-Entity-Id`, opt-in marker, null-omit, login hariç) · redaksiyonlu debug logging · AuthAuthenticator (401→refresh→tek retry, döngü-korumalı) · auth-seam arayüzleri (`SessionTokenProvider`/`TokenRefresher`) + no-op binding (M0.4 override eder, döngü yok) · AuthApi (`api/tenant-auth/login`,`/mfa/verify`,`api/auth/refresh-token`,`/logout`) · per-domain network-security-config (cleartext yalnız emülatör/localhost) + cert-pin hook · `BuildConfig.BASE_URL`/`NETWORK_LOG`. CT: sıfırdan 10 MockWebServer testi yeşil + R8 release derleniyor.

> **Baseline (ölçüldü, AS ile kuruldu):** proje kökü `mobile-android/DitenERP` · AGP **9.4.0** · Kotlin **2.2.10** · Gradle **9.6** · Gradle daemon JDK **25** (foojay auto-provision) · compileSdk/targetSdk **37** · minSdk **26**. Not: yol haritasındaki eski pinler (AGP 8.5/Gradle 8.9/JDK 17/compileSdk 34) bu ölçülen yığına yukarı hizalandı.
>
> **M0.1'de teslim edilenler:** `build-logic` convention plugin'leri (`diten.android.application/library/hilt`) · `:core:common` (UiResult MVI temeli) → `:app` multi-module bağ · Hilt (KSP, DitenApp + @AndroidEntryPoint + AppModule) · LeakCanary (debug-only, release'te yok) · R8 (release minify+shrink, 12M→1.0M) · Detekt+ktlint (detekt JDK21 fork ile JDK25 uyumu) · Di10 indigo tema (#3F3D9C, dynamicColor=false) + markalı placeholder ekran · GitHub Actions CI (`mobile-android/**` path-filtreli). CT bağımsız doğrulama: sıfırdan `assembleDebug/assembleRelease` yeşil + release-classpath'te LeakCanary yok + 4 unit test.

## 0. Onaylı kararlar (Owner)
- **UI:** Jetpack Compose + Material 3
- **Repo konumu:** aynı repo → `/mobile-android`
- **Offline:** Offline-cache (**Room, offline-first**)
- **Varsayılanlar (CT önerisi, onaylandı):** Min SDK 26 (Android 8), tek-Activity Compose, MVI, Kotlin, Gradle multi-module + version catalog.

## 1. İlkeler (Owner şartları)
- Modüler (önce HR, sonra TEP/DKI/Platform); her modül bağımsız `:feature` modülü.
- Web frontend'e benzer tasarım (Di10 indigo teması + readiness durum renkleri).
- **Clean Architecture + SOLID**, katmanlı mimari.
- **Güvenlik** en iyi pratiklerle kapatılmış (aşağıda §5).
- **Memory-leak yok** (aşağıda §6) — LeakCanary CI gate.

## 2. Teknoloji yığını
| Alan | Seçim |
|---|---|
| Dil / UI | Kotlin · Jetpack Compose · Material 3 |
| Mimari | Clean Architecture (domain/data/presentation) · MVI |
| DI | Hilt (Dagger) |
| Ağ | Retrofit · OkHttp · Coroutines · Flow |
| **Offline** | **Room (SSOT)** · WorkManager (senkron) |
| Güvenli depolama | DataStore (encrypted) · Android Keystore (token) |
| Navigasyon | Navigation Compose (tek-Activity) |
| Modülerlik | Gradle multi-module · version catalog |
| Kalite | LeakCanary (debug) · R8/ProGuard (release) · ktlint/Detekt |

## 3. Modül/Gradle yapısı
```
:app                → shell, navigation, DI wiring, permission-gated modül menüsü, legal-entity switcher, dashboard
:core:network       → Retrofit/OkHttp, interceptor'lar (JWT + X-Tenant-Id + X-Legal-Entity-Id), token refresh, cert-pin, Response<T>, hata haritalama
:core:auth          → tenant login, güvenli token store (Keystore), session, legal-entity seçici
:core:database      → Room (base entity/converter, tenant+legal-entity partition anahtarları), migration altyapısı
:core:sync          → WorkManager senkron çerçevesi (offline-first: yerel yaz → sunucuya senkronla → uzlaştır)
:core:design        → Compose tema (web'i aynala), ortak bileşenler (liste/form/durum-chip = golden-compact mobil)
:core:common        → Result/MVI base, permission gating, navigation contract, hata modeli
:feature:<modül>    → domain/ (model + repo interface + use case)
                      data/ (DTO + Retrofit API + Room entity/DAO + sync + repo impl + mapper)
                      presentation/ (ViewModel/MVI + Compose ekranlar)
```
**Feature şablonu (readiness modülü, web golden-compact karşılığı):** Liste ekranı (Room'dan Flow ile gözlemlenir) + Create/Detay (durum alanları dropdown) + Evaluate + Delete. Yerel yaz → senkron → sunucu-otoritatif uzlaştırma.

## 4. Backend entegrasyonu (mevcut sistem)
- **Gateway:** `http://10.0.2.2:5080` (emulator) / LAN IP (cihaz) — BuildConfig configurable.
- **Auth:** `POST /api/auth/login {email,password,tenantId}` → `Response<{accessToken,refreshToken,…}>`. Mobil **Bearer** kullanır (web'deki 431 cookie sorunu yok).
- **Header'lar:** `Authorization: Bearer` + `X-Tenant-Id` + `X-Legal-Entity-Id` (legal-entity seçici — web'le aynı scoping; roll-up parent tüm alt şirketi görür).
- **Modül API:** `/api/<slug>` readiness CRUD, `Response<T>` zarfı, 401/403.
- **Yetki:** JWT `permission` claim → modül görünürlüğü.
- **Offline anahtarlama:** Room cache **tenant + legal-entity** ile partition edilir (yanlış scope veri sızmaz).

## 5. Güvenlik
- Token → Android Keystore + EncryptedDataStore; asla plaintext/log. Refresh + secure logout (wipe).
- OkHttp **certificate pinning** (prod), TLS zorunlu, cleartext yasak (localhost dev hariç).
- R8 minify + **obfuscation**; kodda gömülü secret yok.
- Hassas ekranlarda `FLAG_SECURE` (screenshot/recents engel); opsiyonel biyometrik kilit.
- Opsiyonel root/tamper tespiti; girdi doğrulama; Room DB opsiyonel SQLCipher şifreleme (hassas cache).

## 6. Memory-leak önleme
- `viewModelScope` + `repeatOnLifecycle` (Flow collection lifecycle-aware).
- Context leak yok (`applicationContext`; Activity ref tutma).
- Hilt scoping (manuel singleton yok); Compose `remember`/`DisposableEffect`.
- Coroutine iptal (structured concurrency); WorkManager doğru scope.
- **LeakCanary** debug'da her leak'i yakalar → CI gate (leak = kırmızı).

## 7. Yol haritası (fazlar → parçalar; "geliştir" ile tetiklenir)
| Faz | Parça | İçerik | Bağımlılık |
|---|---|---|---|
| **M0 Foundation** | M0.1 İskelet | `mobile-android` dalı; multi-module Gradle, Compose, Hilt, LeakCanary, R8, ktlint/Detekt, CI | — |
| | M0.2 `:core:network` | Retrofit/OkHttp, interceptor'lar, refresh, cert-pin, Response<T> | M0.1 |
| | M0.3 `:core:database` | Room base + tenant/legal-entity partition + migration | M0.1 |
| | M0.4 `:core:auth` | Tenant login, Keystore token store, session, legal-entity seçici | M0.2,M0.3 |
| | M0.5 `:core:design` | Compose tema (web aynası) + ortak bileşenler | M0.1 |
| | M0.6 `:core:sync` + `:core:common` | Offline-first senkron çerçevesi (WorkManager) + MVI base + permission gating | M0.3 |
| | M0.7 `:app` shell | Navigation, permission-gated modül menüsü, legal-entity switcher, dashboard | M0.2–M0.6 |
| **M1 HR pilot** | Applicant Intake | Uçtan uca 1 feature modülü → şablonu kanıtla (offline-first CRUD + evaluate) | M0 |
| **M1 HR rollout** | 22 HR modülü | Pilot şablonuyla birer birer | M1 pilot |
| **M2+ diğer domainler** | TEP · DKI · Platform | Feature modülleri | M1 |

## 8. Çalışma kadansı (web ile aynı disiplin)
Owner **"<parça/modül> geliştir"** der → CT **DoR-tam @module-pack-author WP'si** üretir → agent geliştirir → **CT bağımsız doğrular** (build + ktlint/Detekt + **LeakCanary leak-check** + API entegrasyon canlı testi + tasarım uyumu + güvenlik kontrolü) → commit. Agent PASS ≠ CT ACCEPTED (K13).

## 9. Doğrulama standardı (her mobil parçada)
- Derleme yeşil + statik analiz (Detekt/ktlint) temiz.
- Unit test (domain/use-case + repo) + (feature'da) ViewModel testi.
- **LeakCanary**: watched senaryolarda 0 leak.
- Canlı API: gateway'e karşı gerçek istek (auth + tenant + legal-entity header) → beklenen davranış.
- Tasarım: web referansıyla görsel uyum.
- Güvenlik: token güvenli depoda, log'da hassas veri yok, cleartext yok.
