# MOD-0230 — Windows erişilemeyen dizin testi için tek kapsam önerisi

Durum: **ÖNERİ / bağımsız inceleme ve sahip onayı bekliyor.** Tarih: 2026-11-20.
Bu belge çalışma izni, pack değişikliği, E2 kabulü veya dağıtım kapısı değildir.

Kaynak CT HEAD: `ce02d1828479026aa81e510d9f1e6ae7e43fc15c`. Mevcut kimlik ve revizyon korunur:
MOD-0230 — Case Intake & Triage, revizyon 20. Onaylı pack SHA-256
`186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`; domain SHA-256
`914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c`.

## Ölçülen sorun ve kapsam sınırı

WIN v1.2'nin normal kapıdan sonraki çalışmasında iki izinli dosyanın WIP değişiklikleri mevcut. CT'nin aktardığı
iki hedef test ve 35 gerçek Mongo testi yeşil; bu belge bunları yeni bir koşuyla doğruladığını iddia etmez.
Salt okunur incelenen `pvg-prep-win-api.trx` ise **395 yürütülen / 394 geçen / 1 başarısız / 0 atlanan** kaydediyor.
Tek hata `Configuration_scan_fails_closed_on_inaccessible_directory` metodunda, satır 1856'daki
`File.SetUnixFileMode` çağrısının Windows'ta `PlatformNotSupportedException` üretmesi. Test erişim reddi önkoşuluna
ve scanner assertion'ına ulaşamıyor. Mevcut iki dosyanın düzeltmesi bunu çözemez; sabotaj/commit ve E2 tamamlanmadı.

Tek öneri: mevcut `WP-0230-DL12-PREP-WIN` kapsamına aşağıdaki **bir dosyanın yalnız bir test metodunun gövdesi**
için açık Windows taşınabilirlik istisnası eklemek:

`services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs`

Metot: `Configuration_scan_fails_closed_on_inaccessible_directory` (mevcut satırlar 1850–1870).
Dosyanın asıl DL-3 sahipliği korunur; dosyanın bütünü PREP-WIN'e açılmaz. Başka test, using bloğu, yardımcı sınıf,
`ScanFixture`, `PvgConfigurationScanner`, host veya ürün davranışı değişmez. Gerekli .NET türleri metot içinde tam
adlarıyla kullanılabilir. Yeni MOD, FU, WP kimliği veya ürün kapsamı önerilmez.

## Somut uygulama sözleşmesi — onaydan sonra

Windows dalında mevcut private `ScanFixture` tarafından oluşturulmuş `fixture.Root/locked` dizininin özgün erişim
ACL'i değişiklikten önce bağımsız bir kopya olarak saklanır. Hedefin aynı fixture içinde, testin kendi `locked`
dizini olduğu doğrulanır; reparse yönlendirmesi veya ortak/üst dizin hedeflenmez. Mevcut .NET 8 ACL API'leriyle
yalnız geçerli test kimliğinin SID'sine, yalnız bu dizinde ve kalıtım yaymadan `ListDirectory` reddi uygulanır.
`ReadPermissions`/`ChangePermissions` ve ACL'i geri yükleme imkânı kapatılmaz; FullControl reddi, genel hesap/grup
ACL'i veya fixture dışı yetki değişimi yoktur.

Erişimi gerçekten reddeden DACL uygulandıktan sonra mevcut
`Directory.EnumerateFileSystemEntries(locked).ToArray()` önkoşulu **UnauthorizedAccessException** üretmelidir.
Runner bunu sağlayamıyorsa test açıkça başarısız olur; skip, early return, sahte exception veya önkoşulu geçerli
sayma yoktur. Ardından gerçek scanner'ın fixture kökünü tarama çağrısı da aynı kilitli dizinden kaynaklanan
**UnauthorizedAccessException** üretmelidir; `ThrowsAny<Exception>` veya ilgisiz bir exception yeterli değildir.
Bu assertion güçlendirmesi yalnız hedef metot gövdesinde kalır. `IgnoreInaccessible=false`, erişilebilir pozitif
kontroller ve scanner'ın geri kalan davranışı değişmez.

`finally`, assertion veya tarama hata verse de özgün DACL'i ve erişim/kalıtım durumunu geri yükler. ACL geri yükleme
hatası yutulmaz. Kilitlemeden önce ve geri yüklemeden sonra aynı nötr JSON dosyasının listelenebildiği ve scanner'ın
bu dosyayı gerçekten gördüğü erişilebilir pozitif kontrol bulunur. Özgün ACL'in geri geldiği ve fixture dispose
sonrasında kendi geçici kökünün kalmadığı ölçülür; helper'ın best-effort temizliği başarı kanıtı sayılmaz. Bu
kontroller hedef metodun gövdesinde kalır.

