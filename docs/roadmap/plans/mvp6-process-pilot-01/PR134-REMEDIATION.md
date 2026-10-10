# PR #134 düzeltme paketi — ölçüm, yol haritası, prompt'lar

Kaynak: gözden geçirenin R-1…R-6 paketi (2026-10-05).
Ölçüm tabanı: merge-base `bc109afa4`, dal HEAD `eb1306590`, `origin/main` tepe `6c038399a` (2026-10-02 16:33).
Bu belge iddia değil ölçümdür; her satırın arkasındaki komut yazılıdır.

---

## 0. Önce: paketin metni ile ölçümün ayrıldığı dört yer

Gözden geçiren çoğunlukla isabetli. Ayrıldığı yerler işi büyütmüyor, **kabul kriterlerini
uygulanamaz kılıyor** — ve ikisi sahip kararı gerektiriyor.

### 0.1 R-1'in dosya listesi tam doğru; tek satırlık komutu ise fazlasını yapar

```
git diff --numstat bc109afa4 HEAD        -- .antigravity/ AGENTS.md   →  5 dosya, +216 -1
git diff --numstat bc109afa4 origin/main -- .antigravity/ AGENTS.md   → 28 dosya, +1479 -18
```

Bizim değiştirdiğimiz **tam olarak** gözden geçirenin saydığı 5 dosya. `git diff origin/main`
1477 "silme" gösteriyor ama bunlar bizim silmelerimiz değil, `main`'in bizde olmayan
eklemeleri (`audit-trail-standard.md` +373, `verify_datatable_page.py` +445, …). **Biz hiçbir
kural dosyası silmedik.**

Sonuç: `git checkout origin/main -- .antigravity/ AGENTS.md` bizim 5 dosyamızı geri almakla
kalmaz, `main`'in 1479 satırlık yeni kuralını da merge'den **önce** dala taşır. R-6 "R-1
sonrası çakışma kalmaz" dediği için bu niyetli görünüyor; yine de commit mesajında "main'in
kural katmanı erken alındı" diye yazılmalı, yoksa 28 dosyalık değişiklik bizim işimiz sanılır.

### 0.2 R-3 bir güvenlik geri alımıdır ve sahip kararı ister

11 dosyanın **hepsi** `origin/main` üzerinde `JwtSettings.Secret` taşıyor, hepsi aynı digest:

```
frontend/Diten.Web · gateway · Auth · Crm · DevEnablement · EnterpriseStrategy
Hcm · HumanCapital · Mdm · Platform · TalentEcosystem        →  hepsi  ff4555d1
```

R-3 bu 11 dosyayı `main` hâline döndürüp **tekrar izlemeye almayı** istiyor. Bu, paylaşımlı
imza anahtarını bu dalın geçmişine geri koymak demektir. `987b9c9d3`, `fa791f07f` ve
`8f60dc6d3` güvenlik commit'leriydi; R-3 ilk ikisini tersine çeviriyor.

Not: `ff4555d1` **Q293'e kadar geçerli bir imza anahtarıdır** — sanitasyon rotasyon değildir.
R-3 uygulanırsa Q293 daha acil hâle gelir, daha az değil.

Hafifletici üç olgu, kararı sahibin verebilmesi için: (a) `main` bu deseni hâlihazırda
uyguluyor, yani anahtar zaten `main` geçmişinde; (b) `.example.json` deseni iki kaynak
yarattığı için K6 anlamında sapma üretiyor; (c) R-3 base config temizliğini ve "secret yoksa
servis açılmaz" kontrolünü **korur**. Yani tersine dönen şey yalnızca izleme durumu.

**→ DC-R3 kararı gerekiyor. Bu karar verilmeden R-3 başlamaz.**

### 0.3 R-4'ün kabul kriteri hiçbir doğru eylemle sağlanamaz

```
git grep -n "5065" -- services/Diten.CrmService watch-diten*.ps1   →  asla boş olmaz
```

