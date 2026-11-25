# MOD-0230 revizyon 20 — kapsam ve yürütme onayı isteği

Durum: ÖNERİ / henüz onaylanmadı. Tarih: 2026-11-20.
Pack: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`.
SHA-256: `f5a028c54ced21480a94d0db45b99a81ab9a982a56f3934818955aa2f44d35b1`.
Domain SHA-256: `b201a180309d5a0e8e04de8b845cc16bd55912085fcc345b4a51f19999c27099`.

## Neden yeni karar gerekiyor?

Kullanıcının önceki onayı revizyon 17 içindi ve önce ayrı sohbet incelemesini şart koşuyordu. Ayrı inceleme engelleri ortaya çıkardı; yeni kapsam ortak test projesi ve CI hazırlığını, Edit formunu ve yeni alan politikası sırasını açıkça içeriyor. Yeni baytlar için eski onay taşınmaz (kart S-53/§7; SOP §20.4). Ortak dosyada değişiklik XMC-001 ve sahip/tek-yazar mutabakatı ister; `.github/CODEOWNERS` CI sahiplerini belirler. Bu yüzden mevcut genel devam sözünü bu yeni ortak dosya/politika onayı gibi kullanmıyoruz.

## Tek önerilen kapsam

1. Revizyon 20 pack/domain kapsamı; diğer modüller veya operasyonel kullanım açılmaz.
2. Alan politikası geçerli bağlam/izin/istek doğrulamasından sonra, depo okumasından önce değerlendirilir. Ret halinde mevcut/yok/başka kiracıdaki kimlikler aynı güvenli ret yanıtını verir; sıfır store okuması/yazması, kayıt varlığı veya değeri sızmaz. Politika izin verirse mevcut tenant-scoped not-found davranışı korunur.
3. Ortak dosya sınırları:
   - `frontend/Diten.Web.Tests/Diten.Web.Tests.csproj`: tek `pvgapi` aliased PVG Api ProjectReference.
   - `.github/workflows/phase1-gates.yml`: mevcut Phase1 öncesinde yalnız mevcut pinned setup-node / Node22 ve frontend lock ile npm ci iki adımı. Trigger/permission/concurrency/diğer kapılar aynı.
   - Aynı modülün `_Form.cshtml`: yalnız Edit kipindeki iki optional alanın required/işaret düzeltmesi.
   - Aynı servisin `PvgIntakeDraftApplicationServiceTests.cs`: yalnız isimli cross-tenant testinin Update satırındaki policy çağrı beklentisi; Triage/Route ve güvenlik kontrolleri korunur.
   - Diğer dar controller/JS/DTO/test/Windows-helper/resx yolları pack'in DL-12 listesinde belirtilen sınırda kalır.
4. İlgili test projesi/CI sahiplerinin bu sınırları ve tek-yazar düzenini benimsemesi gerekir; bu dosya onların rızası değildir.
5. Onay verilirse hazır statüsüne geçiş ve yalnız bu kapsamdaki build/test geliştirmesi için hazırlık yapılır. Önce sınırlı alias/bağlı-test/temiz CI/Windows feasibility ölçülür; kesin sürüm resmî inceleme, HIGH onay kaydı ve dağıtım kapıları geçtikten sonra DL-12 işçisi başlar. Bu adımlar başarı varsaymaz. Sadece yetki/durum metadatasının bu kararla uyumlu promotion'ı kapsam değişikliği değildir; üretim sözleşmesinde yeni değişiklik ayrıca onay ister.

## CT yürütmesi ve kapanış

CT kod yazmaz. Çakışmayan hizmet ve frontend işleri ayrı worktree'lerde, en çok iki etkin işçiyle planlanır; ortak test/CI dosyalarında tek yazıcı olur. İşçiler gerçek kanıt verir; CT bağımsız doğrular ve uygun commit'leri CT dalında birleştirir. Tek son PR hazırlanır; push/PR için son dal/SHA kapısı, başka yetkili insanın merge'i ve güvenli worktree kapanış koşulları korunur.

Son bağımsız ön taslak hükmü revizyon 19'da KOŞULLU/0 ENGEL idi; iki ölçüm notu revizyon 20'de dar yazar geçişiyle netleştirildi. Revizyon 20 henüz resmî tam-sürüm incelemesinden geçmedi. Mevcut durum draft ve DL-12 BLOCKED'dır; bu istek kendi başına izin, HIGH onay veya kabul değildir.