Unix dalında mevcut `SetUnixFileMode(None)` ve `finally` ile izinleri geri verme akışı korunur. ACL dalı yalnız
Windows'ta seçilir. Test adı, erişilemezlik önkoşulu ve scanner'ın fail-closed beklentisi gevşetilmez.

## Mevcut bağımlılıklarla yapılabilirlik dayanağı

Test projesi `net8.0` hedefliyor; mevcut framework referansı ve paket listesi okundu. Kurulu
`Microsoft.NETCore.App.Ref/8.0.34/ref/net8.0` içinde `System.IO.FileSystem.AccessControl.dll`,
`System.Security.AccessControl.dll` ve `System.Security.Principal.Windows.dll` mevcut. Aynı referans paketinin XML
belgelerinde `FileSystemAclExtensions.GetAccessControl/SetAccessControl(DirectoryInfo, ...)`,
`FileSystemAccessRule`, `FileSystemRights.ListDirectory`, security-descriptor kopyalama ve
`WindowsIdentity.GetCurrent().User` API'leri bulunuyor. Dolayısıyla kaynak düzeyinde yeni paket/framework gereksinimi
görünmüyor. **Derleme veya ACL deneyi yapılmadı;** gerçek runner'da erişim reddi/geri yükleme başarısı henüz ölçülmedi.
Ek bağımlılık ya da metot dışı değişiklik gerekirse bu önerinin sınırında durulur; sessiz kapsam genişletilmez.

## Kabul ve bağımsız doğrulama

- Windows'ta yeni ACL dalıyla hedef test ve tam API süiti 0 başarısız/0 atlanan olmalı. Pozitif erişim kontrolü,
  gerçek UnauthorizedAccessException önkoşulu ve scanner çağrısının aynı dizinden gelen UnauthorizedAccessException'ı
  ayrı ölçülür. Yeni test sayısı varsayılmaz; test sayısını düşürmek veya atlamak kabul edilmez.
- Mevcut Mongo guard'ları, iki hedef test ve 35 gerçek Mongo testi yeniden yeşil; özgün 27017 yasağı, binary yolu/
  varlık kontrolü, replica-set/transaction koşulları, kendi süreç/port/veri izolasyonu ve cleanup korunur.
- Bağımsız E2'nin hazırlık/entegrasyon girdisi, **58920519a17e8f4c085738642b33e36689bc8fee baseline'inden son adaya
  kadar iki WIP dosyası ile ACL metodu düzeltmesini BİRLİKTE** kapsayan Windows ölçümleri ve aşağıdaki altı sabotajdır.
  CT'nin aradaki pack/onay commit'leri kaynak zincirinde ayrıca gösterilir; ürün diff'i yalnız bu üç test dosyasının
  yetkili sınırlarını taşır. Önceki dört sabotaj tamamlanmış sayılmaz veya yeni ikisiyle ikame edilmez.

| No | Dış scratch'ta ayrı derlenen sabotaj | Beklenen RED |
|---|---|---|
| 1 | `--nounixsocket` bayrağını Windows'ta koşulsuz ekleme | Windows platform-argüman/gerçek başlatma koruması kırılır |
| 2 | Unix dalından `--nounixsocket` bayrağını düşürme | Mevcut Unix argüman dalı assertion'ı kırılır; bu tek başına Unix runtime kanıtı değildir |
| 3 | `ResolveBinary` missing/relative korumasını kaldırma | Gerçekte olmayan binary ve göreli yol ret fixture'ları kırılır |
| 4 | 27017 ret korumasını kaldırma | Mevcut forbidden/default-port assertion'ları kırılır; gerçek development Mongo'ya bağlanılmaz |
| 5 | Windows ACL deny uygulamasını kaldırma | Gerçek UnauthorizedAccessException erişilemezlik önkoşulu kırılır |
| 6 | Deny yerindeyken scanner'ı fail-open yapma (`IgnoreInaccessible=true`/inaccessible atlama) | Önkoşul hâlâ gerçek UnauthorizedAccessException iken scanner'ın aynı exception assertion'ı kırılır |

1–2 numaralı satırlar mevcut `Pvg_mongo_launch_arguments_follow_the_host_platform_without_weakening_isolation`,
3–4 mevcut `Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod`, 5–6 ise mevcut
`Configuration_scan_fails_closed_on_inaccessible_directory` testiyle ayrı ayrı bağlanır.

Her satır için bağımsız derleme, ilgili isimli testin RED sonucu, sabotajı taşımayan özgün adayın GREEN sonucu ve
kaynak hash çifti kaydedilir. Altısının hiçbiri atlanmaz; aynı anda birden çok sabotaj uygulayıp tek RED yeterli
sayılmaz. Sabotajlar yalnız dış repository-shaped scratch kopyasında yapılır; gerçek işçi dalına/commit'e taşınmaz.
ACL geri yükleme, yeniden erişim ve geçici fixture temizliği normal/başarısız assertion yollarında kanıtlanır.
Yeni başarısızlık yutma, skip veya assertion kaldırma yoktur; kanıt yalnız çıkış koduna indirgenmez.