Çünkü `5065` dizisi şurada da geçiyor:

* 24 commit'lenmiş `.tmp-*` binary'si (`libmongocrypt.so/.dylib/.dll` — bayt eşleşmesi)
* 5 `TestResults/*.trx` dosyası, test GUID'lerinin içinde (`28725065-…`, `c5065bb6-…`)
* 4 CRM controller'ının **yorum satırı** ("there is no direct-to-5065 business surface")

Gerçek port tanımı yalnız 4 yerde: `launchSettings.json` (2 satır), `watch-diten.ps1`,
`watch-diten-bg.ps1`. Kriter bu dört yola daraltılmalı — aksi hâlde Q460'ın şekli tekrar eder:
çıkışı olmayan kapı kapı değildir.

Ayrıca **bant çelişkisi**: `CLAUDE.md:43` mikroservis bandını `5011–5060` ilan ediyor ve 5060'ı
son port sayıyor. 5065 de 5066 da bandın dışında. R-4 port kaydının bu PR'da değişmemesini
istiyor, dolayısıyla kod 5066'ya giderken kayıt hâlâ "bant 5060'ta biter" diyecek.

**→ DC-R4 kararı gerekiyor: 5066 kalsın ve bant kaydı ayrı PR'da mı düzeltilsin, yoksa bant
içi boş bir port mu seçilsin?**

### 0.4 R-1 ile kendi K4 kuralımız çatışıyor

R-1 "geri alınan kurallara atıf yapan kayıtlar düzeltilecek" diyor. K5/K6'ya atıf yapan
kayıtlar bu hafta yazıldı ve **K4 kayıtların yazıldıktan sonra düzeltilmediğini** söylüyor.

