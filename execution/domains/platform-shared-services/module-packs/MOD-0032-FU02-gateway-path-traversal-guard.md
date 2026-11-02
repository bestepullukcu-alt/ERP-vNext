---
id: MOD-0032-FU02
name: Gateway Path Traversal Guard
domain: platform-shared-services
service: Diten.ApiGateway
shell: none
golden_reference: none
entity_base: none
status: ready-for-dev
owner: auth-owner
branch: chore/ct/auth-ct-01
started: 2026-11-02
target: TBD
form_field_count: 0
parent: MOD-0032
---

# MOD-0032-FU02 — Gateway Path Traversal Guard

> **`status: ready-for-dev` yalnız bağımsız inceleme içindir; bu paket kod yazma izni vermez.** Kod için paket `approved` / `ready-for-dev`
> olmalı, bağımsız inceleme (CT-APR-02) yapılmalı ve CT-APR-01 onay satırı bulunmalıdır:
> `python3 scripts/pack_approval.py check <paket>` onaylı (çıkış 0) demelidir. **Onay satırı yoksa kod yok.** Bu iş ağ
> geçidine ve güvenliğe dokunur, bu yüzden yüksek risklidir: onaylayıcı paketi yazmamış ve `high: evet` olan biridir.
> `branch` alanı paket metninin dalıdır. Kod dalı ilk dilimde ayrıca açılır (öneri
> `feature/pss/mod-0032-fu02-gateway-path-traversal-guard`).
>
> **Sahip kararı (CT · Yetki görev iletisi, 2026-11-02):** Sahip bu kulede "Evet, önce bunu aç" dedi. Bu karar işin **bu
> kulede** ve rol atama ekinden (MOD-0018-FU29) **önce** açılmasıdır. **Onay değildir.** Kapsamı, hata kodunu ve açık
> kararları onaylamaz.
>
> **DCP-002 kimlik notu.** Ön kontrol paket yazılmadan hemen önce yeniden koşuldu (2026-11-02, HEAD `6f169f677`):
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0032-FU02 --name "Gateway Path Traversal Guard" --parent MOD-0032`
> → `OK  MOD-0032-FU02: proven against Blueprint/registry.` (çıkış 0). CT'nin `--parent` olmadan koştuğu sonuç da OK'ydi
> (CT iletisi). `git log --all --oneline -S "MOD-0032-FU02"` boş döndü: numara hiçbir dalda kullanılmamış. Kayıtta
> (`execution/registries/module-id-registry.md`) `MOD-0032-FU02` satırı **yok**. Ana modülün satırı `:125`'tedir. `:126`'da
> `MOD-0032-FU1` "Frontend Direct-service Fallback Removal" var (`[Non-executable]`). Bu başka bir iştir ve iki basamaklı FU
> numaralarıyla aynı biçimde yazılmamıştır. Kayıt satırı bu görevin izinli dosyası dışındadır ve ayrı bir değişiklikle yazılır
> (Ready-for-dev Checklist, Açık kararlar (m)). `--discover` bu oturumda koşulmadı.
>
> **Golden Reference kararı.** Bu iş ağ geçidi ara katmanıdır. Yeni sayfa, DataTable, varlık ya da kalıcı kayıt yoktur. Bu
> yüzden `shell: none`, `golden_reference: none`, `entity_base: none`, `form_field_count: 0` bilinçli seçildi. `data_mode`
> yazılmaz, çünkü liste ekranı yoktur.
>
> **Ölçüm tabanı.** Aksi yazılmadıkça her `dosya:satır`, kule dalı `chore/ct/auth-ct-01` @ `6f169f677` üzerinde numaralı
> okumayla alındı. Bu commit `origin/main` `4099e3ad1`'i birleştiriyor. Yol aşma ölçümü CT'nindir (kanıt:
> `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02.md`).
>
> **Durum notu (2026-11-02):** Sahip "Evet, incelemeye gönder" dedi; `ready-for-dev` yalnız bağımsız inceleme ZIP'i
> içindir. Onay değildir, kod izni vermez; kod için CT-APR-01 onay satırı gerekir.

## Module Summary

Ağ geçidi, istek yolunun içindeki **çift kodlanmış** `..` kesimini görmüyor. İstek bu yüzden rotanın önekinden çıkıp aynı
servisin başka bir yoluna ulaşabiliyor. CT 2026-11-02'de bunu gerçek Kestrel ve Ocelot üzerinde, gerçek
`/api/product-master/{everything}` rotasıyla ölçtü:

| İstek | Ağ geçidi | Servise ulaşan yol |
|---|---|---|
| `/api/product-master/%252e%252e/internal/v1/product-master/validate` | 200 | `/api/internal/v1/product-master/validate` |
| `/api/product-master/%252e%252e/%252e%252e/internal/x` | 200 | `/internal/x` |
| `/api/product-master/skus/%252e%252e/%252e%252e/%252e%252e/internal/x` | 200 | `/internal/x` |
| `/api/product-master/%252e%252e%252f%252e%252e%252finternal/x` | 200 | `/api/product-master/..%2f..%2finternal/x` (rota içinde kaldı) |
| `/api/product-master/../../internal/...` | 404 | hiçbir şey |
| `/api/product-master/%2e%2e/%2e%2e/internal/...` | 404 | hiçbir şey |

**Sebep (ölçümün yorumu):** Ağ geçidi `%252e` → `%2e` dönüşümünü bir kez yapıyor. Servisin Kestrel'i ikinci kez çözüyor ve
`..` kesimini normalleştiriyor.

Bu paket ağ geçidine **tek bir ara katman** ekler. Ara katman Ocelot'tan, kimlik doğrulamadan ve kiracı çözümünden önce
koşar. İsteğin ham hedefini (`IHttpRequestFeature.RawTarget`) okur ve yalnız yol kısmını, sabit bir üst sınıra kadar
yinelemeli olarak çözer. Şu durumlardan biri varsa istek 400 ve sabit bir hata koduyla durur:

- herhangi bir aşamada yol kesimlerinden biri `.` ya da `..` ise;
- son turdan sonra yolda `%2e`, `%2f` ya da `%5c` (büyük / küçük harf fark etmez) kalıyorsa;
- yolda ters bölü (`\`) varsa.

Sorgu dizesine bakılmaz. Sorgu dizesinde `..` meşrudur ve isteği geçirmeye devam eder.

**Kullanıcı:** Yeni ekran yoktur. Meşru istekler bugünkü gibi geçer. Yol aşma deneyen istek servise ulaşmaz. Cevapta
kullanıcıya gösterilecek metin yoktur, yalnız makinenin okuyacağı hata kodu vardır.

## Ownership and Boundaries

**Kapsam içi**

- `gateway/Diten.ApiGateway/**` içinde tek yeni ara katman ve onun saf karar yardımcısı.
- `Program.cs` içinde ara katmanın tek kayıt satırı.
- `gateway/Diten.ApiGateway.Tests/**` içinde yeni testler ve gerçek `Program.cs`'i gerçek Kestrel'de açan test konağı.

**Kapsam dışı**

- `ocelot.json` (yalnız `integration-agent` değiştirir; bu iş için değişiklik gerekmiyor — Gateway / API Routing Decision).
- Servislerin iç uç koruması (`X-Internal-Api-Key`) ve bu anahtarın sabit süreli olmayan karşılaştırması. Bu ayrı bir iştir
  (Follow-up Items, Açık kararlar (f)).
- Ağ geçidinin kiracı / yetki denetimlerinin ham yola göre yeniden yazılması (Açık kararlar (e); bugün ölçülmedi).
- Q395 yedek rota / zaman aşımı satırı. CT iletisine göre bu paketle ilişkisi yoktur ve ayrı iştir (Açık kararlar (g)).
- `UseSerilogRequestLogging`'in her istekte bugün yazdığı `RequestPath` alanı (`Program.cs:130-148`, `:144`). Bu paket o
  satırı değiştirmez (Açık kararlar (d)).
- Servis tarafında ikinci bir yol aşma koruması. Bu paket ağ geçidini korur. Servislerin kendi Kestrel'leri bugünkü gibi
  kalır (Follow-up Items).
- `%00`, aşırı uzun UTF-8 (`%c0%ae`) ve tam genişlikte nokta gibi biçimler için **yeni ret kuralı**. Bu biçimler tablo
  testine "rota önekinden çıkmaz" iddiasıyla girer ama ret kuralı eklenmez (Açık kararlar (n)).

**Sahiplik:** Ağ geçidi MOD-0032'dir. Paketi CT · Yetki kulesi açtı (sahip kararı, başlık). `owner: auth-owner` geçici
yazıldı. Doğru sahip onaydan önce kesinleşir (Açık kararlar (j)).

## Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| Ara katman | `PathTraversalGuardMiddleware` (`gateway/Diten.ApiGateway/Middleware/`) | yeni |
| Saf karar yardımcısı | `GatewayPathInspector` — `Inspect(string rawTarget)` → (izin / ret, ret nedeni, yapılan tur sayısı). Durum tutmaz, G/Ç yapmaz | yeni |
| Sabit | `GatewayPathInspector.MaxDecodeRounds` (öneri `3`; Açık kararlar (c)) | yeni |
| Ret nedeni (iç, günlük için) | `DotSegment`, `ResidualEncodedSeparator`, `Backslash` | yeni — kullanıcıya gösterilmez, cevaba yazılmaz |
| Hata kodu | `GATEWAY_PATH_REJECTED` (400) — öneri, Açık kararlar (a) | yeni |
| Kayıt satırı | `Program.cs`'te `app.UseMiddleware<PathTraversalGuardMiddleware>();` | yeni satır |
| Test konağı | `GatewayKestrelProgramHost` — gerçek `Program.cs`, gerçek Kestrel, kayıt tutan Kestrel servis yerine geçeni | yeni (yalnız test) |
| Yazma komutu | — | yok |
| İzin anahtarı | — | yok |
| Uç | — | yeni uç yok |

## Entity Fields

Yeni varlık, koleksiyon, dizin ya da kalıcı alan yok. Ara katman durum tutmaz.

## Repo Scope

### DL-1 — ham hedef koruması + tüm rotalar için tablo testi

- `gateway/Diten.ApiGateway/Middleware/GatewayPathInspector.cs` — **yeni**. Saf karar yardımcısı (Runtime Constraints
  "Çözüm kuralı").
- `gateway/Diten.ApiGateway/Middleware/PathTraversalGuardMiddleware.cs` — **yeni**. `RawTarget`'i okur, yardımcıyı çağırır,
  retteyse 400 cevabını yazar ve tek bir `Warning` günlük satırı bırakır. Retteyse `next` çağrılmaz.
- `gateway/Diten.ApiGateway/Program.cs` — tek satır: `app.UseMiddleware<PathTraversalGuardMiddleware>();`. Öneri yeri
  `UseSerilogRequestLogging` bloğundan (`:130-148`) hemen sonra ve metrik bloğundan (`:150-154`) önce. Böylece sağlık
  (`:156-171`), metrik, çerezden başlığa aktarım (`:173-183`), `UseAuthentication` (`:185`), `TenantResolutionMiddleware`
  (`:186`), `UseAuthorization` (`:187`) ve `UseOcelot` (`:189`) korumadan sonra gelir (Açık kararlar (b), (h)).
- `gateway/Diten.ApiGateway.Tests/GatewayKestrelProgramHost.cs` — **yeni**. Ağ geçidinin kendi giriş noktasını
  (`Program.cs`) gerçek Kestrel'de `http://127.0.0.1:0` üzerinde açar. Kalıp mevcut `GatewayProgramHost`'un
  `HostBuilding` / `HostBuilt` gözlemcisidir (`GatewayPipelineCorrelationTests.cs:62-158`). Fark şudur: `TestServer`
  konmaz, çünkü ham hedef soket üzerinden bayt bayt gönderilir ve hiçbir istemci yolu normalleştirmez (desen
  `Mod0290Fu04ProductMasterTraversalTests.cs:104-116`). Servisin yerine kayıt tutan gerçek bir Kestrel konur. Bu konak her
  isteğin `RawTarget` değerini ve Kestrel'in yönlendirdiği `Request.Path` değerini kaydeder (desen `:60-74`). Gönderilen
  `ocelot.json`'un **yalnız** `DownstreamHostAndPorts` alanı servis yerine geçene çevrilir. Öteki her alan dosyada olduğu gibi
  kalır. Bunu geçici içerik kökündeki kopyayla ya da sonda eklenen yapılandırma kaynağıyla yapmak uygulayıcının seçimidir.
- `gateway/Diten.ApiGateway.Tests/Mod0032Fu02GatewayPathInspectorTests.cs` — **yeni** (birim, saf yardımcı).
- `gateway/Diten.ApiGateway.Tests/Mod0032Fu02PathTraversalGuardTests.cs` — **yeni** (gerçek `Program.cs` + gerçek Kestrel
  + Ocelot + gönderilen `ocelot.json`). Sınıf `GatewayProgramCollection`'a girer
  (`GatewayPipelineCorrelationTests.cs:59-64`, `DisableParallelization = true`), çünkü `Program.cs` süreç düzeyinde Serilog
  kuruyor.
- `gateway/Diten.ApiGateway.Tests/Mod0290Fu04ProductMasterTraversalTests.cs` — **değişmez** (Açık kararlar (l)).

## Protected Paths

- `gateway/Diten.ApiGateway/ocelot.json` (yalnız `integration-agent`; AGENTS.md §4). Bu paket ona yazmaz. Değişiklik gerekirse
  ayrı bir `integration-agent` görevi ve açık karar olur.
- `.antigravity/**` (sahip onayı ister).
- `services/**` — hiçbir servis değişmez. Buna iç uçların `X-Internal-Api-Key` denetimi de dahildir.
- `frontend/**` — ön yüz değişmez.
- `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` ve `gateway/Diten.ApiGateway/Authentication/**` — bu
  pakette değişmez (Açık kararlar (e)).
- `gateway/Diten.ApiGateway/appsettings*.json` — yeni ayar yok (Açık kararlar (k)).
- `tests/architecture/**`, `execution/registries/**` (kayıt satırı ayrı değişiklikle yazılır).
- Aynı klasördeki izlenmeyen yerel taslak `MOD-0018-FU27-finance-treasury-contract-permission-onboarding.md` (bu paketin değil).

## Dependencies

| Bağımlılık | Bugünkü durum (ölçüm) | Bu pakete etkisi |
|---|---|---|
| Ocelot + gönderilen `ocelot.json` | 347 rota; 163'ünün üst yolu `{everything}` ile bitiyor; `/api/internal` üst ya da alt rotası yok; `GlobalConfiguration` yalnız `BaseUrl` taşıyor; yalnız iki rotada `AuthenticationOptions` var (`/api/v1/uploads`, `/api/v1/uploads/{everything}`); bir `{everything}` rotasında üst ve alt önek farklı (`/api/v1/platform/workflows/instances/{everything}` → `/api/v1/workflow/instances/{everything}`); GET taşımayan iki `{everything}` rotası var (`/api/v1/notifications/{everything}`, `/api/crm/visit-content/{everything}`). Ölçüm: dosyanın JSON okuması, @`6f169f677` | Tablo testi rota listesini test anında dosyadan okur. Her rota **kendi alt önekiyle** sınanır. Sayı koda gömülmez |
| `Program.cs` boru hattı | `UseCors` (`:128`) → `CorrelationIdMiddleware` (`:129`) → `UseSerilogRequestLogging` (`:130-148`) → metrik (`:150-154`, koşullu) → sağlık (`:156-171`) → çerez → başlık (`:173-183`) → `UseAuthentication` (`:185`) → `TenantResolutionMiddleware` (`:186`) → `UseAuthorization` (`:187`) → `UseOcelot` (`:189`). Yol aşma denetimi yok. `grep RawTarget` `Middleware/` ve `Authentication/` altında eşleşme vermedi | Kayıt yeri Repo Scope'ta |
| Test konağı deseni | `GatewayProgramHost` gerçek `Program.cs`'i `TestServer` ile açıyor (`GatewayPipelineCorrelationTests.cs:62-158`). `Mod0290Fu04ProductMasterTraversalTests` gerçek Kestrel ve ham soket kullanıyor ama **kendi** konağını kuruyor: `Program.cs` yok, yalnız Ocelot var (`:76-102`) | Yeni konak iki deseni birleştirir. Sabotaj kanıtı ancak `Program.cs`'teki kaydı ölçen testle verilebilir |
| `TenantResolutionMiddleware` | Yolu `context.Request.Path` ile sınıflıyor (`:35`, `:45`, `:73`, `:87`, `:93`, `:99`, `:161`, `:169`, `:180`, `:215`, `:229`). Kimliksiz bir istek kiracı yolunda 403 alabilir (`:229-234`). Kod okuması, koşu değil | Tablo testi ve meşru yol testi, isteği bugün `TenantResolutionMiddleware`'in geçirdiği biçimde gönderir: testte basılan geçerli `tenant_user` belirteci ve test başına rastgele kiracı. Biçim DL-1 başında ölçülür (Implementation Notes "Ölçülmeyenler"). Kimliksiz istek bu durumda zaten 403 alır; o zaman korumanın sabotaj kanıtı bir şey ölçmez |
| `GatewayJwtAuthenticationHandler` | İmzayı `JwtSettings:Secret` ile, `Issuer` ve `Audience`'ı yapılandırmadan doğruluyor (`GatewayJwtAuthenticationHandler.cs:34-43`). Mevcut test konağı rastgele bir sır geçiriyor (`GatewayPipelineCorrelationTests.cs:104-110`) | Test belirteci bu sırla basılır. Üretim kodunda sabit kiracı ya da sır yoktur |
| Kayıt satırı | `MOD-0032-FU02` satırı yok | Ready-for-dev Checklist |
| Onay kapısı (CT-APR-01 / CT-APR-02) | Paket `ready-for-dev` (yalnız inceleme için); inceleme ve onay satırı yok | Ready-for-dev Checklist |

## Runtime Constraints

- **Portlar (AGENTS.md §3):** Ağ geçidi 5000'dir. Yeni port yoktur. Testler `127.0.0.1:0` (işletim sisteminin verdiği boş port)
  kullanır.
- **Kiracı (multi-tenancy):** Koruma kiracı verisine dokunmaz ve kiracı okumaz. Retten sonra istek kiracı çözümüne ulaşmaz.
  Kiracı belirteci yalnız testte üretilir. Üretim kodunda sabit ya da varsayılan kiracı yoktur.
- **Yetki (security-jwt):** Koruma yetki vermez ve kimlik doğrulamanın yerine geçmez. Servislerin kendi `JwtBearer` ve
  `[HasPermission]` denetimleri bugünkü gibi kalır. Ret, kimliği doğrulanmış ve kimliksiz istek için aynıdır. Saldırgan,
  cevaba bakarak belirtecinin geçerli olup olmadığını öğrenemez.
- **Çözüm kuralı (`GatewayPathInspector`):**
  1. Girdi `RawTarget`'tir. Mutlak biçimli hedefte (`GET http://host/path HTTP/1.1`) yol kısmı alınır. Yıldız biçimli
     hedefe (`OPTIONS *`) dokunulmaz.
  2. Yol, ilk `?`'te sorgudan ayrılır. Sorguya hiç bakılmaz.
  3. Aşama 0 ham yoldur. Sonraki her tur, yalnız geçerli `%XX` dizilerini çözer (`+` boşluk sayılmaz). En çok
     `MaxDecodeRounds` tur yapılır. Dize değişmezse turlar erken biter.
  4. Her aşamada yol hem `/` hem `\` ile kesimlere bölünür. Kesimlerden biri tam olarak `.` ya da `..` ise sonuç ret olur
     (`DotSegment`).
  5. Son aşamadan sonra yolda `%2e`, `%2f` ya da `%5c` (büyük / küçük harf fark etmez) kalıyorsa sonuç ret olur
     (`ResidualEncodedSeparator`). Bu kural, tur sınırından derin kodlamayı yakalar.
  6. Herhangi bir aşamada yolda `\` varsa sonuç ret olur (`Backslash`).
  7. Geçersiz `%` dizileri (ör. `%zz`, sondaki tek `%`) olduğu gibi bırakılır. Bunlar tek başına ret nedeni değildir.
- **Neden tur sınırı yeterli:** Bugün yol üzerinde iki çözücü var: ağ geçidi (Ocelot) ve servisin Kestrel'i (ölçüm, Module
  Summary). Öneri sınırı `3`, bu zincirden bir tur fazladır. Daha derin kodlama 5. kurala takılır. Bu gerekçe yeni bir çözücü
  eklenirse (ör. servis içinde ikinci bir çözüm) yeniden ölçülür.
- **Sınırlı iş:** Her tur dizeyi kısaltır ya da değiştirmez. Döngü sabit sınırla biter. Uzunluk sınırı Kestrel'indir
  (`MaxRequestHeadersTotalSize = 64 KB`, `Program.cs:38-41`; istek satırı sınırı Kestrel varsayılanı). Koruma yeni bir
  uzunluk sınırı koymaz.
- **Cevap:** `400`, `Content-Type: application/problem+json`, gövde `{"status":400,"code":"GATEWAY_PATH_REJECTED","traceId":"…"}`.
  `title` ve `detail` yoktur: kullanıcıya görünen metin yoktur ve ret nedeni cevaba yazılmaz. `contentType`, ağ geçidinin
  bugünkü deseniyle `WriteAsJsonAsync`'e geçirilir (`TenantResolutionMiddleware.cs:551-568`). Koruma
  `CorrelationIdMiddleware`'den sonra koştuğu için cevap `X-Correlation-Id` başlığını taşır. Biçim Açık kararlar (a) ve
  (i)'ye bağlıdır.
- **Dil:** Kullanıcıya görünen metin olmadığı için resx yoktur. İstemci kodu ekranda göstermek isterse çeviriyi kendisi
  yapar. Bugün böyle bir istemci yoktur.
- **Ayar anahtarı yok:** Korumayı kapatan bir yapılandırma anahtarı eklenmez (Açık kararlar (k)). Koruma her ortamda açıktır.
- **Günlük:** Her rette bir `Warning` satırı yazılır. Alanlar: `Reason` (iç ret nedeni), `Method`, `RawTargetLength`,
  `DecodeRounds`. `CorrelationId` `LogContext` üzerinden gelir. Ham hedef ve çözülmüş yol **yazılmaz**, çünkü yolda kimlik,
  e-posta ya da ad olabilir (logging-observability "PII"). Bu satır Açık kararlar (d)'ye bağlıdır. `Error` düzeyi
  kullanılmaz, çünkü ret beklenen bir saldırı gürültüsüdür.

## Layout & Shell Contract

- `shell: none`. Yeni sayfa yok. Hiçbir Razor düzeni (`Layout = "..."`) eklenmez ya da değişmez.
- Ön yüz dosyasına dokunulmaz. Ölçüt: `git diff --name-only <taban>..HEAD` çıktısında `frontend/` ile başlayan satır yoktur
  (Acceptance Criteria, kapsam ölçütü).

## Backend File Convention

Golden Reference CQRS düzeni (komut / sorgu / işleyici) bu pakete uygulanmaz. Komut, sorgu ya da işleyici yoktur. Ağ
geçidinin bugünkü düzeni izlenir:

- Ara katmanlar `gateway/Diten.ApiGateway/Middleware/` altındadır. `TenantResolutionMiddleware` gibi `public sealed class`,
  kurucuda `RequestDelegate` ve `ILogger<T>` alır, `InvokeAsync(HttpContext)` taşır.
- Ad alanı `Diten.ApiGateway.Middleware`'dir.
- Karar yardımcısı `public static class GatewayPathInspector`'dır. `HttpContext` almaz. Girdi `string`, çıktı salt-okunur bir
  sonuç kaydıdır (`readonly record struct` öneri). Böylece birim testi ara katmanı ve Kestrel'i açmadan her kuralı ölçer.
- Testler `Diten.ApiGateway.Tests` ad alanındadır. Dosya adları depodaki modül öneki desenini izler (`Mod0290Fu04…`,
  `Mod0173Pm2…`).

## Frontend File Contract

Ön yüz dosyası yok. Yeni sayfa, kısmi görünüm, JS ya da resx yok.

## Validation Rules

Form alanı yok. Doğrulanan şey isteğin hedefidir.

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `RawTarget` yol kısmı | Evet (Kestrel her istekte koyar) | Runtime Constraints "Çözüm kuralı" 1–7. Ret → 400 `GATEWAY_PATH_REJECTED` | — | `RawTarget` boşsa ya da yoksa (Kestrel dışı sunucu, ör. `TestServer`) `Request.PathBase + Request.Path` değerinin `ToUriComponent()` biçimi denetlenir. Bu yedek yol bir kaçış kapısı değildir: aynı kurallar uygulanır. Kestrel'de `RawTarget`'in her zaman dolu olduğu DL-1 başında ölçülür |
| Sorgu dizesi | Hayır | Denetlenmez. `..`, `%2e%2e%2f`, `\` sorguda meşrudur | — | — |
| Çözüm tur sayısı | — | `1 ≤ tur ≤ MaxDecodeRounds` (öneri 3). Dize değişmezse erken biter | — | — |
| Yöntem | — | Bütün yöntemlerde aynı kural (`OPTIONS` ön uçuş isteği dahil) | — | — |

İlişkili kontroller: Kesim denetimi `/` ve `\` ile yapılır. `...`, `a..b`, `..foo`, `.well-known`, `report.v1.pdf` gibi
kesimler `.` ya da `..`'ya eşit olmadığı için geçer.

## Failure Path to Verify

- **Çift kodlanmış nokta-nokta (ölçülen açık)**
  - Expected: `/api/product-master/%252e%252e/internal/v1/product-master/validate` → 400 `GATEWAY_PATH_REJECTED`. Servis
    yerine geçene hiçbir istek ulaşmaz.
- **Ham, tek, çift ve üç kat kodlanmış biçimler; karışık harf; `%2f` / `%5c` ayırıcı**
  - Expected: 400. Servise hiçbir istek ulaşmaz. Ret, rota önekinden bağımsızdır (tüm `{everything}` rotalarında aynı).
- **Tur sınırından derin kodlama (dört kat `%2525252e%2525252e/`)**
  - Expected: 400 (`ResidualEncodedSeparator`). Döngü sınırı aşılmaz.
- **Meşru yol (`report.v1.pdf`, `v1.2`, `...`, `a..b`, `100%25.pdf`, sorguda `..`)**
  - Expected: Bugünkü gibi geçer. Servis yerine geçen konak isteği rota öneki altında alır ve 200 döner. Sorgu bozulmadan
    ulaşır.
- **Kimliksiz istek, korunan rotada yol aşma (`/api/v1/uploads/%252e%252e/x`)**
  - Expected: 400 (korumasız tabanda 403 ya da 401; taban ölçülür). Koruma kimlik doğrulamadan önce koşar ve cevap belirtece
    göre değişmez.
- **Kiracı çözümünün reddedeceği istek + yol aşma**
  - Expected: Korumanın 400 gövdesi döner, `TenantResolutionMiddleware`'in gövdesi dönmez.
- **Sağlık ve metrik uçlarında yol aşma (`/health/%2e%2e/x`, `/metrics/%252e%252e/`)**
  - Expected: 400 (Açık kararlar (b) önerisi). Düz `/health`, `/health/live`, `/health/ready` ve `/metrics` bugünkü durum
    koduyla cevap verir.
- **CORS ön uçuş isteği + yol aşma**
  - Expected: 400. Koruma `UseCors`'tan sonra koştuğu için tarayıcı cevabı okuyabilir (Açık kararlar (h)).
- **Duplicate / Concurrency**
  - Expected: Uygulanmaz. Koruma durum tutmaz, aynı isteğin tekrarı aynı sonucu verir.
- **Unauthorized actor**
  - Expected: Koruma yetki denetlemez. Yetkisiz ama meşru yollu istek bugünkü gibi servise ya da Ocelot'un kimlik denetimine
    gider.

## Authorization Convention

```text
Policy:     — (ağ geçidi ara katmanı; [Authorize] yok)
Permission: — (yeni izin anahtarı yok; PKS-001 uygulanmaz)
Actor type: hepsi — kimliksiz, tenant_user, platform_admin, partner_admin, servis istemcisi
```

- Koruma kimlik ya da yetki kararı vermez. Her istekte, kimlikten önce ve kimlikten bağımsız aynı kuralı uygular.
- Servislerin `[HasPermission]` denetimi ve ağ geçidinin `TenantResolutionMiddleware` denetimleri bugünkü gibi kalır. Bu
  denetimlerin `Request.Path`'e baktığı ve bunun yol aşma ile birlikte ne yaptığı Açık kararlar (e)'dedir.

## Gateway / API Routing Decision

Karar: Gateway **rota** değişikliği **gereksiz**. Ağ geçidi **kodu** değişir (`Program.cs` + yeni ara katman).

- Ön yüz Gateway 5000 üzerinden çağırır, servis portuna doğrudan gitmez. Bu değişmez.
- `ocelot.json` değişmez. Koruma Ocelot'tan önce koştuğu için rota tanımına bağlı değildir. 163 `{everything}` rotasının
  hepsini ve rotası olmayan yolları aynı biçimde kapsar.
- `integration-agent` görevi yok. Uygulama sırasında `ocelot.json` değişikliği gerekirse bu bir açık karar olur ve ayrı bir
  `integration-agent` görevi açılır.

## Acceptance Criteria

Ölçütlerdeki test adları önerilen adlardır. Uygulayıcı adı değiştirirse aynı değişiklikte bu bölüm de değişir.

- [ ] AC-1 (zorunlu) — **Ölçülen açık kapandı:** Gerçek `Program.cs`, gerçek Kestrel'de açılır. Ocelot gönderilen
  `ocelot.json`'u kullanır. Ham soketle gönderilen `/api/product-master/%252e%252e/internal/v1/product-master/validate`,
  `/api/product-master/%252e%252e/%252e%252e/internal/x` ve `/api/product-master/skus/%252e%252e/%252e%252e/%252e%252e/internal/x`
  istekleri 400 `GATEWAY_PATH_REJECTED` alır. Servis yerine geçen konak hiçbir istek kaydetmez. İstekler, kiracı çözümünün
  bugün geçirdiği geçerli `tenant_user` belirteciyle gönderilir. Test adı
  `Mod0032Fu02PathTraversalGuardTests.The_measured_double_encoded_dot_dot_is_refused_before_it_reaches_the_service`.
  Sabotaj: `Program.cs`'ten `app.UseMiddleware<PathTraversalGuardMiddleware>();` satırı çıkarılınca bu test kırmızıya
  döner, çünkü konak `/api/internal/v1/product-master/validate` isteğini kaydeder ve durum kodu 400 olmaz.
- [ ] AC-2 (zorunlu) — **Tüm `{everything}` rotaları, tüm biçimler:** Tablo testi rota listesini test anında gönderilen
  `ocelot.json`'dan okur. Üst yolu `{everything}` ile biten her rota sınanır ve okunan sayı test çıktısına yazılır. Sayı koda
  gömülmez; sıfırsa test başarısız olur. Her rota için rotanın üst öneki ile şu biçimler birleştirilir: ham `../`, `./`,
  `%2e%2e/`, `%2E%2e/`, `.%2e/`, `%2e%2e%2f`, `%2E%2E%2F`, `..%2f`, `..%5c`, `%5c..%5c`, `%252e%252e/`, `%252E%252e/`,
  `%252e%252e%252f`, `%25252e%25252e/` (üç kat), `%2525252e%2525252e/` (dört kat, tur sınırının ötesi). Her biçimin ardına
  `internal/x` eklenir. Yöntem, rota GET taşıyorsa GET'tir, taşımıyorsa rotanın listesindeki ilk yöntemdir. Her satır iki şeyi
  ölçer: cevap 400 `GATEWAY_PATH_REJECTED`'dir ve servis yerine geçen konak, rotanın **alt** öneki dışında yönlendirilmiş
  bir istek kaydetmemiştir (konak koruma varken hiç istek kaydetmez). Test adı
  `Mod0032Fu02PathTraversalGuardTests.Every_everything_route_refuses_every_traversal_form`. Sabotaj: kayıt satırı
  çıkarılınca test kırmızıya döner (ham `../` satırları 400 yerine 404 alır, `%252e%252e/` satırları konakta rota dışına
  ulaşır). Ara katman `RawTarget` yerine `Request.Path` okuyacak biçimde değiştirilince ham `../` ve `./` satırları 400 yerine
  404 alır ve test kırmızıya döner.
- [ ] AC-3 (zorunlu) — **Meşru yollar geçmeye devam eder:** Gerçek konakta şu istekler servis yerine geçene rota öneki altında
  ulaşır ve konağın 200 cevabı istemciye döner: `report.v1.pdf`, `v1.2/items`, `...`, `a..b`, `..foo`, `.well-known`,
  `100%25.pdf`, `a%2Fb` (tek kodlanmış bölü) ve sorgu dizesi `?path=../../x&q=%2e%2e%2f`. Sorgu konağa değeri bozulmadan
  ulaşır; karşılaştırma çözülmüş değerlerle yapılır. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Legitimate_paths_and_queries_still_reach_the_service`. Sabotaj: yardımcı sorguyu da
  denetleyecek biçimde değiştirilince sorgu satırı kırmızıya döner. Kesim denetimi "noktayla başlayan her kesim" diye
  genişletilince `...`, `..foo` ve `.well-known` satırları kırmızıya döner. Bu test kayıt satırı çıkarılınca yeşil kalır.
  Bu beklenen davranıştır, çünkü test aşırı engellemeyi ölçer.
- [ ] AC-4 (zorunlu) — **Ret cevabı yalnız koddur:** Ret cevabı 400'dür, `Content-Type` değeri `application/problem+json`'dur.
  Gövde tam olarak `status`, `code` ve `traceId` alanlarını taşır; `code` değeri `GATEWAY_PATH_REJECTED`'dir. `title` ve
  `detail` alanları yoktur ve ret nedeni gövdede geçmez. Cevap `X-Correlation-Id` başlığını taşır. Test adı
  `Mod0032Fu02PathTraversalGuardTests.The_refusal_carries_the_code_only`. Sabotaj: gövdeye bir `detail` metni ya da ret
  nedeni eklenince, ya da `contentType` yerine `Response.ContentType` atanınca test kırmızıya döner. Kayıt satırı
  çıkarılınca da kırmızıya döner.
- [ ] AC-5 (zorunlu) — **Kimlik doğrulamadan ve kiracı çözümünden önce:** İki ölçüm yapılır. Birincisinde Ocelot'ta kimlik
  isteyen tek `{everything}` rotası olan `/api/v1/uploads/{everything}` üzerinde belirteçsiz `/api/v1/uploads/%252e%252e/x`
  isteği 400 `GATEWAY_PATH_REJECTED` alır. Korumasız tabanda bu istek kiracı çözümünden 403 ya da Ocelot'tan 401 alır;
  hangisi olduğu tabanda ölçülür ve teslim kaydına yazılır. İkincisinde `TenantResolutionMiddleware`'in bugün reddettiği bir istek
  yol aşmayla birlikte gönderilir. Öneri: yönetici yoluna `X-Tenant-Id` başlığıyla giden istek; bugün 400 "Invalid Tenant
  Header" alıyor (`TenantResolutionMiddleware.cs:161-166`). Bu istek korumanın gövdesini alır, kiracı çözümünün gövdesini
  almaz. Test adı `Mod0032Fu02PathTraversalGuardTests.The_guard_answers_before_tenant_resolution_and_route_authentication`.
  Sabotaj: kayıt satırı `app.UseMiddleware<TenantResolutionMiddleware>();` satırından sonraya taşınınca ikinci ölçüm
  kırmızıya döner. Kayıt satırı çıkarılınca iki ölçüm de kırmızıya döner.
- [ ] AC-6 (zorunlu) — **Sağlık ve metrik uçları:** Açık kararlar (b) önerisi kabul edilirse `/health/%2e%2e/x`,
  `/health/live/%252e%252e/x`, `/health/ready/..%2fx` ve `/metrics/%252e%252e/x` istekleri 400 alır. Düz `/health`,
  `/health/live`, `/health/ready` ve `/metrics` istekleri korumasız tabandaki durum kodunu alır. Taban, aynı test konağında
  korumadan önceki commit'te ölçülür ve teslim kaydına yazılır. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Health_and_metrics_paths_are_guarded_and_still_answer`. Sabotaj: kayıt satırı
  çıkarılınca yol aşan sağlık ve metrik satırları 400 almaz ve test kırmızıya döner. Not: kayıt satırını sağlık bloğunun
  altına taşımak bu testi kırmızıya çevirmez, çünkü sağlık ucu yalnız kalan yol boşken eşleşir; yol aşan istek sağlık ucuna
  takılmadan korumaya ulaşır. Bu beklenen davranıştır ve sabotaj olarak sayılmaz. Karar (b) önerisinden farklı kapanırsa
  bu ölçüt onaydan önce yeniden yazılır.
- [ ] AC-7 (zorunlu) — **Her kuralın yalnız onun yakaladığı bir satırı var (birim):** Saf yardımcı için tablo testi şu
  satırları içerir. Yalnız kesim kuralının yakaladığı satır `/a/%2e%2e/b`'dir. Yalnız kalıntı kuralının yakaladığı satır, tur
  sınırının bir fazlası kat kodlanmış `..` kesimidir (öneri sınır 3 için dört kat). Yalnız ters bölü kuralının yakaladığı satır
  noktasız `/a/b%5cc`'dir. Ayrıca mutlak biçimli hedef `http://h/api/x/%252e%252e/y` ret alır ve `OPTIONS *` geçer. Test
  adı `Mod0032Fu02GatewayPathInspectorTests.Each_rule_has_a_row_only_it_refuses`. Sabotaj: kesim kuralı kaldırılınca,
  kalıntı kuralı kaldırılınca ya da ters bölü kuralı kaldırılınca o kurala ait satır kırmızıya döner. Teslim kaydında üç
  sabotaj ayrı ayrı gösterilir.
- [ ] AC-8 (zorunlu) — **Çözüm sınırlıdır:** Yardımcının sonucu yapılan tur sayısını taşır. Sayı hiçbir girdide
  `MaxDecodeRounds`'u geçmez. Girdiler arasında 8 KB uzunluğunda, `%25` katmanlarıyla şişirilmiş bir yol vardır. Değişmeyen
  bir yol bir turda biter. Test adı `Mod0032Fu02GatewayPathInspectorTests.Decoding_stops_at_the_round_cap`. Sabotaj: döngü
  sınırsız "değişmeyene kadar" biçimine çevrilince şişirilmiş satır sınırı aşar ve test kırmızıya döner.
- [ ] AC-9 (zorunlu) — **Ret günlüğü ham hedefi yazmaz:** Gerçek konakta, günlük yakalayıcıyla, her ret tam bir `Warning`
  satırı üretir. Satır `Reason`, `Method`, `RawTargetLength` ve `DecodeRounds` alanlarını taşır. Ne ileti metninde ne de
  özelliklerde ham hedef ya da çözülmüş yol geçer: test, isteğe özgü bir işaret dizesini (ör. rastgele bir GUID kesimi)
  arar ve bulamaz. Test adı `Mod0032Fu02PathTraversalGuardTests.The_refusal_log_holds_no_raw_target`. Sabotaj: günlük
  satırına `RawTarget` eklenince test kırmızıya döner. Kayıt satırı çıkarılınca `Warning` satırı oluşmaz ve test yine
  kırmızıya döner. Açık kararlar (d) farklı kapanırsa bu ölçüt onaydan önce yeniden yazılır.
- [ ] AC-10 (zorunlu) — **Kapsam ve `ocelot.json`:** `git diff --name-only <taban>..HEAD` çıktısındaki her satır
  `gateway/Diten.ApiGateway/` ya da `gateway/Diten.ApiGateway.Tests/` ile başlar. `git diff <taban>..HEAD -- gateway/Diten.ApiGateway/ocelot.json`
  boştur. `gateway/Diten.ApiGateway/appsettings*.json` değişmez. `Mod0290Fu04ProductMasterTraversalTests.cs` değişmez.
  Komutlar ve çıktıları teslim kaydına yazılır.
- [ ] AC-11 (zorunlu) — **Gerileme:** `dotnet test gateway/Diten.ApiGateway.Tests` tam koşulur. Önceden yeşil olan her test
  yeşil kalır. `Mod0290Fu04ProductMasterTraversalTests`, `GatewayPipelineCorrelationTests`, `TenantContradictionGuardTests`,
  `OcelotConfigurationTests` ve rota kapsama testleri bunlara dahildir. Sayılar, koruma öncesi tabanla (aynı makine, aynı
  yapılandırma) karşılaştırmalı yazılır. Önceden bilinen kırmızılar ayrıca adlandırılır. Ağ geçidi Release derlemesi
  (`dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Release`) geçer.
- [ ] AC-12 (zorunlu) — **Çalışan ağ geçidinde kanıt (E3):** `dev` ortamında 5000'de çalışan ağ geçidine
  `curl --path-as-is -s -w '%{http_code}' 'http://localhost:5000/api/product-master/%252e%252e/internal/v1/product-master/validate'`
  gönderilir. Cevap 400'dür ve gövde `GATEWAY_PATH_REJECTED` taşır. MDM'nin (5059) günlüğünde bu istek görünmez. Aynı
  oturumda `curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/health` bugünkü durum kodunu verir. Bir meşru iş
  isteği (ör. ürün listesi, oturum açmış kullanıcıyla) bugünkü gibi cevap verir. Komutlar, çıktılar ve ortamın adı teslim
  kaydına yazılır. Sabotaj kanıtı bu maddede aranmaz: madde çalışma kanıtıdır, test değildir.
- [ ] AC-13 (isteğe bağlı) — **HTTP/2:** Ağ geçidi uç noktası HTTP/2 sunuyorsa (DL-1 başında ölçülür) aynı ölçülen istek
  HTTP/2 üzerinden de 400 alır. HTTP/2 sunulmuyorsa teslim kaydına "uygulanmaz" ve ölçüm komutu yazılır.
- [ ] AC-14 (isteğe bağlı) — **Kural dışı biçimler rota dışına çıkmaz:** Tablo testine `%c0%ae%c0%ae/`, `..;/`, `%00`
  ve tam genişlikte nokta (`%ef%bc%8e%ef%bc%8e/`) satırları eklenir. Bu satırlar 400 beklemez. Yalnız servis yerine geçen
  konağın rotanın alt öneki dışında bir istek kaydetmediğini ölçer. Bir satır rota dışına çıkarsa bu paket genişletilmez:
  bulgu Açık kararlar (n) için sahibe götürülür.

## Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Birim — saf yardımcı | `Mod0032Fu02GatewayPathInspectorTests`: her kural için yalnız onun yakaladığı satır; meşru satırlar; tur sınırı; mutlak biçimli hedef; `OPTIONS *`; geçersiz `%` dizisi (AC-7, AC-8) |
| Gerçek boru hattı | `Mod0032Fu02PathTraversalGuardTests`: gerçek `Program.cs`, gerçek Kestrel, Ocelot, gönderilen `ocelot.json` (yalnız `DownstreamHostAndPorts` çevrilir), ham soket, kayıt tutan Kestrel servis yerine geçeni (AC-1…AC-6, AC-9, AC-14) |
| Sabotaj — kayıt satırı | `app.UseMiddleware<PathTraversalGuardMiddleware>();` çıkarılınca **kırmızı:** `The_measured_double_encoded_dot_dot_is_refused_before_it_reaches_the_service`, `Every_everything_route_refuses_every_traversal_form`, `The_refusal_carries_the_code_only`, `The_guard_answers_before_tenant_resolution_and_route_authentication`, `Health_and_metrics_paths_are_guarded_and_still_answer`, `The_refusal_log_holds_no_raw_target`. **Yeşil kalan (beklenen):** `Legitimate_paths_and_queries_still_reach_the_service` ve `Mod0032Fu02GatewayPathInspectorTests` içindeki her test |
| Sabotaj — yer | Kayıt satırı `TenantResolutionMiddleware`'den sonraya taşınınca `The_guard_answers_before_tenant_resolution_and_route_authentication` kırmızı. Sağlık bloğunun altına taşımak kırmızı üretmez (AC-6 notu); bu bir sabotaj kanıtı olarak sunulmaz |
| Sabotaj — kaynak | `RawTarget` yerine `Request.Path` okununca `Every_everything_route_refuses_every_traversal_form` ham `../` ve `./` satırlarında kırmızı |
| Gerileme | Tam `Diten.ApiGateway.Tests` takımı, tabanla karşılaştırmalı (AC-11) |
| Derleme | Ağ geçidi Release derlemesi (AC-11) |
| Çalışma kanıtı | `dev` ortamında `curl --path-as-is` (AC-12) |
| Süre | Tablo testi yaklaşık 163 rota × 15 biçim ≈ 2.400 soket isteği gönderir. Süre ölçülmedi. Teslimde ölçülür ve yazılır; CI süresine etkisi TEST-HIZ-01 (test hızı işi) için not edilir |

Kural üretim koduyla ölçülür. Gerçek boru hattı testleri ağ geçidinin kendi `Program.cs`'ini koşturur. Korumanın bir kopyasını
kendi konağına koyan test sabotaj kanıtı sayılmaz. Sabotaj kanıtı teslim kaydında "kaldır → kırmızı → geri koy → yeşil"
biçiminde, test adlarıyla gösterilir.

## Ready-for-dev Checklist

- [ ] **Kod izni:** Paket `approved` / `ready-for-dev` ve CT-APR-01 onay satırı var (`python3 scripts/pack_approval.py check <paket>` → 0).
  Onay satırı yoksa kod yok
- [ ] Golden Reference: `golden_reference: none` ve `entity_base: none` gerekçesi okundu (başlık notu)
- [ ] Frontmatter zorunlu alanları dolu. `owner` ve `target` ilk incelemeden **önce** kesinleşti (Açık kararlar (j)). İlk onayda
  UYGUN incelemeden sonra paket değiştirilmez
- [ ] Layout & Shell Contract: `shell: none`, ön yüz dokunuşu yok (AC-10 ölçer)
- [ ] Validation Rules, Failure Path to Verify (en az 4 senaryo), Authorization Convention ve Gateway kararı yazılı
- [ ] Audited Events: yazma komutu yok; ret olayının denetim değil güvenlik günlüğü olduğu yazılı
- [ ] **Kayıt satırı:** `execution/registries/module-id-registry.md`'ye `MOD-0032-FU02` satırı ayrı bir değişiklikle yazıldı ve
  `verify_module_id.py --check-id … --parent MOD-0032` yeniden koşuldu (Açık kararlar (m))
- [x] **Ölçüm kanıtı depoda:** CT'nin 2026-11-02 yol aşma ölçümü
  `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02.md` kaydı olarak alındı
- [ ] Açık kararlar kapandı: onaydan önce (a), (b), (c), (d), (h), (i), (j), (k), (l), (m); bilgi için (e), (f), (g), (n)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) yapıldı ve `reviews.tsv` satırı var
- [ ] CT-APR-01 onay satırı: yüksek risk (güvenlik, ağ geçidi). Onaylayıcı paketi yazmamış ve `high: evet` olan biri
- [ ] **Ölçülmeyenler kapandı:** Implementation Notes "Ölçülmeyenler" tablosundaki her girdi DL-1 başında CT'nin numaralı
  ölçümüyle (`dosya:satır@commit` ya da komut + çıktı) kapatıldı

## Implementation Notes

**Okunan ve ölçülen (@`6f169f677`):**

1. `Program.cs` boru hattı sırası ve satırları (Dependencies). `Program.cs` içinde yol aşma denetimi yok.
2. `ocelot.json` JSON okuması: 347 rota, 163 `{everything}` rotası, iki `AuthenticationOptions` rotası (`/api/v1/uploads`,
   `/api/v1/uploads/{everything}`), `RouteClaimsRequirement` taşıyan rota yok, `Priority` alanı yok, `/api/internal` üst ya da
   alt rotası yok. Bütün `{everything}` rotaları `/api/` ile başlıyor ve `/{everything}` ile bitiyor. Üst ve alt öneki farklı tek
   rota `/api/v1/platform/workflows/instances/{everything}`. GET taşımayan iki `{everything}` rotası var.
3. `Mod0290Fu04ProductMasterTraversalTests.cs`: yedi biçim deniyor (`:29-35`). `%252e%252e/` biçimi yok; çift kodlanmış
   tek satır `%252e%252e%252f` biçimidir (`:35`) ve ölçümde rota içinde kalıyor. Konak `Program.cs`'i değil, yalnız Ocelot'u
   kuruyor (`:76-102`). Durum kodu iddiası yalnız "bir cevap var" diyor (`:53`). Bu test korumayı ölçemez, çünkü korumayı
   içermez. Değişmez kalması önerilir (Açık kararlar (l)).
4. `GatewayPipelineCorrelationTests.cs:62-158`: `GatewayProgramHost` gerçek `Program.cs`'i `HostBuilding` / `HostBuilt` tanı
   olaylarıyla `TestServer` üzerinde açıyor. Sırrı rastgele üretip geçiriyor (`:104-110`).
5. `TenantResolutionMiddleware.cs` yolu `Request.Path` ile sınıflıyor (Dependencies). `IsNonTenantPath` (`:430-436`)
   `/api/lookups` önekini kiracı çözümünün dışında tutuyor. `IsAdminPath` (`:457-462`) `/api/admin` ve `/api/platform`
   öneklerine bakıyor. `/api/lookups/{everything}` rotası Platform servisine (5057) gidiyor (ölçüm 2). Bu bir kod okumasıdır.
   Bunun yol aşma ile birlikte bir atlatma açıp açmadığı **ölçülmedi** (Açık kararlar (e)).
6. Ret cevabı deseni `TenantResolutionMiddleware.cs:551-568`: `WriteAsJsonAsync`, `contentType: "application/problem+json"`.
7. `appsettings.json` ve `appsettings.Development.json`: metrik yolu `/metrics`, sağlık yolları `/health`, `/health/live`,
   `/health/ready`. Metrik bugün açık görünüyor (`"Enabled": true`, `appsettings.json:24`). Hangi `Enabled` satırının hangi
   bölüme ait olduğu yalnız `grep` ile okundu.

**Plan hizalaması:** Bu pakete plan satırı verilmedi; plan dosyası depoda değil. Teslim dilimleri tablosunda `plan kimliği
yok` ve `plan satırı yok` yazıldı. Değer uydurulmadı.

**Koşturulmayan:** Bu yazar derleme, test ya da dev sunucusu koşturmadı. Yol aşma ölçümü CT'nindir.

**Ölçülmeyenler.** Her biri DL-1 başında CT'nin numaralı ölçümüyle kapanır (Ready-for-dev Checklist).

| Girdi | Neden önemli | Yazar okuması |
|---|---|---|
| Gerçek ağ geçidinde (kiracı çözümü dahil) ölçülen isteğin bugün geçip geçmediği; ölçüm Ocelot-yalnız konakta yapıldı | AC-1 ve AC-2'nin sabotaj kanıtı, isteğin kiracı çözümünden geçebilmesine bağlı | ölçülmedi |
| `TenantResolutionMiddleware`'in geçirdiği test belirteci biçimi (`actor_type`, `tenant_id`, `Issuer`, `Audience`) | Test konağının belirteci | kısmen okundu (`:69`, `:330`; `GatewayJwtAuthenticationHandler.cs:34-43`) |
| Kestrel'de `RawTarget`'in her istekte dolu olduğu; HTTP/2 `:path` davranışı | Validation Rules yedek yolu; AC-13 | ölçülmedi |
| Ocelot'un `a%2Fb` ve sorgu dizesini nasıl ilettiği | AC-3 beklenen değeri | ölçülmedi |
| `Program.cs`'in gerçek Kestrel'de `--urls=http://127.0.0.1:0` ile açılabildiği (mevcut konak yalnız `TestServer` kullanıyor) | Test konağı | ölçülmedi |
| İç uçların `X-Internal-Api-Key` denetimi ve sabit süreli olmayan karşılaştırma | Kapsam dışı; Açık kararlar (f) | CT iletisi (Yönetici ölçümü); bu yazar okumadı |
| Tablo testinin süresi | TEST-HIZ-01 | ölçülmedi |
| Ağ geçidi uç noktasının HTTP/2 sunup sunmadığı | AC-13 | ölçülmedi |

## Follow-up Items

- **İç uç anahtarının sabit süreli karşılaştırılması** (`X-Internal-Api-Key`; `CryptographicOperations.FixedTimeEquals`).
  Servislerin kodudur ve her servisin sahibine aittir (XMC-001). Ayrı iş.
- **Kiracı / yetki denetimlerinin yol aşmayla ölçülmesi:** `TenantResolutionMiddleware`'in `Request.Path` sınıflaması
  (Implementation Notes madde 5). Önce ölçüm, sonra gerekirse ayrı paket.
- **Servis tarafında savunma derinliği:** Servislerin kendi Kestrel'lerinde yol aşma koruması. Her servisin sahibine aittir.
- **`Mod0290Fu04ProductMasterTraversalTests`'in gerçek `Program.cs` konağına taşınması** (Açık kararlar (l) seçilirse).
- **İstek günlüğünün `RequestPath` alanı** (`Program.cs:144`): Yolda kişisel veri olabilir. Bu alan bugün her istekte
  yazılıyor. Ayrı karar ve ayrı iş (Açık kararlar (d)).
- **Kural dışı biçimler** (`%00`, aşırı uzun UTF-8, tam genişlikte nokta): AC-14 bulgusuna göre (Açık kararlar (n)).

## Audited Events

**Servisin denetim altyapısı bugün:** Ağ geçidinin bir denetim izi yok. `tests/architecture/audit-ledger/` altında ağ geçidi
için bir defter dosyası bu oturumda aranmadı. Bu paket **yazma komutu eklemez ve değiştirmez**. Koruma hiçbir durumu
değiştirmez. Ret, bir denetim olayı değil, bir **güvenlik günlüğü** satırıdır (Runtime Constraints "Günlük").

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| — (yazma komutu yok) | — | — | — | — | Uygulanmaz: bu paket yazma komutu taşımıyor; AUD-001 tablosu yalnız yazma komutlarını sayar |

- **Kayda yazılmayan:** ham hedef, çözülmüş yol, sorgu dizesi, belirteç, istek başlıkları.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek dilim: ham hedef koruması (tek ara katman + saf karar yardımcısı + `Program.cs` kayıt satırı), gerçek `Program.cs` / Kestrel / Ocelot test konağı, tüm `{everything}` rotaları için tablo testi, meşru yollar, sağlık / metrik, ret gövdesi ve günlük, sabotaj kanıtı, dev çalışma kanıtı; Acceptance Criteria'nın tamamı | Owned Objects, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | E3 (çalışan ağ geçidinde istek / cevap; E2 testleri dahil) | plan kimliği yok | plan satırı yok |

Tek dilim yeterlidir. İş tek bir ara katmandır. Kalıcı veri, ekran ya da servisler arası yeni sözleşme yoktur. Dilim kendi
başına çalışan en küçük korumadır.

## Açık kararlar

| # | Konu | Seçenekler | Önerisi (bu paketin önerisi; karar değil) | Sahip | Bağımlılık / son tarih |
|---|---|---|---|---|---|
| (a) | Hata kodu adı | **`GATEWAY_PATH_REJECTED`** · `GATEWAY_PATH_TRAVERSAL_REJECTED` · `PATH_NOT_ALLOWED` | **`GATEWAY_PATH_REJECTED`.** Kaynağı (ağ geçidi) söyler, saldırgana hangi kuralın tuttuğunu söylemez. Ağ geçidi bugün makine kodu taşımıyor (`title` / `detail` metni var); bu ilk kod olur | MOD-0032 sahibi | Onay öncesi |
| (b) | Sağlık ve metrik uçları | **(b1)** her yola uygulanır (sağlık ve metrik dahil) · (b2) sağlık / metrik yolları muaf | **(b1).** Muafiyet bir önek eşleşmesidir ve kendisi bir atlatma yüzeyi olur. Düz sağlık ve metrik istekleri kurala takılmaz; yoklamalar (probe) etkilenmez. AC-6 bunu ölçer | MOD-0032 sahibi | Onay öncesi |
| (c) | Çözüm tur sayısı | 2 · **3** · "değişmeyene kadar, üst sınır 8" | **3.** Yoldaki iki çözücüden bir fazla (Runtime Constraints). Daha derini kalıntı kuralı yakalar. Sınır bir adlı sabittir; değişmesi paket revizyonudur | MOD-0032 sahibi | Onay öncesi |
| (d) | Günlüğe ne yazılır | **(d1)** neden, yöntem, ham hedef uzunluğu, tur sayısı; ham hedef yok · (d2) ham hedef kırpılmış (ilk N karakter) · (d3) ham hedefin SHA-256 öneki | **(d1).** Yolda kimlik, e-posta ya da ad olabilir (OBS-001 PII kuralı). İlişki `CorrelationId` ile kurulur. Not: istek günlüğü bugün her istekte `RequestPath` alanını yazıyor (`Program.cs:144`). Bu paket o satırı değiştirmez; ayrı karar (Follow-up Items). (d3) seçilirse AC-9 yeniden yazılır | MOD-0032 sahibi | Onay öncesi |
| (e) | Ağ geçidinin kiracı / yetki denetimleri ham yola bakıyor mu | — | **Ölçülmedi.** Kod okuması: `TenantResolutionMiddleware` yolu `Request.Path` ile sınıflıyor (Implementation Notes madde 5). Bu paketin koruması bu denetimlerden önce koştuğu için, koruma varken yol aşan istek o denetimlere ulaşmaz. Korumasız durumda neyin atlatılabildiği ayrı bir ölçüm işidir | MOD-0032 sahibi + CT | Bilgi; ayrı iş |
| (f) | İç uç anahtarının sabit süreli olmayan karşılaştırması | — | **Kapsam dışı, ayrı iş.** CT iletisindeki ölçüme (Yönetici) dayanır; bu yazar okumadı. Servis kodudur (XMC-001) | İlgili servis sahipleri | Bilgi |
| (g) | Q395 (yedek rota / zaman aşımı satırı) ile ilişki | — | **İlişki yok, ayrı iş** (CT iletisi). Bu paket rota ya da zaman aşımı değiştirmez | — | Bilgi |
| (h) | Kayıt satırının yeri | **(h1)** `UseSerilogRequestLogging`'den sonra, metrikten önce · (h2) boru hattının en başı (`UseCors`'tan önce) · (h3) `UseCors` ile `CorrelationIdMiddleware` arasında | **(h1).** Ret istek günlüğünde 400 olarak görünür, cevap `X-Correlation-Id` taşır, CORS başlıkları eklenir (tarayıcı kodu okuyabilir). Sağlık, metrik, kimlik ve kiracı çözümü korumadan sonra gelir. (h1) ile ret, HTTP metriklerine (`UseHttpMetrics`) sayılmaz; sayılması istenirse kayıt satırı metrik bloğunun içine, `UseHttpMetrics` ile `UseMetricServer` arasına girer | MOD-0032 sahibi | Onay öncesi |
| (i) | Ret gövdesinin biçimi | **(i1)** `application/problem+json` + `status`, `code`, `traceId` · (i2) gövdesiz 400 · (i3) ağ geçidinin bugünkü `title` / `detail` biçimi | **(i1).** Ağ geçidinin bugünkü problem+json biçimine yakın kalır ve kullanıcıya görünen metin içermez (Çekirdek kural 4) | MOD-0032 sahibi | Onay öncesi |
| (j) | Paket sahibi ve hedef tarih | `owner`: auth-owner · MOD-0032 sahibi · CT; `target`: tarih | Ağ geçidinin adlı bir sahibi bu oturumda bulunamadı (`MOD-0032-api-gateway.md` künyesiz). **İlk incelemeden önce** doldurulur, çünkü ilk onayda UYGUN incelemeden sonra paket değiştirilmez | Sahip | İnceleme öncesi |
| (k) | Korumayı kapatan ayar | **(k1)** yok · (k2) `Gateway:PathGuard:Enabled` | **(k1).** Kapatılabilen güvenlik koruması üretimde yanlışlıkla kapalı kalabilir. Geri alma kod geri almadır | MOD-0032 sahibi | Onay öncesi |
| (l) | Mevcut `Mod0290Fu04ProductMasterTraversalTests` | **(l1)** değişmez · (l2) ölçülen `%252e%252e/` satırı eklenir · (l3) gerçek `Program.cs` konağına taşınır | **(l1).** Test Ocelot'u yalnız ölçüyor ve korumayı içermiyor. (l2) seçilirse o konakta **kırmızı** olur (ölçüm, Module Summary). Yeni testler ölçülen biçimi gerçek boru hattında kapsıyor. (l3) Follow-up Items'tadır. Test MOD-0290-FU04'e aittir; dokunmak XMC-001 sorusu olur | MOD-0032 sahibi (+ MOD-0290 sahibi (l2) / (l3) için) | Onay öncesi |
| (m) | Kayıt satırı | `module-id-registry.md`'ye `MOD-0032-FU02` satırı | Ayrı bir değişiklikle yazılır. Mevcut `MOD-0032-FU1` satırı (`:126`, başka iş, `[Non-executable]`) değiştirilmez. Adlandırma farkı (`FU1` / `FU02`) kayıt sahibine not edilir | MOD-0032 sahibi + kayıt sahibi | Onay öncesi |
| (n) | Kural dışı biçimler (`%00`, aşırı uzun UTF-8, `..;`, tam genişlikte nokta) | Bu pakete ret kuralı ekle · AC-14 ile yalnız ölç, bulguya göre takip paketi | **Yalnız ölç (AC-14).** Kestrel bu biçimleri `..` olarak normalleştirmiyor diye beklenir ama ölçülmedi. Rota dışına çıkan bir satır bulunursa takip paketi açılır | MOD-0032 sahibi | DL-1 teslimi |