**Unix kapanış kapısı, hazırlık döngüsü oluşturmaz.** Mevcut Unix chmod dalı ve güvenlik assertion'ları kaynakla
birebir kıyaslanıp korunur; Windows/argüman testinden Unix runtime PASS sonucu çıkarılmaz. Hazırlığın Windows
ölçümleri ve altı bağımsız RED/GREEN kanıtı E2 ile doğrulanıp entegre edilebilir; henüz olmayan PR'ın Unix koşusu,
PREP geliştirme/entegrasyonu veya sonraki feature dispatch'i için önkoşul yapılmaz. Unix runtime doğrulaması açık
kalan final kapanış şartı olarak taşınır.

Tek son PR'ın **aynı final head SHA'sında**, `.github/workflows/service-tests.yml` içindeki `pvg-api` /
`ubuntu-22.04` işi hedef test dahil tam API süitini 0 başarısız/0 atlanan olarak çalıştırmalıdır. Kaynakta runner
satır 54, suite/proje satır 99–100, gerçek `dotnet test` satır 281 ve skip denetimi satır 291'dedir. Head değişirse
bu nihai head için sonuç yeniden gerekir. İş pending veya başarısızsa final PR merge/main birleştirmesi ve WP DONE
kapanışı gerçekleşmez. `scripts/run_phase1_gates.sh:331` PVG API projesini yalnız build eder; `:351` Web testlerini
çalıştırır. Phase 1 veya onun Web test başarısı, gerçek Linux `pvg-api` testinin yerine geçmez. Bu belge ne Unix
runtime başarısı ne de bu GitHub işinin koştuğu iddiasıdır; CI dosyası değişikliği önerilmez.

## Kapılar, WIP ve geri dönüş

Önce bu tek kapsam önerisinin bağımsız incelemesi ve insan onayı gerekir. K5 CT takip sahibidir; Beste'nin doğrudan
insan onayı kayıtla bağlanır. Mevcut genel kapsam onayı, DL-3 dosyasındaki bu yeni istisna verilmiş gibi kullanılmaz.
Onaydan sonra paket yazarı gerçek pack/domain uyumunu, PREP-WIN dosya/metot sınırını ve ilgili kabul metnini işler.
Değişen kesin sürüm yeniden resmî inceleme/HIGH onay kaydına bağlanır; yenilenmiş worker prompt'u normal HIGH
inceleme ve dağıtım kapısından geçmeden bu üçüncü dosyada çalışma başlamaz. Bu belge tek başına izin değildir.

**Tek yeni insan onayı yalnız Windows ACL test metodudur; checkpoint istisnası istenmez.** WIN v1.2'nin
394/1/0 sonucu normal teslim commit koşulunu karşılamadı. Kirli iki WIP dosyası ve eski işçi ağacı silinmeden,
değiştirilmeden korunur; bu ağaçta checkpoint commit, yeniden stage veya test-RED iken teslim commit'i yapılmaz.
Kullanıcının önceden yetkilendirdiği ayrı worktree düzeniyle, yeni pack/domain kesin sürümü ve resmî HIGH kayıtları
hazır olduktan sonra temiz bir replacement worker ağacı oluşturulur. Bugünkü baz revizyon 20 kendiliğinden
genişlemez; yeni metot istisnası için insan onayından sonra paket yazarı yeni kesin sürümü hazırlar.

Korunacak kesin iki WIP dosyası ve kaydedilmiş **Windows işçi disk baytlarının SHA-256** değerleri:

| Korunan, zaten izinli WIP yolu | Donmuş işçi disk SHA-256 |
|---|---|
| `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs` | `f8a91d03ab788143a62b2578f774d74c76b847e3a3fbb7a513f6edf06a78b053` |
| `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs` | `763b3305f1207d7228e099437babc87bafb8140d43956b46b6aaf8933bba068a` |

Kaynak snapshot kaydı `docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/capture.json`, disk-hash kaydı
aynı dizindeki `green-hashes.json`, tam patch `worker-wip.diff` dosyasıdır; kaydedilen patch SHA-256
`bd7ebf58ac4866fdba94729d0dfb29f688ec87e8a26d7cc80d1f7bbb9c50617f` gerçek patch baytlarıyla eşleşti.
Snapshot işçi HEAD'i `58920519a17e8f4c085738642b33e36689bc8fee` olarak kaydedilmiştir.

