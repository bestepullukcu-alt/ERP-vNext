# MOD-0022 · WP-0022-VIS1 — kanıt (2026-10-09)

> **İstem:** CT-MVP1-0022-VIS1 v1, taban `907c226ff`, dal `feature/sce/mod-0022-vis1` (yerel, push yok).
> **Konu:** FU-0022-VIS-01 (E5W2 kabulündeki O-E5W2-1) — şirketsiz imzaların görünürlüğü. Part 11 §11.10(b)/(e).
> **Durum:** tamamlandı. Sözleşme, şema, imza işleyicisi ve Web değişmedi; yeni izin anahtarı yok.
> **Betikler:** `mod-0022-vis1-scripts-2026-10-09.md`.

## 1. Sözleşme ölçümü (istem madde 3)

ESIGN-BUNDLE 1.0.0 (`docs/analysis/contracts/esign.openapi.yaml`) şirketi boş kayıtlar için ayrı bir kural yazmıyor.

**Ölçülen cümleler:**
- §4.24 `searchSignatures` (:133): "Rows outside the caller's LegalEntity data scope are excluded (own signatures always
  visible)."
- §4.5 `getSignature` (:151): "404 when missing, in another tenant, or the subject is not visible to the caller."
- §4.6 `listSignaturesBySubject` (:161): "404 … if the subject is invisible."
- §4.25 `createExport` (:574): görünürlük cümlesi yok; filtre `legalEntityId` isteğe bağlı.
- x-rules 2 (:20) ve x-permissions (:1108): "Scope = Tenant for every permission."

**Sonuç: çelişki yok, boşluk var.**
- Şirketi boş bir kayıt hiçbir şirket kapsamının "dışında" değildir.
- §4.24 cümlesi şirketi olan kayıtlar için yazılmış ve aynen geçerli.
- Kiracı düzeyindeki kayıt için sözleşme bir şey söylemiyor → BLOCKED koşulu oluşmadı. Sözleşmeye dokunulmadı.

**Bir sonraki eklemeli sürüm için açıklama notu önerisi** (§4.24 / §4.25 / §4.5 / §4.7):
> "A record without a LegalEntity is a tenant-level record: visible to the tenant's holders of esign.signatures.read
> (search, get, verify) and included in exports for esign.signatures.export holders; its company cell reads 'Kiracı düzeyi'."

**Pack ile ilişkisi:** Pack §8 "Visibility and company" der ki: "Without a scope, the caller sees only their own signatures"
(fail-closed). Bu cümle şirketli kayıtlar için geçerli kalıyor. Şirketsiz kayıt için FU-0022-VIS-01 / CT kararı geçerli
(pack §19 VIS1 notu).

## 2. Kural ve tek yer

