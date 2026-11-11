# MOD-0230 — bağımsız inceleme 01: Control Tower karşılaştırması (taslak, commit edilmedi)

> SOP §20.4-4: CT inceleme sonucunu paket ve kodla karşılaştırır, itirazlarını kaydeder, sonucu körü körüne kopyalamaz.
> Bu not karar, onay, paket değişikliği ya da dağıtım izni değildir. İnceleyenin ham metni değişmez:
> `docs/records/reviews/MOD-0230/2026-10-21-f1b56737.md@cc7b21662` (sha256 `4adc6fafd68e685a…`). Bu dosya aracın
> okuduğu bulgu dosyası değildir; `reviews.tsv` satırı yalnız ham bulgu dosyasını gösterir.

## Künye

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256
  `f1b56737b6760ebd97d6347150e4a8a47b38d9dbe73d3e0419532ac292ee8e56` (`c208ab37e`'de aynı bayt; `shasum -a 256` ile ölçüldü).
- İnceleme: Codex, oturum `01a15b88-15f5-7622-8277-43c3e4a5d265`; model "GPT-6" (inceleyenin sistem tanımı, varyant
  ölçülemedi); effort high istendi, ölçülemedi. İnceleme ZIP'ten, derleme/test koşmadan yapıldı (inceleyen beyanı).
- Karşılaştıran: PVG Control Tower (bu oturum; Claude Code, Fable 5.1). Ölçüm statik (E1) + PVG-M1-02 inceleme notunun
  E3 satırları (`docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md@afe613de8`); bu turda derleme,
  test ve servis açılışı koşulmadı.
- Bağımsız ikinci ölçüm: alt ajan (`general-purpose`; istenen model Opus, effort high — SOP §17.1 r5; ajanın beyanı:
  Claude Opus 5.5 `claude-opus-5-5`, effort oturumdan kesin ölçülemedi; 37 araç çağrısı, 310 s). Raporu §4'te olduğu gibi.
- Ölçüm HEAD: `c208ab37e` (dal `chore/ct/pvg-ct-01`; `origin/main` `9b32cd155` ile birleşik, 0 geri / 12 ileri).
- Satır numaraları `nl -ba` / `grep -n` ile ölçüldü; paket satırları inceleyenin atıflarıyla aynı sürümdür.

## 1. Bulgu bulgu karşılaştırma

| Bulgu | CT kararı | Ölçülen olgular (`yol:satır@c208ab37e`) | Not |
|---|---|---|---|
| 1 (RBAC sözleşmesi "kapalı" ama kanıt "Missing") | kabul — engel; Gateway vurgusu düzeltildi | Paket 224: "**CLOSED 2026-08-09** - consumed directly from merged `Diten.Platform.Common/Authorization`"; 241 hücre 8–9: "Missing - required proof…", "Missing - required tests…"; 789: `[x] MOD-0018 … resolved against merged runtime`; 235: boş ya da yer tutucu değer onayı engeller; 505: aktör eşlemesi ve seed/grant sahipliği MOD-0018 onayı bekler. Kod: `services/Diten.PvgService/src/Diten.PvgService.Api/Program.cs:16-35` kimlik doğrulama / JWT kaydı yok; `PvgCaseIntakeTriageEndpoints.cs:328-331, 338-354` kiracı ve aktör HTTP başlıklarından; `PvgServiceApiHost.cs:35-41` `DenyAllPermissionGate` yalnız `UseNonProductionAdapters` iken. Gateway: `ocelot.json`'da `AuthenticationOptions` yalnız 2597 ve 2624 (`/api/v1/uploads`); `gateway/Diten.ApiGateway/Program.cs:100-101, 111-113` hat genelinde Bearer + `TenantResolutionMiddleware`; ama Gateway `X-Tenant-Id` yazar (`Middleware/TenantResolutionMiddleware.cs:8`), PVG `X-Diten-Tenant-Context` okur ve Gateway bu başlığı yazmaz da silmez (gateway/*.cs grep 0). | Route başına `AuthenticationOptions` yokluğu depo normu (alt ajan, §4); asıl açık servis tarafında ve Gateway–servis başlık sözleşmesinde: kiracı Gateway üzerinden de istemcinin verdiği başlıkla gelir (paket 722'ye aykırı). E3 G1/G4 (dev, 2026-10-20) anonim isteğin API'ye ulaştığını ölçmüştü; üretim profilinde Gateway'in anonim isteği durdurup durdurmadığı bu turda ölçülmedi. İnceleme doğru; vurgu düzeltildi. |
| 2 (izin anahtarı sözleşmesi) | kabul — engel | Paket 477–481: `pvg.case-intake-triage.{read,create,update,triage,route}`; 805: `[x] Authorization actor/role matrix approved for slice 1`; 873: aynı anahtarlar için seed/grant sahipliği. Kod: `services/Diten.PvgService/src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs:7-11` `pvg.mod0230.intake.*`; `tests/Diten.PvgService.RegPvBase.Tests/PvgApplicationContractShapeTests.cs:42-45` aynı anahtarları sabitler; `frontend/` ve `services/Diten.AuthService` içinde iki aileden de anahtar yok (grep boş). | Paket 474 bu listeyi "proposal" diye etiketler ama 805 onaylı sayar; çelişki gerçek. |
| 3 (MVC denetleyicisi kapsam dışı, `[HasPermission]` yok) | kabul — engel, eksik anlatılmış | Paket 161–171 Repo Scope'ta `Controllers/` yok; 185: `frontend/**` korunur (Views + JS/resource hariç). Kod: `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:14` yalnız `[Authorize]`; `HasPermission` 0; 38–56 Index/Create/Edit/Details izin kontrolsüz görünüm döndürür (UAS-001, Çekirdek 3). Paket içinde "Controller" yalnız 125 ve 188'de (Archive). İş paketi `work-packs/WP-0230-07-tenant-ui.md:30-31` denetleyiciyi `CaseIntakeTriageController.cs` adıyla yetkilendirir; koddaki ad farklı. Denetleyici `0860632f1` (2026-08-16), UI testi `f0819509e` (2026-08-19) ile main'e girmiş. | Denetleyici main'de paketin Repo Scope'u dışında duruyor: paket ile WP-0230-07 birbiriyle de çelişiyor (§2 madde 1). |
| 4 (AuditEvent "kapalı", denetim entegrasyonu yok) | kabul — engel; atıf düzeltmesi | Paket 226 (MOD-0021 sözleşme satırı), 243 (kanıt satırı; inceleyen 241 yazmış, 241 RBAC satırıdır) hücre 8–9 "Missing - required proof/tests", hücre 10 "Approved for the build/test gate"; 791: `[x] MOD-0021 … resolved against merged runtime`. Kod: `PvgIntakeDraftApplicationService.cs:30-42` kurucuda denetim bağımlılığı yok; 92–100 depoya ekledikten sonra yalnız `BuildAuditIntent`; `src/Diten.PvgService.Api` altında "audit" geçen satır yok (grep boş). | Atıf kayması sonucu değiştirmez. |
| 5 (açılış fail-closed değil; yapılandırma anahtarı farklı) | kabul — engel | Paket 708–717 eksik sözleşmede açılış ya da mutasyon engeli ister; 780 ve 865: `Pvg:RegPvBase:UseNonProductionAdapters`. Kod: `PvgServiceApiHost.cs:84-97` yalnız iki bayrak; 35–41 adaptörler yalnız `UseNonProductionAdapters` iken kaydedilir; 52–53 ve 109–114 `/health/ready` bağımlılık varlığına bakmaz, bayrakları döndürür; 74–75 anahtar `Pvg:Runtime:UseNonProductionAdapters`; `tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs:740-759` beş servisin `null` olmasını başarı sayar, 729 aynı `Pvg:Runtime:` anahtarı. E3 (PVG-M1-02 notu B3, 2026-10-20): üretim-benzeri + yetkili + adaptörsüz açılış → host açılır, `/health/ready` 200, GET liste → 500 boş gövde. | İnceleme "yeniden üretmedi" diye kendisi sınır koymuş; CT'nin E3 ölçümü bulguyu doğrular. Bu senaryo `Pvg:Runtime:OperationalRuntimeAuthorized=true` ister (70–72); bayrak yalnız yapılandırmadır, onay kanıtına bağlı değildir (§4). |
| 6 (build/test ile runtime sınırı ve port çelişkisi) | kabul — engel | Paket 31 (build/test OPEN: backend, tests, gateway route, tenant UI; local/dev/CI), 43 (servis port 5011) ile 610 ("does not authorize API host startup"), 699–700 (5011'de açılış ayrı onay ister), 667 `[ ]` port politikası açık, 795 `[x]` port çözüldü. `AGENTS.md:85`: 5011 §3 tablosunda yok, "EA kararı bekliyor"; `Program.cs:12-14` varsayılan `http://127.0.0.1:5011`; `.claude/launch.json:19-29` 5011'i Diten.Web `web-verify` için kullanır, paket 298 ve 830 "verified free" der. | Ek olgu: paket 178 ve 183 port kaydı olarak `.antigravity/rules/ports.md`'yi gösterir; Çekirdek 5'e göre tek kaynak AGENTS.md §3 tablosudur (dosya var, kaynak değil). |
| 7 (teslim dilimleri tablosu) | kabul — engel; seviyeye kısmi itiraz | Paket 881–891 DL-1…DL-7. Başlık listesine (`grep -n '^## \|^### '`) göre tabloda olmayan bölümler: Build Authorization (39), Module Summary (72), Ownership and Boundaries (94), Repo Scope (161), Protected Paths (181), Dependencies (194; alt başlıkları 216, 231, 251, 269), Runtime Constraints (278), Acceptance Criteria (564), Current Handoff Evidence (617), Allowed Paths After Explicit Runtime Approval (673), Forbidden Until Separately Approved (685), Ready-for-dev Checklist (782), Implementation Notes (811). "Authorization Convention" hem DL-3 (887) hem DL-6 (890). DL-7 üst başlığı (606) alır; onun alt başlıkları 697, 708, 719 DL-2'de, 727, 737, 750 DL-3/DL-7'de: çift sahiplik. Follow-up Items (859–874) başka sahiplerin işlerini içerir ve DL-6'dadır. | Çakışan bölümler `Kapsanan dilim` alanını belirsizleştirir (prompt kapısı, SOP §20.4-6): engel olarak kabul. İtiraz §3'te. |

## 2. İnceleyenin kaçırdıkları (CT ölçümü; alt ajanın ek bulguları §4'te)

1. **Paket ile WP-0230-07 çelişkisi.** `work-packs/WP-0230-07-tenant-ui.md:17, 30-31, 65, 70` aynı-origin MVC proxy
   denetleyicisini (`Controllers/CaseIntakeTriageController.cs`) yetkilendirir; paketin Repo Scope'u (161–171) ve
   Protected Paths'i (185) bunu dışarıda bırakır. Koddaki dosya adı da WP'dekinden farklıdır
   (`PharmacovigilanceCaseIntakeTriageController.cs`). Bulgu 3 yalnız paket–kod çelişkisini anlatır; paket–WP çelişkisi
   ve main'de kapsam dışı duran yüzey ayrıca kaydedilmelidir.
2. **Kabul ölçütlerinin ölçülebilirliği.** Paket 774–776: port uyum süiti "C-01 through C-17, pending support package
   detail not normative until committed" — belge aslında commit edilmiş
   (`docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md:311-327`), ama belgedeki tip adları
   (`PvgStaticTransitionGate`, `PvgPendingEvidenceStore`, `IPvgSafetyPartnerAdapter`) kodda yok (grep 0) ve 809 yine de
   "concrete" `[x]`. Slice-1 ölçütlerinin (765–780) komutu ya da eşiği yok. İnceleme derinlik listesi ölçülebilirliği
   içeriyordu; bulgu yok.
3. **Kendi kendine onay.** Kanıt tablosunda 241 ve 243 hücre 4 onaylayanı "PVG paket sahibi, against merged … runtime"
   yazar ve hücre 11 `[x] Owner-approved` der: paketin sahibi kendi kanıt satırını onaylamış görünür; MOD-0018 / MOD-0021
   sahibinin onayı yoktur. Bulgu 1 ve 4 "kapalı" iddiasını yakalar, onaylayanın kim olduğunu yakalamaz.
4. **Acceptance Criteria 585–595 "all closed 2026-08-09".** 588 "This module pack is `approved` / `ready-for-dev`"
   der; CT-APR-02'ye göre bu sürümün onay satırı yoktur (`pack_approval.py check` çıkış 1, 2026-10-21) ve 591 bulgu 1
   ve 4'ün çürüttüğü "closed against merged runtime" iddiasını yineler. Bulgu 1/4 bu satırları atıf olarak saymaz.
5. **Sahip alanının tutarlılığı.** Paket künyesi `owner: PVG paket sahibi` (rol); kuyruk satırları PVG-M1-02/03
   `owner` sütununda "NY (paket sahibi)" (`execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv:3-4`). Rol
   etiketi sahip kararıdır (karar kaydı 2026-10-19 madde 3); kuyrukla paket arasındaki fark küçük ama kayıt tutarsızlığıdır.

Kaçırılan sayılmayanlar (ölçüldü, bulgu değil): paket künyesinde `approval_status` alanı yok — `module-pack-standard.md`
bu alanı zorunlu kılmıyor (grep: yalnız `status: draft` örnekleri); CT bu alanı okur, yokluğu kural ihlali değildir.

## 3. İtirazlar (inceleme isteği 02'ye eklenecek)

- **Bulgu 7, seviye:** `execution/approvals/POLICY-REVIEW-START.md:23-25` ve SOP §20.4-6 yalnız bir dilim tablosu ve
  tablodaki bölüm adlarının paket başlığı olmasını ister (araç kontrolü: `scripts/pack_approval.py` "Codex K1"). "Her
  bölüm eşlenmeli; her dilim için sahip olunan yollar, önkoşullar, kapsam dışı işler ve ölçülebilir bitiş koşulları"
  yazılı kuralın ötesindedir; öneri olarak doğru, kural gereği değil. Engel olarak kabul edilmesinin nedeni yalnız
  DL-3/DL-6 ve DL-2/DL-3/DL-7 çakışmaları ile WP prompt'unun atıf yapması gereken Repo Scope / Acceptance Criteria
  bölümlerinin hiçbir dilimde olmamasıdır.
- **Bulgu 4, atıf:** AuditEvent kanıt satırı 243'tür, 241 değil (bulgu 1'de RBAC satırı 239 değil 241). Sonuç değişmez.
- **Bulgu 1, Gateway vurgusu:** route başına `AuthenticationOptions` yokluğu depo normudur (347 route'un 2'sinde var;
  hat genelinde `UseAuthentication` + `TenantResolutionMiddleware`, `gateway/Diten.ApiGateway/Program.cs:100-113`).
  Asıl açık servis tarafında (JWT yok, başlıktan kiracı/aktör) ve Gateway'in `X-Tenant-Id` yazıp PVG'nin
  `X-Diten-Tenant-Context` okumasındadır. Bulgu ayakta kalır; gerekçesi düzeltilir.