Önerim ve gerekçesi: kayıtlar **düzeltilmez**, üzerine bir düzeltme kaydı yazılır (bugün
Q444'te `ff4555d1` için yapılan şeyin aynısı). Kayıt yanlış değildi; dayandığı kural geri
alındı. Bu ayrımı silmek, kaydın o anda neyi ölçtüğünü yok eder.

**→ DC-R1 kararı gerekiyor.** Gözden geçiren "atıflar düzeltilecek" derken kaydın kendisini mi
yoksa pack/plan atıflarını mı kastediyor, netleşmeli. Pack ve planlar düzeltilebilir; kayıtlar
K4 altındadır.

### 0.5 Tek iyi haber: R-3 merge tuzağını yok ediyor

Q463 birleşmeyi `git merge-tree` ile ölçtü: 16 görünür çatışma ve **tek tuzak** —
`services/Diten.CrmService/.../appsettings.Development.json` modify/delete, git'in kendi
ifadesiyle *"version origin/main left in tree"*, yani dikkatsiz çözüm sırlı dosyayı geri koyar.

R-3 o dosyayı zaten geri getirdiği için **bu tuzak tamamen ortadan kalkar**. Güvenlik
maliyetinin karşılığında alınan şey bu.

---

## 1. Benim yapamadığım adımlar — sahibin çalıştırması gerekenler

`.claude/settings.local.json` bu dalda bunları bana kapatıyor. Etrafından dolaşmam:

| adım | gereken komut | neden ben yapamıyorum |
|---|---|---|
| R-1 | `git checkout origin/main -- .antigravity/ AGENTS.md` | `Bash(git checkout *)` **deny** · `Edit(AGENTS.md)` **deny** |
| R-5 | `git rm --cached .claude/settings.local.json` | `Bash(git rm *)` **deny** |
| her adım sonu | `git push` | `Bash(git push *)` **deny** |

`git add` ve `git commit` artık `ask` — onları ben çalıştırıp senin onayına sunabiliyorum.

---

## 2. Bağımlılık sırası

R-6 en sonda olmak zorunda; R-1 ve R-3 merge çatışmalarını değiştirdiği için onlardan önce
gelmeli. R-2 en büyük iş ve diğerlerinden bağımsız, o yüzden paralel gidebilir.

```
DC-R1 · DC-R3 · DC-R4   (sahip kararları — hepsi önce)
        │
        ├─ R-1  (sahip: checkout)  ──┐
        ├─ R-3  (11 dosya geri)    ──┤
        ├─ R-4  (port)             ──┼──→  R-6  (merge + CI)  ──→  PR yorumu
        ├─ R-5  (sahip: git rm)    ──┤
        └─ R-2  (en büyük, paralel)──┘
```

Her madde **ayrı commit**, mesajında madde numarası, force-push yok — paketin istediği gibi.

---

## 3. Adım adım

### Adım 0 — üç kararı al (sen)
DC-R1, DC-R3, DC-R4. Üçü de yukarıda gerekçeli. Karar kaydı
`docs/records/decisions/2026-10/` altına yazılır, CT-QUEUE'ya satır girer.

### Adım 1 — R-1 (sen çalıştırırsın, 1 komut + 1 commit)
```bash
git checkout origin/main -- .antigravity/ AGENTS.md
```
Sonrasını ben yaparım: `git diff origin/main -- .antigravity/ AGENTS.md` boş mu ölçer,
commit'i hazırlar, onayına sunar. DC-R1 "pack/plan atıfları düzeltilecek" derse o ayrı commit.

### Adım 2 — R-5 (sen çalıştırırsın, 1 komut)
```bash
git rm --cached .claude/settings.local.json
```
`.gitignore`'a satırı ben eklerim.

### Adım 3 — R-3 (lane, DC-R3 onayından sonra)
11 dosya `main` hâline döner, `.example.json` 11 dosyası ve `.gitignore` satırları kalkar,
yeni ayarlar (ServiceIdentity, `Loads:ReferenceBaseUrl`) `Development.json` içine taşınır.

### Adım 4 — R-4 (lane)
CRM 5065→5061 geri; SupplyChain 5061→5066; ocelot 2 route → 5066; `ReferenceBaseUrl` → 5066.

### Adım 5 — R-2 (en büyük — sıralı üç ajan)
Auth/Platform geri alma → lookup + sayfa seçimi → MDM fail-closed doğrulama → 7 dil → test.

### Adım 6 — R-6 (merge + CI)
`git merge origin/main`. Q463'ün ölçtüğü 16 çatışma; R-1 ve R-3 sonrası 9'a düşmesi beklenir
(`.antigravity`, `AGENTS.md`, CRM appsettings çıkar). **Beklenti değil, merge sırasında yine
ölçülür.**

### Adım 7 — PR yorumu
R-1…R-6 madde madde, kabul kanıtı komut çıktısıyla. `docs/records` altına yeni kanıt
dosyası **eklenmez** (paketin açık talebi — bu, bizim normal kanıt kuralımızın bu PR için
sahip tarafından daraltılmasıdır, Q19 waiver şeklinde kaydedilir).

---

## 4. Paste-ready prompt'lar

Her prompt §17.4 zorunlu alanlarını içerir ve `.antigravity/**` okumasını **açıkça** emreder;
bu iki şey olmadan G3 düşer. Sıfır-inisiyatif: alan eksikse ajan iş üretmez, soru üretir.

> **R-1 sonrası uyarı:** `.antigravity/` `main` hâline döndüğü için lane'ler `main`'in kural
> katmanını okur (`verify_datatable_page.py` dahil, +445 satır). Prompt'lardaki script
> çağrıları o sürüme göredir.

---

### PROMPT R-3 · `security-agent` → secret deseni `main`'e döner

```text
AGENT: security-agent
WP: PR134-R3-secret-pattern-restore
BRANCH: feature/mvp6-logistics  (yeni dal açma, force-push yok)
COMMIT: tek commit, mesaj "fix(mvp6): R-3 CRM secret deseni korunur — 11 Development config yeniden izlenir"

ZORUNLU OKUMA (atlanırsa dur):
  Read AGENTS.md
  Read .antigravity/rules/*.md          (main hâli — R-1 sonrası)
  Read docs/guides/operations/control-tower-sop.md  §18.0, §32 (K4, K6, K19, K23)
  Read docs/records/decisions/2026-10/mvp6-secret-gate-scope-owner-decision-01.md   (Q460)
  Read docs/records/decisions/2026-10/<DC-R3 kayit dosyasi>                          (bu isin yetkisi)

§17.4 security-agent zorunlu alanlari:
  permission key + policy : yok — bu is config/izleme yuzeyi, yeni permission tanimlanmayacak
  actor tipi              : yok (calisma zamani akisi degismiyor)
  tenant izolasyon yuzeyi : degismiyor — dokunulmaz
  denetlenecek endpoint   : yok; denetlenecek yuzey 11 config dosyasi + .gitignore

GOREV (tam olarak bu, fazlasi yok):
  1. Asagidaki 11 dosyayi origin/main halina dondur ve TEKRAR IZLEMEYE AL:
       frontend/Diten.Web/appsettings.Development.json
       gateway/Diten.ApiGateway/appsettings.Development.json
       services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.Development.json
       services/Diten.CrmService/src/Diten.CrmService.Api/appsettings.Development.json
       services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/appsettings.Development.json
       services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/appsettings.Development.json
       services/Diten.HcmService/src/Diten.HcmService.Api/appsettings.Development.json
       services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/appsettings.Development.json
       services/Diten.MdmService/src/Diten.MdmService.Api/appsettings.Development.json
       services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json
       services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/appsettings.Development.json
  2. 11 adet appsettings.Development.example.json dosyasini kaldir ve .gitignore'daki
     ilgili satirlari sil.
  3. Dalda eklenen yeni ayarlari ilgili Development.json icine tasi:
       - SupplyChain: "Loads": { "ReferenceBaseUrl": ... }   (port degeri icin R-4'u bekle)
       - ServiceIdentity ayarlari (MdmServiceIdentity / PlatformServiceIdentity)
     Hicbir ayari UYDURMA; yalnizca .example.json'da yazili olani tasi.

DOKUNULMAZ (K16 — kapsam genisletme yasak):
  - HumanCapital/TalentEcosystem base config'inden secret'in kaldirilmasi KALIR
  - "secret yoksa servis acilmaz" startup kontrolu KALIR
  - commit'lenmis .tmp-* cikti agaci (Q341'in ayri karari)
  - baska hicbir appsettings*.json

SIR KURALI (ihlali ise son verir):
  - Hicbir sir DEGERI prompt'a, rapora, commit mesajina veya chat'e yazilmaz. Yalniz
    sha256 ilk 8 hanesi.
  - Q235'te aciga cikan deger hicbir yerde yeniden kullanilmaz.
  - Test degeri gerekiyorsa `openssl rand` ile uretilir.

KABUL (kanit komutla, iddia ile degil):
  git diff origin/main --stat -- '*appsettings.Development.json'
     → yalniz yeni ayar EKLEMESI gorunur; silme satiri YOK
  git ls-files '*appsettings.Development.example.json'   → bos
  git grep -n 'appsettings.Development.example' -- .gitignore → bos
  Her 11 dosya icin JwtSettings.Secret digest'i origin/main ile AYNI (deger degil digest yaz)
  dotnet build calisan servisler icin yesil

RAPOR: olculen komut + cikti. "Yaptim" yetmez; komutu ve ciktisini yaz.
BITIS: commit hazirla, PUSH ETME — sahip push eder.
```

---

### PROMPT R-4 · `integration-agent` → port düzeltmesi

```text
AGENT: integration-agent
WP: PR134-R4-port-correction
BRANCH: feature/mvp6-logistics  (yeni dal yok, force-push yok)
COMMIT: tek commit, mesaj "fix(mvp6): R-4 CRM 5061'e doner, SupplyChain 5066'ya tasinir"

ZORUNLU OKUMA (atlanirsa dur):
  Read AGENTS.md  §3 port semasi
  Read .antigravity/rules/ports.md        (main hali)
  Read .antigravity/agents/integration-agent.md
  Read CLAUDE.md:42-43                    (ilan edilen bant 5011-5060)
  Read docs/records/decisions/2026-10/<DC-R4 kayit dosyasi>

§17.4 integration-agent zorunlu alanlari:
  controller/route family : shipment-bundle (2 ocelot route)
  upstream path          : /api/shipment-bundle  ve  /api/shipment-bundle/{everything}
  downstream             : SupplyChainService
  hedef servis portu     : 5066  (DC-R4 ile onaylandi — bandin disinda oldugu bilinerek)
  header gecisi          : Authorization + X-Tenant-Id — MEVCUT DAVRANIS DEGISMEZ
  hangi ocelot dosyasi   : gateway/Diten.ApiGateway/ocelot.json

GOREV (tam olarak bu):
  1. CRM 5065 → 5061 geri al, YALNIZ bu dort yerde:
       services/Diten.CrmService/src/Diten.CrmService.Api/Properties/launchSettings.json  (2 satir)
       watch-diten.ps1
       watch-diten-bg.ps1
  2. services/Diten.SupplyChainService/.../Api/appsettings.json  "Urls" → http://127.0.0.1:5066
  3. ocelot.json shipment-bundle 2 route → Port: 5066
  4. SupplyChain icindeki ReferenceBaseUrl deger(ler)i → 5066

DOKUNULMAZ:
  - AGENTS.md port tablosu ve .antigravity/rules/ports.md  (R-1 geregi bu PR'da DEGISMEZ)
  - CRM controller'larindaki "direct-to-5065" YORUM satirlari — bunlar port tanimi degil,
    aciklama metni; DC-R4 ayri karar vermedikce dokunulmaz
  - TestResults/*.trx ve .tmp-* agaci  (icindeki 5065 GUID/bayt eslesmesidir, port degil)

KABUL (kriter daraltildi — eski hali .tmp-* yuzunden asla gecemezdi):
  git grep -nI 5065 -- \
     services/Diten.CrmService/src/Diten.CrmService.Api/Properties/launchSettings.json \
     watch-diten.ps1 watch-diten-bg.ps1
     → bos
  SupplyChain 5066'da aciliyor  (gercek process, log kaniti)
  Gateway uzerinden /api/shipment-bundle 5066'ya gidiyor  (gateway log satiri kaniti)
  CRM 5061'de aciliyor
  Iki servis ayni anda ayakta — port cakismasi YOK  (lsof/netstat kaniti)

RAPOR: her kabul satiri icin komut + cikti.
BITIS: commit hazirla, PUSH ETME.
```

---

### PROMPT R-2 · `@orchestrator` → token'dan legal entity çıkar, MVP-1 deseni gelir

Bu en büyük madde. Tek ajana verilmez; `@orchestrator` §16.4 single-writer içinde sıralı
çalıştırır. **Ön koşul:** aşağıdaki A0 adımı ölçülmeden B ve C başlamaz.

```text
AGENT: @orchestrator  (alt ajanlar: backend-architect → frontend-ui-ux → l10n-agent → testing-agent)
WP: PR134-R2-legal-entity-mvp1-pattern
BRANCH: feature/mvp6-logistics  (yeni dal yok, force-push yok)
COMMIT: adim basina bir commit, hepsinde "R-2" gecer

ZORUNLU OKUMA (atlanirsa dur):
  Read AGENTS.md
  Read .antigravity/agents/orchestrator.md  + her alt ajanin kendi dosyasi
  Read .antigravity/rules/*.md              (main hali — R-1 sonrasi)
  Read docs/guides/operations/control-tower-sop.md  §18.0 (14 satirlik gate), §32 K12/K16/K18/K23
  Read execution/domains/supply-chain-execution/module-packs/MOD-0183,0184,0185,0186,0187
  Read docs/analysis/contracts/shipment-bundle.openapi.yaml
  MVP-1 REFERANS DESENI (birebir okunacak, taklit edilecek):
    frontend/Diten.Web/Views/CRM/Campaigns/_Form.cshtml
    frontend/Diten.Web/Controllers/.../TasksController.cs
    frontend/Diten.Web/Controllers/.../WorkingCalendarOverridesController.cs

── A0 · ONCE OLC, SONRA YAZ (bu adim atlanirsa WP READY olamaz) ──
  A0.1  GET /api/legal-entities/lookup GERCEKTEN VAR MI?
        `git grep -n 'legal-entities/lookup'` 9 dosyada esliyor — ama bu CT'nin bugun dort kez
        yaptigi hatanin sekli: eslesme varligi kanitlamaz. Oyle ki rapor et:
          - endpoint hangi serviste tanimli (controller + satir), yoksa "YOK" yaz
          - ocelot'ta route'u var mi (dosya + satir)
          - donen sema: alan adlari + tipler
          - tenant filtrelemesi sunucu tarafinda mi
        YOKSA: K12 geregi dur ve contract talep et. Endpoint UYDURMA.
  A0.2  Geri alinacak yuzeyi dosya:satir dok:
          Auth    : ITenantLegalEntityScopeClient, PlatformTenantLegalEntityScopeClient,
                    TokenService.GenerateTenantAccessToken, Login/RefreshToken/VerifyMfa/
                    ForcedChangeTenantPassword handler'lari
          Platform: InternalTenantLegalEntityScopeController + yalniz onun icin eklenen kayitlar
          Web     : 5 SupplyChain controller'inda TryResolveScopeClaim(["legal_entity_id", ...])
          Service : 7 *ContextMiddleware'de "legalEntityId istekte gelirse reddet" mantigi
        Platform DI'de "yalniz onun icin eklenen kayitlar" ifadesini TEK TEK dogrula —
        baska is tarafindan da kullanilan bir kayit varsa onu KALDIRMA, raporla.

── B · backend-architect ──
  §17.4 zorunlu alanlari:
    hedef servis + yol   : Diten.AuthService, Diten.Platform, Diten.SupplyChainService
    aggregate/entity     : yeni entity YOK — mevcut Shipment/Return/Carrier/Load/Claim
    command/query        : her birine LegalEntityId alani; yeni command YOK
    validator kurallari  : LegalEntityId zorunlu; MDM'den tenant'a aitligi dogrulanir;
                           dogrulanamazsa FAIL-CLOSED (403/422 — pack hangisini diyorsa o)
    permission key'leri  : DEGISMEZ — yeni key tanimlanmaz
    Response<T> + CustomBaseController : mevcut desen korunur
  Gorev:
    B.1  A0.2'deki Auth + Platform yuzeyini geri al. JWT'ye legal_entity_id EKLENMEZ.
    B.2  TenantId yine YALNIZ JWT'den gelir — bu degismez, zayiflatilmaz.
    B.3  LegalEntityId istekle gelir; SupplyChainService MDM'den tenant'a aitligini dogrular.
         Dogrulama FAIL-CLOSED: MDM ulasilamazsa istek REDDEDILIR, gecilmez.
    B.4  7 *ContextMiddleware'deki "istekte gelirse reddet" mantigini yeni desene cevir.
  Yasak: DTO/alan uydurmak · validator yazmadan handler yazmak · tenant izolasyonunu gevsetmek

── C · frontend-ui-ux ──
  §17.4 zorunlu alanlari:
    area + module       : SupplyChain / Shipments, Returns, Carriers, Loads, Claims (5 sayfa)
    golden_reference    : her modulun kendi pack'indeki slim|compact — PACK'TEN OKU, SECME
    shell/layout        : mevcut shell korunur
    kolon + filtre      : mevcut set DEGISMEZ; yalniz legal entity SECIM ALANI eklenir
                          (enum degil lookup → Select2, MVP-1 Campaigns _Form.cshtml deseni)
    L10n key seti       : alan etiketi + dogrulama mesajlari (asagida l10n-agent'a devredilir)
    teslim oncesi       : python3 .antigravity/scripts/verify_datatable_page.py . \
                            --area SupplyChain --module <Modul> --reference <slim|compact>
  Gorev: 5 sayfaya legal entity secimi; secenekler A0.1'de dogrulanan lookup'tan gateway
         uzerinden dolar; LegalEntityId istekle gonderilir.
  Yasak: referans/layout belirsizse uretime devam etmek

── D · l10n-agent ──
  §17.4 zorunlu alanlari:
    modul turu   : Tenant → 7 DIL (en, tr, fr, es, zh, ar, ru). Eksik tek dil = is bitmemistir.
    resx yollari : frontend/Diten.Web/Resources/SharedResource.<dil>.resx  ve/veya
                   frontend/Diten.Web/Resources/Views/SupplyChain/<Modul>/<Modul>Index.<dil>.resx
                   (SharedResource mi modul resx'i mi — C adimi hangisini kullandiysa o)
    key listesi + HER DIL ICIN GERCEK METIN : alan etiketi + dogrulama mesajlari
  Yasak: placeholder · Ingilizce kopya · ceviri bilinmiyorsa uydurmak (sor)

── E · testing-agent ──
  §17.4 zorunlu alanlari:
    test edilecek akis : 5 sayfada legal entity secerek create + list
    beklenen davranis  : pack'teki AC — AC yoksa test yazma, contract talep et (K12)
    izolasyon senaryolari : BASKA TENANT'IN legal entity'si gonderilince servis REDDEDER;
                            soft-delete + TenantId izolasyonu
    test projesi yollari : services/Diten.SupplyChainService/tests/... ve frontend/Diten.Web.Tests/...
  Sabotaj kaniti ZORUNLU: dogrulamayi kasten bozup testin KIRMIZI dondugu gosterilir,
  sonra geri alinir. Sabotaj kaniti olmayan guard guard degildir.

── KABUL (R-2 butunu) ──
  git grep -n legal_entity_id -- services/Diten.AuthService          → bos
  git grep -n InternalTenantLegalEntityScope -- services/Diten.Platform → bos
  5 sayfada legal entity secilerek create + list CALISIYOR  (golden flow, her adimdan sonra
    reload, DB mutabakati, gateway log satiri — §18.0 14 satirlik matris doldurulur)
  Baska tenant'in legal entity'si → servis REDDEDIYOR  (test + sabotaj kaniti)
  7 dilin hepsi dolu  (dil basina key sayisi esit, raporda tablo)
  MOD-0183/0184/0185/0186/0187 ve shipment-bundle.openapi.yaml icindeki
    "LegalEntityId yalniz JWT'den" ifadeleri guncellendi

RAPOR: her kabul satirinda komut + cikti. "PASS" yetmez (K13: ajan PASS'i CT ACCEPTED degildir).
BITIS: commit'leri hazirla, PUSH ETME.
```

---

## 5. Bu belgenin ölçüm komutları

Yukarıdaki her sayı şu komutlardan geldi; tekrar ölçülebilir:

```bash
git diff --numstat bc109afa4 HEAD        -- .antigravity/ AGENTS.md
git diff --numstat bc109afa4 origin/main -- .antigravity/ AGENTS.md
git diff --diff-filter=D --name-only bc109afa4 HEAD -- '*appsettings.Development.json'
git merge-tree --write-tree --name-only HEAD origin/main
git grep -nI 5065 -- services/Diten.CrmService 'watch-diten*.ps1'
git grep -ln 'legal_entity_id\|LegalEntityScope\|InternalTenantLegalEntityScope'
```

Sır digest'leri `JwtSettings.Secret` değerinin sha256'sının ilk 8 hanesidir; hiçbir değer
bu belgede yazılı değil.

---

## 6. A0.1 SONUCU — CT ölçtü, kapandı (2026-10-06)

R-2'nin prompt'undaki A0.1 kapısı **CT tarafından ölçüldü ve geçti.** Lane artık A0.2'den
başlar; endpoint arayışını tekrarlamasın.

### Endpoint var ve gateway'den erişilebilir

| | ölçüm |
|---|---|
| tanım | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:13` (`[Route("api/legal-entities")]`) + `:32` (`[HttpGet("lookup")]`) |
| tam yol | `GET /api/legal-entities/lookup` |
| gateway rotası | `/api/legal-entities/{everything}` → port **5059** (MDM) — catch-all taşıyor, adı geçmiyor |
| izin | `[HasPermission("mdm.legal-entities.read")]` |

### Dönen şema

`Response<IReadOnlyList<LegalEntityLookupDto>>`,
`LegalEntityModels.cs:98`:

```
LegalEntityId   Guid
Code            string
LegalName       string
DisplayName     string?
LifecycleState  string
Referenceable   bool
```

### Tenant filtresi SUNUCU TARAFINDA — doğrulandı

`LegalEntityRepository.cs:30-37`:

```csharp
var filter = Builders<LegalEntity>.Filter.And(
    TenantFilter,
    Builders<LegalEntity>.Filter.Eq(x => x.OperationalStatus, LegalEntityOperationalStatus.Active));
```

`TenantFilter`, `:11`'de enjekte edilen `ITenantContext`'ten gelir. İstemci endişesi değil.
Handler (`GetLegalEntitiesLookupHandler.cs:21`) `TenantId` almaz — izolasyon repository'de.

### Beklenmeyen kazanç: doğrulama uç noktası da var

`LegalEntitiesController.cs:48`:

```csharp
[HttpGet("{legalEntityId:guid}/lookup-validation")]
public async Task<IActionResult> ValidateReference(Guid legalEntityId, ...)
```

R-2'nin B.3'ü "SupplyChainService MDM'den tenant'a aitliğini doğrulayacak" diyor — **doğrulama
uç noktası zaten mevcut.** Lane yenisini yazmaz, bunu çağırır ve fail-closed sarar.

### MVP-1 referans deseni — dört Web controller'ı aynı yolu proxy ediyor

```
frontend/Diten.Web/Controllers/TasksController.cs:361
frontend/Diten.Web/Controllers/WorkingCalendarOverridesController.cs:134
frontend/Diten.Web/Controllers/OrganizationUnitsController.cs:74
frontend/Diten.Web/Controllers/LegalEntitiesController.cs:114
```

Hepsi `[HttpGet("api/legal-entities")]` → `{gateway}/api/legal-entities/lookup`. SupplyChain'in
5 controller'ı bu deseni birebir alacak.

### Lane'in çözmesi gereken yeni kalem: izin

`mdm.legal-entities.read` kataloğa `DataSeeder.cs:393` ile eklenmiş (`moduleOverride:
"legal-entity"`) ama **SupplyChain rollerinin varsayılan şablonunda yok.** Shipments/Returns/
Carriers/Loads/Claims kullanıcısı seçim listesini doldurabilmek için bu izni tutmak zorunda.

Bu Q459'un aynı ailesinden: bir eylem, kendi anahtarının **yanı sıra** başka bir modülün
anahtarını gerektiriyor. Lane bunu kendi başına çözmez — ölçer, raporlar, sahibe getirir.

### Dikkat: yakın isimli ama YANLIŞ uç nokta

`LegalEntityLookupsController.cs:14` → `[Route("api/legal-entities/lookups")]` **çoğul**, ve
legal entity listesi döndürmez: Legal Entity sihirbazının referans açılır menülerini
(legal-form, organization-role, control-type, accounting-standard, tax-regime) besler. Lane
bunu `lookup` ile karıştırmasın.