Yeni temiz ağacın ExpectedHEAD'i ve kaynak paketi, onaylı yeni pack/domain ve kaynak/patch zinciri açıkça ölçülür.
Yenilenmiş normal HIGH prompt ve dağıtım kapısı **0'dan SONRA**, exact kaydedilmiş iki WIP patch'i bu temiz ağaca
uygulanır. Uygulama öncesi donmuş ham patch/disk hash'leri; sonrasında her iki dosyanın snapshot'a göre kanonik LF
**içerik ve SHA-256 eşitliği** doğrulanır. Başka hunk üretme, uyuşmayan patch'i sessiz düzeltme veya kaynak zinciri
atlama yoktur; uyuşmazlıkta durulur. Bu, zaten onaylı iki dosya kapsamındaki çalışmanın kontrollü taşınmasıdır;
üçüncü metot için henüz verilmemiş onay veya genel commit koşuluna istisna değildir.

Ardından yalnız yeni onaylı ACL metodu düzeltilir; iki WIP ile birlikte tam testler, altı ayrı derlenen sabotaj ve
cleanup ölçülür. Eski dirty ağaç aynen korunur, ona commit yapılmaz. Normal teslim şartları karşılandıktan sonra
oluşan yalnız final doğrulanmış commit, bağımsız E2 sonrası CT'ye birleştirilebilir. Yeni worktree üzerinde patch
uygulaması, ACL deneyi veya gerçek commit bu öneri hazırlanırken yapılmaz; onay ve normal kapılar beklenir.

İki mevcut izinli dosyanın WIP'si korunur; bu öneri onları geri almaz veya tamamlanmış/entegre saymaz. Olası geri
dönüş yalnız yetkilendirilmiş hedef metot düzeltmesini ayrı diff/commit sınırında geri almaktır; ilk iki dosya ve
aktif WEB işçisi etkilenmez. ACL rollback'i testin `finally` sorumluluğudur; repo rollback'ine bırakılmaz.
PREP-WIN ancak yeni ölçümler, bağımsız E2 ve entegrasyon sonrası tamamlanabilir; ana feature iki hazırlığın kendi
kapıları ve bağımsız E2 entegrasyonlarından sonra açılır. Operasyonel runtime kapalı kalır.

## Salt okunur kaynak ve kanıt bağları

| Kaynak | Ölçülen dayanak / SHA-256 |
|---|---|
| `PvgServiceApiHostTests.cs:1850–1870` | Testin mevcut chmod/önkoşul/finally akışı. Windows disk CRLF SHA-256 `1b37d1ccace74b60753df0cd8f339bc0fd3aa0748baa878ff96988390bf64bcc`; kaynak HEAD `ce02d182...` Git blob LF SHA-256 `ac6dc215a62335b282af56a2d38c2c1bde7e32ea3627666448735de1048fc01d` |
| Aynı dosya `:1898–1928`, `:2004–2011`, `:2125–2149` | Private temp fixture, best-effort dispose, `IgnoreInaccessible=false` ve gerçek scanner yürüyüşü |
| `Diten.PvgService.Api.Tests.csproj` | `net8.0`, mevcut proje. Windows disk CRLF SHA-256 `278406f96cb884227448f0f04b57acc86839823b73e10050ced69ef9e74bf666`; kaynak HEAD `ce02d182...` Git blob LF SHA-256 `09842861bc5bd092e5fdae93c5848e2e23561e990076f73bc4ae50cfdc4e6a8b` |
| `C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/win-58920519-01/pvg-prep-win-api.trx` | 394 PASS/1 FAIL/0 skip; `c3b30fb5a2ce4881d6a24e60480b2c4c6cdac69d1178a0455a6791b65c559404` |
| Aynı kanıt dizininde `api-command.txt` | Gerçek test komutu kaydı; `d78c0696cca0a68372d007066fc0c9cb753199f1b9fea4632fbe0efce48325b7` |

Yukarıdaki iki kaynak metnin CRLF disk baytları yalnız CRLF→LF dönüşümüyle kaynak commit'in Git blob baytlarına
birebir eşittir; ZIP Git blob'u taşıdığında LF hash'i esas alınır, disk hash'iyle karıştırılmaz. TRX satırındaki
`c3b30...` hash'i belirtilen Temp kaynağının **ham baytlarıdır**; Git/staging normalizasyonu uygulanmış kopyanın
hash'i olduğu iddia edilmez. Böyle bir kopya hazırlanırsa CT ham/Git çiftini ayrı metadata ile bağlar.

Temp kanıtları yerel kaynaktır; bu belge onların kalıcı arşivlenmiş olduğunu iddia etmez. CT kesin sürüm incelemesi
için kanıt paketine bağlar. Bu hazırlamada kod/ACL/deneme/build/test/restore/npm/commit çalıştırılmadı; yalnız bu
öneri dosyası yazıldı. İstenen model/effort `gpt-6.2-astra/high`; gerçek runtime değeri bu bağlamda **verilemiyor**.