## 4. Bağımsız ikinci ölçüm (alt ajan) — rapor olduğu gibi, ardından CT'nin kabul/itirazı

Alt ajan: `general-purpose`; istenen Opus · high; beyan edilen Claude Opus 5.5 (`claude-opus-5-5`), effort oturumdan
kesin ölçülemedi. Salt okunur; HEAD `c208ab37e`. Rapor (kısaltılmadan):

```text
Ön doğrulama: HEAD c208ab37e. Paketin sha256 değeri f1b56737b676…e56 ve inceleme dosyasının 3. satırındaki hedefle aynı. `git diff ee2fdcd76 HEAD -- <paket>` boş, yani paket o sürümden beri değişmedi. Yalnız statik okuma yaptım: build, test ya da servis koşmadım, dosya yazmadım.

Satır atıflarında iki kayma var: Codex F-1'de RBAC satırını 239, F-4'te AuditEvent satırını 241 olarak veriyor. 239 tablo başlığıdır. Gerçek satırlar RBAC için 241, AuditEvent için 243. Diğer atıflar doğru.

F-1 | DOĞRU (küçük abartı var) | İç çelişki gerçek. Paket MOD-0018 sözleşmesini "CLOSED" ve [x] gösteriyor. Aynı satırda fail-closed kanıtı ve testler "Missing". 235–236 ise boş değerin onayı engellediğini söylüyor. Kod tarafı da doğru: kiracı ve aktör doğrulanmamış başlıktan okunuyor, JWT kurulumu yok, kapı DenyAll. Abartı Gateway kısmında: `AuthenticationOptions` yokluğu repo normu. Bu seçenek 347 route'un yalnız 2'sinde var ve kimlik doğrulama Gateway hattının genelinde yapılıyor. Asıl açık servis tarafında. | kanıt: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md:224,235-236,241,505,789@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs:321-380@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Api/Program.cs:22-34@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Api/PvgServiceApiHost.cs:40@c208ab37e; gateway/Diten.ApiGateway/ocelot.json:2597,2624,2856-2917@c208ab37e; gateway/Diten.ApiGateway/Program.cs:100,185@c208ab37e

F-2 | DOĞRU | Paket `pvg.case-intake-triage.*` anahtarlarını tanımlıyor ve onaylıyor. Kod `pvg.mod0230.intake.*` anahtarlarını denetliyor, testler de bu anahtarları sabitliyor. AuthService ve Platform içinde iki aileden de anahtar yok (grep 0 sonuç). | kanıt: …MOD-0230-case-intake-triage.md:477-481,241,805,873@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Application/RegPvBase/PvgIntakeDraftApplicationService.cs:7-11@c208ab37e; services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/PvgApplicationContractShapeTests.cs:40-47@c208ab37e

F-3 | DOĞRU | Repo Scope ve Protected Paths denetleyici yolunu kapsamıyor. Denetleyicide yalnız `[Authorize]` var. Index, Create, Edit ve Details izin kontrolü olmadan görünüm döndürüyor; bu UAS-001'e aykırı. Codex'in açık söylemediği bir nokta var: bu dosya 0860632f1'de korunan yola zaten yazılmış. | kanıt: …MOD-0230-case-intake-triage.md:167-169,185,324@c208ab37e; frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:14,38-56@c208ab37e

F-4 | DOĞRU | Paket AuditEvent sözleşmesini "CLOSED" ve [x] gösteriyor; oysa kanıt ve testler "Missing". Kod kaydı depoya ekledikten sonra yalnız bir niyet nesnesi (`PvgAuditIntent`) üretiyor. Kurucuda audit bağımlılığı yok, host bir audit kaydı (sink) kaydetmiyor. Ek kanıt: API yanıtı bu niyeti de düşürüyor. | kanıt: …MOD-0230-case-intake-triage.md:226,243,791@c208ab37e; …PvgIntakeDraftApplicationService.cs:30-42,92-100@c208ab37e; …PvgServiceApiHost.cs:35-42@c208ab37e; …PvgCaseIntakeTriageEndpoints.cs:304-310@c208ab37e

F-5 | DOĞRU | `ThrowIfUnsafe` yalnız iki bayrağa bakıyor. Uygulama servisi her durumda kaydediliyor, bağımlılıkları ise yalnız adaptör bayrağı açıkken kaydediliyor. `/health/ready` yalnız seçeneklere baktığı için "ready" dönüyor. Test eksik bağımlılıkları başarı sayıyor; `ValidateOnBuild` yok. Anahtar uyuşmazlığı da var: paket ve belgeler `Pvg:RegPvBase:UseNonProductionAdapters` diyor, kod `Pvg:Runtime:` okuyor. Varsayılan da farklı: belge `false` diyor, kod üretim-benzeri değilse `true` alıyor. Not: bu senaryo için operasyonel runtime bayrağının elle `true` yapılması gerekir; paket bu durumu zaten NO-GO sayıyor. | kanıt: …PvgServiceApiHost.cs:30-42,52-53,74-76,84-97@c208ab37e; services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs:740-759@c208ab37e; …MOD-0230-case-intake-triage.md:708-717,780,865@c208ab37e; docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md:280,299@c208ab37e

F-6 | DOĞRU | Satır 31, 43 ve 280 local/dev/CI host ve 5011'i açık gösteriyor. 608–615 ve 699–700 ise 5011'de runtime açılışını ayrı onaya bağlıyor. 795 portu [x] gösterirken 667 portu [ ] açık tutuyor. Codex'in kaçırdığı iki nokta: AGENTS.md:85 5011'i açıkça "EA kararı bekliyor" diye listeliyor. `.claude/launch.json` 5011'i Diten.Web için kullanıyor, bu da 298 ve 830'daki "verified free" iddiasıyla çatışıyor. | kanıt: …MOD-0230-case-intake-triage.md:31,43,280,298,608-615,667,699-701,795,830@c208ab37e; AGENTS.md:68-85@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Api/Program.cs:12-14@c208ab37e; .claude/launch.json:19-29@c208ab37e

F-7 | DOĞRU (eksik) | Söyledikleri doğru. Repo Scope, Protected Paths, Runtime Constraints, Acceptance Criteria ve Ready-for-dev Checklist hiçbir dilime eşlenmemiş. Authorization Convention hem DL-3'te hem DL-6'da. DL-6, Follow-up Items üzerinden başka sahiplerin işini ve WP-08 blokajını taşıyor. DL-2 kalıcılık, DL-4 kimlik doğrulama sorumluluğunu tanımlamıyor. Ancak DL-7 çakışması ve diğer eşlenmemiş bölümler kaçmış (aşağıda). | kanıt: …MOD-0230-case-intake-triage.md:68,161,181,278,557,564,782,859-879,885-891@c208ab37e

KAÇIRILANLAR:
- **Platform.Common iddiası kodda yanlış.** Paket RBAC, Audit, TRACE-BUNDLE ve Observability sözleşmelerinin "merged Diten.Platform.Common'dan doğrudan tüketildiğini" söylüyor. PvgService projeleri yalnız kendi projelerine referans veriyor. Program.cs'te auth, korelasyon, tenancy ya da gözlemlenebilirlik middleware'i yok. Codex bu kalıbı TRACE-BUNDLE (229, 246) ve ObservabilityErrorModel (247) için yakalamadı. | …MOD-0230-case-intake-triage.md:224,226,229,246-247,591,794@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj:8-10@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Application/Diten.PvgService.Application.csproj:8@c208ab37e; …Program.cs:22-34@c208ab37e
- **Kiracı başlığı Gateway'i atlıyor.** Gateway kiracıyı çözüp `X-Tenant-Id`'ye yazıyor. PVG ise `X-Diten-Tenant-Context` okuyor ve Gateway bu başlığı ne yazıyor ne siliyor (gateway/*.cs içinde grep 0). Sonuç: kiracıyı Gateway üzerinden istemci belirliyor; bu, paketin 722. satırına aykırı. | gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:8,238@c208ab37e; …PvgCaseIntakeTriageEndpoints.cs:328,338@c208ab37e; …MOD-0230-case-intake-triage.md:722@c208ab37e
- **Kiracılar arası ve bulunamayan kayıt 409 dönüyor.** Paket 404, yetkisiz istek için de 401/403 istiyor. Kod Succeeded ve Invalid dışındaki her sonucu 409'a eşliyor; NotFound da Blocked olarak dönüyor. | …PvgCaseIntakeTriageEndpoints.cs:201-208@c208ab37e; …PvgIntakeDraftApplicationService.cs:125-128@c208ab37e; …MOD-0230-case-intake-triage.md:305,449-452,723@c208ab37e
- **Doğrulama izin kapısından önce çalışıyor.** Yetkisiz çağıran doğrulama kodlarını görebiliyor. CT notu bunu §4-4'te yakalamıştı, Codex atlamış. | …PvgIntakeDraftApplicationService.cs:48-64@c208ab37e
- **Operasyonel runtime yetkisi yalnız bir yapılandırma bayrağı.** Ortam değişkeni ya da komut satırıyla açılabiliyor ve onay kanıtına bağlı değil. | …PvgServiceApiHost.cs:70-72@c208ab37e; …Program.cs:3-6@c208ab37e; …MOD-0230-case-intake-triage.md:249,613@c208ab37e
- **Kalıcılık konusunda paket kendiyle çelişiyor.**
  - 107: migration kapsam dışı.
  - 282: collection, index ve migration yetkili.
  - 690: collection, migration ve Mongo yasak.
  - Kod bellek içi depo kullanıyor. Runtime Constraints hiçbir dilimde değil.
  - | …MOD-0230-case-intake-triage.md:107,282,690@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Infrastructure/RegPvBase/InMemoryPvgIntakeDraftRepository.cs:7@c208ab37e
- **Backend File Convention kodla uyuşmuyor.** Paket `Features/CaseIntakeTriage` CQRS yapısı ve `CreateCaseIntakeTriageCommand`/`*Handler` adlarını şart koşuyor. Kodda `RegPvBase/CreateIntakeDraftCommand` var; `Features/` dosyası 0, Handler sınıfı yok. DL-2 bu bölümü sapmayı anmadan üstleniyor. | …MOD-0230-case-intake-triage.md:345-364@c208ab37e; services/Diten.PvgService/src/Diten.PvgService.Application/RegPvBase/CreateIntakeDraftCommand.cs:3@c208ab37e
- **Kabul ölçütleri ölçülebilir değil.**
  - 774–775 C-01…C-17 belgesini "commit edilene kadar bağlayıcı değil" diyor, ama belge commit edilmiş.
  - Belgedeki tip adları (`PvgStaticTransitionGate`, `PvgPendingEvidenceStore`, `IPvgSafetyPartnerAdapter`) kodda yok (grep 0). C-id'ye atıf yapan test de yok (grep 0).
  - 809 yine de "concrete" [x] işaretli.
  - Slice-1 ölçütlerinin (765–780) komutu ya da eşiği yok.
  - | …MOD-0230-case-intake-triage.md:765-780,809@c208ab37e; docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md:311-327@c208ab37e
- **Teslim dilimleri tablosunda ek kusurlar.**
  - DL-7, 606'daki üst başlığı alıyor. 697, 708, 719, 727, 737 ve 750'deki alt başlıklar bu başlığın altında, ama aynı zamanda DL-2 ve DL-3'e de verilmiş: çift sahiplik.
  - Bu yüzden build-gate dilimleri, 608–611'e göre "yetki vermeyen" operasyonel paket içeriğini taşıyor.
  - Hiçbir dilime eşlenmemiş başka bölümler de var: Build Authorization (39), Ownership (94), Dependencies (194), Evidence Intake Template (231), Submission Checklist (251), Identity Clarification (269), Handoff Evidence (617), Allowed Paths (673), Forbidden (685), Implementation Notes (811).
  - `pack_approval.slices()` yalnız adların başlık olarak var olup olmadığına bakıyor; kapsama ya da tekillik denetimi yok, bu yüzden bu açık makineyle yakalanmıyor.
  - | …MOD-0230-case-intake-triage.md:606-611,697-750,885-891@c208ab37e; scripts/pack_approval.py:858-868@c208ab37e
- **Korunan yol daha önce ihlal edilmiş.** Denetleyici (0860632f1) ve `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` (3b82bfe31) Repo Scope dışında commit edilmiş. 171'deki `tests/**` ifadesi belirsiz. | …MOD-0230-case-intake-triage.md:167-171,185@c208ab37e
- **Domain-config ile paket arasında ifade farkı.** Domain-config MOD-0231/0232/0234 için "API host yok" diyor; MOD-0230 host'u ise bu modüllerin servislerini kaydediyor. Paketteki "composition-only" istisnası domain-config'te yok. Yetki sırasına göre paket üstün geldiği için bu yalnız not. | execution/domains/pharmacovigilance/domain-config.md:42-47@c208ab37e; …PvgServiceApiHost.cs:31-33@c208ab37e; …MOD-0230-case-intake-triage.md:290-293@c208ab37e
- **Kimlik uyumlu.** Registry satırı 152 paketle uyumlu: ad, durum, servis ve port. Blueprint "Buy/Partner" ile paketteki "hybrid internal wrapper" kararının uyumunu yalnız registry metninden görebildim. | execution/registries/module-id-registry.md:152@c208ab37e; …MOD-0230-case-intake-triage.md:338-343,581@c208ab37e
- **CT inceleme notunda iki yanlış, bir eksik.**
  - "4 başka route'ta AuthenticationOptions var" yanlış; gerçek sayı 2, hem 77738ea55'te hem bugün.
  - "`pv.*` izin anahtarı depoda yok" yanıltıcı: servis `pvg.mod0230.intake.*` denetliyor. Not F-2 uyuşmazlığını kaçırmış.
  - Port satırında AGENTS.md:85'teki "EA kararı bekliyor" notu anılmamış.
  - Notun geri kalan statik olguları doğrulandı.
  - | docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md:28,30,31@c208ab37e; gateway/Diten.ApiGateway/ocelot.json:2597,2624@c208ab37e

SINIR:
- Codex'in ZIP'ini ve excerpts dosyalarını görmedim.
- Blueprint 8.8 xlsx'i izinli komutlarla okuyamadım; Blueprint satırı ÖLÇÜLEMEDİ.
- `verify_module_id.py`, dotnet build/test ve servisleri koşmadım. B3'teki 500 sonucu ve 230/230 yeniden üretilmedi.
- Frontend görünüm ve JS dosyalarını (sayfa iskeleti, UAS-001) ve test dosyalarının tamamını satır satır okumadım; yalnız ilgili kısımları okudum.
- Gateway'in anonim isteği geçirip geçirmediğini yalnız middleware akışından çıkardım: `X-Tenant-Id` ya da dev bypass ile geçiyor. Canlı ölçmedim.

kullanılan model/effort: Claude Opus 5.5 (claude-opus-5-5); effort oturumdan kesin ölçülemedi.
```

**CT'nin alt ajan raporuna kabul / itirazı (her madde CT tarafından yeniden ölçüldü, `@c208ab37e`):**

- Bulgu 1–7 yargıları: kabul; 1'deki "Gateway abartısı" kısmen kabul (§1 ve §3'e işlendi: bulgu ayakta, vurgu düzeltildi).
- Platform.Common iddiası: kabul — `Diten.PvgService.Api.csproj:8-10` ve `…Application.csproj:8` yalnız kendi projeleri.
- Kiracı başlığı: kabul — `TenantResolutionMiddleware.cs:8` `X-Tenant-Id`; gateway/*.cs içinde `X-Diten-Tenant-Context` yok.
- 409 eşlemesi: kabul — `PvgCaseIntakeTriageEndpoints.cs:201-208` Succeeded/Invalid dışı her sonuç 409; paket 305 ve 723 404/boş ister.
- Doğrulama izin kapısından önce: kabul (CT notu §4-4 ile aynı).
- Runtime yetkisi yalnız bayrak: kabul — `PvgServiceApiHost.cs:70-72`, `Program.cs:3-6`.
- Kalıcılık çelişkisi: kabul — paket 107 / 282 / 690 ölçüldü.
- Backend File Convention: kabul — paket 345–364; kodda `Features/` yok, `Handler` sınıfı yok, `RegPvBase/*Command.cs`.
- Kabul ölçütleri: kabul — belge var, tipler kodda yok (grep 0), 809 `[x]`; CT §2 madde 2 buna göre düzeltildi.
- Teslim dilimleri ek kusurları: kabul — §1 bulgu 7 satırına eklendi.
- Korunan yol ihlali: kabul — denetleyici ilk ekleme `0860632f1` (2026-08-16); UI testi
  `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` ilk ekleme `b00093ea6` (2026-08-16), son değişiklik
  `f0819509e` (2026-08-19) — ajanın yazdığı `3b82bfe31` bu dosyanın `git log` çıktısında yok (CT ölçümü, `--diff-filter=A`).
- Domain-config ifade farkı: kabul, yalnız not (yetki sırası).
- Kimlik: kabul; Blueprint satırı CT'nin keşfiyle ölçülmüştür (4× `--discover`, 2026-10-21, çıkış 3; Blueprint
  `MOD-0230 'Case Intake & Triage'` eşleşmesi).
- CT notundaki yanlışlar: kabul — PVG-M1-02 notu (`afe613de8`) düzeltme ister: `AuthenticationOptions` 2 route;
  `pvg.mod0230.intake.*` anahtarları servis içinde var; AGENTS.md:85 notu eksik. Düzeltme ayrı bir commit'tir, sahip
  onayı bekler; bu not notu değiştirmez.

## 5. Sonuç ve sıradaki adım (öneri — onay değil)

- Yedi bulgunun yedisi de olgu düzeyinde doğrulandı (CT + bağımsız ikinci ölçüm); CT sonucu: inceleme 01 **UYGUN DEĞİL**
  haklı, 7 engel açık. `reviews.tsv` satırı taslak olarak eklendi (commit edilmedi); onay satırı bu sürüm için
  açılamaz (SOP §20.4-4).
- Paketin yeniden yazımı bulgu 1–7'ye ek olarak §2 (5 madde) ve §4 (alt ajanın 11 ek bulgusu) ile ele alınmalıdır;
  özellikle Gateway–servis kiracı/aktör başlık sözleşmesi, 404/409 hata modeli, kalıcılık kapsamı ve dosya düzeni
  sapması pakete açık yazılmadan inceleme 02'den geçmesi beklenmez.
- Sıradaki iş: MOD-0230 paketinin yeniden yazımı (`module-pack-author`; model Opus, effort high), ardından inceleme 02
  (bu notun §3 itirazları isteğe eklenir), ardından ilk onay satırı (HIGH). Kod işi ve WP dağıtımı yok; operasyonel
  runtime kapısı kapalı kalır.
- Bu notun ve `reviews.tsv` satırının commit'i sahip onayı bekler.