**`ESignatureVisibilityScope`** (`Diten.Platform.Domain/Repositories/IESignatureRepositories.cs`, `ESignatureSearch`'ün yanında) tek kuraldır:

| kayıt | kim görür |
|---|---|
| herhangi biri | imzalayanın kendisi |
| `LegalEntityId` dolu | şirket, çağıranın SEC-002 veri kapsamındaysa. **E2 kuralı, aynen**; kapsam yoksa hiçbiri |
| `LegalEntityId` boş (kiracı düzeyi) | `TenantLevelRecords` doğruysa: Read amacında `esign.signatures.read`, Export amacında `esign.signatures.export` |

Kiracı sınırı kuralın dışında ve önünde: her depo okuması `TenantId` ile başlıyor (`GetAsync`, `SearchAsync`).

**Uygulandığı yerler** — hepsi aynı nesneden:

- **Kapsamın kurulduğu yer:** `ESignatureVisibility.ScopeAsync(actor, purpose)`.
  - Veri kapsamı `IDataScopeResolver`'dan, modül `esign` / `signatures`.
  - Anahtar, token'daki `ITenantAuthorizationContext.PermissionKeys` içinde aranıyor.
  - `_authorization.TenantId == actor.TenantId` koşulu da var.
- **Arama ve dışa aktarma:** `ESignatureRecordRepository.SearchAsync` aynı üç olguyu Mongo filtresine çeviriyor:
  `signer == me ∨ LegalEntityId ∈ scope ∨ (TenantLevelRecords ∧ LegalEntityId == null)`.
- **Getir ve doğrula:** `scope.HidesTenantLevel(record)`.
  - Şirketsiz kayıt yalnız read sahibine ya da imzalayana açılıyor.
  - Şirketli kayıt bu iki işlemde VIS1 öncesindeki kapısını koruyor: getir için konu sağlayıcısı, doğrula için yalnız
    kiracı. Böylece şirketli kayıtların görünürlüğü **değişmedi** (istemin YAPMA maddesi).
- **Konuya göre liste** (`listSignaturesBySubject`): değişmedi. Uç `esign.signatures.read` istiyor, konu sağlayıcıdan
  geçiyor. Kiracı düzeyindeki kayıt zaten read sahibine görünüyordu; prob P5'te doğrulandı.

**Neden BLOCKED değil:**
- Kural tek nesnede toplandı.
- Şema, imza işleyicisi (`SignSubjectCommandHandler`) ve yeni anahtar gerekmedi.
- Dokunulanlar okuma ve dışa aktarma yolları: getir / doğrula / ara handler'ları, `ExportHandlers`, depo sorgusu ve PDF
  satırı.

**Dışa aktarım dosyası** (`ESignatureExportText.Company`):
- CSV'nin "Şirket" hücresi ve PDF'deki yeni "Şirket" satırı: şirket varsa LegalEntity id'si, yoksa **"Kiracı düzeyi"**.
- Dosya hash'i ve dışa aktarım günlüğü bugünkü gibi; prob P6 ve P7'de eşitliği doğrulandı.

## 3. Test sayıları

| paket | önce (`907c226ff`) | sonra (`a9837883e`) |
|---|---|---|
| Platform Application.Tests, filtre `~ESign\|~Workflow\|~WorkAggregation\|~ModuleRegistration\|~ModulePages`, `DITEN_TEST_MONGOD` | **607 / 607** | **613 / 613** (+6 `ESignatureTenantLevelVisibilityTests`) |
| Diten.Web.Tests (tam; Web'e dokunulmadı) | **675 / 675** | **675 / 675** |
| tests/architecture | **18 / 18** | **18 / 18** |

**Derleme:**
- 0 hata.
- Değişen 8 dosyanın hiçbirinde uyarı yok (`--no-incremental` derlemesinde değişen dosyalar için uyarı satırı 0). Toplam
  uyarıların tamamı dokunulmamış dosyalardan.
- Release (AuthService, Platform, Gateway, Web): 0 hata.

**Test host'u:** `WorkflowESignatureTestHost` PDF renderer'ı kaydediyor (E2 host'uyla aynı satır). Bu olmadan dışa aktarım bu
host'ta çözülemiyordu; test altyapısı, ürün kodu değil.

## 4. Senaryolar — önce kırmızı (VIS1 kodu yokken, test commit'ten önce koşuldu)

| # | test | VIS1 öncesi | sonrası | VIS1 öncesi gerçek değer |
|---|---|---|---|---|
| 1 | Read sahibi (kapsamsız, imzalayan değil) şirketsiz workflow imzasını **arar / getirir / doğrular** | **KIRMIZI** | yeşil | arama `[]`. Ayrı ölçüm: getir 200, doğrula 200 (bunlar zaten açıktı, aşağıya bakın) |
| 2 | Export sahibi dışa aktarır → kayıt dosyada, şirket hücresi "Kiracı düzeyi" | **KIRMIZI** | yeşil | dosyada satır yok |
| 3 | Read'siz C (export + verify anahtarları var) → aramada yok, doğrulama 404, getir 403 | **KIRMIZI** | yeşil | doğrulama **200**. VIS1 öncesi doğrulamada kayıt görünürlüğü yoktu |
| 4 | Şirketi dolu ve kapsam dışı kayıt → görünmez; kapsamdaki görünür; aramada ve dışa aktarımda | **KIRMIZI** | yeşil | sınır kontrolleri geçti; testin kiracı düzeyi satırı bulunamadığı için kırmızı |
| 5 | Başka kiracının şirketsiz kaydı → aramada, getirde, doğrulamada ve dışa aktarımda yok | yeşil | yeşil | sınır zaten vardı (koruma testi) |
| 6 | İmzalayan, read anahtarı olmadan kendi kaydını görüyor | yeşil | yeşil | E2 kuralı (koruma testi) |

**İstemin önce-kırmızı beklentisiyle fark:**
- İstem "getir ve doğrula bugün görmüyor" diyordu. Ölçüm farklı: `workflow-task` konu sağlayıcısı kiracı kapsamında çözüyor,
  bu yüzden getir zaten 200'dü. Doğrulamada ise kayıt görünürlüğü hiç yoktu, o da 200'dü.
- VIS1'in kapattığı açık: read'siz bir kullanıcının doğrulamada şirketsiz kaydı görmesi (test 3).
- VIS1'in açtığı: arama ve dışa aktarma (test 1, 2).

## 5. Sabotajlar (K3) — `sabotage.ps1`, her biri `git checkout` ile geri alındı

| # | bozma | kırmızı test |
|---|---|---|
| (a) | `SearchAsync` kiracı koşulu kaldırıldı (`F.Eq(TenantId)` → `F.Empty`) | `Another_tenants_tenant_level_signature_is_never_visible` (1/6) |
| (b) | Şirketsiz kural şirketli kayıtlara da uygulandı (`LegalEntityId == null` → `F.Empty`: şirket filtresi düştü) | `A_record_of_a_company_outside_the_callers_scope_stays_invisible_to_a_read_holder` (1/6) |
| (c) | Read kontrolü kaldırıldı (`PermissionKeys.Contains(key)` → `true`) | `Without_esign_signatures_read_a_user_does_not_see_another_users_tenant_level_signature` (1/6) |

Geri alındıktan sonra 6/6 yeşil; `git status --porcelain` boş.

## 6. Çalışma zamanı probu (kendi yığın) — `probe.js`, 10 / 10 PASS

**Yığın:**
- İzole mongod: replica set, rastgele port, asla 27017.
- AuthService 15056, Platform 15057, gateway 15191, Web 15001. Web açıldı ama kullanılmadı.
- Release derleme.
- Token'lar yalnız kendi AuthService girişinden. Bilet yalnız kendi `esign/reauth`'undan, gerçek (test) parolayla.

**Kullanıcılar:**
- A = john: QA rolü, read + export; imzalayan değil, şirket kapsamı yok.
- B = jane: imzalayan.
- C = bob: yalnız `esign.signatures.verify` rolü.

| # | adım | beklenen | gerçek |
|---|---|---|---|
| P0 | Token anahtarları | A ⊇ {read, export}; C'de ikisi de yok | A=[my-samples.manage, requests.read, requests.reject, signatures.export, signatures.read, signatures.sign, signatures.verify]; C=[signatures.verify] |
| P1 | B imzalar (gerçek parola bileti) ve görevi onaylar | sign 201, `legalEntityId` null, approve 200 | 201 ESIG-2026-000001, `legalEntityId=null`, approve 200 |
| P2 | A arama | 200, kayıt var | 200, var |
| P3 | A getir | 200 | 200 |
| P4 | A doğrula | 200 valid | 200 valid |
| P5 | A konuya göre (Doğrulama ekranının listesi) | 200, kayıt var | 200, n=1 |
| P6 | A CSV dışa aktarım | satır + "Kiracı düzeyi" + SHA-256 = günlük | 200, satır var, etiket var, count 1, hash eşit |
| P7 | A PDF dışa aktarım | SHA-256 = günlük, count ≥ 1 | 200, count 1, hash eşit |
| P8 | C: arama / getir / doğrula / dışa aktar | 403 / 403 / 404 / 403 | 403 / 403 / 404 / 403 |
| P9 | B (imzalayan) kendi kaydını doğrular | 200 | 200 |

**DB okuması** (yığının kendi Platform DB'si, harness'in `dbread` kancası):
- `esign_signature_records`: tek kayıt. `SignatureNumber` ESIG-2026-000001, `Module` workflow, `SubjectType`
  workflow-task, **`LegalEntityId: null`**, `SignerUserId` = B.
- `esign_exports`: iki satır, ikisi de A tarafından.
  - EXP-2026-0001 (csv): `RecordCount` 1, `FileHash` 72d5ff66…
  - EXP-2026-0002 (pdf): `RecordCount` 1, `FileHash` 7eecb891…
  - Probun hesapladığı bayt hash'leriyle aynı.
- C'nin dışa aktarımı 403 olduğu için günlük satırı yok.

## 7. Süreçler / filo

**Sahibin filosu öncesi ve sonrası aynı** (istek gönderilmedi, dokunulmadı):

```
4010:11552 5000:37036 5001:16452 5056:7420 5057:41632 5058:35044 5059:41456 5060:18672 5061:35556 5066:38424 27017:4904
```

**Kendi süreçlerim:**
- 35112 (auth), 26692 (platform), 23096 (gateway), 39592 (web) ve EphemeralMongo mongod.
- Teardown "4 processes stopped, stack mongod disposed, pw.txt deleted".
- Sonrasında 4 PID yok; 15001 / 15056 / 15057 / 15191 boş.

## 8. Açık noktalar (CT)

- **Sözleşme açıklama notu:** §1'deki öneri, bir sonraki eklemeli sürümde (1.0.x / 1.1) eklenecek. Bu WP sözleşmeye
  dokunmadı.
- **Getir ve doğrulada şirketli kayıtlar:** VIS1 öncesi kapılarını koruyor.
  - Getir: konu sağlayıcısı.
  - Doğrula: yalnız kiracı.
  - Doğrulamada şirketli kayıt için şirket kapsamı uygulanmıyor. Bu, istemin "şirketli kayıtların görünürlüğünü
    değiştirme" kuralı gereği bilerek bırakıldı.
  - Tüm işlemlerde tam tek kural (şirketli kayıtlar dahil) istenirse ayrı bir karar / WP gerekir.
- **Push öncesi:** MVP-1 / main senkronu gerekiyor. Bu WP push yapmadı.
