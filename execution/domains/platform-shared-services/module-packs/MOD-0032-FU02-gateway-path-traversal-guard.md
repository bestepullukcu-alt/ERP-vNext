---
id: MOD-0032-FU02
name: Gateway Path Traversal Guard
domain: platform-shared-services
service: Diten.ApiGateway
shell: none
golden_reference: none
entity_base: none
status: ready-for-dev
owner: auth-owner (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi)
branch: chore/ct/auth-ct-01
started: 2026-11-02
target: 2026-11-09
form_field_count: 0
parent: MOD-0032
---

# MOD-0032-FU02 — Gateway Path Traversal Guard

> **`ready-for-dev` yalnız inceleme ZIP'i içindir; kod izni değildir — `pack_approval.py check` onaylı demeden kod yok.**
> Kod için paket `approved` / `ready-for-dev` olmalı, bağımsız inceleme (CT-APR-02) yapılmalı ve CT-APR-01 onay satırı
> bulunmalıdır: `python3 scripts/pack_approval.py check <paket>` onaylı (çıkış 0) demelidir. **Onay satırı yoksa kod yok.**
> Bu iş ağ geçidine ve güvenliğe dokunur, bu yüzden yüksek risklidir. Onaylayıcı Sahiptir (MOD-0018 sahibi; paketi
> yazmadı); `execution/approvals/approvers.tsv`'de `high` sütununun `evet` olduğu, onay satırı yazılırken ölçülür.
> Birleştirme (merge) ikinci kişinindir (CT-APR-01; Ready-for-dev Checklist).
> `branch` alanı paket metninin dalıdır. Kod dalı ilk dilimde ayrıca açılır (öneri
> `feature/pss/mod-0032-fu02-gateway-path-traversal-guard`). Kod dalı CT dalından (`chore/ct/auth-ct-01`) ya da ana daldan
> açılabilir; açıldığı commit AC-10'un tabanıdır.
>
> **Sahip kararı (CT · Yetki görev iletisi, 2026-11-02):** Sahip bu kulede "Evet, önce bunu aç" dedi. Bu karar işin **bu
> kulede** ve rol atama ekinden (MOD-0018-FU29) **önce** açılmasıdır. **Onay değildir.** Kapsamı, hata kodunu ve açık
> kararları onaylamaz.
>
> **Sahiplik kararı (CT · Yetki sohbeti, doğrudan, 2026-11-02):** Sahip bu kulede "Evet, ben, 11-09" dedi. Buna göre paketin
> sahibi ve bu işte MOD-0032 (ağ geçidi) karar sahibi MOD-0018 (Auth) sahibidir; hedef tarih 2026-11-09'dur. Künyedeki
> `owner` ve `target` bu sözden yazıldı ve Açık kararlar (j) bu sözle kapandı. Paket boyunca "Sahip (MOD-0018 sahibi; bu işte
> MOD-0032 karar sahibi)" bu kişiyi anlatır. Bu söz sahipliği ve tarihi belirler. **Onay değildir:** kapsamı, hata kodunu ve
> öteki açık kararları onaylamaz.
>
> **DCP-002 kimlik notu.** Ön kontrol paket yazılmadan hemen önce yeniden koşuldu (2026-11-02, HEAD `6f169f677`):
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0032-FU02 --name "Gateway Path Traversal Guard" --parent MOD-0032`
> → `OK  MOD-0032-FU02: proven against Blueprint/registry.` (çıkış 0). CT'nin `--parent` olmadan koştuğu sonuç da OK'ydi
> (CT iletisi). `git log --all --oneline -S "MOD-0032-FU02"` boş döndü: numara hiçbir dalda kullanılmamış. Kayıtta
> (`execution/registries/module-id-registry.md`) `MOD-0032-FU02` satırı **yok**. Ana modülün satırı `:125`'tedir. `:126`'da
> `MOD-0032-FU1` "Frontend Direct-service Fallback Removal" var (`[Non-executable]`). Bu başka bir iştir ve iki basamaklı FU
> numaralarıyla aynı biçimde yazılmamıştır. Kayıt satırı bu görevin izinli dosyası dışındadır ve ayrı bir değişiklikle yazılır
> (Ready-for-dev Checklist, Açık kararlar (m)).
>
> **Keşif (`--discover`):** CT 2026-11-02'de `python3 .antigravity/scripts/verify_module_id.py . --discover "gateway path traversal"`
> koştu (çıkış 3). Bulgular: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 1. Tek görev bulgusu
> `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q395 satırıdır (ağ geçidinde zaman aşımı yok). Bu satır yol aşmayla
> ilgisizdir ve bu paketin mükerreri değildir (Açık kararlar (g)). Bu yazar keşfi yeniden koşmadı; sonuç CT iletisindendir.
>
> **Golden Reference kararı.** Bu iş ağ geçidi ara katmanıdır. Yeni sayfa, DataTable, varlık ya da kalıcı kayıt yoktur. Bu
> yüzden `shell: none`, `golden_reference: none`, `entity_base: none`, `form_field_count: 0` bilinçli seçildi. `data_mode`
> yazılmaz, çünkü liste ekranı yoktur.
>
> **Ölçüm tabanı.** Aksi yazılmadıkça her `dosya:satır`, kule dalı `chore/ct/auth-ct-01` @ `6f169f677` üzerinde numaralı
> okumayla alındı. Bu commit `origin/main` `4099e3ad1`'i birleştiriyor. Yol aşma ölçümü CT'nindir. Kanıt iki kayıttır:
> ana kayıt `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02.md` ve onu değiştirmeyen ek
> kayıt `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02-ek-01.md` (commit `9b3eacf3d`;
> ölçülen commit `6f169f677`, geçici fark ve ham çıktı alıntısı). İkinci ek kayıt
> `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02-ek-02.md` (commit `40048f37a`; ölçülen
> commit `f30a65627`) meşru yolların korumasız tabanını ölçer. Birinci inceleme turunun yeni okumaları @`6c3f1c3d0`'dadır.
> İkinci turun yeni okumaları (ikinci inceleme bulgularını kapatan; `ocelot.json` JSON okuması, `GatewayPipelineCorrelationTests.cs`,
> `Program.cs`, `TenantResolutionMiddleware.cs` satırları) @`9b3eacf3d`'dedir. **Kod aynı kanıtı:** bu yazar 2026-11-02'de
> `git diff --stat 6f169f677 40048f37a -- gateway services scripts` koştu; çıktı boş, çıkış 0.
> `git diff --name-only 6f169f677 40048f37a` yalnız dört dosya verdi: üç ölçüm kaydı (`docs/records/audits/2026-11/…`) ve
> bu paket. Yani ağ geçidi, servis ve betik kodu `6f169f677`'den `40048f37a`'ya kadar her commit'te aynıdır.
>
> **Durum notu (2026-11-02):** Sahip "Evet, incelemeye gönder" dedi; `ready-for-dev` yalnız bağımsız inceleme ZIP'i
> içindir. Onay değildir, kod izni vermez; kod için CT-APR-01 onay satırı gerekir. İlk bağımsız inceleme (@`6c3f1c3d0`)
> UYGUN DEĞİL dedi (13 bulgu, `docs/records/reviews/MOD-0032-FU02/2026-11-02-6c3f1c3d0.md`); ikinci revizyon onları kapattı.
> İkinci bağımsız inceleme (@`7405749e1`) KOŞULLU dedi (14 bulgu, engel yok,
> `docs/records/reviews/MOD-0032-FU02/2026-11-02-7405749e1.md`). Sahip 2026-11-02 bu kulede "Düzelt, onaya git" dedi; o
> revizyon on dört bulguyu kapattı. Üçüncü bağımsız bakış (@`f30a65627`) KOŞULLU dedi (15 bulgu, engel yok,
> `docs/records/reviews/MOD-0032-FU02/2026-11-02-f30a65627.md`). Sahip 2026-11-02 bu kulede "Bir tur daha düzelt" dedi: bu
> revizyon on beş bulguyu kapatır. Bu sözler **onay değildir**; onay CT-APR-01 satırıyla olur.

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

**Sebep (ölçümün yorumu; çıkarım):** Ek-01'in ham çıktısında servis yerine geçenin **`RawTarget`'i** zaten
`/api/internal/v1/product-master/validate`'tir; yani servise giden istek satırında `..` kesimi kalmamıştır. Bu yüzden ikinci
çözüm ve `..` sıkıştırması servisin Kestrel'inde değil, **ağ geçidinin çıkışında** olur. Zincir şöyle okunur: ağ geçidinin
Kestrel'i `Request.Path`'i bir kez çözer (`%252e` → `%2e`); Ocelot aşağı akış adresini bu değerden kurar; adres
`HttpClient` / `System.Uri` içinden geçerken `%2e` `.`'ya çözülür ve `..` kesimi sıkıştırılır. Ocelot mu `System.Uri` mu
olduğu ayrıca ölçülmedi; çıkarımdır. Destekleyen ölçüm: `%252e%252e%252f…` satırında servise `..%2f..%2f` ulaşır (Module
Summary tablosu): `%2e` çözülmüş, `%2f` kodlu kalmıştır ve `..%2f` kesimi `..` olmadığı için sıkıştırılmamıştır. Servisin
Kestrel'i de yönlendirirken bir kez çözer (ek-02 son satırı: `RawTarget` `…/a%5cb`, yönlendirilen yol `…/a\b`). İki sonuç:
(1) **servis tarafı bir koruma bu açığı kapatmaz**, çünkü servis kendisine `..` sıkıştırılmış, meşru görünen bir yol olarak
ulaşan isteği görür; (2) çıkış davranışı Ocelot'a ve .NET'e bağlıdır, bu yüzden **Ocelot ya da .NET sürüm yükseltmesi yeniden
ölçüm tetikleyicisidir** (Runtime Constraints "Neden tur sınırı yeterli", Follow-up Items).

**Saldırı yüzeyi:** Ölçüm tek rotada yapıldı. Aynı davranış, üst yol şablonunda yer tutucu (`{...}`) taşıyan her rotada
beklenir, çünkü yer tutucunun değeri ağ geçidinin Kestrel'inde bir kez çözülür ve aşağı akış adresi ağ geçidinin
çıkışında kurulur (Sebep). `ocelot.json`'da bugün 347 rotanın
**185**'i yer tutucu taşıyor: 163'ü `{everything}` ile bitiyor, 7'sinde yer tutucu son kesimde, 15'inde ortada (JSON
okuması @`9b3eacf3d`; Dependencies). Ölçülen yalnız `/api/product-master/{everything}`'dir; öteki 184 rota ölçülmedi.

Bu paket ağ geçidine **tek bir ara katman** ekler. Ara katman Ocelot'tan, kimlik doğrulamadan ve kiracı çözümünden önce
koşar. İsteğin ham hedefini (`IHttpRequestFeature.RawTarget`) okur ve yalnız yol kısmını, sabit bir üst sınıra kadar
yinelemeli olarak çözer (bayt düzeyi yüzde çözümü; Runtime Constraints "Çözüm kuralı"). Şu durumlardan biri varsa istek
400 ve sabit bir hata koduyla durur:

- herhangi bir aşamada yol kesimlerinden biri `.` ya da `..` ise;
- turlar bittiğinde yolda geçerli bir `%XX` dizisi kalmışsa (`NotConverged`);
- herhangi bir aşamada yolda ters bölü (`\`) varsa;
- hedef, tanınan biçimlerden hiçbirine uymuyorsa ya da boşsa (`UnrecognizedTarget`; kapalı varsayılan).

Sorgu dizesine bakılmaz. Sorgu dizesinde `..` meşrudur ve isteği geçirmeye devam eder. Koruma rota tanımına bakmaz; bu
yüzden 185 rotanın hepsini ve rotası olmayan yolları aynı kuralla kapsar.

**Kullanıcı:** Yeni ekran yoktur. Meşru istekler bugünkü gibi geçer; bilinçli bedeller hariçtir: kesim içinde kodlanmış
ters bölü, bir kez kodlanmış `%XX` görünümlü veri (ör. `%255c`) ve üç turdan derin kodlama (Çözüm kuralı 4, 5 ve 6). Yol
aşma deneyen istek servise ulaşmaz. Cevapta
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
  kalır (Follow-up Items). Not: servis tarafı bir koruma ölçülen açığı kapatmaz, çünkü `..` sıkıştırması ağ geçidinin
  çıkışında olur (Module Summary "Sebep").
- Ağ geçidi çıkışında aşağı akış adresinin rota alt öneki içinde kaldığını denetleyen ikinci katman (Follow-up Items).
- `%00`, aşırı uzun UTF-8 (`%c0%ae`), `%uXXXX` ve tam genişlikte nokta gibi biçimler için **yeni ret kuralı**. Bu biçimler tablo
  testine "rota önekinden çıkmaz" iddiasıyla girer ama ret kuralı eklenmez (Açık kararlar (n)).

**Sahiplik:** Ağ geçidi MOD-0032'dir. Paketi CT · Yetki kulesi açtı (sahip kararı, başlık). Paketin sahibi ve bu işte
MOD-0032 karar sahibi MOD-0018 (Auth) sahibidir; hedef 2026-11-09 (sahip, 2026-11-02, "Evet, ben, 11-09"; başlıktaki
sahiplik kararı, Açık kararlar (j) kapandı).

## Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| Ara katman | `PathTraversalGuardMiddleware` (`gateway/Diten.ApiGateway/Middleware/`) | yeni |
| Saf karar yardımcısı | `GatewayPathInspector` — `Inspect(string method, string rawTarget)` → (izin / ret, ret nedeni, yapılan tur sayısı). Yöntem yalnız hedef biçimini tanımak için okunur (Çözüm kuralı 1). Durum tutmaz, G/Ç yapmaz | yeni |
| Sabit | `GatewayPathInspector.MaxDecodeRounds` (öneri `3`; Açık kararlar (c)) | yeni |
| Ret nedeni (iç, günlük için) | `DotSegment`, `NotConverged`, `Backslash`, `UnrecognizedTarget` | yeni — kullanıcıya gösterilmez, cevaba yazılmaz |
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

- `gateway/Diten.ApiGateway/Middleware/GatewayPathInspector.cs` — (yeni). Saf karar yardımcısı (Runtime Constraints
  "Çözüm kuralı").
- `gateway/Diten.ApiGateway/Middleware/PathTraversalGuardMiddleware.cs` — (yeni). Yöntemi ve `RawTarget`'i okur,
  yardımcıyı çağırır, retteyse 400 cevabını yazar ve tek bir `Warning` günlük satırı bırakır. Retteyse `next` çağrılmaz.
  `IHttpRequestFeature` yoksa ya da `RawTarget` boşsa yardımcıya boş dize verilir ve sonuç ret olur (Validation Rules).
- `gateway/Diten.ApiGateway/Program.cs` — tek satır: `app.UseMiddleware<PathTraversalGuardMiddleware>();`. Öneri yeri
  `UseSerilogRequestLogging` bloğundan (`:130-148`) hemen sonra ve metrik bloğundan (`:150-154`) önce. Satır koşullu metrik
  bloğunun (`if (observabilityOptions.Metrics.Enabled)`) **içine girmez**; girerse metrik kapalı ortamda koruma da kapanır
  (Açık kararlar (h), AC-6 ölçer). Böylece sağlık
  (`:156-171`), metrik, çerezden başlığa aktarım (`:173-183`), `UseAuthentication` (`:185`), `TenantResolutionMiddleware`
  (`:186`), `UseAuthorization` (`:187`) ve `UseOcelot` (`:189`) korumadan sonra gelir (Açık kararlar (b), (h)).
- `gateway/Diten.ApiGateway.Tests/GatewayKestrelProgramHost.cs` — (yeni). Ağ geçidinin kendi giriş noktasını
  (`Program.cs`) gerçek Kestrel'de `http://127.0.0.1:0` üzerinde açar. Kalıp mevcut `GatewayProgramHost`'un
  `HostBuilding` / `HostBuilt` gözlemcisidir (`GatewayPipelineCorrelationTests.cs:67-158`). Fark şudur: `TestServer`
  konmaz, çünkü ham hedef soket üzerinden bayt bayt gönderilir ve hiçbir istemci yolu normalleştirmez (desen
  `Mod0290Fu04ProductMasterTraversalTests.cs:104-116`). Servisin yerine kayıt tutan gerçek bir Kestrel konur. Bu konak her
  isteğin `RawTarget` değerini ve Kestrel'in yönlendirdiği `Request.Path` değerini kaydeder (desen `:60-74`). Gönderilen
  `ocelot.json`'un **yalnız** `DownstreamHostAndPorts` alanı servis yerine geçene çevrilir. Öteki her alan dosyada olduğu gibi
  kalır. Bunu geçici içerik kökündeki kopyayla ya da sonda eklenen yapılandırma kaynağıyla yapmak uygulayıcının seçimidir.
  İki sınır vardır (kod okuması @`9b3eacf3d`; koşturulmadı):
  - **Rotalar komut satırından ezilemez.** `Program.cs:15` `WebApplication.CreateBuilder(args)` ile komut satırı kaynağını
    kurar; `Program.cs:44` `ocelot.json`'u **ondan sonra** ekler. Yapılandırmada sonra eklenen kaynak kazanır (çerçeve
    kuralı), bu yüzden `--Routes:N:DownstreamHostAndPorts:…` biçimli bir argüman `ocelot.json` tarafından ezilir. Mevcut
    konağın komut satırı argümanları (`GatewayPipelineCorrelationTests.cs:105-110`) bu yüzden rota çevirmek için kullanılamaz.
  - **Geçici içerik kökü seçilirse** o köke `ocelot.json`'un çevrilmiş kopyasıyla birlikte `appsettings.json` ve
    `appsettings.Development.json` de kopyalanır. Mevcut konak içerik kökünü ikili klasörüne verir
    (`--contentRoot=` + `AppContext.BaseDirectory`, `GatewayPipelineCorrelationTests.cs:108`); kök değişirse `appsettings*.json`
    o kökten okunur. Kopyalanmazsa `JwtSettings:Issuer` / `Audience`, `TenantResolution:DevBypassEnabled` ve gözlemlenebilirlik
    ayarları yüklenmez ve konak gönderilen yapılandırmayla açılmamış olur. Kopyalar değiştirilmez.
