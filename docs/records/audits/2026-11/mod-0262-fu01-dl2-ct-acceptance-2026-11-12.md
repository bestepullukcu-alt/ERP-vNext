# MOD-0262-FU01 DL-2 — CT kabulü (2026-11-12)

- CT: MVP-1 CT (oturum `ca7382cf`; ölçüm CT'nin doğrulama alt ajanı, izole worktree, detached `fbb36feef`). Talimat: WP-0262-FU01-DL2-01.
  Paket r8 (onay satırı main'de).
- Hedef: `dae798bc9` "trusted document repository consumer surface (service identity)" — 39 dosya.
- Sonuç: **KOŞULLU** — güvenlik kapısı, kimlik, kapalı liste, yetki ve denetim aktörü sabotajla doğrulandı; takımlar ve kapsam temiz. VERIFIED
  için: ORTA-1 testi (A3 kırmızıya dönmeli) + AC-21 / AC-25 için paket düzeltmesi ya da adlı FU (sahip kararı). AC-29 dilim kabul kapısı
  (Auth audience main'de yok).

## Kapsam
- 39 dosyanın hepsi §5 DL-2 listesinde; `gateway/**`, AuthService, MDM, `frontend/**`, `ocelot.json` değişmedi (AC-28). §6 korunanları
  (`AuditBehavior`, `ValidationBehavior`, `ExceptionBehavior`, `LocalFileSystemContentStorageGateway`) değişmedi. `appsettings.Development.json`
  yazılmadı (Auth istemci kimliği gelince).
- XMC (K-B kararı A): `AuditActorTypeResolver.cs` yalnız `"service" => Service`, `ForDataExport` koruması ve sınıf yorumu.

## Kod okuması (özet)
- Beşinci JWT şeması yalnız `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` audience'ı; `OnTokenValidated` `HasExactServiceClaims`.
- Yürütücü sırası: 2 sn bütçe → 413 → çift başlık 403 → tam Bearer → açık `AuthenticateAsync` (`InvalidOperationException` → 503) → talep
  savunması → kapalı liste (boş 503, dışı 403) → kiracı yalnız token'dan (`TenantScope.Begin`) → scope. Denetleyici `[AllowAnonymous]`
  (şema kapısı yürütücüde, §14), guard listesinde tek ad.
- Başlatma doğrulayıcısı: sabit küme (`MOD-0217` → `Documents`), servis + audience eşitliği, modül / ClientId tekilliği, ≤ 8 girdi,
  `MaxFileSizeBytes` alt sınırı.

## Sabotajlar (her biri ayrı, sha256 ile bayt-eşit geri)
| # | Sabotaj | Test | Sonuç |
|---|---|---|---|
| A1 | AC-18 paket lafzı: `tenantId` form alanı kabul, komut o kiracının scope'unda | `UploadTests.A_tenantId_form_field_is_400_and_never_chooses_the_tenant` | kırmızı |
| A2 | AC-26: kimliksiz retin `ActorType`'ı `ForCommand(principal)`'dan | `GateTests.A_user_jwt_with_read_and_manage_is_401_…` (+1) | kırmızı |
| A3 | `GetByContentIdIncludingCompensatedAsync`'ten TenantId yüklemi çıkarıldı (Mongo deposu) | DocumentRepository tamamı (173) | **YEŞİL** → ORTA-1 |
| A4 | kapalı liste denetimi kaldırıldı (listede olmayan ClientId ilk girdiyi alır) | `GateTests.A_client_not_on_the_closed_list_is_403…` (+1) | kırmızı |
Geliştiricinin 42 sabotajı (S01–S40, S02b, S07b) kırmızı bildirildi; geliştiricinin koşmadığı AC-18 / AC-26 sabotajlarını CT koştu (A1, A2).

## Bulgular
- **ORTA-1 — üretim Mongo deposunda kiracı yüklemi testsiz.** `RepositoryObjectRepository.cs:39-44` sabotajı (A3) yeşil kaldı. Kiracılar
  arası 404 host testi bellek içi sahte depoda koşuyor (`TrustedDocumentRepositoryConsumerHostTests.cs:563-568` `InMemoryObjects`) — kuralın
  kopyası, üretim kodu değil (Çekirdek kural 10); `TrustedDocumentRepositoryConsumerMongoTests` tek kiracı. Gereken: gerçek Mongo + iki kiracı:
  B kiracısı A'nın telafi edilmiş satırını telafi etmeye çalışınca 404 ve A'nın satırı değişmez; A3 kırmızı.
- **ORTA-2 — AC-25 KISMİ.** `StoreTrustedRepositoryObjectCommand` `GetAuditMetadata()`'da `EntityId` yok; §21 `AfterState` (`ContentId`,
  `ByteSize`, `Checksum`, `TrustedConsumerModule`) yok. Neden: `AuditBehavior` (korunan) metadata'yı handler'dan önce alır, `ContentId`'yi
  sağlayıcı (korunan) üretir. Aynı açık DL-1 `StoreRepositoryObjectCommand`'da da var — paket içi çelişki. Bağ bugün yalnız `CorrelationId`.
- **DÜŞÜK-1 — AC-21 KISMİ / `FormatViolated`.** "two-files" vakasında sağlayıcı 1 kez çağrılır, yarım dosya silinir, satır yok, 400
  (`UploadTests.cs:318` `StoreCalls == 1`). Paketin "field-after-file" fikstürü fiilen "dosyadan önce eksik alan"ı ölçüyor; gerçek "4 alan +
  dosya + sonra alan" yalnız birim testte (`UnitTests.cs:132`). Bu yolun kaydını `AuditBehavior` yazıyor (`Failed` / `Create`, 400); §8.1
  tablosu (satır 414) ret yazıcısını (`PermissionDenied`) öngörüyor; kayıt sayısı 1.
- **DÜŞÜK-2** — `IRepositoryObjectRepository.GetByContentIdIncludingCompensatedAsync` varsayılan gövdesi `NotSupportedException`; Mongo dışı
  uygulamada telafi 500 olur (sessiz değil, açık hata).
- **BEKLİYOR — AC-29** Auth audience main'de yok.

## CT karar önerisi (sahibe)
- **AC-21:** kabul edilebilir — paket §12 "depolama çağrılmaz" ile AD-4 akışlı okuma kuralı çelişiyor (dosyadan sonraki bölüm dosya
  kopyalanmadan görülemez); sonuç güvenli. Paket r9: AC-21 iki vakası "sağlayıcı ≤ 1 çağrı, satır yok, yarım dosya yok"; §8.1'de bu yolun
  yazarı `AuditBehavior` (`Failed` / `Create`, 400). FU testi: host'ta "4 alan + dosya + sonra alan".
- **AC-25:** KISMİ kabul + adlı FU (DL-1 açığıyla tek FU): `ContentId`'yi yürütücünün önceden üretmesi (korunan yol) ya da `AuditBehavior`'ın
  yanıttan `EntityId` / `AfterState` alması (MOD-0021 sahibi kararı).

## Takımlar (`fbb36feef`, süzgeçsiz)
| Takım | Sonuç |
|---|---|
| Platform.Application | 9302 / 19 — bilinen 19 Mongo ortam kırmızısı, yeni kırmızı yok |
| TenantArchitecture | 224 / 5 — `DocsPathGuardTests` ×5; `AuditTrailStandardTests` yeşil |
| Gateway | 371 / 0 |
| Platform.BackgroundJobs | 92 / 1 — Release Platform API ister (ortam) |

KOŞULLU
