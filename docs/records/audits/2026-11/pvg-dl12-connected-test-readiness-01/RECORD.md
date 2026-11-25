# DL-12 bağlı test zinciri — salt okunur uygulanabilirlik değerlendirmesi

Tarih: 2026-11-20. Kaynak HEAD: `9fa2ed6755ade67c3a09d70681560e75139f9e4c`.
İnceleyen ajan: `/root/pvg_dl12_readiness`; model mevcut Codex oturumundan devralındı, effort araçla ölçülemedi. Bu kayıt, ajanın teknik briefinin CT tarafından düzenlenmiş özetidir; ham sohbet dökümü değildir.
Bağlantı: ayrı sohbet revizyon 17 incelemesinin F-4 bulgusu.

## Sonuç

Yalnız mevcut iki test dosyası ve alias ProjectReference ile temiz Phase1 garantisi yok. En dar öneri, bağlı testi mevcut C# dosyasında tutmak ve mevcut Phase1 workflow'una Node/npm hazırlığını açıkça eklemek. Yeni workflow/package/framework gerekmiyor; mevcut CI dosyasının sahibi ve kapsam onayı açık.

## Kaynak dayanakları

- `.github/workflows/phase1-gates.yml`: checkout/.NET/jq/Phase1; Node/npm hazırlığı yok.
- `scripts/run_phase1_gates.sh`: Web projesi doğrudan dotnet test ile çalışır.
- `.github/workflows/web-js-tests.yml`: Node 22 ve npm ci ayrı runner'dadır; node_modules Phase1'e taşınmaz.
- `frontend/Diten.Web/package-lock.json`: jsdom 26.1.0 kilitli, Node >=18 yeterli.
- `CiServiceTestsGuardTests.cs`: önerilen setup-node commit'i mevcut izinli action tablosunda.
- CT de iki workflow'u ve CODEOWNERS'ı okudu; CI sahipleri kayıtlıdır, bu inceleme sahip mutabakatı değildir.

## Tek önerilen sözleşme

Mevcut `.github/workflows/phase1-gates.yml` Gates adımından önce yalnız pinned `actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020` / Node 22 ve mevcut frontend lock üzerinden `npm ci --no-audit --no-fund` hazırlığı alır. Yetki, concurrency, olaylar veya diğer kapılar değiştirilmez. Bu henüz dormant kapsam önerisidir.

Test projesi yalnız tek `Aliases="pvgapi"` ProjectReference alır. Bağlı testin sahibi mevcut `PvgCaseIntakeTriageWebTests.cs` olur. Gömülü Node/jsdom programı `node -e` ile gerçek MVC Razor HTML'i ve üretim form.js'i çalıştırır. Yeni JS runner, Content girdisi veya test içinde indirme yok.

Node fetch istekleri stdin/stdout JSON taşımasıyla gerçek MVC test HttpClient'ine gider. FormData ad/değer/sıra/tekrar/boşlukları korunur; yeni PUT gövdesi elle kurulmaz. Cookie/JWT C# tarafında kalır. MVC Gateway handler'ı sabit Wire cevabı yerine gerçek PVG hostuna gider; rastgele loopback test hostu gerçek application ve gerçek in-memory store kullanır. Önceki ve sonraki aynı depo okuması karşılaştırılır.

Node/jsdom yokluğu, süreç veya bridge hatası, timeout, submit yokluğu ve başarılı gönderim senaryosunda sıfır update FAIL olur. Skip, gizli kurulum, fake-success fallback yok. Mesajlar run nonce/istek kimliğiyle eşleşir; yalnız izinli test istekleri kabul edilir. Kasıtlı ret testlerinde beklenen sıfır update ayrıca korunur.

Temiz hazırlık ve hedefli komutlar:
```text
npm --prefix frontend/Diten.Web ci --no-audit --no-fund
dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug --filter "FullyQualifiedName~PvgCaseIntakeTriageWebTests"
```

Mevcut JS test dosyası bağımsız UI regresyonlarını taşır; bağlı ölçüm C# testinden kesin çalışır. Eski 'fixture mevcut jsdom testine aktarılır' taslağı bu sözleşmeye hizalanmalıdır.

## Ölçülmeyenler

Restore, build, npm, Node, ürün testi veya CI deneyi çalıştırılmadı. Alias derlemesi, bridge, gerçek form çalışması ve temiz runner başarısı gelecekte kanıtlanmalı. Kaynak incelemesi E2 kanıtı, test PASS'i veya dispatch izni değildir. Ürün/test/CI dosyası değiştirilmedi. Öneri paket yazarına iletildi.