- `gateway/Diten.ApiGateway.Tests/Mod0032Fu02GatewayPathInspectorTests.cs` — (yeni) (birim, saf yardımcı).
- `gateway/Diten.ApiGateway.Tests/Mod0032Fu02PathTraversalGuardTests.cs` — (yeni) (gerçek `Program.cs` + gerçek Kestrel
  + Ocelot + gönderilen `ocelot.json`). Sınıf `GatewayProgramCollection`'a girer
  (`GatewayPipelineCorrelationTests.cs:61-65`, `DisableParallelization = true`), çünkü `Program.cs` süreç düzeyinde Serilog
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
| Ocelot + gönderilen `ocelot.json` | 347 rota. Üst yol şablonunda yer tutucu (`{...}`) taşıyan **185** rota: 163'ü `{everything}` ile bitiyor; 7'sinde yer tutucu son kesimde (ör. `/api/shipment-bundle/shipments/{shipmentId}`); 15'inde ortada (ör. `/api/lskus/{id}/stock-behaviour`). Dört rota iki yer tutucu taşıyor (ör. `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios/{scenarioId}`), yani sınanacak yer tutucu konumu 189'dur. `{everything}` hiçbir şablonda sonda olmayan bir yerde geçmiyor. `/api/internal` üst ya da alt rotası yok; `GlobalConfiguration` yalnız `BaseUrl` taşıyor; yalnız iki rotada `AuthenticationOptions` var (`/api/v1/uploads`, `/api/v1/uploads/{everything}`). Yer tutucu taşıyan dört rotada üst ve alt önek farklı (`/api/v1/platform/workflows/instances/{everything}` → `/api/v1/workflow/instances/{everything}`; üç `/api/pv-case-intake-triage/{intakeDraftId}…` rotası → `/api/v1/pv-case-intake-triage/…`). Yer tutucu taşıyan 16 rota GET taşımıyor (ör. `/api/v1/notifications/{everything}`, `/api/lskus/{id}/stock-behaviour`). Üst öneki `/api/admin` ya da `/api/platform` kesimiyle başlayan (`TenantResolutionMiddleware.IsAdminPath`, `:457-462`; `/api/platform-auth` bu kesim eşleşmesine girmez) yer tutucu rotası 19'dur. Ölçüm: dosyanın JSON okuması (python3, salt okunur), @`9b3eacf3d`; ilk okuma @`6f169f677` yalnız `{everything}` rotalarını saymıştı (ikinci inceleme F-1) | Tablo testi rota listesini test anında dosyadan okur. Yer tutucu taşıyan her rota, her yer tutucu konumunda ve **kendi alt önekiyle** sınanır. Sayı koda gömülmez |
| `ocelot.json` şablon varsayımları | Hiçbir üst ya da alt yol şablonunda `?` yok. Hiçbir rotanın alt şablonunda, üst şablonunda olmayan bir yer tutucu yok; yani servise giden yolun her değişken parçası üst yoldan gelir. Ölçüm: JSON okuması (python3, salt okunur), @`9b3eacf3d` | Korumanın iki dayanağı bunlardır: sorgu dizesine bakmamak (şablon sorguyla eşleşmiyor) ve yalnız üst yolu denetlemek (alt yolun değişken parçası üst yoldan gelir). Bu varsayımlar test anında dosyadan ölçülür ve bozulursa test kırmızıya döner (AC-16) |
| `Program.cs` boru hattı | `UseCors` (`:128`) → `CorrelationIdMiddleware` (`:129`) → `UseSerilogRequestLogging` (`:130-148`) → metrik (`:150-154`, koşullu) → sağlık (`:156-171`) → çerez → başlık (`:173-183`) → `UseAuthentication` (`:185`) → `TenantResolutionMiddleware` (`:186`) → `UseAuthorization` (`:187`) → `UseOcelot` (`:189`). Yol aşma denetimi yok. `grep RawTarget` `Middleware/` ve `Authentication/` altında eşleşme vermedi | Kayıt yeri Repo Scope'ta |
| Test konağı deseni | `GatewayProgramHost` gerçek `Program.cs`'i `TestServer` ile açıyor (`GatewayPipelineCorrelationTests.cs:67-158`; koleksiyon tanımı `:61-65`). `Mod0290Fu04ProductMasterTraversalTests` gerçek Kestrel ve ham soket kullanıyor ama **kendi** konağını kuruyor: `Program.cs` yok, yalnız Ocelot var (`:76-102`) | Yeni konak iki deseni birleştirir. Sabotaj kanıtı ancak `Program.cs`'teki kaydı ölçen testle verilebilir |
| `TenantResolutionMiddleware` | Yolu `context.Request.Path` ile sınıflıyor (`:35`, `:45`, `:73`, `:87`, `:93`, `:99`, `:161`, `:169`, `:180`, `:215`, `:229`). `OPTIONS` her yolda kiracı çözümünü atlıyor (`:35`). **Belirteçsiz** istek kiracı yolunda 400 "Missing Tenant" alır (`:213-226`); bu dal `SetPasswordPublicEndpointGuardTests.cs:52-59`'da Production ortamında ölçülüyor. Development ortamında `TenantResolution:DevBypassEnabled` açıksa (`appsettings.Development.json:10-11`) eksik kiracının yerine sahte kiracı konur (`:204-211`) ve istek geçer; korunan Ocelot rotasında cevap o zaman Ocelot'un 401'idir (çerçeve bilgisi, ölçülmedi). `tenant_user` belirteci `/api/admin` ve `/api/platform` öneklerinde (kiracı kapsamlı örgüt yolları hariç, `:457-481`) 403 "Forbidden Actor" alır (`:169-173`). Kiracı çözümünün ret gövdeleri `title`, `status`, `detail`, `traceId` taşır, **`code` taşımaz** (`:551-568`). Kod okuması + mevcut test | Tablo testi ve meşru yol testi, isteği bugün `TenantResolutionMiddleware`'in geçirdiği biçimde gönderir: testte basılan geçerli `tenant_user` belirteci ve test başına rastgele kiracı; yönetici öneklerinde ayrıca `platform_admin` belirteci (AC-2). Biçimi DL-1'in ilk adımı ölçer (Implementation Notes "DL-1'in ilk adımı", İ2). Kiracı çözümü de 400 dönebildiği için korumanın ölçütleri durum koduna değil, gövdedeki `code: GATEWAY_PATH_REJECTED` değerine bakar (AC-5) |
| `GatewayJwtAuthenticationHandler` | `Issuer` ve `Audience`'ı yapılandırmadan okuyup doğruluyor (`ValidateIssuer = true`, `ValidateAudience = true`); imza anahtarları `JwtSecretRotationResolver.GetValidationKeys()`'ten geliyor (`GatewayJwtAuthenticationHandler.cs:33-46`). Çözücü `JwtSettings:Secret` ve `JwtSettings:PreviousSecrets` değerlerinden anahtar kuruyor (`services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/JwtSecretRotationResolver.cs:27-45`). Development değerleri `Issuer: diten-auth-service`, `Audience: diten-erp` (`appsettings.Development.json:54-55`). Mevcut test konağı Development ortamında açılıyor ve rastgele bir sır geçiriyor (`GatewayPipelineCorrelationTests.cs:104-110`). Okuma @`6c3f1c3d0` | Test belirteci bu sırla, bu `iss` ve `aud` değerleriyle basılır. Üretim kodunda sabit kiracı ya da sır yoktur |
| Kayıt satırı | `MOD-0032-FU02` satırı yok | Ready-for-dev Checklist |
| Onay kapısı (CT-APR-01 / CT-APR-02) | Paket `ready-for-dev` (yalnız inceleme için). İlk inceleme 2026-11-02 UYGUN DEĞİL (13 bulgu, kapandı); ikinci inceleme 2026-11-02 KOŞULLU (14 bulgu, engel yok, kapandı); üçüncü bakış 2026-11-02 KOŞULLU (15 bulgu, engel yok); bu revizyon on beş bulguyu kapatır. Onaylayıcı Sahip (MOD-0018 sahibi); `high = evet` onay satırı yazılırken ölçülür; birleştirme ikinci kişinin. Onay satırı yok | Ready-for-dev Checklist |

## Runtime Constraints

- **Portlar (AGENTS.md §3):** Ağ geçidi 5000'dir. Yeni port yoktur. Testler `127.0.0.1:0` (işletim sisteminin verdiği boş port)
  kullanır.
- **Kiracı (multi-tenancy):** Koruma kiracı verisine dokunmaz ve kiracı okumaz. Retten sonra istek kiracı çözümüne ulaşmaz.
  Kiracı belirteci yalnız testte üretilir. Üretim kodunda sabit ya da varsayılan kiracı yoktur.
- **Yetki (security-jwt):** Koruma yetki vermez ve kimlik doğrulamanın yerine geçmez. Servislerin kendi `JwtBearer` ve
  `[HasPermission]` denetimleri bugünkü gibi kalır. Ret, kimliği doğrulanmış ve kimliksiz istek için aynıdır. Saldırgan,
  cevaba bakarak belirtecinin geçerli olup olmadığını öğrenemez.
- **Çözüm kuralı (`GatewayPathInspector`):**
  1. **Hedef biçimi (kapalı varsayılan).** Girdi yöntem ve `RawTarget`'tir. Biçim yalnız dize işlemiyle tanınır; `Uri`,
     `Uri.AbsolutePath` ya da başka bir ayrıştırıcı kullanılmaz. Yalnız şu dört biçim tanınır; başka her hedef ret alır
     (`UnrecognizedTarget`, 0 tur):
     - **Köken biçimi:** hedef `/` ile başlar. Yol, hedefin ilk `?`'ten önceki kısmıdır.
     - **Mutlak biçim:** hedef `http://` ya da `https://` ile başlar (şema ASCII büyük-küçük harf duyarsız karşılaştırılır).
       Şemadan sonraki yetki kısmı ilk `/`, `?` ya da `#`'te biter. Yetki kısmında `\` varsa ret (`UnrecognizedTarget`).
       Yol, yetki kısmından sonraki `/`'den başlayıp ilk `?`'ten önce biten kısımdır; yetki kısmından sonra `/` yoksa yol
       boştur ve hedef izin alır (`http://host`). Yetki kısmının geri kalanı denetlenmez.
     - **Yıldız biçimi:** hedef tam olarak `*`'dır **ve** yöntem `OPTIONS`'tır: izin. Başka yöntemle `*` ret alır.
     - **Yetki biçimi:** hedef tam olarak `ad:port` biçimindedir **ve** yöntem `CONNECT`'tir: izin. Katı biçim: `ad` en az bir
       karakterden oluşur ve yalnız ASCII harf, rakam, `.` ve `-` taşır; `port` 1–5 ASCII rakamdır; başka karakter yoktur
       (köşeli parantezli IPv6 dahil). Başka yöntemle ya da başka biçimde ret alır.
     Boş hedef ret alır (Validation Rules). Bu biçimlere uymayan örnekler: `x/../y` (`/` ile başlamıyor), `http:/x/..`
     (`http://` ile başlamıyor), `GET *`, `GET h:443`. Gerekçe: tanınmayan biçimi yol saymak, ayrıştırıcıların farklı
     okuduğu bir hedefi denetimden geçirebilir; tanınmayan biçimin meşru bir istemcisi bilinmiyor (ölçülmedi). İzin alan
     yıldız, yetki ve yolu boş mutlak hedeflerde `DecodeRounds = 0`'dır (8. kural): denetlenecek yol yoktur; isteğin rotaya
     eşleşip eşleşmediği Ocelot'un işidir. `ocelot.json`'da `CONNECT` taşıyan rota yok (JSON okuması @`6c3f1c3d0`: 347
     rotanın hepsi `UpstreamHttpMethod` taşıyor; yöntemler GET, POST, PUT, PATCH, DELETE, OPTIONS). Kestrel'in hangi
     biçimleri uygulamaya ulaştırdığı ve `RawTarget`'e ne koyduğu ölçülmedi (Implementation Notes "Teslimde ölçülen ya da
     seçilen"; ham `\` ve ham `#` için "DL-1'in ilk adımı", İ7).
  2. Yol, ilk `?`'te sorgudan ayrılır (1. kural). Sorguya hiç bakılmaz.
  3. **Çözücü:** `GatewayPathInspector`'ın kendi **bayt düzeyi yüzde çözücüsü**dür. `Uri.UnescapeDataString`,
     `WebUtility.UrlDecode` ve `HttpUtility.UrlDecode` kullanılmaz. Tanım:
     - **Geçerli `%XX` dizisi:** `%` ve ardından iki onaltılık rakam (`0-9`, `a-f`, `A-F`; büyük-küçük harf fark etmez).
       Başka her `%` (ör. `%zz`, `%u002e`, sondaki tek `%`, `100%.pdf`) geçersizdir ve olduğu gibi kalır.
     - **Bir tur:** dize soldan sağa bir kez taranır; her geçerli `%XX` dizisi `XX` değerindeki tek bayta, o bayt da aynı
       numaralı karaktere (U+0000–U+00FF, Latin-1 eşlemesi) çevrilir. Turun ürettiği karakterler aynı turda yeniden
       taranmaz.
     - **UTF-8 kod çözümü yapılmaz.** Bu yüzden geçersiz UTF-8 de olduğu gibi **kalmaz**: `%ff` → `ÿ` (U+00FF),
       `%c0%ae` → `À®`. Karar ve gerekçe: `Uri.UnescapeDataString` geçersiz UTF-8 dizisini kodlu bırakır (çerçeve bilgisi,
       ölçülmedi); o çözücüyle `%ff` hiç değişmez ama geçerli bir `%XX` olarak kalır ve 5. kural belirsizleşirdi. Bayt
       düzeyi çözümde her geçerli `%XX` her turda mutlaka çözülür. Kuralların baktığı karakterler (`.`, `/`, `\`, `?`,
       `#`, `%` ve onaltılık rakamlar) ASCII'dir; UTF-8'de ASCII baytı hiçbir çok baytlı dizinin içinde geçmez. Bu yüzden
       Latin-1 eşlemesi bu karakterleri herhangi bir UTF-8 çözücüsüyle aynı yerde üretir. Aşırı uzun UTF-8 (`%c0%ae`) bu
       çözücüde `.` olmaz; bu biçim ret kuralına girmez, AC-14 ile ölçülür (Açık kararlar (n)).
     - `+` boşluk sayılmaz, olduğu gibi kalır.
     - `%uXXXX` (IIS biçimi) desteklenmez: `%u` geçerli bir `%XX` değildir ve olduğu gibi kalır. `/a/%u002e` hiç
       çözülmez ve izin alır.
  4. **Turlar ve aşamalar.** Aşama 0 ham yoldur; 4. ve 6. kural önce aşama 0'da uygulanır. Sonra en çok
     `MaxDecodeRounds` tur yapılır. Her turun başında yolda geçerli bir `%XX` dizisi yoksa turlar biter. Varsa bir tur
     çözülür ve 4. ile 6. kural yeni aşamada uygulanır. **Kesim kuralı:** her aşamada yol `/`, `\`, `?` ve `#` ile
     kesimlere bölünür (Açık kararlar (p), öneri (p1)). Aşama 0'da ham `?` yolda bulunmaz (2. kural onu sorgu sayar); ham
     `#` ise aşama 0'da da kesimi bitirir: `/a/..#b` aşama 0'da ret alır (0 tur), sonuç yoldaki başka bir `%XX`'e bağlı
     değildir. Çözülmüş aşamalarda `..%3f` aşama 1'de `..?` olur ve `?`'ten önceki kesim `..`'dır. Kesimlerden biri tam
     olarak `.` ya da `..` ise sonuç ret olur (`DotSegment`).
     **Üç tur çözümün bedeli (sınırın içinde):** yolda bir kez kodlanmış, `%XX` gibi görünen meşru veri de iki kez
     çözülür. Örnek: düz metin `a%5cb` taşıyan bir dosya adı istemcide bir kez kodlanınca `/files/a%255cb` olur; aşama 1'de
     `%5c`, aşama 2'de `\` olur ve sonuç ret olur (`Backslash`, 2 tur). Aynı biçimde bir kez kodlanmış `%2e%2e` metni
     (`%252e%252e`) ret alır. Bu bedel aynı zamanda bugünkü bir zayıflığı kapatır: korumasız tabanda ağ geçidi
     `/api/product-master/files/a%255cb` isteğini servise `…/a%5cb` olarak iletiyor ve servis onu `…/a\b` olarak
     yönlendiriyor (ek-02 son satırı); yani bugün bu meşru veri servise zaten bozulmuş ulaşıyor. AC-7 bunu bir satırla
     gösterir.
  5. **Yakınsama kuralı (tek tanım):** turlar bittiğinde yolda geçerli bir `%XX` dizisi kalmışsa sonuç ret olur
     (`NotConverged`). Paket boyunca 5. kural yalnız bu cümleyle anılır. Bu kural kat sayısından bağımsızdır: tur sınırının
     ötesindeki her kodlama katmanı en az bir geçerli `%XX` (`%25…`) bırakır. Eski taslaktaki "son turdan sonra `%2e`,
     `%2f`, `%5c` kalıntısı" kuralı bu iddiayı taşıyamıyordu: yalnız sınırın bir fazlası katı yakalıyordu. Beş kat
     `%252525252e` üç turdan sonra `%252e` olarak kalır ve `%2e` içermez (birinci inceleme bulgusu F-2). Yakınsama
     kuralının bedeli şudur: üç turdan derin kodlanmış **meşru** bir karakter de reddedilir (ör. dört kat kodlanmış `A`,
     `%25252541`). Böyle bir meşru istemci bilinmiyor (ölçülmedi). Bu bedel bilinçlidir ve AC-7'de bir satırla gösterilir.
  6. Herhangi bir aşamada yolda `\` varsa sonuç ret olur (`Backslash`). **Bilinçli bedel:** kesim içinde tek kodlanmış
     ters bölü taşıyan meşru bir yol da reddedilir (ör. dosya adı `a\b.txt` → `/files/a%5Cb.txt`, aşama 1'de `\`). Sahip
     bu bedeli kabul etti (2026-11-02, "Düzelt, onaya git"; ikinci inceleme F-12). Böyle bir meşru istemci bilinmiyor
     (ölçülmedi). AC-3'teki bedel satırı bunu gösterir.
  7. Geçersiz `%` dizileri olduğu gibi kalır (3. kural). Tek başına ret nedeni değildir ve 5. kuralı da tetiklemez.
  8. **`DecodeRounds`:** sonucun taşıdığı, gerçekten yapılan çözüm turu sayısıdır: `0 ≤ DecodeRounds ≤ MaxDecodeRounds`.
     Çözülmeyen hedeflerde (1. kural: `OPTIONS *`, `CONNECT` ile yetki biçimli hedef, yolu boş mutlak hedef) ve
     `UnrecognizedTarget` retlerinde `0`'dır. Aşama 0'da verilen retlerde `0`'dır. Geçerli `%XX` taşımayan yol çözülmeden izin alır ve `0`'dır. Aşama k'da verilen ret `k`'dır.
     `NotConverged` retlerinde `MaxDecodeRounds`'tur.
- **Neden tur sınırı yeterli:** Güvenlik tur sayısından değil, yakınsama kuralından (5. kural) gelir: yakınsayan bir yol
  her aşamada denetlenmiş ve geçerli `%XX` taşımayan son biçimine inmiştir; yakınsamayan her yol ret alır. Tur sınırı bu
  yüzden yalnız aşırı engellemenin (bedelin) nerede başladığını seçer. Ölçülen zincirde üç çözüm noktası vardır (Module
  Summary "Sebep"; ikisi çıkarım): ağ geçidinin Kestrel'i (`Request.Path`'te bir tam çözüm), ağ geçidinin çıkışı (Ocelot →
  `HttpClient` / `System.Uri`; `%2e`'yi çözüp `..`'yı sıkıştırır, `%2f`'yi kodlu bırakır) ve servisin Kestrel'i (bir tam
  çözüm; ek-02 son satırı). Öneri sınırı `3` bu üç noktayı karşılar. Sınırdan derin her kodlama 5. kurala takılır; bunu
  AC-2'deki dört ve beş kat satırları ile AC-7 ölçer. **Yeniden ölçüm tetikleyicisi:** Ocelot ya da .NET (`HttpClient`,
  `System.Uri`, Kestrel) sürüm yükseltmesi ve zincire yeni bir çözücü eklenmesi. Tetiklenince ek-01 / ek-02 ölçümü
  (`Mod0290Fu04ProductMasterTraversalTests` konağında geçici satırlarla) yeniden koşulur ve sonuç yeni bir ek kayda
  yazılır (Follow-up Items).
- **Sınırlı iş:** Bir tur ancak yolda geçerli bir `%XX` dizisi varsa yapılır ve her birini tek karaktere indirir; yani
  her tur dizeyi kısaltır. Döngü sabit sınırla biter. Uzunluk sınırı Kestrel'indir (`MaxRequestHeadersTotalSize = 64 KB`, `Program.cs:38-41`; istek satırı sınırı Kestrel varsayılanı). Koruma yeni bir
  uzunluk sınırı koymaz.
- **Cevap:** `400`, `Content-Type: application/problem+json`, gövde `{"status":400,"code":"GATEWAY_PATH_REJECTED","traceId":"…"}`.
  `title` ve `detail` yoktur: kullanıcıya görünen metin yoktur ve ret nedeni cevaba yazılmaz. `contentType`, ağ geçidinin
  bugünkü deseniyle `WriteAsJsonAsync`'e geçirilir (`TenantResolutionMiddleware.cs:551-568`). Koruma
  `CorrelationIdMiddleware`'den sonra koştuğu için cevap `X-Correlation-Id` başlığını taşır. Biçim Açık kararlar (a) ve
  (i)'ye bağlıdır.
- **`traceId` kaynağı:** `EdgeCorrelation.CorrelationIdOf(context)` (`gateway/Diten.ApiGateway/Observability/EdgeCorrelation.cs:24-25`).
  Bu, `CorrelationIdMiddleware`'in ürettiği ve cevabın `X-Correlation-Id` başlığına yazdığı değerdir
  (`services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/CorrelationIdMiddleware.cs:29-39`). Ağ geçidi
  arayanın imzasız `X-Correlation-Id` değerini benimsemez (`:50-75`; yalnız kendi imzasını taşıyan değeri benimser,
  `:62-70`). `context.TraceIdentifier` **kullanılmaz**: Ocelot'un `RequestId` ara katmanı oraya arayanın `RequestId`
  başlığını denetimsiz yazar (`EdgeCorrelation.cs:19-23`; istek günlüğü de aynı nedenle `CorrelationIdOf` kullanır,
  `Program.cs:142-143`). Koruma bugün Ocelot'tan önce koştuğu için `TraceIdentifier` o anda üretilen değeri taşır
  (`CorrelationIdMiddleware.cs:38`); kaynak yine de `CorrelationIdOf`'a sabitlenir, çünkü kayıt yeri değişirse
  `TraceIdentifier` arayanın değerine dönebilir. `Activity.Current.TraceId` seçilmez: o değer cevap başlığıyla eşleşmez ve
  arayan ret cevabını günlükteki `CorrelationId` ile ilişkilendiremez.
- **`Response<T>` muafiyeti (AGENTS.md §6, `AGENTS.md:135` "API Yanıt: `Response<T>` envelope + `CustomBaseController`"):**
  Ret cevabı `Response<T>` zarfı değildir. Gerekçe: koruma bir ağ geçidi ara katmanıdır, bir servis ucu değildir.
  `CustomBaseController`, MediatR ya da işleyici yoktur; cevap Ocelot'tan ve her servisten önce yazılır. Ağ geçidinin bugünkü
  bütün ret cevapları da zarfsız `application/problem+json`'dur (`TenantResolutionMiddleware.cs:551-593`). Muafiyet yalnız bu
  ret cevabı içindir; servislerin cevapları değişmez.
- **Dil:** Kullanıcıya görünen metin olmadığı için resx yoktur. İstemci kodu ekranda göstermek isterse çeviriyi kendisi
  yapar. Bugün böyle bir istemci yoktur.
- **Ayar anahtarı yok:** Korumayı kapatan bir yapılandırma anahtarı eklenmez (Açık kararlar (k)). Koruma her ortamda açıktır.
- **Günlük:** Her rette korumanın kendi `Warning` satırı yazılır (`SourceContext`:
  `Diten.ApiGateway.Middleware.PathTraversalGuardMiddleware`). Alanlar: `Reason` (iç ret nedeni), `Method`,
  `RawTargetLength`, `DecodeRounds` (Çözüm kuralı 8; aşama 0 retlerinde `0`). `CorrelationId` `LogContext` üzerinden gelir. Ham hedef ve çözülmüş yol **bu satıra
  yazılmaz**, çünkü yolda kimlik, e-posta ya da ad olabilir (logging-observability "PII"). Bu satır Açık kararlar (d)'ye
  bağlıdır. `Error` düzeyi kullanılmaz, çünkü ret beklenen bir saldırı gürültüsüdür. **Bilinen durum:** istek günlüğü
  (`UseSerilogRequestLogging`, `Program.cs:130-148`) reddedilen istekte de satırını yazar ve o satır `RequestPath` alanını,
  yani Kestrel'in bir kez çözdüğü `Request.Path`'i taşır (`Program.cs:144`). Bu paket o satırı değiştirmez (Açık kararlar
  (d), Follow-up Items). Korumanın "ham hedef yazılmaz" iddiası yalnız kendi satırı içindir.

## Layout & Shell Contract

- `shell: none`. Yeni sayfa yok. Hiçbir Razor düzeni (`Layout = "..."`) eklenmez ya da değişmez.
- Ön yüz dosyasına dokunulmaz. Ölçüt: `git diff --name-only <taban>..HEAD` çıktısında `frontend/` ile başlayan satır yoktur
  (Acceptance Criteria, kapsam ölçütü). `<taban>`, DL-1 kod dalının açıldığı commit'tir (AC-10).

## Backend File Convention

Golden Reference CQRS düzeni (komut / sorgu / işleyici) bu pakete uygulanmaz. Komut, sorgu ya da işleyici yoktur. Ağ
geçidinin bugünkü düzeni izlenir:

- Ara katmanlar `gateway/Diten.ApiGateway/Middleware/` altındadır. `TenantResolutionMiddleware` gibi `public sealed class`,
  kurucuda `RequestDelegate` ve `ILogger<T>` alır, `InvokeAsync(HttpContext)` taşır.
- Ad alanı `Diten.ApiGateway.Middleware`'dir.
- Karar yardımcısı `public static class GatewayPathInspector`'dır. `HttpContext` almaz. Girdi iki `string`'dir (yöntem ve
  ham hedef), çıktı salt-okunur bir sonuç kaydıdır (`readonly record struct` öneri). Böylece birim testi ara katmanı ve Kestrel'i açmadan her kuralı ölçer.
- Testler `Diten.ApiGateway.Tests` ad alanındadır. Dosya adları depodaki modül öneki desenini izler (`Mod0290Fu04…`,
  `Mod0173Pm2…`).

## Frontend File Contract

Ön yüz dosyası yok. Yeni sayfa, kısmi görünüm, JS ya da resx yok.

## Validation Rules

Form alanı yok. Doğrulanan şey isteğin hedefidir.

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `RawTarget` yol kısmı | Evet | Runtime Constraints "Çözüm kuralı" 1–8. Ret → 400 `GATEWAY_PATH_REJECTED` | — | **Kapalı varsayılan:** `IHttpRequestFeature` yoksa, `RawTarget` boşsa ya da okunamıyorsa istek ret alır (`UnrecognizedTarget`, 0 tur). `Request.Path`'e dayanan bir yedek yol **yoktur**: o değer Kestrel'in bir kez çözüp normalleştirdiği yoldur ve ham hedefin yerini tutmaz. Test konağı notu: `GatewayPipelineCorrelationTests` gerçek `Program.cs`'i `TestServer` üzerinde açar (`GatewayProgramHost`, `:67-158`; depoda bu konağı kullanan tek test dosyası, `grep` 2026-11-02) ve koruma orada da koşar. Kendi konağını kuran öteki testler (ör. `SetPasswordPublicEndpointGuardTests`, `RejectedTokenClaimRevivalGuardTests`) `Program.cs`'i açmaz ve korumayı içermez; `TestServer`'ın `RawTarget`'i doldurup doldurmadığı ölçülmedi. Boş bırakıyorsa `GatewayPipelineCorrelationTests` korumanın 400'ünü alır ve AC-11 kırmızı olur. DL-1'in ilk adımı bunu ölçer (Implementation Notes "DL-1'in ilk adımı"); boş çıkarsa DL-1 durur, çünkü düzeltme bu paketin izinli dosyaları dışındadır (AC-10) |
| Sorgu dizesi | Hayır | Denetlenmez. `..`, `%2e%2e%2f`, `\` sorguda meşrudur | — | — |
| Çözüm tur sayısı (`DecodeRounds`) | — | `0 ≤ DecodeRounds ≤ MaxDecodeRounds` (öneri 3). Bir tur yalnız yolda geçerli bir `%XX` dizisi varsa yapılır. `0`: çözülmeyen hedefler (`OPTIONS *`, yetki biçimli hedef, yolu boş mutlak hedef), aşama 0 retleri ve geçerli `%XX` taşımayan yollar (Çözüm kuralı 8) | — | — |
| Yöntem | — | Korumaya ulaşan bütün yöntemlerde aynı kural; ön uçuş olmayan `OPTIONS` dahil. **CORS ön uçuşu** (`OPTIONS` + `Origin` + `Access-Control-Request-Method`) korumaya ulaşmaz: `UseCors` (`Program.cs:128`) korumadan önce koşar ve ön uçuşu 204 ile bitirir. Bu çerçeve bilgisidir (ASP.NET Core `CorsMiddleware`), ölçülmedi; DL-1'in ilk adımı ölçer, AC-15 sınar. Ön uçuş Ocelot'a da ulaşmadığı için servise yol taşımaz. Yöntem ayrıca hedef biçimini tanımakta kullanılır: `*` yalnız `OPTIONS`, `ad:port` yalnız `CONNECT` ile izin alır (Çözüm kuralı 1) | — | — |

İlişkili kontroller: Kesim denetimi her aşamada `/`, `\`, `?` ve `#` ile yapılır; aşama 0'da ham `?` zaten sorgudur
(Açık kararlar (p)). `...`, `a..b`, `..foo`, `.well-known`, `report.v1.pdf` gibi kesimler `.` ya da `..`'ya eşit olmadığı
için geçer. Kesim içinde kodlanmış ters bölü (`a%5Cb`) ve bir kez kodlanmış `%5c` metni (`a%255cb`) bilinçli bedel olarak
reddedilir (Çözüm kuralı 4 ve 6).

## Failure Path to Verify

- **Çift kodlanmış nokta-nokta (ölçülen açık)**
  - Expected: `/api/product-master/%252e%252e/internal/v1/product-master/validate` → 400 `GATEWAY_PATH_REJECTED`. Servis
    yerine geçene hiçbir istek ulaşmaz.
- **Ham, tek, çift ve üç kat kodlanmış biçimler; karışık harf; `%2f` / `%5c` ayırıcı**
  - Expected: 400 ve gövdede `code: GATEWAY_PATH_REJECTED`. Servise hiçbir istek ulaşmaz. Ret, rota önekinden bağımsızdır
    (yer tutucu taşıyan 185 rotanın hepsinde ve rotası olmayan yollarda aynı).
- **Tur sınırından derin kodlama (dört kat `%2525252e%2525252e/`, beş kat `%252525252e%252525252e/`)**
  - Expected: 400 (`NotConverged`). Döngü sınırı aşılmaz. Kat sayısı arttıkça sonuç değişmez.
- **Meşru yol (`report.v1.pdf`, `v1.2`, `...`, `a..b`, `100%25.pdf`, `a%2Fb`, sorguda `..`)**
  - Expected: Bugünkü gibi geçer. Servis yerine geçen konak isteği rota öneki altında alır ve 200 döner. Sorgu bozulmadan
    ulaşır. Korumasız taban ölçüldü: bu biçimlerin hepsi bugün 200 alır ve rota öneki içinde kalır; `a%2Fb` ve sorgu
    dizesi servise olduğu gibi iletilir (ek-02).
- **Hedef biçimi tanınmıyor ya da boş (`x/../y`, `http:/x/..`, `GET *`, boş `RawTarget`)**
  - Expected: ret (`UnrecognizedTarget`, kapalı varsayılan; Çözüm kuralı 1, Validation Rules). Birim düzeyinde ölçülür;
    Kestrel'in bu biçimleri uygulamaya ulaştırıp ulaştırmadığı ölçülmedi.
- **Kimliksiz istek, korunan rotada yol aşma (`/api/v1/uploads/%252e%252e/x`)**
  - Expected: 400 ve gövdede `code: GATEWAY_PATH_REJECTED`. Korumasız taban: kiracı çözümünden 400 "Missing Tenant"
    (gövdede `code` yok; `TenantResolutionMiddleware.cs:213-226`), ya da Development'ta dev bypass açıksa Ocelot'tan 401.
    Taban ölçülür. Durum kodu tek başına ayırt etmez (iki taraf da 400 olabilir); ayıran gövdedeki `code`'dur. Koruma kimlik
    doğrulamadan önce koşar ve cevap belirtece göre değişmez.
- **Kiracı çözümünün reddedeceği istek + yol aşma**
  - Expected: Korumanın 400 gövdesi (`code: GATEWAY_PATH_REJECTED`) döner, `TenantResolutionMiddleware`'in gövdesi
    (`title: "Invalid Tenant Header"`, `code` yok) dönmez.
- **Sağlık ve metrik uçlarında yol aşma (`/health/%2e%2e/x`, `/metrics/%252e%252e/`)**
  - Expected: 400 ve gövdede `code: GATEWAY_PATH_REJECTED` (Açık kararlar (b) önerisi). Düz `/health`, `/health/live`, `/health/ready` ve `/metrics` bugünkü durum
    koduyla cevap verir.
- **CORS ön uçuş isteği + yol aşma**
  - Expected: `UseCors` ön uçuşu korumadan önce 204 ile bitirir; servis yerine geçen konak hiçbir istek kaydetmez. Ret
    yoktur, çünkü istek ne Ocelot'a ne servise ulaşır. Bu beklenti çerçeve bilgisidir, ölçülmedi; DL-1'in ilk adımı ölçer
    (Implementation Notes "DL-1'in ilk adımı"), AC-15 sınar.
- **Ön uçuş olmayan `OPTIONS` ve çapraz kökenli `GET` + yol aşma**
  - Expected: 400, `code: GATEWAY_PATH_REJECTED`. Koruma `UseCors`'tan sonra koştuğu için çapraz kökenli ret cevabı
    `Access-Control-Allow-Origin` başlığını taşır ve tarayıcı kodu okuyabilir (Açık kararlar (h), AC-15).
- **Metrik kapalı ortam + yol aşma**
  - Expected: `Observability:Metrics:Enabled=false` iken de ölçülen istek 400 alır. Kayıt satırı koşullu metrik bloğunda
    değildir (Açık kararlar (h), AC-6).
- **Kesim içinde kodlanmış ters bölü (bilinçli bedel)**
  - Expected: `/api/product-master/files/a%5Cb.txt` gibi meşru görünen bir yol 400 ve `code: GATEWAY_PATH_REJECTED` alır
    (Çözüm kuralı 6; Sahip bedeli kabul etti, 2026-11-02). Bir kez kodlanmış `%5c` metni (`a%255cb`) de 400 alır (Çözüm
    kuralı 4'teki üç tur bedeli); bugün o istek servise `a\b` olarak bozulmuş ulaşıyor (ek-02).
- **`?` / `#` ile kesim sonu (`..%3f`, `..%23`, `%252e%252e%253f`, ham `..#`)**
  - Expected: 400 ve `code: GATEWAY_PATH_REJECTED` (Çözüm kuralı 4; Açık kararlar (p), öneri (p1)). Ham `..#` aşama 0'da,
    öbürleri çözülmüş aşamada ret alır.
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
- `ocelot.json` değişmez. Koruma Ocelot'tan önce koştuğu için rota tanımına bağlı değildir. Yer tutucu taşıyan 185
  rotanın (163 `{everything}`, 7 son kesim, 15 ortada; Dependencies) hepsini ve rotası olmayan yolları aynı biçimde kapsar.
- Koruma iki şablon varsayımına dayanır: hiçbir şablonda `?` yok ve alt şablonda üstte olmayan yer tutucu yok
  (Dependencies). İkisi test anında dosyadan ölçülür; `ocelot.json` bunları bozacak biçimde değişirse test kırmızıya döner
  ve bu karar yeniden açılır.
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
- [ ] AC-2 (zorunlu) — **Yer tutucu taşıyan tüm rotalar, tüm biçimler:** Tablo testi rota listesini test anında gönderilen
  `ocelot.json`'dan okur. Üst yol şablonunda yer tutucu (`{...}`) taşıyan **her** rota sınanır: `{everything}` ile bitenler,
  yer tutucusu son kesimde olanlar ve ortada olanlar (bugün 185 rota, 189 yer tutucu konumu; Dependencies). Okunan rota
  ve konum sayısı test çıktısına yazılır. Sayılar koda gömülmez; rota sayısı sıfırsa test başarısız olur. İstek yolu şöyle
  kurulur: sınanan yer tutucunun yerine biçim ve ardından `internal/x` konur; şablonun öteki yer tutucuları sabit meşru
  değer `p1` alır; şablonun kalanı olduğu gibi kalır (ör. `/api/lskus/%252e%252e/internal/x/stock-behaviour`). İki yer
  tutucu taşıyan rotada her konum ayrı sınanır. Biçimler (27): ham `../`, `./`, ham ters bölülü `..\`, ham `..#` (ham `#`
  aşama 0'da kesim sonu), `%2e%2e/`, `%2E%2e/`, `.%2e/`, `%2e./`, `%2e%2e%2f`, `%2E%2E%2F`, `..%2f`, `..%5c`, `%5c..%5c`,
  `..%3f` (çözülmüş `?` kesim sonu), `..%23` (çözülmüş `#` kesim sonu), `%252e%252e/`, `%252E%252e/`, `%252e./`,
  `.%252e/`, `%252e%252e%252f`, `%252E%252E%252F` (tam büyük harf çift kod), `%252e%252e%253f` (çift kodlu `?` kesim
  sonu), `..%255c` (çift kodlu ters bölü), `%252e%252e%255c`, `%25252e%25252e/` (üç kat), `%2525252e%2525252e/` (dört
  kat, tur sınırının ötesi), `%252525252e%252525252e/` (beş kat; yakınsama kuralının kat sayısından bağımsız olduğunu
  ölçer). Ham `\` ve ham `#` taşıyan hedefi Kestrel'in uygulamaya ulaştırdığı ölçülmedi; DL-1'in ilk adımı ölçer ve
  Kestrel bunları korumadan önce reddederse DL-1 durur (Implementation Notes "DL-1'in ilk adımı"). Yöntem, rota GET taşıyorsa GET'tir, taşımıyorsa rotanın
  listesindeki ilk yöntemdir. Her istek `tenant_user` belirteciyle gönderilir. Üst öneki `/api/admin` ya da `/api/platform`
  kesimiyle başlayan rotalar (bugün 19) ayrıca `platform_admin` belirteciyle de gönderilir: korumasız tabanda `tenant_user`
  bu öneklerin çoğunda kiracı çözümünde 403 "Forbidden Actor" alır (`TenantResolutionMiddleware.cs:169-173`), kiracı
  kapsamlı örgüt yollarında (`:468-481`) ise kod yorumuna göre `platform_admin` reddedilir (`:467`; kod yolu
  ölçülmedi); iki belirteçle her rotada taban erişilebilirliği ölçülmüş olur. Her satır iki şeyi ölçer: cevap 400'dür ve gövdesi `code: GATEWAY_PATH_REJECTED`
  taşır; servis yerine geçen konak, sınanan yer tutucudan önceki **alt** şablon öneki dışında yönlendirilmiş bir istek
  kaydetmemiştir (konak koruma varken hiç istek kaydetmez). Durum koduna tek başına bakılmaz, çünkü kiracı çözümü de 400
  dönebilir. Kapsam notu: `/api/lookups/{everything}` rotası da bu tablodadır. Bu önek kiracı çözümünün dışında tutulur
  (`TenantResolutionMiddleware.cs:430-436`) ve Platform servisine gider (Açık kararlar (e)). Koruma varken bu önek
  üzerinden yol aşma, öteki rotalar gibi 400 alır. Test her satır için rotayı, konumu, belirteç türünü, yöntemi, durum
  kodunu, gövdedeki `code` değerini ve konağın kaydettiği yolu (yoksa "yok") test çıktısına yazar. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Every_placeholder_route_refuses_every_traversal_form`. Sabotaj: kayıt satırı
  çıkarılınca her satır kırmızıya döner, çünkü hiçbir cevap `code: GATEWAY_PATH_REJECTED` taşımaz. Korumasız davranış
  satıra göre değişir ve test bunu tek tip saymaz: `./` satırları ağ geçidinin Kestrel'inde normalleşip rota içinde kalır
  ve konaktan 200 alır; ham `../` satırları normalleşir ve rota eşleşmesine göre çoğunlukla 404 alır; `%252e%252e/`
  satırları `{everything}` rotalarında konakta rota dışına ulaşır (ölçüm, Module Summary); `..%3f`, `..%23`, ham `..#`
  ve ham `..\` satırlarının korumasız davranışı ölçülmedi; yer tutucusu tek kesim olan
  rotalarda biçim ve `internal/x` birden çok kesim taşıdığı için rota eşleşmesi tabanda ölçülür (ölçülmedi); belirteç
  türünün kiracı çözümünde reddedildiği satırlar servise hiç ulaşmaz. Bu yüzden sabotaj koşusunun test çıktısı, iki
  belirteç türüyle, rota başına taban erişilebilirliği olarak teslim kaydına girer: hangi rota, konum, biçim ve belirteçte
  istek servise ulaştı, hangisinde ağ geçidinde durdu. Ara katman `RawTarget` yerine `Request.Path` okuyacak biçimde
  değiştirilince ham `../` ve `./` satırları (ve Kestrel'in normalleştirdiği tek kodlanmış nokta satırları) ret almaz ve
  test kırmızıya döner.
- [ ] AC-3 (zorunlu) — **Meşru yollar geçmeye devam eder; kodlanmış ters bölünün bedeli görünür:** Gerçek konakta
  `/api/product-master/` altında şu istekler servis yerine geçene rota öneki altında ulaşır ve konağın 200 cevabı
  istemciye döner: `report.v1.pdf`, `v1.2/items`, `...`, `a..b`, `..foo`, `.well-known`, `100%25.pdf`, `a%2Fb` (tek
  kodlanmış bölü) ve sorgu dizesi `?path=../../x&q=%2e%2e%2f`. Sorgu konağa değeri bozulmadan ulaşır; karşılaştırma
  çözülmüş değerlerle yapılır. **Korumasız taban ölçüldü** (ek-02, `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02-ek-02.md`,
  ölçülen commit `f30a65627`, Ocelot'lu konak): aynı biçimler `/api/product-master/files/` altında (`report.v1.pdf`,
  `v1.2/items`, `.../x`, `a..b`, `..foo`, `.well-known`, `100%25.pdf`, `a%2Fb`, sorgu `?q=../..`) bugün 200 alır ve rota
  öneki içinde kalır; `a%2Fb` ve sorgu dizesi servise olduğu gibi iletilir. Ölçülmeyen: aynı satırların gerçek `Program.cs`
  konağındaki tabanı ve bu ölçütteki tam sorgu `?path=../../x&q=%2e%2e%2f`; DL-1'in ilk adımı ölçer. Test adı `Mod0032Fu02PathTraversalGuardTests.Legitimate_paths_and_queries_still_reach_the_service`.
  Sabotaj: yardımcı sorguyu da denetleyecek biçimde değiştirilince sorgu satırı kırmızıya döner. Kesim denetimi "noktayla
  başlayan her kesim" diye genişletilince `...`, `..foo` ve `.well-known` satırları kırmızıya döner. Bu test kayıt satırı
  çıkarılınca yeşil kalır. Bu beklenen davranıştır, çünkü test aşırı engellemeyi ölçer. **Bedel satırı (ayrı test):** kesim
  içinde tek kodlanmış ters bölü taşıyan `/api/product-master/files/a%5Cb.txt` 400 alır, gövdesi
  `code: GATEWAY_PATH_REJECTED` taşır ve konak istek kaydetmez. Bu bilinçli bedeldir (Çözüm kuralı 6; Sahip kabul etti,
  2026-11-02). Test adı `Mod0032Fu02PathTraversalGuardTests.An_encoded_backslash_inside_a_segment_is_refused_as_a_known_cost`.
  Aynı testte ikinci bedel satırı: bir kez kodlanmış `%5c` metni taşıyan `/api/product-master/files/a%255cb` de 400 alır,
  gövdesi `code: GATEWAY_PATH_REJECTED` taşır ve konak istek kaydetmez (Çözüm kuralı 4, üç tur bedeli; korumasız tabanda
  200 ve servis `…/a\b` yönlendirir, ek-02).
  Sabotaj: kayıt satırı çıkarılınca bu test kırmızıya döner. Ters bölü kuralı (Çözüm kuralı 6) kaldırılınca da kırmızıya
  döner: ters bölü yalnız kesim ayırıcısı olarak kalır, `a\b.txt`'nin kesimleri `.` ya da `..` değildir ve istek geçer.
- [ ] AC-4 (zorunlu) — **Ret cevabı yalnız koddur:** Ret cevabı 400'dür, `Content-Type` değeri `application/problem+json`'dur.
  Gövde tam olarak `status`, `code` ve `traceId` alanlarını taşır; `code` değeri `GATEWAY_PATH_REJECTED`'dir. `title` ve
  `detail` alanları yoktur ve ret nedeni gövdede geçmez. Cevap `X-Correlation-Id` başlığını taşır ve gövdedeki `traceId`
  bu başlığın değerine eşittir (kaynak `EdgeCorrelation.CorrelationIdOf`, Runtime Constraints). Arayanın değeri yansımaz:
  istek rastgele, imzasız bir `X-Correlation-Id` işareti ve ayrı bir rastgele `RequestId` işareti taşır; iki işaret de ne
  gövdede ne cevap başlıklarında geçer. Test adı `Mod0032Fu02PathTraversalGuardTests.The_refusal_carries_the_code_only`.
  Sabotaj: gövdeye bir `detail` metni ya da ret nedeni eklenince, `contentType` yerine `Response.ContentType` atanınca, ya da
  `traceId`'ye gelen `X-Correlation-Id` başlığı yazılınca test kırmızıya döner. Kayıt satırı çıkarılınca da kırmızıya döner.
  `context.TraceIdentifier` kullanmak bugün kırmızı üretmez, çünkü koruma Ocelot'tan önce koşar ve o alan o anda üretilen
  değeri taşır (`CorrelationIdMiddleware.cs:38`); bu yüzden kaynak kuralla sabitlenir ve bu değişiklik sabotaj kanıtı olarak
  sunulmaz.
- [ ] AC-5 (zorunlu) — **Kimlik doğrulamadan ve kiracı çözümünden önce:** İki ölçüm yapılır. İkisi de durum koduna değil,
  gövdedeki `code` alanına bakar: kiracı çözümünün retleri de 400 dönebilir ve `code` alanı taşımaz, `title` taşır
  (`TenantResolutionMiddleware.cs:551-568`). Birincisinde Ocelot'ta kimlik isteyen tek `{everything}` rotası olan
  `/api/v1/uploads/{everything}` üzerinde belirteçsiz `/api/v1/uploads/%252e%252e/x` isteği 400 alır ve gövdesi
  `code: GATEWAY_PATH_REJECTED` taşır. Korumasız tabanda bu istek iki cevaptan birini alır. Dev bypass kapalıysa kiracı
  çözümünden 400 "Missing Tenant" alır (`TenantResolutionMiddleware.cs:213-226`; aynı dal
  `SetPasswordPublicEndpointGuardTests.cs:52-59`'da ölçülüyor). Test konağı Development ortamında açılır ve
  `TenantResolution:DevBypassEnabled` açıksa (`appsettings.Development.json:10-11`), istek sahte kiracıyla geçer ve Ocelot'un
  kimlik denetiminden 401 alır (çerçeve bilgisi, ölçülmedi). Hangisi olduğunu DL-1'in ilk adımı ölçer ve teslim kaydına
  yazar; iki tabanda da ölçüt aynıdır. Taban bu iki cevaptan biri değilse DL-1 durur (Implementation Notes "DL-1'in ilk
  adımı").
  İkincisinde `TenantResolutionMiddleware`'in bugün reddettiği bir istek yol aşmayla birlikte gönderilir. Öneri: yönetici
  yoluna `X-Tenant-Id` başlığıyla giden istek; bugün korumayla **aynı durum kodu** olan 400 "Invalid Tenant Header" alıyor
  (`TenantResolutionMiddleware.cs:161-166`). Bu istek korumanın gövdesini (`code: GATEWAY_PATH_REJECTED`, `title` yok) alır,
  kiracı çözümünün gövdesini almaz. Test adı
  `Mod0032Fu02PathTraversalGuardTests.The_guard_answers_before_tenant_resolution_and_route_authentication`. Sabotaj: kayıt
  satırı `app.UseMiddleware<TenantResolutionMiddleware>();` satırından sonraya taşınınca ikinci ölçüm kırmızıya döner (gövde
  `title: "Invalid Tenant Header"` taşır, `code` taşımaz). Kayıt satırı çıkarılınca iki ölçüm de kırmızıya döner, çünkü iki
  tabanın hiçbiri `code: GATEWAY_PATH_REJECTED` taşımaz.
- [ ] AC-6 (zorunlu) — **Sağlık ve metrik uçları:** Açık kararlar (b) önerisi (b1) üzerine yazıldı; `/health/%2e%2e/x`,
  `/health/live/%252e%252e/x`, `/health/ready/..%2fx` ve `/metrics/%252e%252e/x` isteklerinin dördü de 400 alır ve
  gövdeleri `code: GATEWAY_PATH_REJECTED` taşır; durum koduna tek başına bakılmaz. Düz `/health`,
  `/health/live`, `/health/ready` ve `/metrics` istekleri korumasız tabandaki durum kodunu alır. Tabanı DL-1'in ilk adımı
  aynı test konağında, kayıt satırı yokken ölçer ve teslim kaydına yazar. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Health_and_metrics_paths_are_guarded_and_still_answer`. Sabotaj: kayıt satırı
  çıkarılınca yol aşan dört satırın gövdesi `code: GATEWAY_PATH_REJECTED` taşımaz ve test kırmızıya döner (korumasız
  tabanda bir satır başka bir katmandan, ör. kiracı çözümünden "Missing Tenant" ile 400 alsa bile). Not: kayıt satırını sağlık bloğunun
  altına taşımak bu testi kırmızıya çevirmez, çünkü sağlık ucu yalnız kalan yol boşken eşleşir; yol aşan istek sağlık ucuna
  takılmadan korumaya ulaşır. Bu beklenen davranıştır ve sabotaj olarak sayılmaz. Aynı not metrik bloğu için de
  geçerlidir: kayıt satırı metrik bloğunun (`Program.cs:150-154`) altına taşınırsa `/metrics/%252e%252e/x` satırının
  sonucu, metrik sunucusunun yolu önek olarak mı yoksa tam eşleşmeyle mi yakaladığına bağlıdır ve bu ölçülmedi; bu taşıma
  da sabotaj kanıtı olarak sunulmaz. Kayıt satırı koşullu metrik bloğunun içine girmez (Açık kararlar (h)). Bunu ölçmek
  için test konağı ikinci kez `--Observability:Metrics:Enabled=false` ile açılır (`Program.cs:46-48` ayarı
  `builder.Configuration`'dan okur; kod okuması). O konakta ölçülen istek
  (`/api/product-master/%252e%252e/internal/v1/product-master/validate`) yine 400 alır ve gövdesi
  `code: GATEWAY_PATH_REJECTED` taşır. Sabotaj: kayıt satırı `if (observabilityOptions.Metrics.Enabled)` bloğunun içine
  taşınınca bu satır kırmızıya döner. Karar (b) için önerilenden farklı bir karar paket revizyonu ve yeniden inceleme
  ister (Açık kararlar başı).
- [ ] AC-7 (zorunlu) — **Her kuralın yalnız onun yakaladığı bir satırı var (birim):** Saf yardımcı için tablo testi şu
  satırları içerir; her satır beklenen sonucu, ret nedenini ve `DecodeRounds` değerini (Çözüm kuralı 8) birlikte ölçer.
  Yalnız kesim kuralının yakaladığı satır `/a/%2e%2e/b`'dir (`DotSegment`, 1 tur). Aşama 0 satırı `/a/../b`'dir
  (`DotSegment`, 0 tur). Yalnız yakınsama kuralının yakaladığı iki satır vardır: tur sınırının bir fazlası kat kodlanmış `..`
  kesimi (öneri sınır 3 için dört kat, `/a/%2525252e%2525252e/b`) ve beş kat kodlanmış `..` kesimi
  (`/a/%252525252e%252525252e/b`); ikisi de `NotConverged`, `MaxDecodeRounds` tur. Beş kat satırı, üç turdan sonra `%252e`
  kalan ve `%2e` içermeyen biçimdir; yakınsama kuralının kat sayısından bağımsız olduğunu ölçer. Yalnız ters bölü kuralının
  yakaladığı satırlar noktasız `/a/b%5cc` (`Backslash`, 1 tur) ve ham ters bölülü `/a/b\c`'dir (`Backslash`, 0 tur).
  `?` / `#` kesim sonu satırları (Açık kararlar (p), öneri (p1)): çözülmüş `/a/..%3fb` ve `/a/..%23b` (`DotSegment`, 1 tur);
  ham `#` taşıyan `/a/..#b` (`DotSegment`, 0 tur; aşama 0'da kesim sonu). Bedel satırları: dört kat kodlanmış meşru `A`
  (`/a/%25252541`) ret alır (`NotConverged`, `MaxDecodeRounds` tur); bir kez kodlanmış `%5c` metni (`/a/%255cb`) ret alır
  (`Backslash`, 2 tur; Çözüm kuralı 4, üç tur bedeli). Çözücü satırları (Çözüm kuralı 3): `/a/%ff` izin alır
  (1 tur; `%ff` U+00FF'e çözülür ve geçerli `%XX` kalmaz); `/a/%c0%ae` izin alır (1 tur; aşırı uzun UTF-8 `.` olmaz);
  `/a/%u002e` izin alır (0 tur; `%u` geçerli `%XX` değildir); `/a/b+c` izin alır (0 tur; `+` boşluk sayılmaz ve `+`
  olarak kalır). Yakınsayan satırlar ret almaz: `/a/100%25.pdf` (1 tur) ve geçersiz `%` dizisi `/a/%zz` (0 tur). Hedef
  biçimi satırları (Çözüm kuralı 1; her satır yöntemle birlikte verilir): mutlak biçimli `GET http://h/api/x/%252e%252e/y`
  ret alır (`DotSegment`, 2 tur; yol dize işlemiyle alınır); `OPTIONS *`, `CONNECT h:443` ve yolu boş `GET http://h`
  izin alır (0 tur); `GET x/../y`, `GET http:/x/..`, `GET *`, `GET h:443`, `CONNECT h:443/x` ve boş hedef ret alır
  (`UnrecognizedTarget`, 0 tur). Test adı `Mod0032Fu02GatewayPathInspectorTests.Each_rule_has_a_row_only_it_refuses`.
  Sabotaj: kesim kuralı kaldırılınca, yakınsama kuralı kaldırılınca ya da ters bölü kuralı kaldırılınca o kurala ait satır
  kırmızıya döner. Yakınsama kuralı eski "`%2e` / `%2f` / `%5c` kalıntısı" kuralıyla değiştirilince beş kat satırı kırmızıya
  döner. Çözücü `Uri.UnescapeDataString` ile değiştirilince `/a/%ff` satırı kırmızıya döner (`%ff` kodlu kalır ve geçerli
  `%XX` olarak `NotConverged` üretir; çerçeve bilgisi, ölçülmedi, teslimde gösterilir). `?` ve `#` çözülmüş aşamada
  ayırıcıdan çıkarılınca `..%3f` ve `..%23` satırları kırmızıya döner. Ham `#` aşama 0'da ayırıcıdan çıkarılınca `/a/..#b`
  satırı kırmızıya döner. Tanınmayan hedef biçimi izin alacak ya da köken biçimli yol sayılacak biçimde değiştirilince
  `x/../y` ve `http:/x/..` satırları kırmızıya döner (sonuç ya izin olur ya da ret nedeni `UnrecognizedTarget` olmaz).
  Teslim kaydında bu sekiz sabotaj ayrı ayrı gösterilir.
- [ ] AC-8 (zorunlu) — **Çözüm sınırlıdır:** Yardımcının sonucu yapılan tur sayısını (`DecodeRounds`) taşır. Sayı
  hiçbir girdide `MaxDecodeRounds`'u geçmez. Girdiler arasında 8 KB uzunluğunda, `%25` katmanlarıyla şişirilmiş bir yol
  vardır; sonucu `NotConverged` ve `DecodeRounds = MaxDecodeRounds`'tur. Geçerli `%XX` taşımayan yol (`/a/b`) çözülmeden
  izin alır ve `DecodeRounds = 0`'dır. Aşama 0'da verilen ret (`/a/../b`) `DecodeRounds = 0` taşır. Çözülmeyen hedefler
  (`OPTIONS *`, `CONNECT h:443`, yolu boş `http://h`) ve `UnrecognizedTarget` retleri `DecodeRounds = 0` taşır. Test adı
  `Mod0032Fu02GatewayPathInspectorTests.Decoding_stops_at_the_round_cap`. Sabotaj: döngü sınırsız "geçerli `%XX` kalmayana
  kadar" biçimine çevrilince şişirilmiş satır sınırı aşar ve test kırmızıya döner.
- [ ] AC-9 (zorunlu) — **Korumanın ret satırı ham hedefi yazmaz:** Ölçüm yalnız korumanın kendi `Warning` satırına bakar:
  `SourceContext` değeri `Diten.ApiGateway.Middleware.PathTraversalGuardMiddleware` olan olaylar. Gerçek konakta, günlük
  yakalayıcıyla, her ret bu kaynaktan tam bir `Warning` satırı üretir. Satır `Reason`, `Method`, `RawTargetLength` ve
  `DecodeRounds` alanlarını taşır. O satırın ne ileti metninde ne de özelliklerinde ham hedef ya da çözülmüş yol geçer: test,
  isteğe özgü bir işaret dizesini (ör. rastgele bir GUID kesimi) bu satırda arar ve bulamaz. İstek günlüğünün satırı
  (`UseSerilogRequestLogging`, `Program.cs:130-148`) ölçümün dışındadır: o satır reddedilen istekte de `RequestPath`
  alanını yazar (`Program.cs:144`) ve işaret dizesi orada görünür. Bu bilinen durumdur ve bu paket onu değiştirmez (Açık
  kararlar (d), Follow-up Items). Günlük yakalama yöntemi DL-1'de seçilir (Implementation Notes "Teslimde ölçülen ya da seçilen"). Test
  adı `Mod0032Fu02PathTraversalGuardTests.The_refusal_log_holds_no_raw_target`. Sabotaj: korumanın satırına `RawTarget`
  eklenince test kırmızıya döner. Kayıt satırı çıkarılınca bu kaynaktan `Warning` satırı oluşmaz ve test yine kırmızıya
  döner. Açık kararlar (d) için önerilenden farklı bir karar paket revizyonu ve yeniden inceleme ister.
- [ ] AC-10 (zorunlu) — **Kapsam ve `ocelot.json`:** `<taban>`, DL-1 kod dalının **açıldığı commit'tir**. Kod dalı CT dalından
  (`chore/ct/auth-ct-01`) ya da ana daldan (`origin/main`) açılabilir. Dal açılır açılmaz kaynak dalın adı ve
  `git rev-parse HEAD` çıktısı teslim kaydına yazılır; `<taban>` odur. Kaynak dal sonradan kod dalına birleştirilirse
  `<taban>` yeniden ölçülür (`git merge-base <kaynak dal> HEAD`) ve komut ile çıktısı teslim kaydına yazılır. Ölçülen şey
  **kod diff'idir**:
  `git diff --name-only <taban>..HEAD -- . ':(exclude)execution/approvals/**' ':(exclude)execution/registries/**' ':(exclude)docs/records/**' ':(exclude)execution/domains/platform-shared-services/module-packs/MOD-0032-FU02-gateway-path-traversal-guard.md'`.
  Bu çıktıdaki her satır şu altı yoldan biridir, başka yol yoktur: iki yeni ara katman dosyası
  `gateway/Diten.ApiGateway/Middleware/GatewayPathInspector.cs` (yeni) ve
  `gateway/Diten.ApiGateway/Middleware/PathTraversalGuardMiddleware.cs` (yeni); `gateway/Diten.ApiGateway/Program.cs`;
  test dosyaları `gateway/Diten.ApiGateway.Tests/GatewayKestrelProgramHost.cs` (yeni),
  `gateway/Diten.ApiGateway.Tests/Mod0032Fu02GatewayPathInspectorTests.cs` (yeni) ve
  `gateway/Diten.ApiGateway.Tests/Mod0032Fu02PathTraversalGuardTests.cs` (yeni). Program.cs ölçüsü:
  `git diff -U0 <taban>..HEAD -- gateway/Diten.ApiGateway/Program.cs` çıktısında, `+++` ve `---` başlık satırları
  dışında, `+` ile başlayan tam bir satır vardır ve baştaki `+` ile boşluklar atılınca değeri
  `app.UseMiddleware<PathTraversalGuardMiddleware>();`'dir; `-` ile başlayan satır yoktur (ad alanı `Program.cs:1`'de
  zaten içeri alınmış). Adlı istisnalar dörttür: onay ve inceleme satırları (`execution/approvals/**`), kayıt satırı
  (`execution/registries/**`; Açık kararlar (m)), teslim kaydı (`docs/records/**`) ve bu paket dosyası (Acceptance
  Criteria girişindeki "test adı değişirse bu bölüm de değişir" kuralı). İstisnaların yolları ayrıca
  `git diff --name-only <taban>..HEAD -- execution/approvals execution/registries docs/records <bu paket>` ile
  listelenir ve teslim kaydına yazılır. Altı yolun dışındaki her yol kırmızıdır; ek bir dosya gerekirse (ör. test
  projesinin `.csproj`'u) DL-1 durur ve paket revizyonu, yeni inceleme, yeni onay gerekir. Bu yüzden
  `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs`, `gateway/Diten.ApiGateway/Authentication/**`,
  `gateway/Diten.ApiGateway/ocelot.json` (`git diff <taban>..HEAD -- gateway/Diten.ApiGateway/ocelot.json` boş),
  `gateway/Diten.ApiGateway/appsettings*.json` ve `Mod0290Fu04ProductMasterTraversalTests.cs` değişmez. Komutlar ve
  çıktıları teslim kaydına yazılır.
- [ ] AC-11 (zorunlu) — **Gerileme:** `dotnet test gateway/Diten.ApiGateway.Tests` tam koşulur. Önceden yeşil olan her test
  yeşil kalır. `Mod0290Fu04ProductMasterTraversalTests`, `GatewayPipelineCorrelationTests`, `TenantContradictionGuardTests`,
  `OcelotConfigurationTests` ve rota kapsama testleri bunlara dahildir. Sayılar, koruma öncesi tabanla (aynı makine, aynı
  yapılandırma) karşılaştırmalı yazılır. Önceden bilinen kırmızılar ayrıca adlandırılır. Ağ geçidi Release derlemesi
  (`dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Release`) geçer.
- [ ] AC-12 (zorunlu) — **Çalışan ağ geçidinde kanıt (E3):** `dev` ortamında 5000'de çalışan ağ geçidine
  `curl --path-as-is -s -w '%{http_code}' 'http://localhost:5000/api/product-master/%252e%252e/internal/v1/product-master/validate'`
  gönderilir. Cevap 400'dür ve gövde `GATEWAY_PATH_REJECTED` taşır. MDM'nin (5059) günlüğünde bu istek görünmez. Bu
  negatif kanıtın bir kontrol satırı vardır: aynı oturumda `/api/product-master/` altındaki meşru bir iş isteği (ör. ürün
  listesi, oturum açmış kullanıcıyla) gönderilir ve bugünkü gibi cevap verir. İki cevabın `X-Correlation-Id` değerleri
  aynı komutla MDM günlüğünde aranır: meşru isteğinki bulunur, reddedilenin değeri bulunmaz. Kontrol isteği MDM günlüğünde
  bulunmazsa negatif kanıt geçersizdir ve madde kırmızıdır. Arama yeri (günlük dosyası, konsol çıktısı ya da Seq) DL-1'de
  seçilir. Aynı oturumda `curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/health` bugünkü durum kodunu verir.
  Komutlar, çıktılar ve ortamın adı teslim kaydına yazılır. Sabotaj kanıtı bu maddede aranmaz: madde çalışma kanıtıdır,
  test değildir.
- [ ] AC-13 (isteğe bağlı) — **HTTP/2:** Ağ geçidi uç noktası HTTP/2 sunuyorsa (teslimde ölçülür) aynı ölçülen istek
  HTTP/2 üzerinden de 400 alır. HTTP/2 sunulmuyorsa teslim kaydına "uygulanmaz" ve ölçüm komutu yazılır.
- [ ] AC-14 (isteğe bağlı) — **Kural dışı biçimler rota dışına çıkmaz:** Tablo testine `%c0%ae%c0%ae/`, `..;/`, `%00`,
  `%u002e%u002e/` ve tam genişlikte nokta (`%ef%bc%8e%ef%bc%8e/`) satırları eklenir. Bu satırlar 400 beklemez. Yalnız servis
  yerine geçen konağın rotanın alt öneki dışında bir istek kaydetmediğini ölçer. Bir satır rota dışına çıkarsa bu paket
  genişletilmez: bulgu Açık kararlar (n) için Sahibe götürülür. Bu tabloda yalnız ret kuralı olmayan biçimler vardır;
  kuralın reddettiği `?` / `#` kesim sonu biçimleri zorunlu tablo testindedir. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Off_rule_forms_stay_inside_the_route`. Sabotaj kanıtı bu maddede aranmaz: satırlar
  koruma varken de yokken de 400 beklemez.
- [ ] AC-15 (zorunlu) — **CORS ön uçuşu korumaya ulaşmaz, çapraz kökenli ret okunabilir:** Üç istek gönderilir. Üçü de
  `Origin: http://localhost:5001` taşır (izinli köken; CORS politikası `Program.cs:106-117`, kökenler `:110`) ve yolu
  `/api/product-master/%252e%252e/internal/x`'tir. Birinci istek CORS ön uçuşudur (`OPTIONS` +
  `Access-Control-Request-Method: GET`): `UseCors` (`Program.cs:128`) onu korumadan önce bitirir, cevap 204'tür ve servis
  yerine geçen konak hiçbir istek kaydetmez. Bu beklenti çerçeve bilgisidir ve ölçülmedi. DL-1'in ilk adımı ölçer;
  beklenenden farklı çıkarsa DL-1 durur (Implementation Notes "DL-1'in ilk adımı"). İkinci istek ön uçuş olmayan `OPTIONS`'tur (`Access-Control-Request-Method`
  yok): 400 alır ve gövdesi `code: GATEWAY_PATH_REJECTED` taşır. Üçüncü istek `GET`'tir: 400 alır, gövdesi
  `code: GATEWAY_PATH_REJECTED` taşır ve cevap `Access-Control-Allow-Origin: http://localhost:5001` başlığını taşır. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Cors_preflight_ends_at_cors_and_cross_origin_refusals_stay_readable`. Sabotaj: kayıt
  satırı çıkarılınca ikinci ve üçüncü istek kırmızıya döner. Kayıt satırı `UseCors`'tan önceye taşınınca (Açık kararlar
  (h2)) birinci istek 400 alır ve üçüncü istek `Access-Control-Allow-Origin` başlığını kaybeder; test kırmızıya döner.
  Açık kararlar (h) için önerilenden farklı bir karar paket revizyonu ve yeniden inceleme ister.
- [ ] AC-16 (zorunlu) — **Şablon varsayımları dosyadan ölçülür:** Test, gönderilen `ocelot.json`'u test anında okur ve iki
  varsayımı ölçer: hiçbir rotanın `UpstreamPathTemplate` ya da `DownstreamPathTemplate` değeri `?` taşımaz; hiçbir rotanın
  alt şablonunda, üst şablonunda olmayan bir yer tutucu yoktur. Okunan rota sayısı test çıktısına yazılır; sıfırsa test
  başarısız olur. İhlal eden rota varsa test o rotanın üst şablonunu adıyla yazar ve kırmızıya döner. Test adı
  `Mod0032Fu02PathTraversalGuardTests.Shipped_routes_keep_the_guard_assumptions`. Sabotaj: `ocelot.json`'a dokunulmaz
  (korunan yol). Testin denetimi, okunan listenin bellekteki kopyasına geçici olarak `?x=1` ekli bir üst şablon ve alt
  şablonda fazladan `{z}` taşıyan bir rota eklenerek koşulur; iki ihlal ayrı ayrı kırmızı üretir, geçici değişiklik geri
  alınır. Bu test kayıt satırı çıkarılınca yeşil kalır; bu beklenen davranıştır, çünkü korumayı değil dayanağını ölçer.

## Test Expectations

| Alan | Gerekli kanıt |
|---|---|
| Birim — saf yardımcı | `Mod0032Fu02GatewayPathInspectorTests`: her kural için yalnız onun yakaladığı satır (yakınsama kuralı için dört ve beş kat); aşama 0 satırları (`/a/../b`, ham `\`, ham `#`); çözülmüş `?` / `#` kesim sonu satırları; bedel satırları (`%25252541`, `%255cb`); çözücü satırları (`%ff`, `%c0%ae`, `%u002e`, `+`); meşru ve yakınsayan satırlar; her satırda `DecodeRounds`; tur sınırı; hedef biçimi satırları (mutlak biçim, `OPTIONS *`, `CONNECT h:443`, yolu boş mutlak hedef; tanınmayan ve boş hedefler `UnrecognizedTarget`); geçersiz `%` dizisi (AC-7, AC-8) |
| Gerçek boru hattı | `Mod0032Fu02PathTraversalGuardTests`: gerçek `Program.cs`, gerçek Kestrel, Ocelot, gönderilen `ocelot.json` (yalnız `DownstreamHostAndPorts` çevrilir), ham soket, kayıt tutan Kestrel servis yerine geçeni (AC-1…AC-6, AC-9, AC-14, AC-15). Ret ölçütleri durum koduna değil gövdedeki `code`'a bakar |
| Yapılandırma bütünlüğü | `Shipped_routes_keep_the_guard_assumptions`: şablonlarda `?` yok, alt şablonda üstte olmayan yer tutucu yok; dosyadan test anında (AC-16) |
| Sabotaj — kayıt satırı | `app.UseMiddleware<PathTraversalGuardMiddleware>();` çıkarılınca **kırmızı:** `The_measured_double_encoded_dot_dot_is_refused_before_it_reaches_the_service`, `Every_placeholder_route_refuses_every_traversal_form`, `The_refusal_carries_the_code_only`, `The_guard_answers_before_tenant_resolution_and_route_authentication`, `Health_and_metrics_paths_are_guarded_and_still_answer`, `The_refusal_log_holds_no_raw_target`, `Cors_preflight_ends_at_cors_and_cross_origin_refusals_stay_readable`, `An_encoded_backslash_inside_a_segment_is_refused_as_a_known_cost` (`?` / `#` kesim sonu biçimleri `Every_placeholder_route_refuses_every_traversal_form` içindedir). **Yeşil kalan (beklenen):** `Legitimate_paths_and_queries_still_reach_the_service`, `Shipped_routes_keep_the_guard_assumptions`, `Off_rule_forms_stay_inside_the_route` (isteğe bağlı; 400 beklemez) ve `Mod0032Fu02GatewayPathInspectorTests` içindeki her test. Bu koşunun `Every_placeholder_route_refuses_every_traversal_form` çıktısı, `tenant_user` ve (yönetici öneklerinde) `platform_admin` belirteçleriyle, rota başına taban erişilebilirliği olarak teslim kaydına girer (AC-2) |
| Sabotaj — yer | Kayıt satırı `TenantResolutionMiddleware`'den sonraya taşınınca `The_guard_answers_before_tenant_resolution_and_route_authentication` kırmızı. Koşullu metrik bloğunun içine taşınınca `Health_and_metrics_paths_are_guarded_and_still_answer`'ın metrik kapalı satırı kırmızı. `UseCors`'tan önceye taşınınca `Cors_preflight_ends_at_cors_and_cross_origin_refusals_stay_readable` kırmızı. Sağlık ya da metrik bloğunun altına taşımak kırmızı üretmeyebilir (AC-6 notu); bunlar sabotaj kanıtı olarak sunulmaz |
| Sabotaj — kaynak | `RawTarget` yerine `Request.Path` okununca `Every_placeholder_route_refuses_every_traversal_form` ham `../` ve `./` satırlarında kırmızı |
| Sabotaj — kural | Kesim, yakınsama ve ters bölü kuralı ayrı ayrı kaldırılınca; yakınsama kuralı eski kalıntı kuralıyla değiştirilince; çözücü `Uri.UnescapeDataString` ile değiştirilince; `?` / `#` çözülmüş aşamada ayırıcıdan çıkarılınca; ham `#` aşama 0'da ayırıcıdan çıkarılınca; tanınmayan hedef biçimi izin alınca ya da yol sayılınca `Each_rule_has_a_row_only_it_refuses` kırmızı (AC-7, sekiz koşu) |
| Sabotaj — şablon varsayımı | Okunan rota listesinin bellekteki kopyasına `?` taşıyan ve alt şablonda fazladan yer tutucu taşıyan rota eklenince `Shipped_routes_keep_the_guard_assumptions` kırmızı; `ocelot.json`'a dokunulmaz (AC-16) |
| Sabotaj — `traceId` | `traceId`'ye gelen `X-Correlation-Id` başlığı yazılınca `The_refusal_carries_the_code_only` kırmızı. `context.TraceIdentifier`'a geçmek bugün kırmızı üretmez (AC-4); sabotaj kanıtı olarak sunulmaz |
| Gerileme | Tam `Diten.ApiGateway.Tests` takımı, tabanla karşılaştırmalı (AC-11) |
| Derleme | Ağ geçidi Release derlemesi (AC-11) |
| Çalışma kanıtı | `dev` ortamında `curl --path-as-is` (AC-12) |
| Süre | Tablo testi bugün 189 yer tutucu konumu (185 rota) `tenant_user` ile ve 19 yönetici önekli rota ayrıca `platform_admin` ile, yani 208 konum-belirteç × 27 biçim ≈ 5.600 soket isteği gönderir. Süre ölçülmedi. **Eşik önerisi (Açık kararlar (o), karar değil):** `Every_placeholder_route_refuses_every_traversal_form` yerelde Release derlemede 60 sn'yi geçmez. Süre teslimde ölçülür ve yazılır; eşik aşılırsa teslim durur ve konu Sahibe gelir (biçim ya da rota sayısı koda gömülerek azaltılmaz). CI süresine etkisi TEST-HIZ-01 (test hızı işi) için not edilir |

Kural üretim koduyla ölçülür. Gerçek boru hattı testleri ağ geçidinin kendi `Program.cs`'ini koşturur. Korumanın bir kopyasını
kendi konağına koyan test sabotaj kanıtı sayılmaz. Sabotaj kanıtı teslim kaydında "kaldır → kırmızı → geri koy → yeşil"
biçiminde, test adlarıyla gösterilir.

## Ready-for-dev Checklist

**Onay öncesi maddeler** (CT-APR-01 onay satırından önce tamamlanır):

- [ ] **Kod izni:** Paket `approved` / `ready-for-dev` ve CT-APR-01 onay satırı var (`python3 scripts/pack_approval.py check <paket>` → 0).
  Onay satırı yoksa kod yok
- [ ] Golden Reference: `golden_reference: none` ve `entity_base: none` gerekçesi okundu (başlık notu)
- [x] Frontmatter zorunlu alanları dolu. `owner` ve `target` sahip sözüyle kesinleşti (2026-11-02, "Evet, ben, 11-09";
  Açık kararlar (j) kapandı); ilk UYGUN incelemeden **önce** yazıldı. İlk onayda UYGUN incelemeden sonra paket değiştirilmez
- [ ] Layout & Shell Contract: `shell: none`, ön yüz dokunuşu yok (AC-10 ölçer)
- [ ] Validation Rules, Failure Path to Verify (en az 4 senaryo), Authorization Convention ve Gateway kararı yazılı
- [ ] Audited Events: yazma komutu yok; ret olayının denetim değil güvenlik günlüğü olduğu yazılı
- [ ] **Kayıt satırı:** `execution/registries/module-id-registry.md`'ye `MOD-0032-FU02` satırı ayrı bir değişiklikle yazıldı ve
  `verify_module_id.py --check-id … --parent MOD-0032` yeniden koşuldu (Açık kararlar (m))
- [x] **Ölçüm kanıtı depoda:** CT'nin 2026-11-02 yol aşma ölçümü
  `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02.md` kaydı, ek kaydı
  `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02-ek-01.md` (commit `9b3eacf3d`) ve meşru
  yolların tabanını ölçen ikinci ek kaydı
  `docs/records/audits/2026-11/mod-0032-fu02-gateway-traversal-measure-2026-11-02-ek-02.md` (commit `40048f37a`) olarak alındı
- [ ] **Açık kararlar:** Sahip kararı (2026-11-02, üçüncü inceleme F-5): "«onayla» sözü açık kararlar (a)…(p)'nin yazılı
  önerilerini olduğu gibi kabul eder; farklı bir karar paket revizyonu ve yeniden inceleme ister." Buna göre (a), (b), (c),
  (d), (h), (i), (k), (l), (m), (o), (p) CT-APR-01 onayıyla yazılı önerileriyle kapanır; (j) kapandı (sahip, 2026-11-02);
  bilgi için (e), (f), (g), (n). Adsız karar sahibi kalmadı (Sahip kararı 2026-11-02, ikinci inceleme F-4)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) yapıldı ve `reviews.tsv` satırı var. İlk inceleme (@`6c3f1c3d0`)
  UYGUN DEĞİL dedi; ikinci inceleme (@`7405749e1`) KOŞULLU dedi (14 bulgu, engel yok); üçüncü bakış (@`f30a65627`) KOŞULLU
  dedi (15 bulgu, engel yok). Bu revizyon on beş bulguyu kapatır; Sahip "Bir tur daha düzelt" dedi (2026-11-02). Yeniden
  inceleme gerekip gerekmediği CT-APR-02 kuralına göre CT'nin kararıdır
- [ ] **CT-APR-01 onay satırı:** yüksek risk (güvenlik, ağ geçidi). Onaylayıcı Sahiptir (MOD-0018 sahibi; bu işte MOD-0032
  karar sahibi; paketi yazmadı). `execution/approvals/approvers.tsv`'de onaylayıcının `high` sütununun `evet` olduğu, onay
  satırı yazılırken ölçülür; değilse satır yazılmaz. Birleştirme (merge) ikinci kişinindir

**DL-1'in ilk adımı** (onaydan **sonra**, kod dalında; onay maddesi değildir):

- [ ] Implementation Notes "DL-1'in ilk adımı" tablosundaki her girdi, kayıt satırı eklenmeden önce numaralı ölçümle
  (komut + çıktı, ölçülen commit) teslim kaydına yazıldı. Bir girdi paketin yazdığı beklentiden farklı çıktıysa DL-1
  durdu: paket revizyonu, yeni bağımsız inceleme, yeni CT-APR-01 onayı

## Implementation Notes

**Okunan ve ölçülen (@`6f169f677`):**

1. `Program.cs` boru hattı sırası ve satırları (Dependencies). `Program.cs` içinde yol aşma denetimi yok.
2. `ocelot.json` JSON okuması: 347 rota, 163 `{everything}` rotası (ikinci okuma @`9b3eacf3d`: yer tutucu taşıyan rota
   185, konum 189; Dependencies), iki `AuthenticationOptions` rotası (`/api/v1/uploads`,
   `/api/v1/uploads/{everything}`), `RouteClaimsRequirement` taşıyan rota yok, `Priority` alanı yok, `/api/internal` üst ya da
   alt rotası yok. Bütün `{everything}` rotaları `/api/` ile başlıyor ve `/{everything}` ile bitiyor. Üst ve alt öneki farklı tek
   `{everything}` rotası `/api/v1/platform/workflows/instances/{everything}`. GET taşımayan iki `{everything}` rotası var.
3. `Mod0290Fu04ProductMasterTraversalTests.cs`: yedi biçim deniyor (`:29-35`). `%252e%252e/` biçimi yok; çift kodlanmış
   tek satır `%252e%252e%252f` biçimidir (`:35`) ve ölçümde rota içinde kalıyor. Konak `Program.cs`'i değil, yalnız Ocelot'u
   kuruyor (`:76-102`). Durum kodu iddiası yalnız "bir cevap var" diyor (`:53`). Bu test korumayı ölçemez, çünkü korumayı
   içermez. Değişmez kalması önerilir (Açık kararlar (l)).
4. `GatewayPipelineCorrelationTests.cs:67-158` (koleksiyon tanımı `:61-65`; satırlar @`9b3eacf3d`'de düzeltildi, ikinci inceleme F-8): `GatewayProgramHost` gerçek `Program.cs`'i `HostBuilding` / `HostBuilt` tanı
   olaylarıyla `TestServer` üzerinde açıyor. Sırrı rastgele üretip geçiriyor (`:104-110`).
5. `TenantResolutionMiddleware.cs` yolu `Request.Path` ile sınıflıyor (Dependencies). `IsNonTenantPath` (`:430-436`)
   `/api/lookups` önekini kiracı çözümünün dışında tutuyor. `IsAdminPath` (`:457-462`) `/api/admin` ve `/api/platform`
   öneklerine bakıyor. `/api/lookups/{everything}` rotası Platform servisine (5057) gidiyor (ölçüm 2). Bu bir kod okumasıdır.
   Bunun yol aşma ile birlikte bir atlatma açıp açmadığı **ölçülmedi** (Açık kararlar (e)). Koruma varken bu önek
   üzerinden yol aşma, AC-2 tablosunun `/api/lookups/{everything}` satırlarında 400 alır.
6. Ret cevabı deseni `TenantResolutionMiddleware.cs:551-568`: `WriteAsJsonAsync`, `contentType: "application/problem+json"`.
7. `appsettings.json` ve `appsettings.Development.json`: metrik yolu `/metrics`, sağlık yolları `/health`, `/health/live`,
   `/health/ready`. Metrik bugün açık (`Observability:Metrics:Enabled: true`, `appsettings.json:23-26`, numaralı okuma
   @`6c3f1c3d0`).
8. İnceleme bulgularını kapatan okumalar (@`6c3f1c3d0`): `GatewayJwtAuthenticationHandler.cs:25-46` ve
   `JwtSecretRotationResolver.cs:27-45` (Dependencies); `TenantResolutionMiddleware.cs:33-39`, `:161-177`, `:204-236`,
   `:430-481`, `:551-593` (Dependencies, AC-2, AC-5); `SetPasswordPublicEndpointGuardTests.cs:52-59` (Production ortamında
   belirteçsiz `/api/users` → 400); `appsettings.Development.json:10-11` (`DevBypassEnabled: true`) ve `:54-55`;
   `GatewayPipelineCorrelationTests.cs:104-110` (`--environment=Development`); `Program.cs:60-72` (Serilog yalnız konsola
   JSON yazıyor; Seq koşullu), `:106-117` (CORS politikası; kökenler `:110`; @`9b3eacf3d`'de düzeltildi), `:128-154`; `EdgeCorrelation.cs:14-25`;
   `CorrelationIdMiddleware.cs:27-75` (Common); `ocelot.json` JSON okuması (`/api/lookups/{everything}` var; `CONNECT`
   taşıyan rota yok); `AGENTS.md:135`.
9. İkinci inceleme bulgularını kapatan okumalar (@`9b3eacf3d`): `ocelot.json` JSON okuması (python3, salt okunur) — yer
   tutucu taşıyan 185 rota (163 + 7 + 15), iki yer tutucu taşıyan 4 rota, önek farklı 4 rota, GET taşımayan 16 rota,
   yönetici önekli 19 rota; hiçbir şablonda `?` yok; alt şablonda üstte olmayan yer tutucu yok. `GatewayPipelineCorrelationTests.cs:61-65`
   (`GatewayProgramCollection`), `:67-158` (`GatewayProgramHost`), `:105-110` (argümanlar, `--contentRoot` `:108`).
   `Program.cs:15` (`CreateBuilder(args)`), `:44` (`ocelot.json` sonradan eklenir), `:106-117` (CORS politikası, kökenler
   `:110`). `TenantResolutionMiddleware.cs:457-462` (`IsAdminPath`), `:467-481` (`IsTenantScopedOrgPath`; `platform_admin`
   reddi yorumu `:467`, `/api/platform/navigation` `:474`), `:523-527` (`IsPlatformActor`).
10. Üçüncü bakış bulgularını kapatan okumalar ve komutlar (2026-11-02, @`40048f37a`): ek-02 kaydı (meşru yolların
    korumasız tabanı); `git diff --stat 6f169f677 40048f37a -- gateway services scripts` (boş, çıkış 0) ve
    `git diff --name-only 6f169f677 40048f37a` (üç ölçüm kaydı + bu paket); `Program.cs:1`
    (`using Diten.ApiGateway.Middleware;`), `:129` ve `:186` (`UseMiddleware` satırları); `grep GatewayProgramHost`
    `gateway/Diten.ApiGateway.Tests/` altında yalnız `GatewayPipelineCorrelationTests.cs`'te eşleşti;
    `SetPasswordPublicEndpointGuardTests.cs` ve `RejectedTokenClaimRevivalGuardTests.cs` kendi `UseTestServer` konaklarını
    kuruyor (`grep`; ör. `RejectedTokenClaimRevivalGuardTests.cs:170`).

**Plan hizalaması:** Bu pakete plan satırı verilmedi; plan dosyası depoda değil. Teslim dilimleri tablosunda `plan kimliği
yok` ve `plan satırı yok` yazıldı. Değer uydurulmadı.

**Koşturulmayan:** Bu yazar derleme, test ya da dev sunucusu koşturmadı. Yol aşma ölçümü CT'nindir.

**Üçüncü bakıştan sonra ölçülenler (ek-02, ölçülen commit `f30a65627`, Ocelot'lu konak):** meşru yolların korumasız tabanı
(hepsi 200, rota öneki içinde); Ocelot'un `a%2Fb`'yi ve sorgu dizesini servise olduğu gibi ilettiği; bir kez kodlanmış
`%255c`'nin servise `%5c` olarak gidip `\` olarak yönlendirildiği. Bu girdiler artık "ölçülmedi" değildir (AC-3, Çözüm
kuralı 4).

**DL-1'in ilk adımı (durma kuralı).** Aşağıdaki girdiler onaydan önce ölçülemez: korumasız gerçek `Program.cs`'i Kestrel'de
açan konak, bu paketin yazdıracağı test kodudur. DL-1'in ilk adımı, kayıt satırı eklenmeden önce, her girdiyi numaralı
ölçümle (komut + çıktı, ölçülen commit) teslim kaydına yazar. **Bir girdi paketin yazdığı beklentiden farklı çıkarsa DL-1
durur: paket revizyonu, yeni bağımsız inceleme, yeni CT-APR-01 onayı.** Paket "onaydan önce yeniden yazılır" yolunu
kullanmaz.

| # | Girdi | Paketin beklentisi | Etkilediği ölçüt |
|---|---|---|---|
| İ1 | Gerçek `Program.cs`'in gerçek Kestrel'de `--urls=http://127.0.0.1:0` ile açılması; `ocelot.json`'un yalnız `DownstreamHostAndPorts` alanının çevrilmesi ve geçici içerik kökünde `appsettings*.json` kopyasıyla konağın gönderilen yapılandırmayla açılması | açılır (kod okuması `Program.cs:15`, `:44`; koşturulmadı) | bütün gerçek boru hattı ölçütleri |
| İ2 | Testte basılan `tenant_user` ve `platform_admin` belirteçlerinin kiracı çözümünden geçmesi (`actor_type`, `tenant_id`, `Issuer`, `Audience`) | geçer (kısmen okundu: `TenantResolutionMiddleware.cs:69`, `:330`; `GatewayJwtAuthenticationHandler.cs:33-46`; `JwtSecretRotationResolver.cs:27-45`; `appsettings.Development.json:54-55`) | AC-1, AC-2 |
| İ3 | Korumasız gerçek boru hattında (kiracı çözümü dahil) ölçülen isteğin servise `/api/internal/v1/product-master/validate` olarak ulaşması | ulaşır (ek-01; yalnız Ocelot'lu konakta ölçüldü) | AC-1 sabotajı |
| İ4 | AC-5 tabanı: belirteçsiz `/api/v1/uploads/%252e%252e/x` | kiracı çözümünden 400 "Missing Tenant" ya da dev bypass ile Ocelot'tan 401; ikisinde de gövdede `code` yok | AC-5 |
| İ5 | CORS ön uçuşu (`OPTIONS` + `Origin: http://localhost:5001` + `Access-Control-Request-Method: GET`) | `UseCors`'ta 204; konak istek kaydetmez (çerçeve bilgisi) | AC-15 |
| İ6 | Düz `/health`, `/health/live`, `/health/ready`, `/metrics` taban durum kodları ve `/metrics/%252e%252e/x`'in korumasız cevabı | düz uçlar cevap verir; yol aşan satırın gövdesinde `code` yok | AC-6 |
| İ7 | Kestrel'in ham `\` ve ham `#` taşıyan hedefi uygulamaya ulaştırması ve `RawTarget`'te olduğu gibi bırakması | ulaştırır, bırakır (ölçülmedi) | AC-2 (`..\`, `..#` satırları) |
| İ8 | Kestrel'de `RawTarget`'in her istekte dolu olması; `TestServer`'ın `RawTarget`'i doldurması | ikisi de dolu (ölçülmedi); `TestServer` boş bırakırsa `GatewayPipelineCorrelationTests` korumanın 400'ünü alır | Validation Rules, AC-11 |
| İ9 | AC-3 meşru satırlarının gerçek `Program.cs` konağındaki tabanı, tam sorgu `?path=../../x&q=%2e%2e%2f` dahil | 200, rota öneki içinde (ek-02 Ocelot'lu konakta ölçtü) | AC-3 |

**Teslimde ölçülen ya da seçilen** (durma kuralı yok; sonuç teslim kaydına yazılır):

| Girdi | Neden önemli | Yazar okuması |
|---|---|---|
| Günlük yakalama yöntemi: `Program.cs` Serilog'u yalnız konsola JSON yazacak biçimde kuruyor (`:60-72`) ve süreç düzeyinde tek; ağ geçidi testlerinde günlük yakalayan bir desen yok (`grep` `ILogEventSink` / `InMemorySink` eşleşme vermedi). Seçenekler: konsol çıktısını test süresince yakalamak (koleksiyon paralel değil) ya da test konağına yapılandırmayla ek bir sink vermek | AC-9 | seçim; DL-1'de seçilir ve teslim kaydına yazılır |
| Kestrel'in yetki biçimli hedefi (`CONNECT host:443`) kabul edip etmediği ve `RawTarget`'e ne koyduğu | Çözüm kuralı 1 (kural birim düzeyinde ölçülür) | ölçülmedi |
| Metrik sunucusunun `/metrics` yolunu önek olarak mı tam eşleşmeyle mi yakaladığı | AC-6 notu (sabotaj kanıtı sayılmayan taşıma) | ölçülmedi |
| MDM (5059) günlüğünde ağ geçidinin ilettiği `X-Correlation-Id` değerinin aranabildiği | AC-12 kontrol satırı (bulunmazsa madde kırmızı) | ölçülmedi |
| HTTP/2 `:path` davranışı ve ağ geçidi uç noktasının HTTP/2 sunup sunmadığı | AC-13 (isteğe bağlı) | ölçülmedi |
| Ocelot'un ortadaki ve son kesimdeki yer tutucuya birden çok kesim taşıyan değeri eşleyip eşlemediği | AC-2 taban erişilebilirliği (koruma ölçütünü etkilemez) | ölçülmedi |
| Ağ geçidi çıkışının çözülmüş `?` / `#`'ı aşağı akış adresinde sorgu ya da parça başlangıcı sayıp saymadığı | Açık kararlar (p) gerekçesi; AC-2 `..%3f` / `..%23` satırlarının taban erişilebilirliği | ölçülmedi |
| İkinci çözümü ve `..` sıkıştırmasını Ocelot'un mu `HttpClient` / `System.Uri`'nin mi yaptığı | Module Summary "Sebep" (çıkarım); koruma ölçütünü etkilemez | ölçülmedi |
| İç uçların `X-Internal-Api-Key` denetimi ve sabit süreli olmayan karşılaştırma | Kapsam dışı; Açık kararlar (f) | CT iletisi (Yönetici ölçümü); bu yazar okumadı |
| Tablo testinin süresi | Açık kararlar (o); TEST-HIZ-01 | ölçülmedi |

## Follow-up Items

- **İç uç anahtarının sabit süreli karşılaştırılması** (`X-Internal-Api-Key`; `CryptographicOperations.FixedTimeEquals`).
  Servislerin kodudur ve her servisin sahibine aittir (XMC-001). Ayrı iş; açılıp açılmayacağına Sahip karar verir, servis
  sahiplerine XMC-001 sorusu olarak gider.
- **Kiracı / yetki denetimlerinin yol aşmayla ölçülmesi:** `TenantResolutionMiddleware`'in `Request.Path` sınıflaması
  (Implementation Notes madde 5). Önce ölçüm, sonra gerekirse ayrı paket.
- **Servis tarafında savunma derinliği:** Servislerin kendi Kestrel'lerinde yol aşma koruması. Her servisin sahibine aittir; karar Sahip'tedir (XMC-001
  sorusu). Not: bu, ölçülen açığı kapatmaz; `..` sıkıştırması ağ geçidinin çıkışında olur ve servis meşru görünen bir yol
  alır (Module Summary "Sebep").
- **Ağ geçidi çıkışında rota alt öneki denetimi (ikinci katman):** Ocelot'un aşağı akış isteğine bir `DelegatingHandler`
  eklenir; handler, giden adresin yolunun (`HttpRequestMessage.RequestUri`) rotanın alt şablon öneki içinde kaldığını
  denetler ve dışına çıkan isteği durdurur. Bu, bu paketin ham hedef korumasından bağımsız olarak çıkıştaki sıkıştırmayı
  doğrudan yakalar. Handler'ın global mi rota başına mı kaydedileceği ve `ocelot.json` değişikliği gerektirip
  gerektirmediği ölçülmedi; `ocelot.json` gerekirse `integration-agent` görevidir. Ayrı paket; karar Sahip'tedir (MOD-0018
  sahibi; bu işte MOD-0032 karar sahibi).
- **Yeniden ölçüm tetikleyicisi:** Ocelot ya da .NET (`HttpClient`, `System.Uri`, Kestrel) sürüm yükseltmesinde ek-01 /
  ek-02 ölçümü yeniden koşulur ve yeni bir ek kayda yazılır (Runtime Constraints "Neden tur sınırı yeterli"). Tetikleyicinin
  yükseltme işine nasıl bağlanacağı (ör. yükseltme paketinin Acceptance Criteria'sına bir madde) ayrı karardır.
- **CT kurulumuna öneri (üçüncü inceleme F-13):** `status: ready-for-dev` düz okumada kod izni gibi görünüyor; bu paket bunu
  künyenin altındaki uyarıyla karşılıyor. Kalıcı çözüm SOP düzeyindedir: örneğin inceleme için ayrı bir durum değeri ya da
  kod kapısının `pack_approval.py check` sonucunu tek ölçüt sayması. `.antigravity/**` ve SOP değişikliği sahip onayı
  ister; bu paket önermekle kalır.
- **`Mod0290Fu04ProductMasterTraversalTests`'in gerçek `Program.cs` konağına taşınması** (Açık kararlar (l) seçilirse).
- **İstek günlüğünün `RequestPath` alanı** (`Program.cs:144`): Yolda kişisel veri olabilir. Bu alan bugün her istekte,
  korumanın reddettiği istekte de yazılıyor (Kestrel'in bir kez çözdüğü `Request.Path`). Ayrı karar ve ayrı iş (Açık
  kararlar (d)).
- **Kural dışı biçimler** (`%00`, aşırı uzun UTF-8, tam genişlikte nokta): AC-14 bulgusuna göre (Açık kararlar (n)).
- **Ret başına iki günlük satırı, örnekleme ya da hız sınırı yok:** Her ret korumanın `Warning` satırını ve istek
  günlüğünün satırını (`Program.cs:130-148`) yazar. Saldırgan istek başına iki satır ürettirebilir; örnekleme ya da hız
  sınırı yoktur. Sahip kararı (2026-11-02, ikinci inceleme F-13): **bu işte çözülmez.** Ayrı karar ve ayrı iş; karar sahibi
  Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi).

## Audited Events

**Servisin denetim altyapısı bugün:** Ağ geçidinin bir denetim izi yok. `tests/architecture/audit-ledger/` altında ağ geçidi
için bir defter dosyası bu oturumda aranmadı. Bu paket **yazma komutu eklemez ve değiştirmez**. Koruma hiçbir durumu
değiştirmez. Ret, bir denetim olayı değil, bir **güvenlik günlüğü** satırıdır (Runtime Constraints "Günlük").

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| — (yazma komutu yok) | — | — | — | — | Uygulanmaz: bu paket yazma komutu taşımıyor; AUD-001 tablosu yalnız yazma komutlarını sayar |

- **Korumanın `Warning` satırına yazılmayan:** ham hedef, çözülmüş yol, sorgu dizesi, belirteç, istek başlıkları.
- **Bilinen durum:** istek günlüğü (`UseSerilogRequestLogging`, `Program.cs:130-148`) reddedilen istekte de kendi satırını
  yazar ve o satır `RequestPath` alanını, yani Kestrel'in bir kez çözdüğü yolu taşır (`Program.cs:144`). Bu paket o satırı
  değiştirmez (Açık kararlar (d), Follow-up Items). Yani ağ geçidinin günlüğünde reddedilen isteğin yolu bugün görünür;
  "yazılmaz" iddiası yalnız korumanın kendi satırı içindir (AC-9).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek dilim: ham hedef koruması (tek ara katman + saf karar yardımcısı + `Program.cs` kayıt satırı), gerçek `Program.cs` / Kestrel / Ocelot test konağı, yer tutucu taşıyan tüm rotalar için tablo testi, şablon varsayımları testi, meşru yollar, sağlık / metrik, ret gövdesi ve günlük, sabotaj kanıtı, dev çalışma kanıtı; Acceptance Criteria'nın tamamı | Owned Objects, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | E3 (çalışan ağ geçidinde istek / cevap; E2 testleri dahil) | plan kimliği yok | plan satırı yok |

Tek dilim yeterlidir. İş tek bir ara katmandır. Kalıcı veri, ekran ya da servisler arası yeni sözleşme yoktur. Dilim kendi
başına çalışan en küçük korumadır.

## Açık kararlar

**Sahip kararı (2026-11-02, üçüncü inceleme F-5):** "«onayla» sözü açık kararlar (a)…(p)'nin yazılı önerilerini olduğu
gibi kabul eder; farklı bir karar paket revizyonu ve yeniden inceleme ister." Bu yüzden aşağıdaki "Önerisi" sütunu,
CT-APR-01 onay satırıyla birlikte kararın kendisi olur. Paketin ölçütleri bu öneriler üzerine yazıldı; bir karar öneriden
farklı verilirse paket onaydan önce değil, revizyonla değişir ve yeniden incelenir.

| # | Konu | Seçenekler | Önerisi (bu paketin önerisi; karar değil) | Sahip | Bağımlılık / kapanış |
|---|---|---|---|---|---|
| (a) | Hata kodu adı | **`GATEWAY_PATH_REJECTED`** · `GATEWAY_PATH_TRAVERSAL_REJECTED` · `PATH_NOT_ALLOWED` | **`GATEWAY_PATH_REJECTED`.** Kaynağı (ağ geçidi) söyler, saldırgana hangi kuralın tuttuğunu söylemez. Ağ geçidi bugün makine kodu taşımıyor (`title` / `detail` metni var); bu ilk kod olur | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (b) | Sağlık ve metrik uçları | **(b1)** her yola uygulanır (sağlık ve metrik dahil) · (b2) sağlık / metrik yolları muaf | **(b1).** Muafiyet bir önek eşleşmesidir ve kendisi bir atlatma yüzeyi olur. Düz sağlık ve metrik istekleri kurala takılmaz; yoklamalar (probe) etkilenmez. AC-6 bunu ölçer | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (c) | Çözüm tur sayısı | 2 · **3** · "geçerli `%XX` kalmayana kadar, üst sınır 8" | **3.** Ölçülen zincirdeki üç çözüm noktasını karşılar: ağ geçidinin Kestrel'i, ağ geçidinin çıkışı (Ocelot → `HttpClient` / `System.Uri`; çıkarım) ve servisin Kestrel'i (Runtime Constraints "Neden tur sınırı yeterli"). Güvenlik tur sayısından değil yakınsama kuralından gelir; sınır yalnız bedelin nerede başladığını seçer. Sınırdan derin her kodlamayı yakınsama kuralı (5. kural, `NotConverged`) kat sayısından bağımsız yakalar; AC-2 ve AC-7'deki dört ve beş kat satırları bunu ölçer. Bedeli: üç turdan derin kodlanmış meşru karakter ve bir kez kodlanmış `%XX` görünümlü meşru veri (ör. `%255c`) reddedilir (Çözüm kuralı 4). Ocelot ya da .NET yükseltmesi yeniden ölçüm tetikleyicisidir. Sınır bir adlı sabittir; değişmesi paket revizyonudur | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (d) | Günlüğe ne yazılır | **(d1)** neden, yöntem, ham hedef uzunluğu, tur sayısı; ham hedef yok · (d2) ham hedef kırpılmış (ilk N karakter) · (d3) ham hedefin SHA-256 öneki | **(d1).** Yolda kimlik, e-posta ya da ad olabilir (OBS-001 PII kuralı). İlişki `CorrelationId` ile kurulur. (d) yalnız korumanın kendi `Warning` satırını kapsar. Not: istek günlüğü bugün her istekte, korumanın reddettiği istekte de, `RequestPath` alanını yazıyor (`Program.cs:144`); yani reddedilen isteğin yolu ağ geçidinin günlüğünde bugün görünür. Bu paket o satırı değiştirmez; ayrı karar (Follow-up Items). Öneriden farklı karar (ör. (d3)) paket revizyonu ve yeniden inceleme ister (AC-9 ona bağlı) | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (e) | Ağ geçidinin kiracı / yetki denetimleri ham yola bakıyor mu | — | **Ölçülmedi.** Kod okuması: `TenantResolutionMiddleware` yolu `Request.Path` ile sınıflıyor (Implementation Notes madde 5). Bu paketin koruması bu denetimlerden önce koştuğu için, koruma varken yol aşan istek o denetimlere ulaşmaz. Korumasız durumda neyin atlatılabildiği ayrı bir ölçüm işidir. Kapsam notu: kiracı çözümünün dışında tutulan `/api/lookups` öneki (`TenantResolutionMiddleware.cs:430-436`) üzerinden yol aşma bu paketin kapsamındadır: `/api/lookups/{everything}` AC-2 tablosundadır ve koruma varken her biçimde 400 alır. **Kaçış örneği (kod okuması, ölçülmedi):** `/api/platform/navigation/%252e%252e/tenants/x`. Kiracı çözümü bir kez çözülmüş `Request.Path`'e (`/api/platform/navigation/%2e%2e/tenants/x`) bakar ve onu kiracı kapsamlı örgüt yolu sayar (`:474`): `tenant_user` belirteci kabul edilir; kod yorumuna göre `platform_admin` reddedilir (`:467`). Ocelot isteği `/api/platform/navigation/{everything}` rotasıyla Platform servisine iletir; ağ geçidinin çıkışı (Ocelot → `HttpClient` / `System.Uri`) ölçülen rotadaki gibi `%2e%2e`'yi çözüp `..`'yı sıkıştırırsa (bu rotada ölçülmedi; Module Summary "Sebep") istek `/api/platform/tenants/x`'e, yani yalnız platform yöneticisine açık bir önek altına `tenant_user` belirteciyle ulaşır. Servisin kendi `[HasPermission]` denetiminin bunu durdurup durdurmadığı ölçülmedi. Koruma varken bu istek 400 alır (AC-2, `/api/platform/navigation/{everything}` satırları) | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) + CT | Bilgi; ayrı iş |
| (f) | İç uç anahtarının sabit süreli olmayan karşılaştırması | — | **Kapsam dışı, ayrı iş.** CT iletisindeki ölçüme (Yönetici) dayanır; bu yazar okumadı. Servis kodudur (XMC-001); işin açılması servis sahiplerine XMC-001 sorusudur | Bilgi sahibi: Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) (Sahip kararı 2026-11-02) | Bilgi |
| (g) | Q395 (yedek rota / zaman aşımı satırı) ile ilişki | — | **İlişki yok, ayrı iş** (CT iletisi). Bu paket rota ya da zaman aşımı değiştirmez | Bilgi sahibi: Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) (Sahip kararı 2026-11-02) | Bilgi |
| (h) | Kayıt satırının yeri | **(h1)** `UseSerilogRequestLogging`'den sonra, metrikten önce · (h2) boru hattının en başı (`UseCors`'tan önce) · (h3) `UseCors` ile `CorrelationIdMiddleware` arasında | **(h1).** Ret istek günlüğünde 400 olarak görünür, cevap `X-Correlation-Id` taşır, çapraz kökenli rette CORS başlıkları eklenir (tarayıcı kodu okuyabilir). CORS ön uçuşu `UseCors`'ta 204 ile biter ve korumaya ulaşmaz (çerçeve bilgisi, ölçülmedi; AC-15). Sağlık, metrik, kimlik ve kiracı çözümü korumadan sonra gelir. **Kayıt satırı koşullu metrik bloğuna (`if (observabilityOptions.Metrics.Enabled)`, `Program.cs:150-154`) girmez:** girerse metrik kapalı ortamda koruma da kapanır; AC-6 bunu metrik kapalı ikinci konakla ölçer. (h1) ile ret HTTP metriklerine (`UseHttpMetrics`) sayılmaz. Sayılması istenirse bu ayrı bir karardır ve kayıt satırını koşullu bloğa taşımadan çözülür (ör. korumanın kendi sayacı); bu paket sayaç eklemez. Öneriden farklı karar (ör. (h2)) paket revizyonu ve yeniden inceleme ister (AC-15 ona bağlı) | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (i) | Ret gövdesinin biçimi | **(i1)** `application/problem+json` + `status`, `code`, `traceId` · (i2) gövdesiz 400 · (i3) ağ geçidinin bugünkü `title` / `detail` biçimi | **(i1).** Ağ geçidinin bugünkü problem+json biçimine yakın kalır ve kullanıcıya görünen metin içermez (Çekirdek kural 4) | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (j) | Paket sahibi ve hedef tarih | `owner`: auth-owner · MOD-0032 sahibi · CT; `target`: tarih | **Kapandı.** Sahip 2026-11-02'de bu kulede (CT · Yetki sohbeti, doğrudan) "Evet, ben, 11-09" dedi: paketin sahibi ve bu işte MOD-0032 karar sahibi MOD-0018 (Auth) sahibidir; `target: 2026-11-09`. Künye buna göre yazıldı. Ağ geçidi paketine (`MOD-0032-api-gateway.md`) künye eklemek bu paketin işi değildir | Sahip | Kapandı 2026-11-02 |
| (k) | Korumayı kapatan ayar | **(k1)** yok · (k2) `Gateway:PathGuard:Enabled` | **(k1).** Kapatılabilen güvenlik koruması üretimde yanlışlıkla kapalı kalabilir. Geri alma kod geri almadır | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (l) | Mevcut `Mod0290Fu04ProductMasterTraversalTests` | **(l1)** değişmez · (l2) ölçülen `%252e%252e/` satırı eklenir · (l3) gerçek `Program.cs` konağına taşınır | **(l1).** Test Ocelot'u yalnız ölçüyor ve korumayı içermiyor. (l2) seçilirse o konakta **kırmızı** olur (ölçüm, Module Summary). Yeni testler ölçülen biçimi gerçek boru hattında kapsıyor. (l3) Follow-up Items'tadır. Test MOD-0290-FU04'e aittir; dokunmak XMC-001 sorusu olur | Karar: Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi). MOD-0290 (ürün ana verisi) kulesi kararla bilgilendirilir; karar vermez (Sahip kararı 2026-11-02) | «onayla» ile öneri kabul edilir |
| (m) | Kayıt satırı | `module-id-registry.md`'ye `MOD-0032-FU02` satırı | Ayrı bir değişiklikle yazılır. Mevcut `MOD-0032-FU1` satırı (`:126`, başka iş, `[Non-executable]`) değiştirilmez. Adlandırma farkı (`FU1` / `FU02`) satırın notunda yazılır | Yazan: CT · Yetki. Onaylayan: Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) (Sahip kararı 2026-11-02) | «onayla» ile öneri kabul edilir |
| (n) | Kural dışı biçimler (`%00`, aşırı uzun UTF-8, `..;`, tam genişlikte nokta) | Bu pakete ret kuralı ekle · AC-14 ile yalnız ölç, bulguya göre takip paketi | **Yalnız ölç (AC-14).** Kestrel bu biçimleri `..` olarak normalleştirmiyor diye beklenir ama ölçülmedi. Rota dışına çıkan bir satır bulunursa takip paketi açılır | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | DL-1 teslimi |
| (o) | Tablo testinin süre eşiği | **(o1)** yerelde Release derlemede 60 sn; aşılırsa teslim durur ve sahibe gelir · (o2) eşik yok, yalnız ölç ve yaz · (o3) daha sıkı eşik (ör. 20 sn) | **(o1).** Süre ölçülmedi (Test Expectations "Süre"); 60 sn, ≈ 5.600 yerel soket isteği için geniş bir tavandır (ölçülmedi) ve TEST-HIZ-01'e karşı bir çit koyar. Eşik aşılırsa biçim ya da rota sayısı koda gömülerek azaltılmaz; çözüm sahiple seçilir | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
| (p) | Çözülmüş aşamada `?` ve `#` kesim sonlandırıcı sayılır mı | **(p1)** evet: her aşamada `?` ve `#` de kesimi bitirir (aşama 0'da ham `#` dahil; ham `?` aşama 0'da zaten sorgudur) · (p2) hayır: kesim yalnız `/` ve `\` ile bölünür | **(p1).** Ağ geçidi `%3f` / `%23`'ü bir kez çözüp aşağı akış adresini kurarken çözülmüş `?` sorguyu, `#` parçayı başlatabilir; o zaman `..%3f` kesimi aşağı akışta tam `..` kesimi olur (çerçeve davranışı, ölçülmedi). Ham `#` aşama 0'da da kesim sonu sayılır; yoksa `/api/x/..#y`'nin sonucu yoldaki ilgisiz bir `%XX`'e bağlı kalırdı (üçüncü inceleme F-2). Bedeli: `..?` ya da `.?` ile biten kodlanmış kesim taşıyan meşru yol reddedilir; böyle bir istemci bilinmiyor (ölçülmedi). Aşama 0'da `?` zaten sorgudur (Çözüm kuralı 2). Paketin kuralları, AC-2 biçim listesi ve AC-7 (p1) üzerine yazıldı; (p2) paket revizyonu ve yeniden inceleme ister | Sahip (MOD-0018 sahibi; bu işte MOD-0032 karar sahibi) | «onayla» ile öneri kabul edilir |
